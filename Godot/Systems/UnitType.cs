using Godot;

public enum UnitKind
{
	Worker,
	Swordsman,
	Spearman,
	Archer,
	Crossbowman,
	GroundVehicle,
	Mech,
}

[GlobalClass]
public partial class UnitType : Resource
{
	[Export] public string Name { get; set; } = "";
	[Export] public PackedScene Scene { get; set; }
	[Export] public int CostMetal { get; set; }
	[Export] public int CostGas { get; set; }
	[Export] public float TrainingTime { get; set; } = 10;
	[Export] public BuildingKind TrainedAt { get; set; } = BuildingKind.TrainingCenter;
	[Export] public UnitKind MadeFrom { get; set; } = UnitKind.Worker;
	[Export] public int MadeFromCount { get; set; } = 1;

	public string CostText()
	{
		string text = $"{CostMetal} Blech";
		if (CostGas > 0)
			text += $", {CostGas} Gas";
		if (MadeFromCount > 0)
			text += $", {MadeFromCount} {UnitNames.Of(MadeFrom)}";
		return text;
	}
}

public static class UnitNames
{
	public static string Of(UnitKind kind)
	{
		return kind switch
		{
			UnitKind.Worker => "Arbeiter",
			UnitKind.Swordsman => "Schwertkämpfer",
			UnitKind.Spearman => "Speerkämpfer",
			UnitKind.Archer => "Bogenschütze",
			UnitKind.Crossbowman => "Armbrustschütze",
			UnitKind.GroundVehicle => "Bodenfahrzeug",
			UnitKind.Mech => "Mech",
			_ => kind.ToString(),
		};
	}
}
