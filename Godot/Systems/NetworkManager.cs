using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class NetworkManager : Node
{
	public const int Port = 7777;
	private const int MaxClients = 1;
	private const int HostTeam = 0;
	private const int ClientTeam = 1;
	private const string MatchScenePath = "res://Maps/1v1.tscn";
	private const string MainMenuScenePath = "res://Systems/UI/MainMenu.tscn";
	private const string LoadingScreenScenePath = "res://Systems/UI/LoadingScreen.tscn";

	[Signal] public delegate void StatusChangedEventHandler(string message);

	public static NetworkManager Instance { get; private set; }

	public string LastStatus { get; private set; } = "";

	private readonly Dictionary<long, int> _teamsByPeer = new();
	private readonly HashSet<long> _peersWithLoadedMatch = new();
	private LoadingScreen _loadingScreen;
	private bool _isLoadingMatch;

	public int LocalTeam => GetTeam(Multiplayer.GetUniqueId());

	public bool AllPeersLoaded => Multiplayer.GetPeers().All(peerId => _peersWithLoadedMatch.Contains(peerId));

	public override void _Ready()
	{
		Instance = this;
		_loadingScreen = GD.Load<PackedScene>(LoadingScreenScenePath).Instantiate<LoadingScreen>();
		AddChild(_loadingScreen);
		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ConnectedToServer += () => SetStatus("Connected, waiting for the host to start...");
		Multiplayer.ConnectionFailed += () => ReturnToMainMenu("Could not connect to host.");
		Multiplayer.ServerDisconnected += () => ReturnToMainMenu("Lost connection to host.");
	}

public override void _Process(double delta)
	{
		if (_isLoadingMatch)
			ContinueLoadingMatch();
	}

	public bool Host()
	{
		var peer = new ENetMultiplayerPeer();
		Error error = peer.CreateServer(Port, MaxClients);
		if (error != Error.Ok)
		{
			SetStatus($"Could not host on port {Port}: {error}");
			return false;
		}

		Multiplayer.MultiplayerPeer = peer;
		_teamsByPeer.Clear();
		_teamsByPeer[Multiplayer.GetUniqueId()] = HostTeam;
		SetStatus($"Hosting on port {Port}, waiting for opponent...");
		return true;
	}

	public bool Join(string address)
	{
		var peer = new ENetMultiplayerPeer();
		Error error = peer.CreateClient(address, Port);
		if (error != Error.Ok)
		{
			SetStatus($"Could not connect to {address}: {error}");
			return false;
		}

		Multiplayer.MultiplayerPeer = peer;
		SetStatus($"Connecting to {address}:{Port}...");
		return true;
	}

	public void ReportSceneLoaded()
	{
		if (!Multiplayer.IsServer())
			RpcId(1, MethodName.PeerLoadedMatch);
	}

public void MatchHasStarted()
	{
		_loadingScreen.HideScreen();
	}

	public void LeaveMatch()
	{
		ReturnToMainMenu("");
	}

	// Unknown peers (including the offline peer when a map is run directly) fall back to the host team.
	public int GetTeam(long peerId) => _teamsByPeer.GetValueOrDefault(peerId, HostTeam);

	private void OnPeerConnected(long peerId)
	{
		if (!Multiplayer.IsServer())
			return;

		_teamsByPeer[peerId] = ClientTeam;
		Rpc(MethodName.StartMatch, new Godot.Collections.Dictionary<long, int>(_teamsByPeer));
	}

	private void OnPeerDisconnected(long peerId)
	{
		if (Multiplayer.IsServer())
			ReturnToMainMenu("Opponent disconnected.");
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void PeerLoadedMatch()
	{
		_peersWithLoadedMatch.Add(Multiplayer.GetRemoteSenderId());
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void StartMatch(Godot.Collections.Dictionary<long, int> teamsByPeer)
	{
		_teamsByPeer.Clear();
		_peersWithLoadedMatch.Clear();
		foreach (var (peerId, team) in teamsByPeer)
			_teamsByPeer[peerId] = team;

		SetStatus("");
		_loadingScreen.ShowLoading(0);
		ResourceLoader.LoadThreadedRequest(MatchScenePath);
		_isLoadingMatch = true;
	}

	// The map is big, so it is loaded in the background while the loading screen shows the progress.
	private void ContinueLoadingMatch()
	{
		var progress = new Godot.Collections.Array();
		ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(MatchScenePath, progress);
		if (status == ResourceLoader.ThreadLoadStatus.InProgress)
		{
			_loadingScreen.ShowLoading((float)progress[0]);
			return;
		}

		_isLoadingMatch = false;
		if (status != ResourceLoader.ThreadLoadStatus.Loaded)
		{
			ReturnToMainMenu("Could not load the map.");
			return;
		}

		_loadingScreen.ShowWaitingForOtherPlayer();
		GetTree().ChangeSceneToPacked((PackedScene)ResourceLoader.LoadThreadedGet(MatchScenePath));
	}

	private void ReturnToMainMenu(string reason)
	{
		// Swap the peer out before closing it so its shutdown can't re-trigger these handlers.
		MultiplayerPeer closingPeer = Multiplayer.MultiplayerPeer;
		Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
		closingPeer?.Close();

		_teamsByPeer.Clear();
		_peersWithLoadedMatch.Clear();
		_isLoadingMatch = false;
		_loadingScreen.HideScreen();
		SetStatus(reason);
		GetTree().ChangeSceneToFile(MainMenuScenePath);
	}

	private void SetStatus(string message)
	{
		LastStatus = message;
		EmitSignal(SignalName.StatusChanged, message);
	}
}
