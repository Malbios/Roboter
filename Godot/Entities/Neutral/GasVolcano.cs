using Godot;

public partial class GasVolcano : StaticBody3D, IEntity
{
	[Export] public int Gas { get; set; } = 1000;
	[Export] public float Radius { get; set; } = 2;
	// The gas mine is built against the volcano: its back (-Z side) touches the volcano's front (+Z side).
	[Export] public float MineDockingDistance { get; set; } = 2.85f;

	public GasMine Mine { get; set; }
	public bool HasMine => Entity.IsInstanceValid(Mine) && !Mine.IsQueuedForDeletion();
	public Vector3 MineDockPosition => GlobalPosition + GlobalBasis.Z.Normalized() * MineDockingDistance;

	public override void _Ready()
	{
		AddToGroup("GasVolcanoes");
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