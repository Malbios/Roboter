using Godot;

public partial class Worker : Unit
{
	[Export] public int ScrapCapacity { get; set; } = 50;
	[Export] public int ScrapPerGrab { get; set; } = 10;
	[Export] public float SecondsPerGrab { get; set; } = 1;
	[Export] public int GasPerTrip { get; set; } = 5;
	[Export] public float GasPickupTime { get; set; } = 5;

	[Export] public int CarriedScrap { get; set; }
	[Export] public int CarriedGas { get; set; }

	private ScrapPile _scrapPile;
	private Building _construction;
	private GasMine _gasMine;
	private float _workTimer;

	public override void RightClick(Node3D target, Vector3 point)
	{
		if (target is ScrapPile scrapPile)
			GatherScrap(scrapPile);
		else if (target is Building building && building.Team == Team)
			RightClickOwnBuilding(building, point);
		else
			base.RightClick(target, point);
	}

	public void GatherScrap(ScrapPile scrapPile)
	{
		_scrapPile = scrapPile;
		_workTimer = 0;
		State = UnitState.GatheringScrap;
	}

	public void Build(Building construction)
	{
		_construction = construction;
		State = UnitState.Building;
	}

	protected override void UpdateState(float delta)
	{
		switch (State)
		{
			case UnitState.GatheringScrap:
				UpdateGatheringScrap(delta);
				break;
			case UnitState.BringingScrap:
				UpdateBringingScrap();
				break;
			case UnitState.Building:
				UpdateBuilding(delta);
				break;
			case UnitState.GoingIntoMine:
				UpdateGoingIntoMine();
				break;
			case UnitState.FetchingGas:
				UpdateFetchingGas(delta);
				break;
			case UnitState.BringingGas:
				UpdateBringingGas();
				break;
			default:
				base.UpdateState(delta);
				break;
		}
	}

	private void RightClickOwnBuilding(Building building, Vector3 point)
	{
		if (!building.IsFinished)
			Build(building);
		else if (building is GasMine gasMine)
			GoToGasMine(gasMine);
		else if (building is Bunker && CarriedScrap > 0)
			State = UnitState.BringingScrap;
		else if (building is Bunker && CarriedGas > 0)
			State = UnitState.BringingGas;
		else
			MoveTo(point);
	}

	private void GoToGasMine(GasMine gasMine)
	{
		_gasMine = gasMine;
		_workTimer = 0;
		State = gasMine.HasFreeSlot() ? UnitState.GoingIntoMine : UnitState.FetchingGas;
	}

	private void UpdateGatheringScrap(float delta)
	{
		if (!Entity.IsInstanceValid(_scrapPile) || _scrapPile.Amount <= 0)
		{
			_scrapPile = Match.Instance.FindScrapPileForTeam(Team, GlobalPosition);
			if (_scrapPile == null)
			{
				State = CarriedScrap > 0 ? UnitState.BringingScrap : UnitState.Idle;
				return;
			}
		}

		if (!IsNear(_scrapPile))
		{
			_workTimer = 0;
			WalkTo(_scrapPile.GlobalPosition);
			return;
		}

		StandStill();
		FaceTowards(_scrapPile.GlobalPosition);
		_workTimer += delta;
		if (_workTimer < SecondsPerGrab)
			return;

		_workTimer = 0;
		int wanted = Mathf.Min(ScrapPerGrab, ScrapCapacity - CarriedScrap);
		CarriedScrap += _scrapPile.Take(wanted);
		if (CarriedScrap >= ScrapCapacity)
			State = UnitState.BringingScrap;
	}

	private void UpdateBringingScrap()
	{
		Bunker bunker = Match.Instance.FindNearestBunker(Team, GlobalPosition);
		if (bunker == null)
		{
			State = UnitState.Idle;
			return;
		}

		if (!IsNear(bunker))
		{
			WalkTo(bunker.GlobalPosition);
			return;
		}

		bunker.StoredScrap += CarriedScrap;
		CarriedScrap = 0;
		GatherScrap(_scrapPile);
	}

	private void UpdateBuilding(float delta)
	{
		if (!Entity.IsAlive(_construction) || _construction.IsFinished)
		{
			State = UnitState.Idle;
			return;
		}

		if (!IsNear(_construction))
		{
			WalkTo(_construction.GlobalPosition);
			return;
		}

		StandStill();
		FaceTowards(_construction.GlobalPosition);
		_construction.Build(delta);
	}

	private void UpdateGoingIntoMine()
	{
		if (!Entity.IsAlive(_gasMine))
		{
			State = UnitState.Idle;
			return;
		}

		if (!IsNear(_gasMine))
		{
			WalkTo(_gasMine.GlobalPosition);
			return;
		}

		if (_gasMine.TryLetWorkerIn())
			QueueFree();
		else
			State = UnitState.FetchingGas;
	}

	private void UpdateFetchingGas(float delta)
	{
		if (!Entity.IsAlive(_gasMine))
		{
			State = CarriedGas > 0 ? UnitState.BringingGas : UnitState.Idle;
			return;
		}

		if (!IsNear(_gasMine))
		{
			_workTimer = 0;
			WalkTo(_gasMine.GlobalPosition);
			return;
		}

		StandStill();
		if (_gasMine.StoredGas <= 0)
			return;

		_workTimer += delta;
		if (_workTimer < GasPickupTime)
			return;

		_workTimer = 0;
		CarriedGas += _gasMine.TakeGas(GasPerTrip);
		State = UnitState.BringingGas;
	}

	private void UpdateBringingGas()
	{
		Bunker bunker = Match.Instance.FindNearestBunker(Team, GlobalPosition);
		if (bunker == null)
		{
			State = UnitState.Idle;
			return;
		}

		if (!IsNear(bunker))
		{
			WalkTo(bunker.GlobalPosition);
			return;
		}

		Match.Instance.ResourcesOf(Team).Gas += CarriedGas;
		CarriedGas = 0;
		_workTimer = 0;
		State = Entity.IsAlive(_gasMine) ? UnitState.FetchingGas : UnitState.Idle;
	}
}
