using Godot;

public partial class GasVolcano : StaticBody3D, IEntity
{
	[Export] public int Gas { get; set; } = 1000;
	[Export] public float Radius { get; set; } = 2;

	public bool HasMine
	{
		get => _hasMine;
		set
		{
			_hasMine = value;
			GetNode<Node3D>("Model").Visible = !value;
			CollisionLayer = value ? 0 : Entity.NeutralLayer;
		}
	}

	private bool _hasMine;

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
