using Godot;

public partial class Bunker : Building
{
	[Export] public float SecondsPerScrap { get; set; } = 1;
	[Export] public PackedScene UpgradedScene { get; set; }
	[Export] public int UpgradeCostMetal { get; set; } = 100;
	[Export] public int UpgradeCostGas { get; set; } = 5;
	[Export] public float UpgradeTime { get; set; } = 10;

	[Export] public int StoredScrap { get; set; }
	[Export] public bool IsUpgrading { get; set; }

	private float _scrapTimer;
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

	private void TurnScrapIntoMetal(float delta)
	{
		if (StoredScrap <= 0)
		{
			_scrapTimer = 0;
			return;
		}

		_scrapTimer += delta;
		if (_scrapTimer < SecondsPerScrap)
			return;

		_scrapTimer = 0;
		StoredScrap -= 1;
		Match.Instance.ResourcesOf(Team).Metal += 1;
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
		QueueFree();
	}
}
