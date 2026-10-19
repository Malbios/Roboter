using System.Collections.Generic;
using System.Linq;
using Godot;

// Plays the voice lines the local player hears: the announcer and the units answering.
public partial class Voices : Node
{
	[Export] public AudioStream NoMetalSound { get; set; }
	[Export] public AudioStream NoGasSound { get; set; }
	[Export] public AudioStream NoResourcesSound { get; set; }
	[Export] public AudioStream NoWorkersSound { get; set; }

	public static Voices Instance { get; private set; }

	private AudioStreamPlayer _announcer;
	private AudioStreamPlayer _unitVoice;
	private readonly Queue<AudioStream> _announcementsToSay = new();

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		_announcer = GetNode<AudioStreamPlayer>("Announcer");
		_unitVoice = GetNode<AudioStreamPlayer>("UnitVoice");
		_announcer.Finished += SayNextAnnouncement;
	}

	public void SayMissing(bool workers, bool metal, bool gas)
	{
		if (_announcer.Playing || _announcementsToSay.Count > 0)
			return;

		if (workers)
			_announcementsToSay.Enqueue(NoWorkersSound);
		if (metal && gas)
			_announcementsToSay.Enqueue(NoResourcesSound);
		else if (metal)
			_announcementsToSay.Enqueue(NoMetalSound);
		else if (gas)
			_announcementsToSay.Enqueue(NoGasSound);

		SayNextAnnouncement();
	}

	public void SayWhatIsMissingFor(int metal, int gas, bool workersMissing = false)
	{
		TeamResources resources = Match.Instance.ResourcesOf(PlayerInput.LocalTeam);
		SayMissing(workersMissing, !resources.HasEnoughMetal(metal), !resources.HasEnoughGas(gas));
	}

	public void PlaySelectedLine(IEnumerable<Unit> units)
	{
		PlayUnitLine(units.Select(unit => unit.SelectedSound).FirstOrDefault(sound => sound != null));
	}

	public void PlayCommandLine(IEnumerable<Unit> units)
	{
		PlayUnitLine(units.Select(unit => unit.CommandSound).FirstOrDefault(sound => sound != null));
	}

	private void PlayUnitLine(AudioStream line)
	{
		if (line == null)
			return;

		_unitVoice.Stream = line;
		_unitVoice.Play();
	}

	private void SayNextAnnouncement()
	{
		if (_announcementsToSay.Count == 0)
			return;

		_announcer.Stream = _announcementsToSay.Dequeue();
		_announcer.Play();
	}
}
