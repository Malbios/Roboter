using Godot;

public interface IEntity
{
	float Radius { get; }
}

public interface IDamageable : IEntity
{
	int Team { get; }
	int Health { get; }
	int MaxHealth { get; }
	void TakeDamage(int damage, Node3D attacker);
}

public static class Entity
{
	public const int NoTeam = -1;

	public const uint WorldLayer = 1;
	public const uint UnitLayer = 2;
	public const uint BuildingLayer = 4;
	public const uint NeutralLayer = 8;

	public static bool IsAlive(Node3D node)
	{
		return IsInstanceValid(node) && node.IsInsideTree() && !node.IsQueuedForDeletion()
			&& node is IDamageable damageable && damageable.Health > 0;
	}

	public static bool IsInstanceValid(Node3D node)
	{
		return node != null && GodotObject.IsInstanceValid(node);
	}

	public static bool AreEnemies(Node3D a, Node3D b)
	{
		return a is IDamageable first && b is IDamageable second
			&& first.Team != NoTeam && second.Team != NoTeam && first.Team != second.Team;
	}

	public static float FlatDistance(Vector3 a, Vector3 b)
	{
		return new Vector2(a.X - b.X, a.Z - b.Z).Length();
	}

	public static float EdgeDistance(Node3D a, Node3D b)
	{
		return FlatDistance(a.GlobalPosition, b.GlobalPosition) - RadiusOf(a) - RadiusOf(b);
	}

	public static float RadiusOf(Node3D node)
	{
		return node is IEntity entity ? entity.Radius : 0;
	}
}
