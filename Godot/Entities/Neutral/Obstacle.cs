using Godot;

public partial class Obstacle : StaticBody3D, IEntity
{
	[Export] public float Radius { get; set; } = 1;

	public override void _Ready()
	{
		AddToGroup("Obstacles");
	}
}
