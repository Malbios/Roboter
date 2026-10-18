using Godot;

public partial class Bullet : Area3D
{
	[Export] public float Speed = 20f;
	[Export] public int Damage = 5;

	private float _lifetime = 3f;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		GlobalPosition += GlobalBasis.Z * Speed * (float)delta;

		_lifetime -= (float)delta;
		if (_lifetime <= 0)
		{
			QueueFree();
		}
	}

	private void OnBodyEntered(Node3D body)
	{
		if (body is WalkingDummy dummy && dummy.IsEnemy)
		{
			dummy.TakeDamage(Damage);
			QueueFree();
		}
	}
}
