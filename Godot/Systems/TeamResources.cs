using Godot;

public partial class TeamResources : Node
{
	[Export] public int Metal { get; set; }
	[Export] public int Gas { get; set; }

	public bool CanAfford(int metal, int gas)
	{
		return HasEnoughMetal(metal) && HasEnoughGas(gas);
	}

	public bool HasEnoughMetal(int metal)
	{
		return Metal >= metal;
	}

	public bool HasEnoughGas(int gas)
	{
		return Gas >= gas;
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
