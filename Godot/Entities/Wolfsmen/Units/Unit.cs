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
	private const float ArrivalDistance = 0.4f;

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
	[Export] public Texture2D Portrait { get; set; }

	public UnitState State { get; protected set; } = UnitState.Idle;
	public Building TrainingBuilding { get; private set; }
	public UnitType TrainingType { get; private set; }

	protected NavigationAgent3D NavAgent;

	private Vector3 _moveTarget;
	private Node3D _attackTarget;
	private float _attackCooldown;
	private float _lookForEnemiesTimer;
	private MeshInstance3D _selectionCircle;

	public override void _Ready()
	{
		AddToGroup("Units");
		NavAgent = GetNode<NavigationAgent3D>("NavigationAgent3D");
		NavAgent.Radius = Radius;
		NavAgent.MaxSpeed = Speed;
		NavAgent.VelocityComputed += OnVelocityComputed;
		NavAgent.AvoidanceEnabled = Multiplayer.IsServer();
		_selectionCircle = SelectionCircle.AddTo(this, Radius);
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
		NavAgent.TargetPosition = point;
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

	protected void WalkTo(Vector3 destination)
	{
		if (NavAgent.TargetPosition.DistanceTo(destination) > 0.5f)
			NavAgent.TargetPosition = destination;

		Vector3 nextPoint = NavAgent.GetNextPathPosition();
		Vector3 direction = nextPoint - GlobalPosition;
		direction.Y = 0;
		NavAgent.Velocity = direction.Normalized() * Speed;
	}

	protected void StandStill()
	{
		NavAgent.Velocity = Vector3.Zero;
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

	protected bool IsNear(Node3D other, float distance = 0.5f)
	{
		return Entity.EdgeDistance(this, other) <= distance;
	}

	private void OnVelocityComputed(Vector3 safeVelocity)
	{
		if (safeVelocity.Length() < 0.01f)
			return;

		float delta = (float)GetPhysicsProcessDeltaTime();
		Vector3 newPosition = GlobalPosition + safeVelocity * delta;
		GlobalPosition = Match.Instance.SnapToGround(newPosition);
		FaceTowards(GlobalPosition + safeVelocity);
	}

	private void UpdateMoving()
	{
		if (Entity.FlatDistance(GlobalPosition, _moveTarget) <= ArrivalDistance || NavAgent.IsNavigationFinished())
		{
			State = UnitState.Idle;
			StandStill();
			return;
		}

		WalkTo(_moveTarget);
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

		if (IsNear(TrainingBuilding))
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
