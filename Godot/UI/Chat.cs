using Godot;

public partial class Chat : CanvasLayer
{
	private const int MaxMessageLength = 200;
	private const int HostPeerId = 1;

	private RichTextLabel _log;
	private LineEdit _input;

	public override void _Ready()
	{
		_log = GetNode<RichTextLabel>("%Log");
		_input = GetNode<LineEdit>("%Input");
		_input.TextSubmitted += OnTextSubmitted;
		_input.GuiInput += OnInputGuiInput;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (IsKeyPress(@event, Key.Enter) || IsKeyPress(@event, Key.KpEnter))
		{
			_input.GrabFocus();
			GetViewport().SetInputAsHandled();
		}
	}

	public void SendChatMessage(string text)
	{
		string trimmedText = text.StripEdges();
		if (trimmedText.Length == 0)
			return;

		RpcId(HostPeerId, MethodName.SubmitMessage, trimmedText);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SubmitMessage(string text)
	{
		if (!Multiplayer.IsServer())
			return;

		int senderTeam = NetworkManager.Instance.GetTeam(Multiplayer.GetRemoteSenderId());
		string limitedText = text.Length > MaxMessageLength ? text[..MaxMessageLength] : text;
		Rpc(MethodName.ReceiveMessage, $"Player {senderTeam + 1}", limitedText);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ReceiveMessage(string senderName, string text)
	{
		if (_log.GetParsedText().Length > 0)
			_log.Newline();
		_log.AddText($"{senderName}: {text}");
	}

	private void OnTextSubmitted(string text)
	{
		SendChatMessage(text);
		CloseInput();
	}

	private void OnInputGuiInput(InputEvent @event)
	{
		if (!IsKeyPress(@event, Key.Escape))
			return;

		CloseInput();
		_input.AcceptEvent();
	}

	private void CloseInput()
	{
		_input.Clear();
		_input.ReleaseFocus();
	}

	private static bool IsKeyPress(InputEvent @event, Key key) =>
		@event is InputEventKey { Pressed: true, Echo: false } keyEvent && keyEvent.Keycode == key;
}
