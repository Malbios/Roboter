using System.Linq;
using Godot;

public partial class Minimap : Control
{
	private const float CameraHeight = 300;
	private const float SecondsBeforeTerrainPhoto = 1;
	private static readonly Color OwnColor = new(0.35f, 0.65f, 1f);
	private static readonly Color EnemyColor = new(1f, 0.3f, 0.25f);
	private static readonly Color ScrapColor = new(0.7f, 0.7f, 0.7f);
	private static readonly Color VolcanoColor = new(0.4f, 1f, 0.4f);
	private static readonly Color CameraColor = Colors.White;
	private static readonly Color BorderColor = new(0, 0, 0, 0.8f);
	private static readonly Color FogColor = new(1, 1, 1, 0.6f);

	private SubViewport _terrainPhoto;
	private Node3D _cameraRig;

	public override void _Ready()
	{
		_terrainPhoto = GetNode<SubViewport>("TerrainPhoto");
		_cameraRig = Match.Instance.GetNode<Node3D>("RtsController");

		Camera3D photoCamera = _terrainPhoto.GetNode<Camera3D>("Camera3D");
		photoCamera.Size = Match.Instance.MapSize;
		photoCamera.SetCullMaskValue(FogOfWar.DarkeningRenderLayer, false);
		photoCamera.Position = new Vector3(Match.Instance.MapCenter.X, CameraHeight, Match.Instance.MapCenter.Y);

		TakeTerrainPhotoSoon();
	}

	public override void _Process(double delta)
	{
		QueueRedraw();
	}

	public override void _GuiInput(InputEvent @event)
	{
		bool leftPressed = @event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true };
		bool leftDragged = @event is InputEventMouseMotion motion && motion.ButtonMask.HasFlag(MouseButtonMask.Left);
		if (!leftPressed && !leftDragged)
			return;

		Vector3 worldPosition = ToWorld(GetLocalMousePosition());
		_cameraRig.GlobalPosition = new Vector3(worldPosition.X, _cameraRig.GlobalPosition.Y, worldPosition.Z);
		AcceptEvent();
	}

	public override void _Draw()
	{
		var area = new Rect2(Vector2.Zero, Size);
		DrawTextureRect(_terrainPhoto.GetTexture(), area, false);

		DrawTextureRect(FogOfWar.Instance.FogTexture, area, false, FogColor);

		foreach (ScrapPile pile in GetTree().GetNodesInGroup("ScrapPiles").OfType<ScrapPile>().Where(pile => pile.Visible))
			DrawCircle(ToMinimap(pile.GlobalPosition), 1.5f, ScrapColor);

		foreach (GasVolcano volcano in GetTree().GetNodesInGroup("GasVolcanoes").OfType<GasVolcano>())
			DrawCircle(ToMinimap(volcano.GlobalPosition), 3, VolcanoColor);

		foreach (Building building in GetTree().GetNodesInGroup("Buildings").OfType<Building>().Where(building => building.Visible))
			DrawBuilding(building.GlobalPosition, building.Team);

		foreach (BuildingGhost ghost in GetTree().GetNodesInGroup("BuildingGhosts").OfType<BuildingGhost>())
			DrawBuilding(ghost.GlobalPosition, ghost.Team);

		foreach (Unit unit in GetTree().GetNodesInGroup("Units").OfType<Unit>().Where(unit => unit.Visible))
			DrawCircle(ToMinimap(unit.GlobalPosition), 2, TeamColor(unit.Team));

		DrawRect(new Rect2(ToMinimap(_cameraRig.GlobalPosition) - new Vector2(8, 8), new Vector2(16, 16)), CameraColor, false, 1.5f);
		DrawRect(area, BorderColor, false, 2);
	}

	private void DrawBuilding(Vector3 position, int team)
	{
		DrawRect(new Rect2(ToMinimap(position) - new Vector2(3, 3), new Vector2(6, 6)), TeamColor(team));
	}

	private async void TakeTerrainPhotoSoon()
	{
		await ToSignal(GetTree().CreateTimer(SecondsBeforeTerrainPhoto), SceneTreeTimer.SignalName.Timeout);
		_terrainPhoto.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
	}

	private static Color TeamColor(int team)
	{
		return team == PlayerInput.LocalTeam ? OwnColor : EnemyColor;
	}

	private Vector2 TopLeftCornerInWorld()
	{
		return Match.Instance.MapCenter - Vector2.One * Match.Instance.MapSize / 2;
	}

	private Vector2 ToMinimap(Vector3 worldPosition)
	{
		Vector2 fromCorner = new Vector2(worldPosition.X, worldPosition.Z) - TopLeftCornerInWorld();
		return fromCorner / Match.Instance.MapSize * Size;
	}

	private Vector3 ToWorld(Vector2 minimapPosition)
	{
		Vector2 world = TopLeftCornerInWorld() + minimapPosition / Size * Match.Instance.MapSize;
		return new Vector3(world.X, 0, world.Y);
	}
}
