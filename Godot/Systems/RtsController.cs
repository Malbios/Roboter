using Godot;

public partial class RtsController : Node3D
{
	private const float ZoomSmoothing = 0.1f;
	private const float PanSensitivity = 0.1f;

	[Export(PropertyHint.Range, "0,1000")] public float MovementSpeed { get; set; } = 64;
	[Export(PropertyHint.Range, "0,1000")] public float RotationSpeed { get; set; } = 5;
	[Export(PropertyHint.Range, "0,1000,0.1")] public float ZoomSpeed { get; set; } = 50;
	[Export(PropertyHint.Range, "0,1000")] public float MinZoom { get; set; } = 32;
	[Export(PropertyHint.Range, "0,1000")] public float MaxZoom { get; set; } = 256;
	[Export(PropertyHint.Range, "0,90")] public float MinElevationAngle { get; set; } = 10;
	[Export(PropertyHint.Range, "0,90")] public float MaxElevationAngle { get; set; } = 90;
	[Export] public bool AllowRotation { get; set; } = true;
	[Export] public bool AllowZoom { get; set; } = true;
	[Export] public bool AllowPan { get; set; } = true;

	private Camera3D _camera;
	private Node3D _elevationNode;

	private bool _isRotating;
	private bool _isPanning;
	private Vector2 _lastMousePosition;
	private float _zoomLevel = 64;

	public override void _Ready()
	{
		_camera = GetNode<Camera3D>("Elevation/Camera3D");
		_elevationNode = GetNode<Node3D>("Elevation");
		_zoomLevel = _camera.Position.Y;
	}

	public override void _Process(double delta)
	{
		float deltaSeconds = (float)delta;

		if (_isPanning)
		{
			if (AllowPan)
				HandlePanning();
			return;
		}

		HandleKeyboardMovement(deltaSeconds);

		if (AllowRotation)
			HandleRotation(deltaSeconds);

		if (AllowZoom)
			HandleZoom();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("camera_rotate"))
		{
			_isRotating = true;
			_lastMousePosition = GetViewport().GetMousePosition();
		}
		else if (@event.IsActionReleased("camera_rotate"))
		{
			_isRotating = false;
		}

		if (@event.IsActionPressed("camera_pan"))
		{
			_isPanning = true;
			_lastMousePosition = GetViewport().GetMousePosition();
		}
		else if (@event.IsActionReleased("camera_pan"))
		{
			_isPanning = false;
		}

		if (@event.IsActionPressed("zoom_in"))
			_zoomLevel -= ZoomSpeed;
		else if (@event.IsActionPressed("zoom_out"))
			_zoomLevel += ZoomSpeed;
	}

	private void HandleKeyboardMovement(float delta)
	{
		Vector3 direction = Vector3.Zero;
		if (Input.IsActionPressed("camera_forward"))
			direction.Z -= 1;
		if (Input.IsActionPressed("camera_back"))
			direction.Z += 1;
		if (Input.IsActionPressed("camera_left"))
			direction.X -= 1;
		if (Input.IsActionPressed("camera_right"))
			direction.X += 1;

		MoveInDirection(direction, delta);
	}

	private void MoveInDirection(Vector3 direction, float delta)
	{
		if (direction == Vector3.Zero)
			return;

		GlobalTranslate(direction.Normalized() * MovementSpeed * delta);
	}

	private void HandleRotation(float delta)
	{
		if (!_isRotating)
			return;

		Vector2 mouseDisplacement = ConsumeMouseDisplacement();

		Vector3 rotation = Rotation;
		rotation.Y -= Mathf.DegToRad(mouseDisplacement.X * RotationSpeed * delta);
		Rotation = rotation;

		float elevationAngle = Mathf.RadToDeg(_elevationNode.Rotation.X);
		elevationAngle = Mathf.Clamp(
			elevationAngle - mouseDisplacement.Y * RotationSpeed * delta,
			MinElevationAngle,
			MaxElevationAngle);

		Vector3 elevationRotation = _elevationNode.Rotation;
		elevationRotation.X = Mathf.DegToRad(elevationAngle);
		_elevationNode.Rotation = elevationRotation;
	}

	private void HandleZoom()
	{
		_zoomLevel = Mathf.Clamp(_zoomLevel, MinZoom, MaxZoom);

		Vector3 cameraPosition = _camera.Position;
		cameraPosition.Y = Mathf.Lerp(cameraPosition.Y, _zoomLevel, ZoomSmoothing);
		_camera.Position = cameraPosition;
	}

	private void HandlePanning()
	{
		Vector2 displacement = ConsumeMouseDisplacement();
		GlobalTranslate(new Vector3(-displacement.X, 0, -displacement.Y) * PanSensitivity);
	}

	private Vector2 ConsumeMouseDisplacement()
	{
		Vector2 currentMousePosition = GetViewport().GetMousePosition();
		Vector2 displacement = currentMousePosition - _lastMousePosition;
		_lastMousePosition = currentMousePosition;
		return displacement;
	}
}
