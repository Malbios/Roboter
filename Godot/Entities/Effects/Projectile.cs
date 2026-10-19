using Godot;

public partial class Projectile : Node3D
{
	[Export] public float Speed { get; set; } = 15;

	private Vector3 _target;

	public void Launch(Vector3 from, Vector3 to, PackedScene model)
	{
		GlobalPosition = from;
		_target = to;
		if (model != null)
		{
			GetNode("PlaceholderModel").QueueFree();
			AddChild(model.Instantiate());
		}
		bool flyingStraightDown = Entity.FlatDistance(from, to) < 0.01f;
		LookAt(to, flyingStraightDown ? Vector3.Forward : Vector3.Up, true);
	}

	public override void _Process(double delta)
	{
		GlobalPosition = GlobalPosition.MoveToward(_target, Speed * (float)delta);
		if (GlobalPosition.DistanceTo(_target) < 0.05f)
			QueueFree();
	}
}
