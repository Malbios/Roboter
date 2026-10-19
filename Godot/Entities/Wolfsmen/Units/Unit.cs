using Godot;

public enum UnitState
{
	Idle,
	Moving,
	Attacking,
	GoingToTraining,
	WaitingForTraining,
	GatheringScrap,
	BringingScrap,
	Building,
	GoingIntoMine,
	FetchingGas,
	BringingGas,
}

public partial class Unit : CharacterBody3D, IDamageable
{
	private const float LookForEnemiesInterval = 0.5f;
	private const float ArrivalDistance = 0.5f;
	private const float WaypointReachedMargin = 0.1f;
	private const float GiveUpOnWaypointAfter = 0.5f;
	private const float NoticeableProgress = 0.05f;

	[Export] public string DisplayName { get; set; } = "Einheit";
	[Export] public UnitKind Kind { get; set; }
	[Export] public int Team { get; set; }
	[Export] public int MaxHealth { get; set; } = 10;
	[Export] public int Health { get; set; } = 10;
	[Export] public float Speed { get; set; } = 1;
	[Export] public float Radius { get; set; } = 0.3f;
	[Export] public float HealthBarHeight { get; set; } = 1.3f;
	[Export] public float AttackRange { get; set; } = 1;
	[Export] public int AttackDamage { get; set; } = 5;
	[Export] public float AttackInterval { get; set; } = 1;
	[Export] public float SightRange { get; set; } = 8;
	[Export] public bool AttacksEnemiesNearby { get; set; } = true;
	[Export] public PackedScene AttackEffect { get; set; }
	[Export] public PackedScene ProjectileModel { get; set; }
	[Export] public AudioStream AttackSound { get; set; }
	[Export] public Texture2D Portrait { get; set; }
	[Export] public AudioStream SelectedSound { get; set; }
	[Export] public AudioStream CommandSound { get; set; }

	public UnitState State { get; protected set; } = UnitState.Idle;
	public Building TrainingBuilding { get; private set; }
	public UnitType TrainingType { get; private set; }

	protected NavigationAgent3D NavAgent;

	private Vector3 _moveTarget;
	private Node3D _attackTarget;
	private float _attackCooldown;
	private float _lookForEnemiesTimer;
	private MeshInstance3D _selectionCircle;
	private Vector3[] _path = [];
	private int _nextWaypoint;
	private Vector3 _pathDestination;
	private float _closestDistanceToWaypoint;
	private float _noProgressTimer;
	private Vector3 _positionOnNavigationMesh;
	private float _climbSpeed;

	public override void _Ready()
	{
		AddToGroup("Units");
		NavAgent = GetNode<NavigationAgent3D>("NavigationAgent3D");
		NavAgent.Radius = Radius;
		NavAgent.MaxSpeed = Speed;
		NavAgent.VelocityComputed += OnVelocityComputed;
		NavAgent.AvoidanceEnabled = Multiplayer.IsServer();
		_positionOnNavigationMesh = Match.Instance.WalkablePointUnder(GlobalPosition);
		_selectionCircle = SelectionCircle.AddTo(this, Radius);
		FogOfWar.Instance.ShowOrHide(this);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!Multiplayer.IsServer())
			return;

		float deltaSeconds = (float)delta;
		_attackCooldown -= deltaSeconds;
		UpdateState(deltaSeconds);
	}

	public void SetSelected(bool selected)
	{
		_selectionCircle.Visible = selected;
	}

	public virtual void RightClick(Node3D target, Vector3 point)
	{
		if (Entity.AreEnemies(this, target))
			Attack(target);
		else
			MoveTo(point);
	}

	public void MoveTo(Vector3 point)
	{
		_moveTarget = point;
		FindPathTo(point);
		State = UnitState.Moving;
	}

	public void Attack(Node3D target)
	{
		_attackTarget = target;
		State = UnitState.Attacking;
	}

	public void GoToTraining(Building building, UnitType type)
	{
		TrainingBuilding = building;
		TrainingType = type;
		State = UnitState.GoingToTraining;
	}

	public void TakeDamage(int damage, Node3D attacker)
	{
		if (Health <= 0)
			return;

		Health -= damage;
		if (Health <= 0)
			Die();
		else if (State == UnitState.Idle && AttacksEnemiesNearby && Entity.IsAlive(attacker))
			Attack(attacker);
	}

	protected virtual void UpdateState(float delta)
	{
		switch (State)
		{
			case UnitState.Idle:
				StandStill();
				LookForEnemies(delta);
				break;
			case UnitState.Moving:
				UpdateMoving();
				break;
			case UnitState.Attacking:
				UpdateAttacking();
				break;
			case UnitState.GoingToTraining:
				UpdateGoingToTraining();
				break;
			case UnitState.WaitingForTraining:
				StandStill();
				if (!Entity.IsAlive(TrainingBuilding))
					State = UnitState.Idle;
				break;
		}
	}

	// The unit walks from waypoint to waypoint along a path on the navigation mesh.
	// It also follows the height of the path, so it stays on the right level (on a bridge, not under it).
	protected void WalkTo(Vector3 destination)
	{
		float delta = (float)GetPhysicsProcessDeltaTime();
		bool destinationMoved = Entity.FlatDistance(_pathDestination, destination) > 0.5f;
		if (destinationMoved)
			FindPathTo(destination);

		// A waypoint only counts when the unit really is there, otherwise it could cut a corner into a wall.
		float waypointReachedDistance = WaypointReachedMargin + Speed * delta;
		while (!IsPathFinished() && Entity.FlatDistance(GlobalPosition, _path[_nextWaypoint]) <= waypointReachedDistance)
			GoToNextWaypoint();

		if (!IsPathFinished() && IsStuckOnWaypoint(delta))
			GoToNextWaypoint();

		if (IsPathFinished())
		{
			StandStill();
			return;
		}

		Vector3 nextWaypoint = _path[_nextWaypoint];
		Vector3 flatDirection = new Vector3(nextWaypoint.X - GlobalPosition.X, 0, nextWaypoint.Z - GlobalPosition.Z).Normalized();
		float flatDistance = Entity.FlatDistance(_positionOnNavigationMesh, nextWaypoint);
		_climbSpeed = (nextWaypoint.Y - _positionOnNavigationMesh.Y) / flatDistance * Speed;
		RequestVelocity(flatDirection * Speed);
	}

	protected bool IsPathFinished()
	{
		return _nextWaypoint >= _path.Length;
	}

	protected void StandStill()
	{
		_climbSpeed = 0;
		RequestVelocity(Vector3.Zero);
	}

	// The navigation server adjusts the wanted velocity so units walk around each other
	// and sends the result back to OnVelocityComputed.
	private void RequestVelocity(Vector3 wantedVelocity)
	{
		NavigationServer3D.AgentSetVelocity(NavAgent.GetRid(), wantedVelocity);
	}

	protected void FaceTowards(Vector3 point)
	{
		Vector3 direction = point - GlobalPosition;
		if (new Vector2(direction.X, direction.Z).Length() < 0.01f)
			return;

		Vector3 rotation = Rotation;
		rotation.Y = Mathf.Atan2(direction.X, direction.Z);
		Rotation = rotation;
	}

	protected bool HasArrivedAt(Node3D target)
	{
		bool isCloseEnough = Entity.EdgeDistance(this, target) <= ArrivalDistance;
		bool isWalkingToTarget = Entity.FlatDistance(_pathDestination, target.GlobalPosition) <= 0.5f;
		bool cannotGetAnyCloser = isWalkingToTarget && IsPathFinished();
		return isCloseEnough || cannotGetAnyCloser;
	}

	private void FindPathTo(Vector3 destination)
	{
		_path = NavigationServer3D.MapGetPath(GetWorld3D().NavigationMap, _positionOnNavigationMesh, destination, true);
		_nextWaypoint = -1;
		_pathDestination = destination;
		GoToNextWaypoint();
	}

	private void GoToNextWaypoint()
	{
		_nextWaypoint += 1;
		_closestDistanceToWaypoint = float.MaxValue;
		_noProgressTimer = 0;
	}

	// Other units or a tight corner can keep a unit from ever reaching a waypoint.
	// If it doesn't get any closer for a moment, it gives up on that waypoint and walks to the next one.
	private bool IsStuckOnWaypoint(float delta)
	{
		float distance = Entity.FlatDistance(GlobalPosition, _path[_nextWaypoint]);
		if (distance < _closestDistanceToWaypoint - NoticeableProgress)
		{
			_closestDistanceToWaypoint = distance;
			_noProgressTimer = 0;
			return false;
		}

		_noProgressTimer += delta;
		return _noProgressTimer > GiveUpOnWaypointAfter;
	}

	private void OnVelocityComputed(Vector3 safeVelocity)
	{
		if (safeVelocity.Length() < 0.01f)
			return;

		// Units move along the navigation mesh, and their model stands on the real ground below it.
		float delta = (float)GetPhysicsProcessDeltaTime();
		Vector3 step = (safeVelocity + Vector3.Up * _climbSpeed) * delta;
		_positionOnNavigationMesh = Match.Instance.ClosestPointOnNavigationMesh(_positionOnNavigationMesh + step);
		GlobalPosition = Match.Instance.GroundUnder(_positionOnNavigationMesh);
		FaceTowards(GlobalPosition + safeVelocity);
	}

	private void UpdateMoving()
	{
		WalkTo(_moveTarget);
		if (Entity.FlatDistance(GlobalPosition, _moveTarget) <= ArrivalDistance || IsPathFinished())
		{
			State = UnitState.Idle;
			StandStill();
		}
	}

	private void UpdateAttacking()
	{
		if (!Entity.IsAlive(_attackTarget))
		{
			State = UnitState.Idle;
			StandStill();
			return;
		}

		if (Entity.EdgeDistance(this, _attackTarget) > AttackRange)
		{
			WalkTo(_attackTarget.GlobalPosition);
			return;
		}

		StandStill();
		FaceTowards(_attackTarget.GlobalPosition);
		if (_attackCooldown > 0)
			return;

		_attackCooldown = AttackInterval;
		Rpc(MethodName.ShowAttack, _attackTarget.GlobalPosition);
		((IDamageable)_attackTarget).TakeDamage(AttackDamage, this);
	}

	private void UpdateGoingToTraining()
	{
		if (!Entity.IsAlive(TrainingBuilding))
		{
			State = UnitState.Idle;
			StandStill();
			return;
		}

		if (HasArrivedAt(TrainingBuilding))
		{
			StandStill();
			State = UnitState.WaitingForTraining;
			return;
		}

		WalkTo(TrainingBuilding.GlobalPosition);
	}

	private void LookForEnemies(float delta)
	{
		if (!AttacksEnemiesNearby)
			return;

		_lookForEnemiesTimer -= delta;
		if (_lookForEnemiesTimer > 0)
			return;

		_lookForEnemiesTimer = LookForEnemiesInterval;
		Node3D enemy = Match.Instance.FindNearestEnemy(this, SightRange);
		if (enemy != null)
			Attack(enemy);
	}

	private void Die()
	{
		Match.Instance.EntityDestroyed(this);
		QueueFree();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ShowAttack(Vector3 targetPosition)
	{
		if (!Visible)
			return;

		SoundEffect.PlayAt(AttackSound, GlobalPosition);
		if (AttackEffect == null)
			return;

		Node3D effect = AttackEffect.Instantiate<Node3D>();
		GetTree().CurrentScene.AddChild(effect);

		if (effect is Projectile projectile)
			projectile.Launch(GlobalPosition + Vector3.Up * HealthBarHeight * 0.6f, targetPosition + Vector3.Up * 0.5f, ProjectileModel);
		else
			effect.GlobalPosition = targetPosition + Vector3.Up * 0.5f;
	}
}
