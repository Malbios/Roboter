using Godot;

public partial class ScrapPile : StaticBody3D, IEntity
{
	[Export] public int Amount { get; set; } = 70;
	[Export] public float Radius { get; set; } = 0.4f;

	public override void _Ready()
	{
		AddToGroup("ScrapPiles");
	}

	public int Take(int wanted)
	{
		int taken = Mathf.Min(wanted, Amount);
		Rpc(MethodName.SetAmount, Amount - taken);
		return taken;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SetAmount(int amount)
	{
		Amount = amount;
		if (Amount <= 0)
			QueueFree();
	}
}
