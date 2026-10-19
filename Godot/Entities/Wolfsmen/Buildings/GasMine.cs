using System.Collections.Generic;
using Godot;

public enum GasMineJob
{
	WorkInside,
	CarryGas,
}

public partial class GasMine : Building
{
	[Export] public int MaxWorkers { get; set; } = 2;
	[Export] public int GasPerWorker { get; set; } = 5;
	[Export] public float WorkTime { get; set; } = 5;
	[Export] public PackedScene WorkerScene { get; set; }

	[Export] public int WorkersInside { get; set; }
	[Export] public int StoredGas { get; set; }
	[Export] public int CarrierCount { get; set; }

	public GasVolcano Volcano { get; private set; }

	private float _workTimer;
	private readonly List<Worker> _workersOnTheirWayIn = new();
	private readonly List<Worker> _carriers = new();

	public override void _Ready()
	{
		base._Ready();
		Volcano = Match.Instance.FindVolcanoWithMineDockAt(GlobalPosition);
		if (Volcano != null)
			Volcano.Mine = this;
	}

	public override void _ExitTree()
	{
		if (Entity.IsInstanceValid(Volcano) && Volcano.Mine == this)
			Volcano.Mine = null;
	}

	// 1st worker works inside, 2nd carries gas, 3rd works inside, every worker after that carries.
	public GasMineJob ChooseJobFor(Worker worker)
	{
		ForgetWorkersThatLeft();
		_workersOnTheirWayIn.Remove(worker);
		_carriers.Remove(worker);

		int workersInsideOrOnTheirWay = WorkersInside + _workersOnTheirWayIn.Count;
		bool isFirstWorker = workersInsideOrOnTheirWay == 0;
		bool isWorkerAfterTheFirstCarrier = workersInsideOrOnTheirWay == 1 && _carriers.Count > 0;
		if (workersInsideOrOnTheirWay < MaxWorkers && (isFirstWorker || isWorkerAfterTheFirstCarrier))
		{
			_workersOnTheirWayIn.Add(worker);
			return GasMineJob.WorkInside;
		}

		_carriers.Add(worker);
		CarrierCount = _carriers.Count;
		return GasMineJob.CarryGas;
	}

	public bool TryLetWorkerIn(Worker worker)
	{
		_workersOnTheirWayIn.Remove(worker);
		if (!IsFinished || WorkersInside >= MaxWorkers)
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
		ForgetWorkersThatLeft();

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

private void ForgetWorkersThatLeft()
	{
		_workersOnTheirWayIn.RemoveAll(worker => !Entity.IsAlive(worker) || !worker.IsWorkingFor(this));
		_carriers.RemoveAll(worker => !Entity.IsAlive(worker) || !worker.IsWorkingFor(this));
		CarrierCount = _carriers.Count;
	}

	protected override void OnDestroyed()
	{
		for (int i = 0; i < WorkersInside; i++)
			Match.Instance.SpawnScrapPile(GlobalPosition + new Vector3(Radius + 1 + i, 0, 0));

		WorkersInside = 0;
	}
}
