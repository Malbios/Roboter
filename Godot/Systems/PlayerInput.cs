using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class PlayerInput : Control
{
	private const float RayLength = 2000;
	private const float MinimumBoxSize = 6;
	private const uint ClickableLayers = Entity.UnitLayer | Entity.BuildingLayer | Entity.NeutralLayer;
	private static readonly Color BoxFillColor = new(0.55f, 0.85f, 1f, 0.15f);
	private static readonly Color BoxBorderColor = new(0.55f, 0.85f, 1f, 0.9f);
	private static readonly Color CanPlaceColor = new(0.3f, 1f, 0.3f, 0.6f);
	private static readonly Color CannotPlaceColor = new(1f, 0.25f, 0.25f, 0.6f);

	public List<Node3D> Selected
	{
		get
		{
			_selected.RemoveAll(entity => !Entity.IsAlive(entity) || !entity.IsVisibleInTree());
			return _selected;
		}
	}

	private readonly List<Node3D> _selected = new();

	private bool _isDragging;
	private Vector2 _dragStart;
	private int _placingBuildingType = -1;
	private Node3D _ghost;
	private StandardMaterial3D _ghostRingMaterial;

	public static int LocalTeam => NetworkManager.Instance.LocalTeam;

	public IEnumerable<Unit> SelectedOwnUnits()
	{
		return Selected.OfType<Unit>().Where(unit => unit.Team == LocalTeam);
	}

	public override void _Process(double delta)
	{
		if (_ghost != null)
			MoveGhostToMouse();
		if (_isDragging)
			QueueRedraw();
	}

	public void StartPlacingBuilding(int buildingTypeIndex)
	{
		CancelPlacing();
		_placingBuildingType = buildingTypeIndex;
		_ghost = CreateGhost(Match.Instance.BuildingTypes[buildingTypeIndex]);
	}

	public void SelectOnly(Node3D entity)
	{
		ClearSelection();
		AddToSelection(entity);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } leftClick)
			HandleLeftClick(leftClick);
		else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
			HandleRightClick();
		else if (@event is InputEventKey { Keycode: Key.Escape, Pressed: true })
			HandleEscape();
	}

	public override void _Draw()
	{
		if (!_isDragging)
			return;

		Rect2 box = DragBox();
		DrawRect(box, BoxFillColor);
		DrawRect(box, BoxBorderColor, false, 1);
	}

	private void HandleLeftClick(InputEventMouseButton click)
	{
		if (click.Pressed && _ghost != null)
		{
			PlaceBuilding();
		}
		else if (click.Pressed)
		{
			_isDragging = true;
			_dragStart = click.Position;
		}
		else if (_isDragging)
		{
			_isDragging = false;
			QueueRedraw();
			if (DragBox().Size.Length() >= MinimumBoxSize)
				SelectUnitsInBox(DragBox(), click.ShiftPressed);
			else
				SelectClickedEntity(click.ShiftPressed);
		}
		GetViewport().SetInputAsHandled();
	}

	private void HandleRightClick()
	{
		if (_ghost != null)
		{
			CancelPlacing();
			GetViewport().SetInputAsHandled();
			return;
		}

		List<Unit> units = SelectedOwnUnits().ToList();
		if (units.Count == 0)
			return;

		Node3D target = RaycastFromMouse(ClickableLayers, out _);
		RaycastFromMouse(Entity.WorldLayer, out Vector3 groundPoint);
		Match.Instance.Commands.RightClick(units, target, groundPoint);
		GetViewport().SetInputAsHandled();
	}

	private void HandleEscape()
	{
		if (_ghost != null)
			CancelPlacing();
		else
			ClearSelection();
	}

	private void SelectClickedEntity(bool addToSelection)
	{
		if (!addToSelection)
			ClearSelection();

		Node3D clicked = RaycastFromMouse(ClickableLayers, out _);
		if (clicked is Unit or Building)
			AddToSelection(clicked);
	}

	private void SelectUnitsInBox(Rect2 box, bool addToSelection)
	{
		if (!addToSelection)
			ClearSelection();

		Camera3D camera = GetViewport().GetCamera3D();
		foreach (Unit unit in GetTree().GetNodesInGroup("Units").OfType<Unit>())
		{
			if (unit.Team != LocalTeam || camera.IsPositionBehind(unit.GlobalPosition))
				continue;

			if (box.HasPoint(camera.UnprojectPosition(unit.GlobalPosition)))
				AddToSelection(unit);
		}
	}

	private void AddToSelection(Node3D entity)
	{
		if (Selected.Contains(entity))
			return;

		Selected.Add(entity);
		SetSelectionCircle(entity, true);
	}

	private void ClearSelection()
	{
		foreach (Node3D entity in Selected)
			SetSelectionCircle(entity, false);

		_selected.Clear();
	}

	private static void SetSelectionCircle(Node3D entity, bool visible)
	{
		if (entity is Unit unit)
			unit.SetSelected(visible);
		else if (entity is Building building)
			building.SetSelected(visible);
	}

	private Rect2 DragBox()
	{
		return new Rect2(_dragStart, GetViewport().GetMousePosition() - _dragStart).Abs();
	}

	private Node3D RaycastFromMouse(uint collisionMask, out Vector3 hitPoint)
	{
		hitPoint = Vector3.Zero;
		Camera3D camera = GetViewport().GetCamera3D();
		Vector2 mouse = GetViewport().GetMousePosition();
		Vector3 from = camera.ProjectRayOrigin(mouse);
		Vector3 to = from + camera.ProjectRayNormal(mouse) * RayLength;

		var query = PhysicsRayQueryParameters3D.Create(from, to, collisionMask);
		var hit = camera.GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0)
			return null;

		hitPoint = (Vector3)hit["position"];
		Node3D hitEntity = hit["collider"].As<Node3D>();
		return hitEntity.IsVisibleInTree() ? hitEntity : null;
	}

	private Node3D CreateGhost(BuildingType type)
	{
		var ghost = new Node3D { Name = "BuildingGhost" };
		ghost.AddChild(type.Model.Instantiate());

		_ghostRingMaterial = new StandardMaterial3D
		{
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		};
		var ring = new MeshInstance3D
		{
			Mesh = new TorusMesh { InnerRadius = type.Radius - 0.1f, OuterRadius = type.Radius, Material = _ghostRingMaterial },
			Position = new Vector3(0, 0.05f, 0),
		};
		ghost.AddChild(ring);

		GetTree().CurrentScene.AddChild(ghost);
		return ghost;
	}

	private void MoveGhostToMouse()
	{
		RaycastFromMouse(Entity.WorldLayer, out Vector3 groundPoint);
		BuildingType type = Match.Instance.BuildingTypes[_placingBuildingType];
		bool canPlace = Match.Instance.CanPlaceBuilding(type, groundPoint, out Vector3 placedPosition);
		_ghost.GlobalPosition = placedPosition;
		_ghostRingMaterial.AlbedoColor = canPlace ? CanPlaceColor : CannotPlaceColor;
	}

	private void PlaceBuilding()
	{
		RaycastFromMouse(Entity.WorldLayer, out Vector3 groundPoint);
		BuildingType type = Match.Instance.BuildingTypes[_placingBuildingType];
		if (!Match.Instance.CanPlaceBuilding(type, groundPoint, out _))
			return;

		Match.Instance.Commands.PlaceBuilding(SelectedOwnUnits().OfType<Worker>(), _placingBuildingType, groundPoint);
		CancelPlacing();
	}

	private void CancelPlacing()
	{
		_ghost?.QueueFree();
		_ghost = null;
		_placingBuildingType = -1;
	}
}
