using Godot;
using System;

public partial class WalkingDummy : CharacterBody3D
{
	[Export] public float Speed = 1.5f;
	[Export] public bool IsEnemy = true;
	[Export] public int Health = 10;
	[Export] public int Damage = 5;
	[Export] public float AttackInterval = 1f;
	[Export] public float FallTime = 0.5f;
	[Export] public float FadeTime = 1f;
	[Export] public PackedScene SlashScene;
	[Export] public PackedScene SparksScene;

	private Area3D _attackRange;
	private float _attackTimer;

	public override void _Ready()
	{
		_attackRange = GetNode<Area3D>("AttackRange");

		if (!IsEnemy)
		{
			RemoveFromGroup("Enemy");
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Health <= 0)
		{
			return;
		}

		WolfsmenBunkerUpgraded turret = FindTurretInReach();
		if (IsEnemy && turret != null)
		{
			_attackTimer -= (float)delta;
			if (_attackTimer <= 0)
			{
				ShowAttackEffects();
				turret.TakeDamage(Damage);
				_attackTimer = AttackInterval;
			}
			return;
		}

		Velocity = GlobalBasis.Z * Speed;
		MoveAndSlide();
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
			Die();
		}
	}

	private void ShowAttackEffects()
	{
		Node3D slash = SlashScene.Instantiate<Node3D>();
		AddChild(slash);
		slash.Position = new Vector3(0, 0.8f, 0.4f);

		Node3D sparks = SparksScene.Instantiate<Node3D>();
		AddChild(sparks);
		sparks.Position = new Vector3(0, 0.8f, 0.8f);
	}

	private WolfsmenBunkerUpgraded FindTurretInReach()
	{
		foreach (Node3D body in _attackRange.GetOverlappingBodies())
		{
			if (body is WolfsmenBunkerUpgraded turret)
			{
				return turret;
			}
		}

		return null;
	}

	private void Die()
	{
		RemoveFromGroup("Enemy");
		GetNode<CollisionShape3D>("CollisionShape3D").SetDeferred("disabled", true);

		Node3D model = GetNode<Node3D>("Model");
		MeshInstance3D mesh = (MeshInstance3D)model.FindChildren("*", "MeshInstance3D", true, false)[0];

		StandardMaterial3D material = (StandardMaterial3D)mesh.GetActiveMaterial(0).Duplicate();
		material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		mesh.MaterialOverride = material;

		Tween tween = CreateTween();
		tween.TweenProperty(model, "rotation_degrees:x", -90f, FallTime)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.In);
		tween.TweenProperty(material, "albedo_color:a", 0f, FadeTime);
		tween.TweenCallback(Callable.From(QueueFree));
	}
}
