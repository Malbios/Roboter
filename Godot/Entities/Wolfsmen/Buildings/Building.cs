using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class Building : StaticBody3D, IDamageable
{
	private const float SmallestConstructionScale = 0.3f;

	[Export] public string DisplayName { get; set; } = "Gebäude";
	[Export] public BuildingKind Kind { get; set; }
	[Export] public int Team { get; set; }
	[Export] public int MaxHealth { get; set; } = 10;
	[Export] public int Health { get; set; } = 10;
	[Export] public float Radius { get; set; } = 1;
	[Export] public float HealthBarHeight { get; set; } = 2;
	[Export] public float SightRange { get; set; } = 10;
	[Export] public float BuildTime { get; set; } = 10;
	[Export] public float AttackRange { get; set; }
	[Export] public int AttackDamage { get; set; }
	[Export] public float AttackInterval { get; set; } = 1;
	[Export] public PackedScene AttackEffect { get; set; }
	[Export] public PackedScene ProjectileModel { get; set; }

	[Export] public float BuildProgress { get; set; } = 1;
	[Export] public float WorkProgress { get; set; }
	[Export] public int QueueLength { get; set; }
	[Export] public int SpawnNumber { get; set; }

	public bool IsFinished => BuildProgress >= 1;

	public bool IsSeenByLocalPlayer { get; set; }
	public bool IsKnownToLocalPlayer { get; private set; }
	public int ShownHealth => IsSeenByLocalPlayer ? Health : _lastSeenHealth;
	public float ShownBuildProgress => IsSeenByLocalPlayer ? BuildProgress : _lastSeenBuildProgress;

	private readonly Queue<UnitType> _trainingQueue = new();
	private float _trainingTimer;
	private float _attackCooldown;
	private ulong _lastBuildFrame;
	private Node3D _model;
	private MeshInstance3D _selectionCircle;
	private int _lastSeenHealth;
	private float _lastSeenBuildProgress;

	public override void _Ready()
	{
		AddToGroup("Buildings");
		_model = GetNode<Node3D>("Model");
		_selectionCircle = SelectionCircle.AddTo(this, Radius);
		FogOfWar.Instance.ShowOrHide(this);
	}

	public override void _Process(double delta)
	{
		float scale = Mathf.Lerp(SmallestConstructionScale, 1f, Mathf.Clamp(ShownBuildProgress, 0, 1));
		_model.Scale = Vector3.One * scale;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!Multiplayer.IsServer() || !IsFinished)
			return;

		float deltaSeconds = (float)delta;
		StartTrainingWaitingUnits();
		UpdateFinishedBuilding(deltaSeconds);
		UpdateAttacking(deltaSeconds);
	}

	public void SetSelected(bool selected)
	{
		_selectionCircle.Visible = selected;
	}

	public void RememberWhatIsSeen()
	{
		IsKnownToLocalPlayer = true;
		_lastSeenHealth = Health;
		_lastSeenBuildProgress = BuildProgress;
	}

	public void Build(float delta)	{
		ulong frame = Engine.GetPhysicsFrames();
		bool someoneAlreadyBuiltThisFrame = frame == _lastBuildFrame;
		if (someoneAlreadyBuiltThisFrame)
			return;

		_lastBuildFrame = frame;
		BuildProgress = Mathf.Min(1, BuildProgress + delta / BuildTime);
	}

	public void AddToTrainingQueue(UnitType type)
	{
		_trainingQueue.Enqueue(type);
		QueueLength = _trainingQueue.Count;
	}

	public virtual bool CanTrain(UnitType type)
	{
		return IsFinished && type.TrainedAt == Kind;
	}

	public void TakeDamage(int damage, Node3D attacker)
	{
		if (Health <= 0)
			return;

		Health -= damage;
		if (Health <= 0)
			Destroy();
	}

	protected virtual void UpdateFinishedBuilding(float delta)
	{
		UpdateTraining(delta);
	}

	protected virtual void OnDestroyed()
	{
	}

	protected void RemoveFromMap()
	{
		FogOfWar.Instance.RememberIfDestroyedUnseen(this);
		QueueFree();
	}

	protected void UpdateTraining(float delta)	{
		if (_trainingQueue.Count == 0)
		{
			WorkProgress = 0;
			return;
		}

		UnitType type = _trainingQueue.Peek();
		_trainingTimer += delta;
		WorkProgress = _trainingTimer / type.TrainingTime;
		if (_trainingTimer < type.TrainingTime)
			return;

		_trainingTimer = 0;
		_trainingQueue.Dequeue();
		QueueLength = _trainingQueue.Count;
		Match.Instance.SpawnUnitNextTo(type.Scene, Team, this);
	}

	private void StartTrainingWaitingUnits()
	{
		var waitingUnits = GetTree().GetNodesInGroup("Units").OfType<Unit>()
			.Where(unit => unit.State == UnitState.WaitingForTraining && unit.TrainingBuilding == this && Entity.IsAlive(unit))
			.ToList();

		foreach (var group in waitingUnits.GroupBy(unit => unit.TrainingType))
		{
			UnitType type = group.Key;
			List<Unit> units = group.ToList();
			while (units.Count >= type.MadeFromCount)
			{
				if (!Match.Instance.ResourcesOf(Team).TrySpend(type.CostMetal, type.CostGas))
					break;

				for (int i = 0; i < type.MadeFromCount; i++)
				{
					units[0].QueueFree();
					units.RemoveAt(0);
				}
				AddToTrainingQueue(type);
			}
		}
	}

	private void UpdateAttacking(float delta)
	{
		if (AttackDamage <= 0)
			return;

		_attackCooldown -= delta;
		if (_attackCooldown > 0)
			return;

		Node3D enemy = Match.Instance.FindNearestEnemy(this, AttackRange);
		if (enemy == null)
			return;

		_attackCooldown = AttackInterval;
		Rpc(MethodName.ShowAttack, enemy.GlobalPosition);
		((IDamageable)enemy).TakeDamage(AttackDamage, this);
	}

	private void Destroy()
	{
		OnDestroyed();
		Match.Instance.EntityDestroyed(this);
		RemoveFromMap();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ShowAttack(Vector3 targetPosition)
	{
		if (AttackEffect == null || !IsSeenByLocalPlayer)
			return;

		Projectile projectile = AttackEffect.Instantiate<Projectile>();
		GetTree().CurrentScene.AddChild(projectile);
		projectile.Launch(GlobalPosition + Vector3.Up * HealthBarHeight * 0.8f, targetPosition + Vector3.Up * 0.5f, ProjectileModel);
	}
}
