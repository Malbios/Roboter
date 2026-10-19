using System.Linq;
using Godot;

public partial class HealthBars : Control
{
	private const float UnitBarWidth = 30;
	private const float BuildingBarWidth = 60;
	private const float BarHeight = 4;
	private static readonly Color BackgroundColor = new(0, 0, 0, 0.7f);
	private static readonly Color OwnColor = new(0.3f, 0.9f, 0.3f);
	private static readonly Color EnemyColor = new(0.95f, 0.25f, 0.2f);
	private static readonly Color ProgressColor = new(1f, 0.85f, 0.2f);

	public override void _Process(double delta)
	{
		QueueRedraw();
	}

	public override void _Draw()
	{
		Camera3D camera = GetViewport().GetCamera3D();
		if (camera == null)
			return;

		foreach (Unit unit in GetTree().GetNodesInGroup("Units").OfType<Unit>())
			DrawBars(camera, unit.GlobalPosition + Vector3.Up * unit.HealthBarHeight, UnitBarWidth, unit.Health, unit.MaxHealth, unit.Team, 0);

		foreach (Building building in GetTree().GetNodesInGroup("Buildings").OfType<Building>())
		{
			float progress = building.IsFinished ? building.WorkProgress : building.BuildProgress;
			DrawBars(camera, building.GlobalPosition + Vector3.Up * building.HealthBarHeight, BuildingBarWidth, building.Health, building.MaxHealth, building.Team, progress);
		}
	}

	private void DrawBars(Camera3D camera, Vector3 worldPosition, float width, int health, int maxHealth, int team, float progress)
	{
		if (camera.IsPositionBehind(worldPosition))
			return;

		Vector2 center = camera.UnprojectPosition(worldPosition);
		var healthBar = new Rect2(center.X - width / 2, center.Y, width, BarHeight);
		if (!GetViewportRect().Intersects(healthBar))
			return;

		Color color = team == PlayerInput.LocalTeam ? OwnColor : EnemyColor;
		DrawFilledBar(healthBar, (float)health / maxHealth, color);

		if (progress > 0)
			DrawFilledBar(new Rect2(healthBar.Position + new Vector2(0, BarHeight + 1), healthBar.Size), progress, ProgressColor);
	}

	private void DrawFilledBar(Rect2 bar, float fraction, Color color)
	{
		DrawRect(bar, BackgroundColor);
		DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * Mathf.Clamp(fraction, 0, 1), bar.Size.Y)), color);
	}
}
