using Godot;

public partial class Bunker : Building
{
	[Export] public float SecondsToTurnScrapIntoMetal { get; set; } = 1;
	[Export] public PackedScene UpgradedScene { get; set; }
	[Export] public int UpgradeCostMetal { get; set; } = 100;
	[Export] public int UpgradeCostGas { get; set; } = 5;
	[Export] public float UpgradeTime { get; set; } = 10;

	[Export] public int StoredScrap { get; set; }
	[Export] public bool IsUpgrading { get; set; }

	private float _scrapTimer;
	private int _scrapBeingTurnedIntoMetal;
	private float _upgradeTimer;

	public bool CanBeUpgraded => UpgradedScene != null && IsFinished && !IsUpgrading && QueueLength == 0;

	public override bool CanTrain(UnitType type)
	{
		return base.CanTrain(type) && !IsUpgrading;
	}

	public void StartUpgrade()
	{
		IsUpgrading = true;
		_upgradeTimer = 0;
	}

	protected override void UpdateFinishedBuilding(float delta)
	{
		TurnScrapIntoMetal(delta);

		if (IsUpgrading)
			UpdateUpgrade(delta);
		else
			UpdateTraining(delta);
	}

	// All scrap in the bunker becomes metal at once after one second.
	// Scrap that is delivered during that second waits for the next round.
	private void TurnScrapIntoMetal(float delta)
	{
		if (_scrapBeingTurnedIntoMetal == 0)
		{
			if (StoredScrap <= 0)
				return;

			_scrapBeingTurnedIntoMetal = StoredScrap;
			_scrapTimer = 0;
		}

		_scrapTimer += delta;
		if (_scrapTimer < SecondsToTurnScrapIntoMetal)
			return;

		StoredScrap -= _scrapBeingTurnedIntoMetal;
		Match.Instance.ResourcesOf(Team).Metal += _scrapBeingTurnedIntoMetal;
		_scrapBeingTurnedIntoMetal = 0;
	}

	private void UpdateUpgrade(float delta)
	{
		_upgradeTimer += delta;
		WorkProgress = _upgradeTimer / UpgradeTime;
		if (_upgradeTimer < UpgradeTime)
			return;

		Bunker upgraded = Match.Instance.CreateBuilding<Bunker>(UpgradedScene, Team, GlobalPosition);
		upgraded.Rotation = Rotation;
		upgraded.StoredScrap = StoredScrap;
		upgraded.SpawnNumber = SpawnNumber;
		int damageTaken = MaxHealth - Health;
		upgraded.Health = upgraded.MaxHealth - damageTaken;
		Match.Instance.AddEntity(upgraded);
		RemoveFromMap();
	}
}
