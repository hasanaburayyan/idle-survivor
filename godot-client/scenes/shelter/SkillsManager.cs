using Godot;
using SpacetimeDB;
using SpacetimeDB.Types;
using System;
using System.Linq;

public partial class SkillsManager : Node
{
	private Label _skillsAvailableLabel;
	private VBoxContainer _skillsList;
	private bool _isOpen;

	public void Init(Label skillsAvailableLabel, VBoxContainer skillsList)
	{
		_skillsAvailableLabel = skillsAvailableLabel;
		_skillsList = skillsList;
	}

	public void SetOpen(bool open)
	{
		_isOpen = open;
		if (open) Refresh();
	}

	public void RefreshIfOpen()
	{
		if (_isOpen) Refresh();
	}

	public void Refresh()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		var pl = conn.Db.PlayerLevel.Owner.Find(localId);
		uint availableSp = pl?.AvailableSkillPoints ?? 0;

		_skillsAvailableLabel.Text = $"Available Skill Points: {availableSp}";

		foreach (var child in _skillsList.GetChildren())
			child.QueueFree();

		foreach (var skill in conn.Db.SkillDefinition.Iter())
		{
			if (!IsSkillExposed(conn, localId, skill))
				continue;

			bool owned = conn.Db.PlayerSkill.BySkillOwnerDef
				.Filter((Owner: localId, SkillDefinitionId: skill.Id)).Any();

			bool meetsPrereq = true;
			if (skill.PrerequisiteSkillId is not null || skill.PrerequisiteSkillId2 is not null)
			{
				bool has1 = skill.PrerequisiteSkillId is not ulong r1
					|| conn.Db.PlayerSkill.BySkillOwnerDef.Filter((Owner: localId, SkillDefinitionId: r1)).Any();
				bool has2 = skill.PrerequisiteSkillId2 is not ulong r2
					|| conn.Db.PlayerSkill.BySkillOwnerDef.Filter((Owner: localId, SkillDefinitionId: r2)).Any();
				meetsPrereq = has1 || has2;
			}
			bool meetsLevel = skill.RequiredLevel is not uint reqLvl || (pl?.Level ?? 0) >= reqLvl;
			bool canAfford = availableSp >= skill.Cost;

			var outer = new VBoxContainer();
			outer.AddThemeConstantOverride("separation", 2);

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);

			var nameLabel = new Label();
			nameLabel.Text = skill.Name;
			nameLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
			nameLabel.AddThemeFontSizeOverride("font_size", 18);
			row.AddChild(nameLabel);

			var costLabel = new Label();
			costLabel.Text = $"{skill.Cost} SP";
			costLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
			row.AddChild(costLabel);

			if (owned)
			{
				var ownedLabel = new Label();
				ownedLabel.Text = "Learned";
				ownedLabel.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.3f));
				row.AddChild(ownedLabel);
			}
			else
			{
				var purchaseBtn = new Button();
				purchaseBtn.Text = "Learn";
				purchaseBtn.CustomMinimumSize = new Vector2(80, 28);
				purchaseBtn.Disabled = !meetsLevel || !meetsPrereq || !canAfford;
				var capturedId = skill.Id;
				purchaseBtn.Pressed += () =>
				{
					conn.Reducers.PurchaseSkill(capturedId);
					CallDeferred(nameof(Refresh));
				};
				row.AddChild(purchaseBtn);
			}

			outer.AddChild(row);

			var descLabel = new Label();
			descLabel.Text = skill.Description;
			descLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
			descLabel.AddThemeFontSizeOverride("font_size", 14);
			outer.AddChild(descLabel);

			_skillsList.AddChild(outer);
		}
	}

	private static bool IsSkillExposed(DbConnection conn, SpacetimeDB.Identity owner, SpacetimeDB.Types.SkillDefinition skill)
	{
		bool hasAnyPrereq = skill.PrerequisiteSkillId is not null || skill.PrerequisiteSkillId2 is not null;
		if (!hasAnyPrereq) return true;

		if (skill.PrerequisiteSkillId is ulong p1
			&& conn.Db.PlayerSkill.BySkillOwnerDef.Filter((Owner: owner, SkillDefinitionId: p1)).Any())
			return true;
		if (skill.PrerequisiteSkillId2 is ulong p2
			&& conn.Db.PlayerSkill.BySkillOwnerDef.Filter((Owner: owner, SkillDefinitionId: p2)).Any())
			return true;

		return false;
	}
}
