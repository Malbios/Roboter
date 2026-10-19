using Godot;

public partial class LoadingScreen : CanvasLayer
{
	private Label _message;
	private ProgressBar _progress;

	public override void _Ready()
	{
		_message = GetNode<Label>("%Message");
		_progress = GetNode<ProgressBar>("%Progress");
		Visible = false;
	}

	public void ShowLoading(float progress)
	{
		Visible = true;
		_message.Text = "Spiel wird geladen ...";
		_progress.Visible = true;
		_progress.Value = progress * 100;
	}

	public void ShowWaitingForOtherPlayer()
	{
		Visible = true;
		_message.Text = "Warte auf den anderen Spieler ...";
		_progress.Visible = false;
	}

	public void HideScreen()
	{
		Visible = false;
	}
}
