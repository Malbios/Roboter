using Godot;

public partial class MatchSetup : Node3D
{
	public override void _Ready()
	{
		var startPosition = GetNode<Marker3D>($"StartPositions/Team{NetworkManager.Instance.LocalTeam}");
		GetNode<Node3D>("RtsController").GlobalTransform = startPosition.GlobalTransform;
	}
}
