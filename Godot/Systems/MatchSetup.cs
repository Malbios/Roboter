using Godot;

public partial class MatchSetup : Node3D
{
	public override void _Ready()
	{
		var startPosition = GetNode<Marker3D>($"StartPositions/Team{NetworkManager.Instance.LocalTeam}");

		var rtsControllerScene = GD.Load<PackedScene>("res://Systems/RtsController.tscn");
		var rtsController = rtsControllerScene.Instantiate<Node3D>();
		AddChild(rtsController);
		rtsController.GlobalTransform = startPosition.GlobalTransform;
	}
}
