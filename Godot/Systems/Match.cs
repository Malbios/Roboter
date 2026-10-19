using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class Match : Node3D
{
	private const int TeamCount = 2;
	private const float LoseCheckInterval = 1;
	private const float WorkerRingDistance = 2;
	private const float ExtraUnitRingDistance = 4.5f;

	[Signal] public delegate void GameOverEventHandler(int losingTeam);

	public static Match Instance { get; private set; }

	[Export] public Node3D StartPositions { get; set; }
	[Export] public bool ShuffleStartPositions { get; set; } = true;
	[Export] public int StartWorkers { get; set; } = 12;
	[Export] public int StartMetal { get; set; }
	[Export] public int StartGas { get; set; }
	[Export] public PackedScene[] ExtraStartUnits { get; set; } = [];
	[Export] public PackedScene WorkerScene { get; set; }
	[Export] public PackedScene BunkerScene { get; set; }
	[Export] public PackedScene ScrapPileScene { get; set; }
	[Export] public PackedScene ExplosionScene { get; set; }
	[Export] public UnitType[] UnitTypes { get; set; } = [];
	[Export] public BuildingType[] BuildingTypes { get; set; } = [];
	[Export] public Vector2 MapCenter { get; set; }
	[Export] public float MapSize { get; set; } = 100;

	public bool IsOver { get; private set; }
	public PlayerCommands Commands { get; private set; }

	private Node3D _entities;
	private Node3D _scrapPiles;
	private bool _hasStarted;
	private int _nextSpawnNumber = 1;
	private float _loseCheckTimer;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		_entities = GetNode<Node3D>("Entities");
		_scrapPiles = GetNode<Node3D>("ScrapPiles");
		Commands = GetNode<PlayerCommands>("Commands");
		NetworkManager.Instance.ReportSceneLoaded();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!Multiplayer.IsServer())
			return;

		if (!_hasStarted)
		{
			if (NetworkManager.Instance.AllPeersLoaded && IsNavigationReady())
				StartMatch();
			return;
		}

		_loseCheckTimer -= (float)delta;
		if (_loseCheckTimer <= 0 && !IsOver)
		{
			_loseCheckTimer = LoseCheckInterval;
			CheckForLosers();
		}
	}

	public TeamResources ResourcesOf(int team)
	{
		return GetNode<TeamResources>($"Team{team}");
	}

	public IEnumerable<Unit> UnitsOf(int team)
	{
		return GetTree().GetNodesInGroup("Units").OfType<Unit>().Where(unit => unit.Team == team && Entity.IsAlive(unit));
	}

	public IEnumerable<Building> BuildingsOf(int team)
	{
		return GetTree().GetNodesInGroup("Buildings").OfType<Building>().Where(building => building.Team == team && Entity.IsAlive(building));
	}

	public IEnumerable<Bunker> FinishedBunkersOf(int team)
	{
		return BuildingsOf(team).OfType<Bunker>().Where(bunker => bunker.IsFinished);
	}

	public int CountUnits(int team)
	{
		int unitsInsideBuildings = BuildingsOf(team).Sum(building => building.QueueLength + (building is GasMine mine ? mine.WorkersInside : 0));
		return UnitsOf(team).Count() + unitsInsideBuildings;
	}

	public int CountScrap(int team)
	{
		return BuildingsOf(team).OfType<Bunker>().Sum(bunker => bunker.StoredScrap);
	}

	public Vector3 SnapToGround(Vector3 position)
	{
		Vector3 onNavigationMesh = NavigationServer3D.MapGetClosestPoint(GetWorld3D().NavigationMap, position);
		return GroundBelow(onNavigationMesh) ?? onNavigationMesh;
	}

	public Unit CreateUnit(PackedScene scene, int team, Vector3 position)
	{
		Unit unit = scene.Instantiate<Unit>();
		PrepareEntity(unit, position);
		unit.Team = team;
		return unit;
	}

	public T CreateBuilding<T>(PackedScene scene, int team, Vector3 position) where T : Building
	{
		T building = scene.Instantiate<T>();
		PrepareEntity(building, position);
		building.Team = team;
		building.SpawnNumber = _nextSpawnNumber;
		return building;
	}

	public void AddEntity(Node3D entity)
	{
		_entities.AddChild(entity);
	}

	public Unit SpawnUnit(PackedScene scene, int team, Vector3 position)
	{
		Unit unit = CreateUnit(scene, team, position);
		AddEntity(unit);
		return unit;
	}

	public Unit SpawnUnitNextTo(PackedScene scene, int team, Building building)
	{
		float angle = (float)GD.RandRange(0, Mathf.Tau);
		Vector3 offset = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * (building.Radius + 1);
		return SpawnUnit(scene, team, building.GlobalPosition + offset);
	}

	public void EntityDestroyed(Node3D entity)
	{
		Rpc(MethodName.ShowExplosion, entity.GlobalPosition);
		SpawnScrapPile(entity.GlobalPosition);
	}

	public void SpawnScrapPile(Vector3 position)
	{
		Rpc(MethodName.CreateScrapPile, $"Scrap{_nextSpawnNumber++}", SnapToGround(position));
	}

	public Node3D FindNearestEnemy(Node3D seeker, float range)
	{
		IEnumerable<Node3D> candidates = GetTree().GetNodesInGroup("Units").OfType<Node3D>()
			.Concat(GetTree().GetNodesInGroup("Buildings").OfType<Node3D>());

		Node3D nearest = null;
		float nearestDistance = range;
		foreach (Node3D candidate in candidates)
		{
			if (!Entity.AreEnemies(seeker, candidate) || !Entity.IsAlive(candidate))
				continue;

			float distance = Entity.EdgeDistance(seeker, candidate);
			if (distance <= nearestDistance)
			{
				nearest = candidate;
				nearestDistance = distance;
			}
		}
		return nearest;
	}

	public Bunker FindNearestBunker(int team, Vector3 position)
	{
		return FinishedBunkersOf(team)
			.OrderBy(bunker => Entity.FlatDistance(bunker.GlobalPosition, position))
			.FirstOrDefault();
	}

	public ScrapPile FindScrapPileForTeam(int team, Vector3 workerPosition)
	{
		Bunker oldestBunker = FinishedBunkersOf(team).OrderBy(bunker => bunker.SpawnNumber).FirstOrDefault();
		Vector3 searchFrom = oldestBunker?.GlobalPosition ?? workerPosition;

		return GetTree().GetNodesInGroup("ScrapPiles").OfType<ScrapPile>()
			.Where(pile => pile.Amount > 0 && !pile.IsQueuedForDeletion())
			.OrderBy(pile => Entity.FlatDistance(pile.GlobalPosition, searchFrom))
			.FirstOrDefault();
	}

	public GasVolcano FindVolcanoAt(Vector3 position)
	{
		return GetTree().GetNodesInGroup("GasVolcanoes").OfType<GasVolcano>()
			.Where(volcano => Entity.FlatDistance(volcano.GlobalPosition, position) <= volcano.Radius + 1)
			.OrderBy(volcano => Entity.FlatDistance(volcano.GlobalPosition, position))
			.FirstOrDefault();
	}

	public bool CanPlaceBuilding(BuildingType type, Vector3 position, out Vector3 placedPosition)
	{
		placedPosition = position;

		if (type.Kind == BuildingKind.GasMine)
		{
			GasVolcano volcano = FindVolcanoAt(position);
			if (volcano == null || volcano.HasMine)
				return false;

			placedPosition = volcano.GlobalPosition;
			return true;
		}

		if (Entity.FlatDistance(SnapToGround(position), position) > 0.5f)
			return false;

		IEnumerable<Node3D> obstacles = GetTree().GetNodesInGroup("Buildings").OfType<Node3D>()
			.Concat(GetTree().GetNodesInGroup("ScrapPiles").OfType<Node3D>())
			.Concat(GetTree().GetNodesInGroup("GasVolcanoes").OfType<Node3D>())
			.Concat(GetTree().GetNodesInGroup("Obstacles").OfType<Node3D>());

		foreach (Node3D obstacle in obstacles)
		{
			if (Entity.FlatDistance(obstacle.GlobalPosition, position) < type.Radius + Entity.RadiusOf(obstacle))
				return false;
		}
		return true;
	}

	private void PrepareEntity(Node3D entity, Vector3 position)
	{
		entity.Name = $"{entity.Name}{_nextSpawnNumber}";
		entity.Position = SnapToGround(position) - _entities.GlobalPosition;
		_nextSpawnNumber += 1;
	}

	private bool IsNavigationReady()
	{
		if (NavigationServer3D.MapGetIterationId(GetWorld3D().NavigationMap) == 0)
			return false;

		Vector3 firstStart = StartPositions.GetChild<Node3D>(0).GlobalPosition;
		Vector3 closestWalkablePoint = NavigationServer3D.MapGetClosestPoint(GetWorld3D().NavigationMap, firstStart);
		return Entity.FlatDistance(closestWalkablePoint, firstStart) < 1;
	}

	// The navigation mesh floats a bit above the real ground, so look for the real ground just below it.
	// Bridges have no collision, so on a bridge nothing is hit and the navigation mesh height is used.
	private Vector3? GroundBelow(Vector3 point)
	{
		var query = PhysicsRayQueryParameters3D.Create(point + Vector3.Up, point + Vector3.Down * 2, Entity.WorldLayer);
		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		return hit.Count > 0 ? (Vector3)hit["position"] : null;
	}

	private void StartMatch()
	{
		_hasStarted = true;

		List<Marker3D> markers = StartPositions.GetChildren().OfType<Marker3D>().ToList();
		if (ShuffleStartPositions)
			markers = markers.OrderBy(_ => GD.Randi()).ToList();

		for (int team = 0; team < TeamCount; team++)
		{
			TeamResources resources = ResourcesOf(team);
			resources.Metal = StartMetal;
			resources.Gas = StartGas;
			resources.GetNode<MultiplayerSynchronizer>("MultiplayerSynchronizer").PublicVisibility = true;
			SpawnStartingBase(team, markers[team]);
		}
	}

	private void SpawnStartingBase(int team, Marker3D marker)
	{
		Vector3 center = marker.GlobalPosition;
		Bunker bunker = CreateBuilding<Bunker>(BunkerScene, team, center);
		bunker.Rotation = marker.Rotation;
		AddEntity(bunker);

		for (int i = 0; i < StartWorkers; i++)
			SpawnUnit(WorkerScene, team, center + marker.GlobalBasis * PointOnRing(i, StartWorkers, bunker.Radius + WorkerRingDistance));

		for (int i = 0; i < ExtraStartUnits.Length; i++)
			SpawnUnit(ExtraStartUnits[i], team, center + marker.GlobalBasis * PointOnRing(i, ExtraStartUnits.Length, bunker.Radius + ExtraUnitRingDistance));

		Rpc(MethodName.FocusCamera, team, center, marker.GlobalRotation.Y);
	}

	private static Vector3 PointOnRing(int index, int count, float distance)
	{
		float angle = Mathf.Tau * index / count;
		return new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * distance;
	}

	private void CheckForLosers()
	{
		for (int team = 0; team < TeamCount; team++)
		{
			bool hasNothingLeft = CountUnits(team) == 0 && !FinishedBunkersOf(team).Any();
			if (hasNothingLeft)
			{
				Rpc(MethodName.EndMatch, team);
				return;
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void FocusCamera(int team, Vector3 position, float yRotation)
	{
		if (team != NetworkManager.Instance.LocalTeam)
			return;

		Node3D camera = GetNode<Node3D>("RtsController");
		camera.GlobalPosition = position;
		camera.GlobalRotation = new Vector3(0, yRotation, 0);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ShowExplosion(Vector3 position)
	{
		Node3D explosion = ExplosionScene.Instantiate<Node3D>();
		AddChild(explosion);
		explosion.GlobalPosition = position + Vector3.Up * 0.5f;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void CreateScrapPile(string name, Vector3 position)
	{
		ScrapPile pile = ScrapPileScene.Instantiate<ScrapPile>();
		pile.Name = name;
		_scrapPiles.AddChild(pile);
		pile.GlobalPosition = position;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void EndMatch(int losingTeam)
	{
		IsOver = true;
		EmitSignal(SignalName.GameOver, losingTeam);
	}
}
