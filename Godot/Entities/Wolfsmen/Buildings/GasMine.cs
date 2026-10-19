using Godot;

public partial class GasMine : Building
{
	[Export] public int MaxWorkers { get; set; } = 2;
	[Export] public int GasPerWorker { get; set; } = 5;
	[Export] public float WorkTime { get; set; } = 5;
	[Export] public PackedScene WorkerScene { get; set; }

	[Export] public int WorkersInside { get; set; }
	[Export] public int StoredGas { get; set; }

	public GasVolcano Volcano { get; private set; }

	private float _workTimer;

	public override void _Ready()
	{
		base._Ready();
		Volcano = Match.Instance.FindVolcanoAt(GlobalPosition);
		if (Volcano != null)
			Volcano.HasMine = true;
	}

	public override void _ExitTree()
	{
		if (Entity.IsInstanceValid(Volcano))
			Volcano.HasMine = false;
	}

	public bool HasFreeSlot()
	{
		return WorkersInside < MaxWorkers;
	}

	public bool TryLetWorkerIn()
	{
		if (!IsFinished || !HasFreeSlot())
			return false;

		WorkersInside += 1;
		return true;
	}

	public int TakeGas(int wanted)
	{
		int taken = Mathf.Min(wanted, StoredGas);
		StoredGas -= taken;
		return taken;
	}

	public void LetWorkersOut()
	{
		for (int i = 0; i < WorkersInside; i++)
			Match.Instance.SpawnUnitNextTo(WorkerScene, Team, this);

		WorkersInside = 0;
	}

	protected override void UpdateFinishedBuilding(float delta)
	{
		if (WorkersInside == 0 || Volcano == null || Volcano.Gas <= 0)
		{
			_workTimer = 0;
			WorkProgress = 0;
			return;
		}

		_workTimer += delta;
		WorkProgress = _workTimer / WorkTime;
		if (_workTimer < WorkTime)
			return;

		_workTimer = 0;
		StoredGas += Volcano.Take(GasPerWorker * WorkersInside);
	}

	protected override void OnDestroyed()
	{
		for (int i = 0; i < WorkersInside; i++)
			Match.Instance.SpawnScrapPile(GlobalPosition + new Vector3(Radius + 1 + i, 0, 0));

		WorkersInside = 0;
	}
}
