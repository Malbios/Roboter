using Godot;

// A sound that plays once at a place in the world and then removes itself.
// It lives on its own, so it isn't cut off when the arrow or explosion that made it is already gone.
public static class SoundEffect
{
	public static void PlayAt(AudioStream sound, Vector3 position)
	{
		if (sound == null)
			return;

		var player = new AudioStreamPlayer3D { Stream = sound };
		player.Finished += player.QueueFree;
		Match.Instance.AddChild(player);
		player.GlobalPosition = position;
		player.Play();
	}
}
