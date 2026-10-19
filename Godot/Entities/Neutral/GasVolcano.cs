using Godot;

public partial class GasVolcano : StaticBody3D, IEntity
{
	[Export] public int Gas { get; set; } = 1000;
	[Export] public float Radius { get; set; } = 2;

	public GasMine Mine { get; set; }
	public bool HasMine => Entity.IsInstanceValid(Mine) && !Mine.IsQueuedForDeletion();

	private Node3D _model;

	public override void _Ready()
	{
		AddToGroup("GasVolcanoes");
		_model = GetNode<Node3D>("Model");
	}

	public override void _Process(double delta)
	{
		bool mineIsShownOnTop = HasMine && Mine.Visible;
		_model.Visible = !mineIsShownOnTop;
		CollisionLayer = mineIsShownOnTop ? 0 : Entity.NeutralLayer;
	}

	public int Take(int wanted)
	{
		int taken = Mathf.Min(wanted, Gas);
		Rpc(MethodName.SetGas, Gas - taken);
		return taken;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SetGas(int gas)
	{
		Gas = gas;
	}
}