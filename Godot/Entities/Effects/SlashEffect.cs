using Godot;

public partial class SlashEffect : Node3D
{
	[Export] public float Duration { get; set; } = 0.25f;

	public override void _Ready()
	{
		MeshInstance3D mesh = GetNode<MeshInstance3D>("MeshInstance3D");
		StandardMaterial3D material = (StandardMaterial3D)mesh.GetSurfaceOverrideMaterial(0);

		Tween tween = CreateTween();
		tween.TweenProperty(this, "rotation_degrees:y", -60f, Duration).From(60f);
		tween.Parallel().TweenProperty(material, "albedo_color:a", 0f, Duration);
		tween.TweenCallback(Callable.From(QueueFree));
	}
}
