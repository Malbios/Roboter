using Godot;
using System;

public partial class WalkingDummy : CharacterBody3D
{
	[Export] public float Speed = 1.5f;

	public override void _PhysicsProcess(double delta)
	{
		Velocity = GlobalBasis.Z * Speed;
		MoveAndSlide();
	}
}
