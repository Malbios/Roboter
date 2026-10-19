using System;
using System.Linq;
using Godot;

// Every player computes their own fog of war: what their own units and buildings can see right now.
public partial class FogOfWar : Node
{
	private const float CellSize = 2;
	private const float UpdateInterval = 0.2f;
	private const byte HiddenFog = 255;
	private const byte NoFog = 0;

	// The darkening covers the screen of every camera that renders it, so the minimap camera skips this layer.
	public const int DarkeningRenderLayer = 20;

	[Export] public Shader DarkeningShader { get; set; }
	[Export] public float Darkness { get; set; } = 0.45f;

	public static FogOfWar Instance { get; private set; }

	public ImageTexture FogTexture { get; private set; }

	private int _cellsPerSide;
	private bool[] _visibleCells;
	private byte[] _fogPixels;
	private Image _fogImage;
	private float _updateTimer;
	private Node3D _ghosts;

	private static int LocalTeam => NetworkManager.Instance.LocalTeam;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		_cellsPerSide = Mathf.CeilToInt(Match.Instance.MapSize / CellSize);
		_visibleCells = new bool[_cellsPerSide * _cellsPerSide];
		_fogPixels = new byte[_cellsPerSide * _cellsPerSide * 4];
		_fogImage = Image.CreateFromData(_cellsPerSide, _cellsPerSide, false, Image.Format.Rgba8, _fogPixels);
		FogTexture = ImageTexture.CreateFromImage(_fogImage);
		_ghosts = Match.Instance.GetNode<Node3D>("BuildingGhosts");

		Match.Instance.GetNode<MultiplayerSpawner>("EntitySpawner").Despawned += OnEntityDespawned;
		AddDarkeningToCamera();
		UpdateFog();
	}

	public override void _Process(double delta)
	{
		_updateTimer -= (float)delta;
		if (_updateTimer > 0)
			return;

		_updateTimer = UpdateInterval;
		UpdateFog();
	}

	public bool IsVisible(Vector3 position)
	{
		int column = ColumnOf(position.X);
		int row = RowOf(position.Z);
		if (!IsInsideMap(column, row))
			return false;

		return _visibleCells[row * _cellsPerSide + column];
	}

	public void ShowOrHide(Unit unit)
	{
		unit.Visible = unit.Team == LocalTeam || IsVisible(unit.GlobalPosition);
	}

	public void ShowOrHide(Building building)
	{
		building.IsSeenByLocalPlayer = building.Team == LocalTeam || IsVisible(building.GlobalPosition);
		if (building.IsSeenByLocalPlayer)
			building.RememberWhatIsSeen();

		building.Visible = building.IsKnownToLocalPlayer;
	}

	// The host frees buildings itself, clients hear about it from the spawner (see OnEntityDespawned).
	public void RememberIfDestroyedUnseen(Building building)
	{
		if (building.Team == LocalTeam || building.IsSeenByLocalPlayer || !building.IsKnownToLocalPlayer)
			return;

		_ghosts.AddChild(BuildingGhost.CopyOf(building));
	}

	private void OnEntityDespawned(Node entity)
	{
		if (entity is Building building)
			RememberIfDestroyedUnseen(building);
	}

	private void UpdateFog()
	{
		Array.Clear(_visibleCells);
		foreach (Unit unit in Match.Instance.UnitsOf(LocalTeam))
			MarkVisibleAround(unit.GlobalPosition, unit.SightRange);
		foreach (Building building in Match.Instance.BuildingsOf(LocalTeam))
			MarkVisibleAround(building.GlobalPosition, building.SightRange);

		foreach (Unit unit in GetTree().GetNodesInGroup("Units").OfType<Unit>())
			ShowOrHide(unit);
		foreach (Building building in GetTree().GetNodesInGroup("Buildings").OfType<Building>())
			ShowOrHide(building);
		foreach (ScrapPile pile in GetTree().GetNodesInGroup("ScrapPiles").OfType<ScrapPile>())
			pile.Visible = pile.Visible || IsVisible(pile.GlobalPosition);
		foreach (BuildingGhost ghost in GetTree().GetNodesInGroup("BuildingGhosts").OfType<BuildingGhost>())
		{
			if (IsVisible(ghost.GlobalPosition))
				ghost.QueueFree();
		}

		UpdateFogTexture();
	}

	private void MarkVisibleAround(Vector3 center, float sightRange)
	{
		int firstColumn = ColumnOf(center.X - sightRange);
		int lastColumn = ColumnOf(center.X + sightRange);
		int firstRow = RowOf(center.Z - sightRange);
		int lastRow = RowOf(center.Z + sightRange);

		for (int row = firstRow; row <= lastRow; row++)
		{
			for (int column = firstColumn; column <= lastColumn; column++)
			{
				if (!IsInsideMap(column, row))
					continue;

				Vector2 cellCenter = CellCenter(column, row);
				if (cellCenter.DistanceTo(new Vector2(center.X, center.Z)) <= sightRange)
					_visibleCells[row * _cellsPerSide + column] = true;
			}
		}
	}

	private void UpdateFogTexture()
	{
		for (int cell = 0; cell < _visibleCells.Length; cell++)
			_fogPixels[cell * 4 + 3] = _visibleCells[cell] ? NoFog : HiddenFog;

		_fogImage.SetData(_cellsPerSide, _cellsPerSide, false, Image.Format.Rgba8, _fogPixels);
		FogTexture.Update(_fogImage);
	}

	private void AddDarkeningToCamera()
	{
		var material = new ShaderMaterial { Shader = DarkeningShader };
		material.SetShaderParameter("fog_texture", FogTexture);
		material.SetShaderParameter("map_corner", MapCorner());
		material.SetShaderParameter("map_size", Match.Instance.MapSize);
		material.SetShaderParameter("darkness", Darkness);

		var screenCover = new MeshInstance3D
		{
			Name = "FogOfWarDarkening",
			Mesh = new QuadMesh { Size = new Vector2(2, 2) },
			MaterialOverride = material,
			ExtraCullMargin = 16384,
			Layers = 1u << (DarkeningRenderLayer - 1),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		Match.Instance.GetNode<Camera3D>("RtsController/Elevation/Camera3D").AddChild(screenCover);
	}

	private static Vector2 MapCorner()
	{
		return Match.Instance.MapCenter - Vector2.One * Match.Instance.MapSize / 2;
	}

	private Vector2 CellCenter(int column, int row)
	{
		return MapCorner() + new Vector2(column + 0.5f, row + 0.5f) * CellSize;
	}

	private int ColumnOf(float worldX)
	{
		return Mathf.FloorToInt((worldX - MapCorner().X) / CellSize);
	}

	private int RowOf(float worldZ)
	{
		return Mathf.FloorToInt((worldZ - MapCorner().Y) / CellSize);
	}

	private bool IsInsideMap(int column, int row)
	{
		return column >= 0 && row >= 0 && column < _cellsPerSide && row < _cellsPerSide;
	}
}
