using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class Hud : CanvasLayer
{
	private static readonly Vector2 ButtonSize = new(170, 56);
	private static readonly Color NotPossibleTint = new(1, 1, 1, 0.45f);

	private PlayerInput _playerInput;
	private Label _scrapLabel;
	private Label _metalLabel;
	private Label _gasLabel;
	private Label _unitsLabel;
	private Label _selectionLabel;
	private Control _commandPanel;
	private TextureRect _portrait;
	private GridContainer _buttonGrid;
	private Control _gameOverScreen;
	private Label _gameOverLabel;

	private readonly List<Button> _buildButtons = new();
	private readonly List<Button> _unitButtons = new();
	private Button _upgradeButton;
	private Button _letWorkersOutButton;

	private static int LocalTeam => PlayerInput.LocalTeam;
	private static PlayerCommands Commands => Match.Instance.Commands;
	private TeamResources OwnResources => Match.Instance.ResourcesOf(LocalTeam);

	public override void _Ready()
	{
		_playerInput = GetNode<PlayerInput>("PlayerInput");
		_scrapLabel = GetNode<Label>("%ScrapLabel");
		_metalLabel = GetNode<Label>("%MetalLabel");
		_gasLabel = GetNode<Label>("%GasLabel");
		_unitsLabel = GetNode<Label>("%UnitsLabel");
		_selectionLabel = GetNode<Label>("%SelectionLabel");
		_commandPanel = GetNode<Control>("CommandPanel");
		_portrait = GetNode<TextureRect>("Portrait");
		_buttonGrid = GetNode<GridContainer>("%ButtonGrid");
		_gameOverScreen = GetNode<Control>("GameOver");
		_gameOverLabel = GetNode<Label>("%GameOverLabel");

		GetNode<Button>("%MenuButton").Pressed += () => NetworkManager.Instance.LeaveMatch();
		Match.Instance.GameOver += ShowGameOver;
		CreateCommandButtons();
	}

	public override void _Process(double delta)
	{
		UpdateResourceBar();
		UpdateSelectionInfo();
		UpdateCommandButtons();
	}

	private void CreateCommandButtons()
	{
		for (int i = 0; i < Match.Instance.BuildingTypes.Length; i++)
		{
			BuildingType type = Match.Instance.BuildingTypes[i];
			int typeIndex = i;
			Button button = AddButton($"{type.Name}\n{type.CostText()}", () => StartPlacingBuilding(typeIndex));
			_buildButtons.Add(button);
		}

		for (int i = 0; i < Match.Instance.UnitTypes.Length; i++)
		{
			UnitType type = Match.Instance.UnitTypes[i];
			int typeIndex = i;
			Button button = AddButton($"{type.Name}\n{type.CostText()}", () => TrainUnit(typeIndex));
			_unitButtons.Add(button);
		}

		_upgradeButton = AddButton("Bunker verbessern", UpgradeBunker);
		_letWorkersOutButton = AddButton("Arbeiter rausholen", () => Commands.LetWorkersOut(SelectedOwn<GasMine>()));
	}

	private Button AddButton(string text, System.Action onPressed)
	{
		var button = new Button { Text = text, CustomMinimumSize = ButtonSize, Visible = false };
		button.Pressed += onPressed;
		_buttonGrid.AddChild(button);
		return button;
	}

	private void StartPlacingBuilding(int typeIndex)
	{
		BuildingType type = Match.Instance.BuildingTypes[typeIndex];
		if (OwnResources.CanAfford(type.CostMetal, type.CostGas))
			_playerInput.StartPlacingBuilding(typeIndex);
		else
			Voices.Instance.SayWhatIsMissingFor(type.CostMetal, type.CostGas);
	}

	private void TrainUnit(int typeIndex)
	{
		UnitType type = Match.Instance.UnitTypes[typeIndex];
		if (type.MadeFromCount == 0)
		{
			if (OwnResources.CanAfford(type.CostMetal, type.CostGas))
				Commands.TrainInBuilding(SelectedOwn<Bunker>(), typeIndex);
			else
				Voices.Instance.SayWhatIsMissingFor(type.CostMetal, type.CostGas);
			return;
		}

		List<Unit> units = _playerInput.SelectedOwnUnits().Where(unit => unit.Kind == type.MadeFrom).ToList();
		bool workersMissing = units.Count < type.MadeFromCount;
		if (workersMissing || !OwnResources.CanAfford(type.CostMetal, type.CostGas))
		{
			Voices.Instance.SayWhatIsMissingFor(type.CostMetal, type.CostGas, workersMissing);
			return;
		}

		Commands.TrainUnits(units, typeIndex);
		Voices.Instance.PlayCommandLine(units);
	}

	private void UpgradeBunker()
	{
		Bunker bunker = SelectedOwn<Bunker>();
		if (OwnResources.CanAfford(bunker.UpgradeCostMetal, bunker.UpgradeCostGas))
			Commands.UpgradeBunker(bunker);
		else
			Voices.Instance.SayWhatIsMissingFor(bunker.UpgradeCostMetal, bunker.UpgradeCostGas);
	}

	private void UpdateResourceBar()
	{
		_scrapLabel.Text = $"Schrott: {Match.Instance.CountScrap(LocalTeam)}";
		_metalLabel.Text = $"Blech: {OwnResources.Metal}";
		_gasLabel.Text = $"Gas: {OwnResources.Gas}";
		_unitsLabel.Text = $"Einheiten: {Match.Instance.CountUnits(LocalTeam)}";
	}

	private void UpdateCommandButtons()
	{
		bool hasWorkers = _playerInput.SelectedOwnUnits().OfType<Worker>().Any();
		for (int i = 0; i < _buildButtons.Count; i++)
		{
			BuildingType type = Match.Instance.BuildingTypes[i];
			_buildButtons[i].Visible = hasWorkers;
			ShowAsPossible(_buildButtons[i], OwnResources.CanAfford(type.CostMetal, type.CostGas));
		}

		for (int i = 0; i < _unitButtons.Count; i++)
			UpdateUnitButton(_unitButtons[i], Match.Instance.UnitTypes[i]);

		Bunker bunker = SelectedOwn<Bunker>();
		_upgradeButton.Visible = bunker != null && bunker.UpgradedScene != null;
		if (bunker != null)
			_upgradeButton.Text = $"Bunker verbessern\n{bunker.UpgradeCostMetal} Blech, {bunker.UpgradeCostGas} Gas";
		_upgradeButton.Disabled = bunker == null || !bunker.CanBeUpgraded;
		ShowAsPossible(_upgradeButton, bunker != null && OwnResources.CanAfford(bunker.UpgradeCostMetal, bunker.UpgradeCostGas));

		GasMine gasMine = SelectedOwn<GasMine>();
		_letWorkersOutButton.Visible = gasMine != null;
		_letWorkersOutButton.Disabled = gasMine == null || gasMine.WorkersInside == 0;
	}

	private void UpdateUnitButton(Button button, UnitType type)
	{
		bool canAfford = OwnResources.CanAfford(type.CostMetal, type.CostGas);

		if (type.MadeFromCount == 0)
		{
			Building building = _playerInput.Selected.OfType<Building>().FirstOrDefault(b => b.Team == LocalTeam && b.Kind == type.TrainedAt);
			button.Visible = building != null && building.IsFinished;
			button.Disabled = building == null || !building.CanTrain(type);
			ShowAsPossible(button, canAfford);
			return;
		}

		int ingredientCount = _playerInput.SelectedOwnUnits().Count(unit => unit.Kind == type.MadeFrom);
		bool hasTrainingBuilding = Match.Instance.BuildingsOf(LocalTeam).Any(building => building.CanTrain(type));
		button.Visible = ingredientCount > 0;
		button.Disabled = !hasTrainingBuilding;
		ShowAsPossible(button, canAfford && ingredientCount >= type.MadeFromCount);
	}

	// Buttons for things the player can't afford stay clickable, so clicking them can say what is missing.
	private static void ShowAsPossible(Button button, bool possible)
	{
		button.Modulate = possible ? Colors.White : NotPossibleTint;
	}

	private T SelectedOwn<T>() where T : Building
	{
		return _playerInput.Selected.OfType<T>().FirstOrDefault(building => building.Team == LocalTeam);
	}

	private void UpdateSelectionInfo()
	{
		List<Node3D> selected = _playerInput.Selected;
		_commandPanel.Visible = selected.Count > 0;
		_portrait.Texture = selected.Count == 1 && selected[0] is Unit unit ? unit.Portrait : null;
		_portrait.Visible = _portrait.Texture != null;
		_selectionLabel.Text = selected.Count switch
		{
			0 => "",
			1 => DescribeEntity(selected[0]),
			_ => $"{selected.Count} ausgewählt",
		};
	}

	private static string DescribeEntity(Node3D entity)
	{
		return entity switch
		{
			Worker worker => $"{worker.DisplayName}  {worker.Health}/{worker.MaxHealth} LP"
				+ (worker.CarriedScrap > 0 ? $"\nträgt {worker.CarriedScrap} Schrott" : "")
				+ (worker.CarriedGas > 0 ? $"\nträgt {worker.CarriedGas} Gas" : ""),
			Unit unit => $"{unit.DisplayName}  {unit.Health}/{unit.MaxHealth} LP",
			Building building => DescribeBuilding(building),
			ScrapPile scrapPile => $"Schrotthaufen\nSchrott: {scrapPile.Amount}",
			_ => "",
		};
	}

	private static string DescribeBuilding(Building building)
	{
		string text = $"{building.DisplayName}  {building.ShownHealth}/{building.MaxHealth} LP";
		if (building.Team != LocalTeam)
			return text;

		if (!building.IsFinished)
			return text + $"\nwird gebaut: {Mathf.RoundToInt(building.BuildProgress * 100)}%";

		if (building is Bunker bunker)
			text += $"\nSchrott wartet: {bunker.StoredScrap}" + (bunker.IsUpgrading ? "\nwird verbessert" : "");
		if (building is GasMine gasMine)
			text += $"\nArbeiter drin: {gasMine.WorkersInside}/{gasMine.MaxWorkers}\nTräger: {gasMine.CarrierCount}\nGas bereit: {gasMine.StoredGas}"
				+ (gasMine.Volcano != null ? $"\nGas im Vulkan: {gasMine.Volcano.Gas}" : "");
		if (building.QueueLength > 0)
			text += $"\nin Ausbildung: {building.QueueLength}";
		return text;
	}

	private void ShowGameOver(int losingTeam)
	{
		_gameOverLabel.Text = losingTeam == LocalTeam ? "Verloren!" : "Gewonnen!";
		_gameOverScreen.Visible = true;
	}
}
