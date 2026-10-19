using Godot;

public static class SelectionCircle
{
	private static readonly Color LightBlue = new(0.55f, 0.85f, 1f, 0.6f);
	private const float HeightAboveGround = 0.05f;
	private const float RingThickness = 0.08f;
	private const float SpaceAroundEntity = 0.15f;

	public static MeshInstance3D AddTo(Node3D entity, float entityRadius)
	{
		float outerRadius = entityRadius + SpaceAroundEntity;

		var material = new StandardMaterial3D
		{
			AlbedoColor = LightBlue,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		};

		var ring = new TorusMesh
		{
			InnerRadius = outerRadius - RingThickness,
			OuterRadius = outerRadius,
			Material = material,
		};

		var circle = new MeshInstance3D
		{
			Name = "SelectionCircle",
			Mesh = ring,
			Visible = false,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Position = new Vector3(0, HeightAboveGround, 0),
			Scale = new Vector3(1, 0.2f, 1),
		};
		entity.AddChild(circle);
		return circle;
	}
}
