using Godot;
using System;

public partial class WalkingDummy : CharacterBody3D
{
	[Export] public float Speed = 1.5f;
	[Export] public bool IsEnemy = true;

	public override void _Ready()
	{
		if (!IsEnemy)
		{
			RemoveFromGroup("Enemy");
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		Velocity = GlobalBasis.Z * Speed;
		MoveAndSlide();
	}
}
