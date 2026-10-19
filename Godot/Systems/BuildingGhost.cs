using Godot;

// What a player still remembers of an enemy building that was destroyed while nobody was watching.
public partial class BuildingGhost : Node3D
{
	public int Team { get; private set; }
	public int Health { get; private set; }
	public int MaxHealth { get; private set; }
	public float HealthBarHeight { get; private set; }

	public static BuildingGhost CopyOf(Building building)
	{
		var ghost = new BuildingGhost
		{
			Name = $"{building.Name}Ghost",
			Team = building.Team,
			Health = building.ShownHealth,
			MaxHealth = building.MaxHealth,
			HealthBarHeight = building.HealthBarHeight,
			Transform = building.GlobalTransform,
		};
		ghost.AddChild(building.GetNode("Model").Duplicate());
		ghost.AddToGroup("BuildingGhosts");
		return ghost;
	}
}
