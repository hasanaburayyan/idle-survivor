using Godot;
using SpacetimeDB;
using SpacetimeDB.Types;
using System.Collections.Generic;

public partial class PlayerStatsPanel : VBoxContainer
{
	private readonly Dictionary<ulong, Label> valueLabels = new();
	private Identity playerIdentity;

	public void InitStats(Identity identity)
	{
		playerIdentity = identity;
		var conn = SpacetimeNetworkManager.Instance.Conn;

		foreach (var stat in conn.Db.PlayerStat.Owner.Filter(playerIdentity))
		{
			AddOrUpdateStatRow(stat);
		}

		conn.Db.PlayerStat.OnInsert += OnStatInsert;
		conn.Db.PlayerStat.OnUpdate += OnStatUpdate;
	}

	private void OnStatInsert(EventContext ctx, SpacetimeDB.Types.PlayerStat stat)
	{
		if (stat.Owner != playerIdentity) return;
		AddOrUpdateStatRow(stat);
	}

	private void OnStatUpdate(EventContext ctx, SpacetimeDB.Types.PlayerStat oldStat, SpacetimeDB.Types.PlayerStat newStat)
	{
		if (newStat.Owner != playerIdentity) return;
		if (valueLabels.TryGetValue(newStat.Id, out var label))
		{
			label.Text = newStat.Value.ToString();
		}
	}

	private void AddOrUpdateStatRow(SpacetimeDB.Types.PlayerStat stat)
	{
		if (valueLabels.ContainsKey(stat.Id))
		{
			valueLabels[stat.Id].Text = stat.Value.ToString();
			return;
		}

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);

		var nameLabel = new Label();
		nameLabel.Text = stat.Stat.ToString();
		nameLabel.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
		row.AddChild(nameLabel);

		var valueLabel = new Label();
		valueLabel.Text = stat.Value.ToString();
		valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
		valueLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.4f));
		row.AddChild(valueLabel);

		AddChild(row);
		valueLabels[stat.Id] = valueLabel;
	}
}
