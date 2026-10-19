using Godot;

public partial class TeamResources : Node
{
	[Export] public int Metal { get; set; }
	[Export] public int Gas { get; set; }

	public bool CanAfford(int metal, int gas)
	{
		return Metal >= metal && Gas >= gas;
	}

	public bool TrySpend(int metal, int gas)
	{
		if (!CanAfford(metal, gas))
			return false;

		Metal -= metal;
		Gas -= gas;
		return true;
	}
}
