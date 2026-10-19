using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class PlayerCommands : Node
{
	private const int HostPeerId = 1;
	private const float FormationSpacing = 1.5f;

	public void RightClick(IEnumerable<Unit> units, Node3D target, Vector3 point)
	{
		string targetPath = target != null ? target.GetPath().ToString() : "";
		RpcId(HostPeerId, MethodName.RequestRightClick, PathsOf(units), targetPath, point);
	}

	public void PlaceBuilding(IEnumerable<Unit> workers, int buildingTypeIndex, Vector3 position)
	{
		RpcId(HostPeerId, MethodName.RequestPlaceBuilding, PathsOf(workers), buildingTypeIndex, position);
	}

	public void TrainUnits(IEnumerable<Unit> units, int unitTypeIndex)
	{
		RpcId(HostPeerId, MethodName.RequestTrainUnits, PathsOf(units), unitTypeIndex);
	}

	public void TrainInBuilding(Building building, int unitTypeIndex)
	{
		RpcId(HostPeerId, MethodName.RequestTrainInBuilding, building.GetPath().ToString(), unitTypeIndex);
	}

	public void UpgradeBunker(Bunker bunker)
	{
		RpcId(HostPeerId, MethodName.RequestUpgradeBunker, bunker.GetPath().ToString());
	}

	public void LetWorkersOut(GasMine gasMine)
	{
		RpcId(HostPeerId, MethodName.RequestLetWorkersOut, gasMine.GetPath().ToString());
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RequestRightClick(string[] unitPaths, string targetPath, Vector3 point)
	{
		if (!Multiplayer.IsServer() || Match.Instance.IsOver)
			return;

		List<Unit> units = OwnUnits(unitPaths);
		Node3D target = targetPath == "" ? null : GetNodeOrNull<Node3D>(targetPath);

		for (int i = 0; i < units.Count; i++)
			units[i].RightClick(target, point + FormationOffset(i, units.Count));
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RequestPlaceBuilding(string[] workerPaths, int buildingTypeIndex, Vector3 position)
	{
		if (!Multiplayer.IsServer() || Match.Instance.IsOver)
			return;

		List<Worker> workers = OwnUnits(workerPaths).OfType<Worker>().ToList();
		BuildingType type = Match.Instance.BuildingTypes[buildingTypeIndex];
		if (workers.Count == 0 || !Match.Instance.CanPlaceBuilding(type, position, out Vector3 placedPosition, out float placedRotation))
			return;

		if (!Match.Instance.ResourcesOf(SenderTeam()).TrySpend(type.CostMetal, type.CostGas))
			return;

		Building building = Match.Instance.CreateBuilding<Building>(type.Scene, SenderTeam(), placedPosition);
		building.BuildProgress = 0;
		building.Rotation = new Vector3(0, placedRotation, 0);
		Match.Instance.AddEntity(building);

		foreach (Worker worker in workers)
			worker.Build(building);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RequestTrainUnits(string[] unitPaths, int unitTypeIndex)
	{
		if (!Multiplayer.IsServer() || Match.Instance.IsOver)
			return;

		UnitType type = Match.Instance.UnitTypes[unitTypeIndex];
		List<Unit> units = OwnUnits(unitPaths).Where(unit => unit.Kind == type.MadeFrom).ToList();
		if (units.Count == 0)
			return;

		Building building = Match.Instance.BuildingsOf(SenderTeam())
			.Where(candidate => candidate.CanTrain(type))
			.OrderBy(candidate => Entity.FlatDistance(candidate.GlobalPosition, units[0].GlobalPosition))
			.FirstOrDefault();
		if (building == null)
			return;

		foreach (Unit unit in units)
			unit.GoToTraining(building, type);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RequestTrainInBuilding(string buildingPath, int unitTypeIndex)
	{
		if (!Multiplayer.IsServer() || Match.Instance.IsOver)
			return;

		Building building = OwnBuilding<Building>(buildingPath);
		UnitType type = Match.Instance.UnitTypes[unitTypeIndex];
		if (building == null || !building.CanTrain(type) || type.MadeFromCount > 0)
			return;

		if (Match.Instance.ResourcesOf(SenderTeam()).TrySpend(type.CostMetal, type.CostGas))
			building.AddToTrainingQueue(type);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RequestUpgradeBunker(string bunkerPath)
	{
		if (!Multiplayer.IsServer() || Match.Instance.IsOver)
			return;

		Bunker bunker = OwnBuilding<Bunker>(bunkerPath);
		if (bunker == null || !bunker.CanBeUpgraded)
			return;

		if (Match.Instance.ResourcesOf(SenderTeam()).TrySpend(bunker.UpgradeCostMetal, bunker.UpgradeCostGas))
			bunker.StartUpgrade();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RequestLetWorkersOut(string gasMinePath)
	{
		if (!Multiplayer.IsServer() || Match.Instance.IsOver)
			return;

		OwnBuilding<GasMine>(gasMinePath)?.LetWorkersOut();
	}

	private int SenderTeam()
	{
		return NetworkManager.Instance.GetTeam(Multiplayer.GetRemoteSenderId());
	}

	private List<Unit> OwnUnits(string[] paths)
	{
		int team = SenderTeam();
		return paths
			.Select(path => GetNodeOrNull<Unit>(path))
			.Where(unit => Entity.IsAlive(unit) && unit.Team == team)
			.ToList();
	}

	private T OwnBuilding<T>(string path) where T : Building
	{
		T building = GetNodeOrNull<T>(path);
		return Entity.IsAlive(building) && building.Team == SenderTeam() ? building : null;
	}

	private static string[] PathsOf(IEnumerable<Unit> units)
	{
		return units.Select(unit => unit.GetPath().ToString()).ToArray();
	}

	private static Vector3 FormationOffset(int index, int count)
	{
		int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
		int row = index / columns;
		int column = index % columns;
		float centerShift = (columns - 1) * 0.5f;
		return new Vector3(column - centerShift, 0, row - centerShift) * FormationSpacing;
	}
}
