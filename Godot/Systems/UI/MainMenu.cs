using Godot;

public partial class MainMenu : Control
{
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
		NetworkManager.Instance.StatusChanged += ShowStatus;
		ShowStatus(NetworkManager.Instance.LastStatus);
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
