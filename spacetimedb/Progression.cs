using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Table(Accessor = "PlayerLevel", Public = true)]
    public partial struct PlayerLevel
    {
        [SpacetimeDB.PrimaryKey]
        public Identity Owner;
        public uint Level;
        public ulong Xp;
        public uint AvailableSkillPoints;
    }

    [SpacetimeDB.Table(Accessor = "SkillDefinition", Public = true)]
    public partial struct SkillDefinition
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Unique]
        public string Name;
        public string Description;
        public uint Cost;
        public uint? RequiredLevel;
        public ulong? PrerequisiteSkillId;
        public ulong? PrerequisiteSkillId2;
    }

    [SpacetimeDB.Table(Accessor = "PlayerSkill", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "by_skill_owner_def", Columns = new[] { "Owner", "SkillDefinitionId" })]
    public partial struct PlayerSkill
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public Identity Owner;
        public ulong SkillDefinitionId;
    }

    /// <summary>XP needed to advance from level L to L+1: floor(20 * 1.5^L)</summary>
    public static ulong XpForNextLevel(uint level) =>
        (ulong)Math.Floor(20.0 * Math.Pow(1.5, level));

    public static void GrantExperience(ReducerContext ctx, Identity participant, ulong amount)
    {
        PlayerLevel row;
        if (ctx.Db.PlayerLevel.Owner.Find(participant) is PlayerLevel existing)
        {
            row = existing;
        }
        else
        {
            row = ctx.Db.PlayerLevel.Insert(new PlayerLevel
            {
                Owner = participant,
                Level = 0,
                Xp = 0,
                AvailableSkillPoints = 0
            });
        }

        var level = row.Level;
        var xp = row.Xp + amount;
        var skillPoints = row.AvailableSkillPoints;

        var needed = XpForNextLevel(level);
        while (xp >= needed)
        {
            xp -= needed;
            level += 1;
            skillPoints += 1;
            needed = XpForNextLevel(level);
            Log.Info($"Player leveled up to {level}!");
        }

        ctx.Db.PlayerLevel.Owner.Update(row with
        {
            Level = level,
            Xp = xp,
            AvailableSkillPoints = skillPoints
        });
    }

    [SpacetimeDB.Reducer]
    public static void DebugLevelUp(ReducerContext ctx)
    {
        if (ctx.Db.PlayerLevel.Owner.Find(ctx.Sender) is not PlayerLevel pl)
            throw new Exception("Player level not found");
        var needed = XpForNextLevel(pl.Level);
        GrantExperience(ctx, ctx.Sender, needed - pl.Xp);
    }

    [SpacetimeDB.Reducer]
    public static void PurchaseSkill(ReducerContext ctx, ulong skillDefinitionId)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player)
            throw new Exception("Player not found");

        if (ctx.Db.SkillDefinition.Id.Find(skillDefinitionId) is not SkillDefinition skill)
            throw new Exception("Skill not found");

        if (ctx.Db.PlayerLevel.Owner.Find(ctx.Sender) is not PlayerLevel plRow)
            throw new Exception("Player level not found");

        if (skill.RequiredLevel is uint reqLevel && plRow.Level < reqLevel)
            throw new Exception($"Requires level {reqLevel}");

        if (skill.PrerequisiteSkillId is not null || skill.PrerequisiteSkillId2 is not null)
        {
            bool hasPrereq1 = skill.PrerequisiteSkillId is not ulong p1
                || ctx.Db.PlayerSkill.by_skill_owner_def.Filter((Owner: ctx.Sender, SkillDefinitionId: p1)).Any();
            bool hasPrereq2 = skill.PrerequisiteSkillId2 is not ulong p2
                || ctx.Db.PlayerSkill.by_skill_owner_def.Filter((Owner: ctx.Sender, SkillDefinitionId: p2)).Any();
            if (!hasPrereq1 && !hasPrereq2)
                throw new Exception("Missing prerequisite skill");
        }

        if (ctx.Db.PlayerSkill.by_skill_owner_def
            .Filter((Owner: ctx.Sender, SkillDefinitionId: skillDefinitionId)).Any())
            throw new Exception("Skill already purchased");

        if (plRow.AvailableSkillPoints < skill.Cost)
            throw new Exception("Not enough skill points");

        ctx.Db.PlayerLevel.Owner.Update(plRow with
        {
            AvailableSkillPoints = plRow.AvailableSkillPoints - skill.Cost
        });

        ctx.Db.PlayerSkill.Insert(new PlayerSkill
        {
            Id = 0,
            Owner = ctx.Sender,
            SkillDefinitionId = skillDefinitionId
        });
    }
}
