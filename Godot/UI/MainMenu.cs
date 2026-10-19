using Godot;

public partial class MainMenu : Control
{
	private const string JoinArgument = "--join=";

	private static bool _commandLineHandled;

	private Button _hostButton;
	private Button _joinButton;
	private LineEdit _addressInput;
	private Label _statusLabel;

	public override void _Ready()
	{
		_hostButton = GetNode<Button>("%HostButton");
		_joinButton = GetNode<Button>("%JoinButton");
		_addressInput = GetNode<LineEdit>("%AddressInput");
		_statusLabel = GetNode<Label>("%StatusLabel");

		_hostButton.Pressed += OnHostPressed;
		_joinButton.Pressed += OnJoinPressed;
		GetNode<Button>("%QuitButton").Pressed += () => GetTree().Quit();
		NetworkManager.Instance.StatusChanged += ShowStatus;
		ShowStatus(NetworkManager.Instance.LastStatus);
		HandleCommandLineOnce();
	}

	public override void _ExitTree()
	{
		NetworkManager.Instance.StatusChanged -= ShowStatus;
	}

	private void OnHostPressed()
	{
		if (NetworkManager.Instance.Host())
			DisableButtons();
	}

	private void OnJoinPressed()
	{
		if (NetworkManager.Instance.Join(_addressInput.Text.StripEdges()))
			DisableButtons();
	}

	private void HandleCommandLineOnce()
	{
		if (_commandLineHandled)
			return;

		_commandLineHandled = true;
		foreach (string argument in OS.GetCmdlineUserArgs())
		{
			if (argument == "--host")
			{
				OnHostPressed();
			}
			else if (argument.StartsWith(JoinArgument))
			{
				_addressInput.Text = argument[JoinArgument.Length..];
				OnJoinPressed();
			}
		}
	}

	private void DisableButtons()
	{
		_hostButton.Disabled = true;
		_joinButton.Disabled = true;
	}

	private void ShowStatus(string message)
	{
		_statusLabel.Text = message;
	}
}
