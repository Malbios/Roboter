using Godot;

public partial class WolfsmenBunkerUpgraded : StaticBody3D
{
	[Export] public float TurnSpeed = 60f;
	[Export] public int Health = 40;
	[Export] public float ShootInterval = 1f;
	[Export] public PackedScene BulletScene;
	[Export] public PackedScene ExplosionScene;

	private Area3D _turretRadius;
	private Marker3D _muzzle;
	private Node3D _target;
	private float _shootTimer;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_turretRadius = GetNode<Area3D>("TurretRange");
		_turretRadius.BodyExited += OnBodyExited;
		_muzzle = GetNode<Marker3D>("Muzzle");
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
		if (_target == null || !_target.IsInGroup("Enemy"))
		{
			_target = FindTarget();
		}

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

		_shootTimer -= (float)delta;
		bool isAimed = Mathf.Abs(Mathf.AngleDifference(rotation.Y, targetAngle)) < 0.1f;
		if (isAimed && _shootTimer <= 0)
		{
			Shoot();
			_shootTimer = ShootInterval;
		}
	}

	public void TakeDamage(int damage)
	{
		if (Health <= 0)
		{
			return;
		}

		Health -= damage;
		if (Health <= 0)
		{
			Explode();
		}
	}

	private Node3D FindTarget()
	{
		foreach (Node3D body in _turretRadius.GetOverlappingBodies())
		{
			if (body.IsInGroup("Enemy"))
			{
				return body;
			}
		}

		return null;
	}

	private void Shoot()
	{
		Node3D bullet = BulletScene.Instantiate<Node3D>();
		GetParent().AddChild(bullet);
		bullet.GlobalPosition = _muzzle.GlobalPosition;
		bullet.LookAt(_target.GlobalPosition + new Vector3(0, 0.5f, 0), Vector3.Up, true);
	}

	private void Explode()
	{
		Node3D explosion = ExplosionScene.Instantiate<Node3D>();
		GetParent().AddChild(explosion);
		explosion.GlobalPosition = GlobalPosition + new Vector3(0, 2, 0);
		QueueFree();
	}
}
