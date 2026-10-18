using Godot;

public partial class WolfsmenBunkerUpgraded : Node3D
{
	[Export] public float TurnSpeed = 60f;

	private Area3D _turretRadius;
	private Node3D _target;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_turretRadius = GetNode<Area3D>("TurretRange");
		_turretRadius.BodyEntered += OnBodyEntered;
		_turretRadius.BodyExited += OnBodyExited;
	}

	private void OnBodyEntered(Node3D body)
	{
		if (body.IsInGroup("Enemy"))
		{
			_target = body;
		}
	}

	private void OnBodyExited(Node3D body)
	{
		if (body == _target)
		{
			_target = null;
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (_target == null)
		{
			return;
		}

		Vector3 direction = _target.GlobalPosition - GlobalPosition;
		float targetAngle = Mathf.Atan2(direction.X, direction.Z);
		float maxStep = Mathf.DegToRad(TurnSpeed) * (float)delta;

		Vector3 rotation = GlobalRotation;
		rotation.Y = Mathf.RotateToward(rotation.Y, targetAngle, maxStep);
		GlobalRotation = rotation;
	}
}
