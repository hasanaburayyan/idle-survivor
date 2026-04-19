using SpacetimeDB;

[SpacetimeDB.Type]
public enum SkillTreeEffectKind : byte
{
    None,
    AutoKillEnable,
    UnlockUpgrade,
    ScavengeUnlock,
    UpgradeActivity,
    AutoActivity,
    ActivitySpeedUpgrade,
}

public static partial class Module
{
    [SpacetimeDB.Table(Accessor = "SkillTreeNode", Public = true)]
    public partial struct SkillTreeNode
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Unique]
        public string Name;

        public string Tooltip;
        public ulong? PrerequisiteNodeId;
        public uint PrerequisiteMinLevel;

        // Optional: the node the connecting line should be drawn FROM.
        // Defaults to PrerequisiteNodeId when null. Set this when a node should
        // visually attach to a different parent than its gating prerequisite.
        public ulong? VisualPrerequisiteNodeId;

        public float PosX;
        public float PosY;

        public SkillTreeEffectKind EffectKind;
        // Interpretation depends on EffectKind:
        //   AutoKillEnable: ignored
        //   UnlockUpgrade : cast to (byte)UpgradeType
        //   ScavengeUnlock: cast to (byte)ResourceType
        public uint EffectParam;

        public uint BaseMaxLevel;
        public ulong BaseCost;

        // Branch tier index — used by the compounding cost/max-level rule.
        //   0 = Auto Kill chain (AS/KPC/ZD), 1 = Wood chain, 2 = Metal chain, 3 = Fabric chain.
        // A leveled node at tier T has effective max = BaseMaxLevel * (1 + unlockedTiers - T),
        // and its Nth level costs (in addition to Money): all R_i for i < T from L1, R_T from L≥6,
        // and R_i for i > T starting at L > 5 * (i - T).
        public uint BranchTier;
    }

    [SpacetimeDB.Table(Accessor = "PlayerSkillTreeUnlock", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "by_owner_and_node",
                             Columns = new[] { "Owner", "NodeId" })]
    public partial struct PlayerSkillTreeUnlock
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public Identity Owner;
        public ulong NodeId;
        public uint Level;
    }

    public static uint GetPlayerSkillTreeLevel(ReducerContext ctx, Identity owner, ulong nodeId)
    {
        foreach (var u in ctx.Db.PlayerSkillTreeUnlock.by_owner_and_node
                   .Filter((Owner: owner, NodeId: nodeId)))
            return u.Level;
        return 0;
    }

    public static bool HasScavengeUnlock(ReducerContext ctx, Identity owner, ResourceType rt)
    {
        foreach (var u in ctx.Db.PlayerSkillTreeUnlock.Owner.Filter(owner))
        {
            if (u.Level < 1) continue;
            if (ctx.Db.SkillTreeNode.Id.Find(u.NodeId) is SkillTreeNode node
                && node.EffectKind == SkillTreeEffectKind.ScavengeUnlock
                && (ResourceType)(byte)node.EffectParam == rt)
                return true;
        }
        return false;
    }

    // Resources awarded by ScavengeUnlock markers, in tier order
    // (Wood=1, Metal=2, Fabric=3, Food=4, Parts=5).
    private static readonly ResourceType[] TierResources = new[]
    {
        ResourceType.Wood,
        ResourceType.Metal,
        ResourceType.Fabric,
        ResourceType.Food,
        ResourceType.Parts,
    };

    public static uint CountUnlockedResourceTiers(ReducerContext ctx, Identity owner)
    {
        uint count = 0;
        foreach (var u in ctx.Db.PlayerSkillTreeUnlock.Owner.Filter(owner))
        {
            if (u.Level < 1) continue;
            if (ctx.Db.SkillTreeNode.Id.Find(u.NodeId) is SkillTreeNode node
                && node.EffectKind == SkillTreeEffectKind.ScavengeUnlock)
                count++;
        }
        return count;
    }

    public static uint GetEffectiveMaxLevel(ReducerContext ctx, Identity owner, SkillTreeNode node)
    {
        if (node.BaseMaxLevel <= 1) return node.BaseMaxLevel;
        uint tiers = CountUnlockedResourceTiers(ctx, owner);
        if (tiers <= node.BranchTier) return node.BaseMaxLevel;
        return node.BaseMaxLevel * (1 + tiers - node.BranchTier);
    }

    // Cost tier = which R_i resources are required to purchase the next level of this node.
    // Rule: band k = (L - 1) / 5 where L is the level being purchased.
    //   - Normal leveled / one-time nodes at tier T: cost tier = T + k.
    //   - Tier markers (ScavengeUnlock): cost tier = T - 1 (can't pay in the resource being unlocked).
    // So tier-0 L1-5 costs Money; L6-10 adds Wood; L11-15 adds Metal; L16-20 adds Fabric.
    // Tier-1 L1-5 is Money + Wood; L6-10 adds Metal; etc.
    public static uint GetCostTier(SkillTreeNode node, uint currentLevel)
    {
        if (node.EffectKind == SkillTreeEffectKind.ScavengeUnlock)
            return node.BranchTier > 0 ? node.BranchTier - 1 : 0;
        uint nextLevel = currentLevel + 1;
        uint band = (nextLevel - 1) / 5;
        return node.BranchTier + band;
    }

    public static List<ActivityCost> GetNextLevelCost(SkillTreeNode node, uint currentLevel)
    {
        var costs = new List<ActivityCost>();
        ulong perResource = (ulong)Math.Max(1.0, Math.Floor(node.BaseCost * Math.Pow(1.5, currentLevel)));

        costs.Add(new ActivityCost { Type = ResourceType.Money, Amount = perResource });
        uint costTier = GetCostTier(node, currentLevel);
        for (uint i = 1; i <= costTier && i <= TierResources.Length; i++)
            costs.Add(new ActivityCost { Type = TierResources[i - 1], Amount = perResource });

        return costs;
    }

    [SpacetimeDB.Reducer]
    public static void PurchaseSkillTreeNode(ReducerContext ctx, ulong nodeId)
    {
        if (ctx.Db.SkillTreeNode.Id.Find(nodeId) is not SkillTreeNode node)
            throw new Exception("Skill tree node not found");

        bool hasExisting = false;
        PlayerSkillTreeUnlock existing = default;
        uint currentLevel = 0;
        foreach (var u in ctx.Db.PlayerSkillTreeUnlock.by_owner_and_node
                    .Filter((Owner: ctx.Sender, NodeId: nodeId)))
        {
            existing = u;
            hasExisting = true;
            currentLevel = u.Level;
            break;
        }

        uint maxLevel = GetEffectiveMaxLevel(ctx, ctx.Sender, node);
        if (currentLevel >= maxLevel)
            throw new Exception("Already at max level");

        if (node.PrerequisiteNodeId is ulong prereqId)
        {
            uint prereqLevel = GetPlayerSkillTreeLevel(ctx, ctx.Sender, prereqId);
            if (prereqLevel < node.PrerequisiteMinLevel)
                throw new Exception("Prerequisite not met");
        }

        var costs = GetNextLevelCost(node, currentLevel);
        foreach (var cost in costs)
        {
            var rows = ctx.Db.ResourceTracker.by_owner_and_type
                .Filter((Owner: ctx.Sender, Type: cost.Type));
            if (!rows.Any() || rows.First().Amount < cost.Amount)
                throw new Exception($"Insufficient {cost.Type} (need {cost.Amount})");
        }

        foreach (var cost in costs)
        {
            var row = ctx.Db.ResourceTracker.by_owner_and_type
                .Filter((Owner: ctx.Sender, Type: cost.Type)).First();
            row.Amount -= cost.Amount;
            ctx.Db.ResourceTracker.Id.Update(row);
        }

        if (hasExisting)
        {
            existing.Level = currentLevel + 1;
            ctx.Db.PlayerSkillTreeUnlock.Id.Update(existing);
        }
        else
        {
            ctx.Db.PlayerSkillTreeUnlock.Insert(new PlayerSkillTreeUnlock
            {
                Id = 0,
                Owner = ctx.Sender,
                NodeId = nodeId,
                Level = 1
            });

            if (node.EffectKind == SkillTreeEffectKind.AutoActivity)
            {
                var actType = (ActivityType)(byte)node.EffectParam;
                var activityRow = ctx.Db.Activity.by_activity_participant_type
                    .Filter((Participant: ctx.Sender, Type: actType)).FirstOrDefault();
                ulong baseMs = activityRow.Id != 0 ? activityRow.DurationMs : 3000UL;
                var intervalMs = GetEffectiveActivityDurationMs(ctx, ctx.Sender, actType, baseMs);
                ActivityOnInterval(ctx, ctx.Sender, actType, intervalMs, true);
            }
        }
    }

    public static List<ResourceType> GetScavengePool(ReducerContext ctx, Identity owner)
    {
        // Wood and future resources come from dedicated gathering activities — not zombie kills.
        return new List<ResourceType> { ResourceType.Money };
    }

    public static uint GetSkillTreeLevelForEffect(ReducerContext ctx, Identity owner,
                                                   SkillTreeEffectKind kind, uint param)
    {
        foreach (var u in ctx.Db.PlayerSkillTreeUnlock.Owner.Filter(owner))
        {
            if (u.Level < 1) continue;
            if (ctx.Db.SkillTreeNode.Id.Find(u.NodeId) is SkillTreeNode node
                && node.EffectKind == kind
                && node.EffectParam == param)
                return u.Level;
        }
        return 0;
    }

    public static bool HasAutoActivity(ReducerContext ctx, Identity owner, ActivityType type)
    {
        return GetSkillTreeLevelForEffect(ctx, owner, SkillTreeEffectKind.AutoActivity,
                                          (uint)(byte)type) >= 1;
    }

    public static ulong GetEffectiveActivityDurationMs(ReducerContext ctx, Identity owner, ActivityType type, ulong baseDurationMs)
    {
        uint speedLevel = GetSkillTreeLevelForEffect(ctx, owner,
            SkillTreeEffectKind.ActivitySpeedUpgrade, (uint)(byte)type);
        ulong reduction = (ulong)speedLevel * 100;
        return reduction >= baseDurationMs ? 1UL : baseDurationMs - reduction;
    }

    public static bool IsUpgradeUnlocked(ReducerContext ctx, Identity owner, UpgradeType type)
    {
        foreach (var unlock in ctx.Db.PlayerSkillTreeUnlock.Owner.Filter(owner))
        {
            if (unlock.Level < 1) continue;
            if (ctx.Db.SkillTreeNode.Id.Find(unlock.NodeId) is SkillTreeNode node
                && node.EffectKind == SkillTreeEffectKind.UnlockUpgrade
                && (UpgradeType)(byte)node.EffectParam == type)
            {
                return true;
            }
        }
        return false;
    }

    public static bool HasAutoKill(ReducerContext ctx, Identity owner)
    {
        foreach (var unlock in ctx.Db.PlayerSkillTreeUnlock.Owner.Filter(owner))
        {
            if (unlock.Level < 1) continue;
            if (ctx.Db.SkillTreeNode.Id.Find(unlock.NodeId) is SkillTreeNode node
                && node.EffectKind == SkillTreeEffectKind.AutoKillEnable)
            {
                return true;
            }
        }
        return false;
    }
}
