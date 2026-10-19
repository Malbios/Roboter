using Godot;

public enum BuildingKind
{
	Bunker,
	GasMine,
	TrainingCenter,
	Workshop,
}

[GlobalClass]
public partial class BuildingType : Resource
{
	[Export] public string Name { get; set; } = "";
	[Export] public PackedScene Scene { get; set; }
	[Export] public PackedScene Model { get; set; }
	[Export] public BuildingKind Kind { get; set; }
	[Export] public int CostMetal { get; set; }
	[Export] public int CostGas { get; set; }
	[Export] public float Radius { get; set; } = 1;

	public string CostText()
	{
		return CostGas > 0 ? $"{CostMetal} Blech, {CostGas} Gas" : $"{CostMetal} Blech";
	}
}
