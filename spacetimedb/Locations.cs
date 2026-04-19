using SpacetimeDB;

[SpacetimeDB.Type]
public enum LocationType : byte
{
    Shelter,
    GuildHall,
    Wastes
}

public static partial class Module
{
    [SpacetimeDB.Table(Accessor = "StructureDefinition", Public = true)]
    public partial struct StructureDefinition
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Unique]
        public string Name;

        public List<ActivityCost> Cost;

        public bool IndoorOnly;
    }

    [SpacetimeDB.Table(Accessor = "PlayerShelter", Public = true)]
    public partial struct PlayerShelter
    {
        [SpacetimeDB.PrimaryKey]
        public Identity Owner;

        public byte Level;
        public Timestamp BuiltAt;
    }

    [SpacetimeDB.Table(Accessor = "PlayerStructure", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "by_owner_and_definition", Columns = new[] { "Owner", "DefinitionId" })]
    public partial struct PlayerStructure
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public Identity Owner;

        public ulong DefinitionId;
        public uint Level;
        public Timestamp BuiltAt;
        public int PosX;
        public int PosY;
    }

    [SpacetimeDB.Table(Accessor = "CraftingRecipe", Public = true)]
    public partial struct CraftingRecipe
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public ulong StructureDefinitionId;

        public string Name;
        public List<ActivityCost> InputCost;
        public ResourceType OutputResource;
        public ulong OutputAmount;
        public ulong DurationMs;
        public bool IsGearRecipe;
    }

    [SpacetimeDB.Table(Accessor = "StructureModifier", Public = true)]
    public partial struct StructureModifier
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public ulong PlayerStructureId;

        public string ModifierName;
        public int Value;
    }

    static bool IsLocationValid(LocationType? required, LocationType playerLoc) =>
        required is null ||
        required == playerLoc ||
        (required == LocationType.Shelter &&
            (playerLoc == LocationType.GuildHall || playerLoc == LocationType.Wastes));

    [SpacetimeDB.Reducer(ReducerKind.Init)]
    public static void Init(ReducerContext ctx)
    {
        Log.Info("Module initialized, seeding structure definitions");

        var smelter = ctx.Db.StructureDefinition.Insert(new StructureDefinition
        {
            Id = 0,
            Name = "Smelter",
            Cost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Metal, Amount = 50 },
                new ActivityCost { Type = ResourceType.Parts, Amount = 30 }
            },
            IndoorOnly = true
        });

        var workbench = ctx.Db.StructureDefinition.Insert(new StructureDefinition
        {
            Id = 0,
            Name = "Workbench",
            Cost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Wood, Amount = 20 },
                new ActivityCost { Type = ResourceType.Parts, Amount = 5 }
            },
            IndoorOnly = true
        });

        var tailorStation = ctx.Db.StructureDefinition.Insert(new StructureDefinition
        {
            Id = 0,
            Name = "Tailor Station",
            Cost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Fabric, Amount = 30 },
                new ActivityCost { Type = ResourceType.Wood, Amount = 15 }
            },
            IndoorOnly = true
        });

        var weaponStation = ctx.Db.StructureDefinition.Insert(new StructureDefinition
        {
            Id = 0,
            Name = "Weapon Station",
            Cost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Metal, Amount = 40 },
                new ActivityCost { Type = ResourceType.Parts, Amount = 20 }
            },
            IndoorOnly = true
        });

        Log.Info("Seeding crafting recipes");

        ctx.Db.CraftingRecipe.Insert(new CraftingRecipe
        {
            Id = 0,
            StructureDefinitionId = smelter.Id,
            Name = "Smelt Ingot",
            InputCost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Metal, Amount = 8 },
                new ActivityCost { Type = ResourceType.Wood, Amount = 3 }
            },
            OutputResource = ResourceType.Metal,
            OutputAmount = 12,
            DurationMs = 5000
        });

        ctx.Db.CraftingRecipe.Insert(new CraftingRecipe
        {
            Id = 0,
            StructureDefinitionId = workbench.Id,
            Name = "Craft Parts",
            InputCost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Wood, Amount = 3 },
                new ActivityCost { Type = ResourceType.Fabric, Amount = 3 }
            },
            OutputResource = ResourceType.Parts,
            OutputAmount = 2,
            DurationMs = 4000
        });

        ctx.Db.CraftingRecipe.Insert(new CraftingRecipe
        {
            Id = 0,
            StructureDefinitionId = workbench.Id,
            Name = "Craft Planks",
            InputCost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Wood, Amount = 10 }
            },
            OutputResource = ResourceType.Wood,
            OutputAmount = 5,
            DurationMs = 3000
        });

        ctx.Db.CraftingRecipe.Insert(new CraftingRecipe
        {
            Id = 0,
            StructureDefinitionId = tailorStation.Id,
            Name = "Weave Fabric",
            InputCost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Fabric, Amount = 5 },
                new ActivityCost { Type = ResourceType.Parts, Amount = 2 }
            },
            OutputResource = ResourceType.Fabric,
            OutputAmount = 8,
            DurationMs = 4000
        });

        ctx.Db.CraftingRecipe.Insert(new CraftingRecipe
        {
            Id = 0,
            StructureDefinitionId = tailorStation.Id,
            Name = "Craft Bandage",
            InputCost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Fabric, Amount = 10 }
            },
            OutputResource = ResourceType.Food,
            OutputAmount = 5,
            DurationMs = 3000
        });

        ctx.Db.CraftingRecipe.Insert(new CraftingRecipe
        {
            Id = 0,
            StructureDefinitionId = weaponStation.Id,
            Name = "Forge Blade",
            InputCost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Metal, Amount = 15 },
                new ActivityCost { Type = ResourceType.Parts, Amount = 5 }
            },
            OutputResource = ResourceType.Money,
            OutputAmount = 10,
            DurationMs = 6000
        });

        ctx.Db.CraftingRecipe.Insert(new CraftingRecipe
        {
            Id = 0,
            StructureDefinitionId = weaponStation.Id,
            Name = "Smelt Ore",
            InputCost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Metal, Amount = 10 }
            },
            OutputResource = ResourceType.Metal,
            OutputAmount = 15,
            DurationMs = 5000
        });

        Log.Info("Seeding skill tree nodes");

        var autoKillNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Auto Kill",
            Tooltip = "Automatically kill nearby zombies",
            PrerequisiteNodeId = null,
            PrerequisiteMinLevel = 0,
            VisualPrerequisiteNodeId = null,
            PosX = 0f,
            PosY = 0f,
            EffectKind = SkillTreeEffectKind.AutoKillEnable,
            EffectParam = 0,
            BaseMaxLevel = 1,
            BaseCost = 20,
            BranchTier = 0
        });

        // Combat chain — angle 0° (straight up / north)
        var attackSpeedNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Attack Speed",
            Tooltip = "Swing faster. -5% attack cooldown per level.",
            PrerequisiteNodeId = autoKillNode.Id,
            PrerequisiteMinLevel = 1,
            VisualPrerequisiteNodeId = null,
            PosX = 0f,
            PosY = -150f,
            EffectKind = SkillTreeEffectKind.UnlockUpgrade,
            EffectParam = (uint)(byte)UpgradeType.AttackSpeed,
            BaseMaxLevel = 5,
            BaseCost = 15,
            BranchTier = 0
        });

        var killsPerClickNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Kills Per Click",
            Tooltip = "+1 additional zombie per swing per level.",
            PrerequisiteNodeId = attackSpeedNode.Id,
            PrerequisiteMinLevel = 1,
            VisualPrerequisiteNodeId = null,
            PosX = 0f,
            PosY = -300f,
            EffectKind = SkillTreeEffectKind.UnlockUpgrade,
            EffectParam = (uint)(byte)UpgradeType.KillsPerClick,
            BaseMaxLevel = 5,
            BaseCost = 15,
            BranchTier = 0
        });

        var zombieDensityNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Zombie Density",
            Tooltip = "+20 more zombies roaming the shelter per level.",
            PrerequisiteNodeId = killsPerClickNode.Id,
            PrerequisiteMinLevel = 1,
            VisualPrerequisiteNodeId = null,
            PosX = 0f,
            PosY = -450f,
            EffectKind = SkillTreeEffectKind.UnlockUpgrade,
            EffectParam = (uint)(byte)UpgradeType.ZombieDensity,
            BaseMaxLevel = 5,
            BaseCost = 15,
            BranchTier = 0
        });

        // Wood chain — angle 60° (upper-right)
        var unlockWoodNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Unlock Wood",
            Tooltip = "Unlocks the Chop Wood activity. Previously leveled nodes gain +5 max level; Wood is added to upgrade costs past that threshold.",
            PrerequisiteNodeId = zombieDensityNode.Id,
            PrerequisiteMinLevel = 5,
            VisualPrerequisiteNodeId = autoKillNode.Id,
            PosX = 130f,
            PosY = -75f,
            EffectKind = SkillTreeEffectKind.ScavengeUnlock,
            EffectParam = (uint)(byte)ResourceType.Wood,
            BaseMaxLevel = 1,
            BaseCost = 150,
            BranchTier = 1
        });

        var chopWoodEfficiencyNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Chop Wood Efficiency",
            Tooltip = "+1 Wood per Chop Wood completion per level.",
            PrerequisiteNodeId = unlockWoodNode.Id,
            PrerequisiteMinLevel = 1,
            VisualPrerequisiteNodeId = null,
            PosX = 260f,
            PosY = -150f,
            EffectKind = SkillTreeEffectKind.UpgradeActivity,
            EffectParam = (uint)(byte)ActivityType.ChopWood,
            BaseMaxLevel = 5,
            BaseCost = 20,
            BranchTier = 1
        });

        var chopWoodSpeedNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Chop Wood Speed",
            Tooltip = "-0.1s Chop Wood duration per level.",
            PrerequisiteNodeId = chopWoodEfficiencyNode.Id,
            PrerequisiteMinLevel = 1,
            VisualPrerequisiteNodeId = null,
            PosX = 390f,
            PosY = -225f,
            EffectKind = SkillTreeEffectKind.ActivitySpeedUpgrade,
            EffectParam = (uint)(byte)ActivityType.ChopWood,
            BaseMaxLevel = 5,
            BaseCost = 25,
            BranchTier = 1
        });

        ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Auto Chop Wood",
            Tooltip = "Chop Wood runs automatically on a recurring schedule.",
            PrerequisiteNodeId = chopWoodSpeedNode.Id,
            PrerequisiteMinLevel = 1,
            VisualPrerequisiteNodeId = null,
            PosX = 520f,
            PosY = -300f,
            EffectKind = SkillTreeEffectKind.AutoActivity,
            EffectParam = (uint)(byte)ActivityType.ChopWood,
            BaseMaxLevel = 1,
            BaseCost = 120,
            BranchTier = 1
        });

        // Metal chain — angle 120° (lower-right)
        var unlockScrapMetalNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Unlock Scrap Metal",
            Tooltip = "Unlocks the Mine activity. Previously leveled nodes gain +5 max level; Scrap Metal is added to upgrade costs past that threshold.",
            PrerequisiteNodeId = chopWoodSpeedNode.Id,
            PrerequisiteMinLevel = 5,
            VisualPrerequisiteNodeId = autoKillNode.Id,
            PosX = 130f,
            PosY = 75f,
            EffectKind = SkillTreeEffectKind.ScavengeUnlock,
            EffectParam = (uint)(byte)ResourceType.Metal,
            BaseMaxLevel = 1,
            BaseCost = 250,
            BranchTier = 2
        });

        var mineEfficiencyNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Mine Efficiency",
            Tooltip = "+1 Scrap Metal per Mine completion per level.",
            PrerequisiteNodeId = unlockScrapMetalNode.Id,
            PrerequisiteMinLevel = 1,
            VisualPrerequisiteNodeId = null,
            PosX = 260f,
            PosY = 150f,
            EffectKind = SkillTreeEffectKind.UpgradeActivity,
            EffectParam = (uint)(byte)ActivityType.Mine,
            BaseMaxLevel = 5,
            BaseCost = 30,
            BranchTier = 2
        });

        var mineSpeedNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Mine Speed",
            Tooltip = "-0.1s Mine duration per level.",
            PrerequisiteNodeId = mineEfficiencyNode.Id,
            PrerequisiteMinLevel = 1,
            VisualPrerequisiteNodeId = null,
            PosX = 390f,
            PosY = 225f,
            EffectKind = SkillTreeEffectKind.ActivitySpeedUpgrade,
            EffectParam = (uint)(byte)ActivityType.Mine,
            BaseMaxLevel = 5,
            BaseCost = 40,
            BranchTier = 2
        });

        ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Auto Mine",
            Tooltip = "Mine runs automatically on a recurring schedule.",
            PrerequisiteNodeId = mineSpeedNode.Id,
            PrerequisiteMinLevel = 1,
            VisualPrerequisiteNodeId = null,
            PosX = 520f,
            PosY = 300f,
            EffectKind = SkillTreeEffectKind.AutoActivity,
            EffectParam = (uint)(byte)ActivityType.Mine,
            BaseMaxLevel = 1,
            BaseCost = 200,
            BranchTier = 2
        });

        // Fabric chain — angle 180° (straight down / south)
        var unlockFabricNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Unlock Fabric",
            Tooltip = "Unlocks the Gather Fabric activity. Previously leveled nodes gain +5 max level; Fabric is added to upgrade costs past that threshold.",
            PrerequisiteNodeId = mineSpeedNode.Id,
            PrerequisiteMinLevel = 5,
            VisualPrerequisiteNodeId = autoKillNode.Id,
            PosX = 0f,
            PosY = 150f,
            EffectKind = SkillTreeEffectKind.ScavengeUnlock,
            EffectParam = (uint)(byte)ResourceType.Fabric,
            BaseMaxLevel = 1,
            BaseCost = 400,
            BranchTier = 3
        });

        var gatherFabricEfficiencyNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Gather Fabric Efficiency",
            Tooltip = "+1 Fabric per Gather Fabric completion per level.",
            PrerequisiteNodeId = unlockFabricNode.Id,
            PrerequisiteMinLevel = 1,
            VisualPrerequisiteNodeId = null,
            PosX = 0f,
            PosY = 300f,
            EffectKind = SkillTreeEffectKind.UpgradeActivity,
            EffectParam = (uint)(byte)ActivityType.GatherFabric,
            BaseMaxLevel = 5,
            BaseCost = 45,
            BranchTier = 3
        });

        var gatherFabricSpeedNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Gather Fabric Speed",
            Tooltip = "-0.1s Gather Fabric duration per level.",
            PrerequisiteNodeId = gatherFabricEfficiencyNode.Id,
            PrerequisiteMinLevel = 1,
            VisualPrerequisiteNodeId = null,
            PosX = 0f,
            PosY = 450f,
            EffectKind = SkillTreeEffectKind.ActivitySpeedUpgrade,
            EffectParam = (uint)(byte)ActivityType.GatherFabric,
            BaseMaxLevel = 5,
            BaseCost = 60,
            BranchTier = 3
        });

        ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Auto Gather Fabric",
            Tooltip = "Gather Fabric runs automatically on a recurring schedule.",
            PrerequisiteNodeId = gatherFabricSpeedNode.Id,
            PrerequisiteMinLevel = 1,
            VisualPrerequisiteNodeId = null,
            PosX = 0f,
            PosY = 600f,
            EffectKind = SkillTreeEffectKind.AutoActivity,
            EffectParam = (uint)(byte)ActivityType.GatherFabric,
            BaseMaxLevel = 1,
            BaseCost = 320,
            BranchTier = 3
        });

        // Food chain — angle 240° (lower-left)
        var unlockFoodNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Unlock Food",
            Tooltip = "Unlocks the Forage activity. Previously leveled nodes gain +5 max level; Food is added to upgrade costs past that threshold.",
            PrerequisiteNodeId = gatherFabricSpeedNode.Id,
            PrerequisiteMinLevel = 5,
            VisualPrerequisiteNodeId = autoKillNode.Id,
            PosX = -130f,
            PosY = 75f,
            EffectKind = SkillTreeEffectKind.ScavengeUnlock,
            EffectParam = (uint)(byte)ResourceType.Food,
            BaseMaxLevel = 1,
            BaseCost = 600,
            BranchTier = 4
        });

        var forageEfficiencyNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Forage Efficiency",
            Tooltip = "+1 Food per Forage completion per level.",
            PrerequisiteNodeId = unlockFoodNode.Id,
            PrerequisiteMinLevel = 1,
            VisualPrerequisiteNodeId = null,
            PosX = -260f,
            PosY = 150f,
            EffectKind = SkillTreeEffectKind.UpgradeActivity,
            EffectParam = (uint)(byte)ActivityType.Forage,
            BaseMaxLevel = 5,
            BaseCost = 65,
            BranchTier = 4
        });

        var forageSpeedNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Forage Speed",
            Tooltip = "-0.1s Forage duration per level.",
            PrerequisiteNodeId = forageEfficiencyNode.Id,
            PrerequisiteMinLevel = 1,
            VisualPrerequisiteNodeId = null,
            PosX = -390f,
            PosY = 225f,
            EffectKind = SkillTreeEffectKind.ActivitySpeedUpgrade,
            EffectParam = (uint)(byte)ActivityType.Forage,
            BaseMaxLevel = 5,
            BaseCost = 90,
            BranchTier = 4
        });

        ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Auto Forage",
            Tooltip = "Forage runs automatically on a recurring schedule.",
            PrerequisiteNodeId = forageSpeedNode.Id,
            PrerequisiteMinLevel = 1,
            VisualPrerequisiteNodeId = null,
            PosX = -520f,
            PosY = 300f,
            EffectKind = SkillTreeEffectKind.AutoActivity,
            EffectParam = (uint)(byte)ActivityType.Forage,
            BaseMaxLevel = 1,
            BaseCost = 500,
            BranchTier = 4
        });

        // Parts chain — angle 300° (upper-left)
        var unlockPartsNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Unlock Parts",
            Tooltip = "Unlocks the Salvage activity. Previously leveled nodes gain +5 max level; Parts is added to upgrade costs past that threshold.",
            PrerequisiteNodeId = forageSpeedNode.Id,
            PrerequisiteMinLevel = 5,
            VisualPrerequisiteNodeId = autoKillNode.Id,
            PosX = -130f,
            PosY = -75f,
            EffectKind = SkillTreeEffectKind.ScavengeUnlock,
            EffectParam = (uint)(byte)ResourceType.Parts,
            BaseMaxLevel = 1,
            BaseCost = 900,
            BranchTier = 5
        });

        var salvageEfficiencyNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Salvage Efficiency",
            Tooltip = "+1 Parts per Salvage completion per level.",
            PrerequisiteNodeId = unlockPartsNode.Id,
            PrerequisiteMinLevel = 1,
            VisualPrerequisiteNodeId = null,
            PosX = -260f,
            PosY = -150f,
            EffectKind = SkillTreeEffectKind.UpgradeActivity,
            EffectParam = (uint)(byte)ActivityType.Salvage,
            BaseMaxLevel = 5,
            BaseCost = 100,
            BranchTier = 5
        });

        var salvageSpeedNode = ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Salvage Speed",
            Tooltip = "-0.1s Salvage duration per level.",
            PrerequisiteNodeId = salvageEfficiencyNode.Id,
            PrerequisiteMinLevel = 1,
            VisualPrerequisiteNodeId = null,
            PosX = -390f,
            PosY = -225f,
            EffectKind = SkillTreeEffectKind.ActivitySpeedUpgrade,
            EffectParam = (uint)(byte)ActivityType.Salvage,
            BaseMaxLevel = 5,
            BaseCost = 140,
            BranchTier = 5
        });

        ctx.Db.SkillTreeNode.Insert(new SkillTreeNode
        {
            Id = 0,
            Name = "Auto Salvage",
            Tooltip = "Salvage runs automatically on a recurring schedule.",
            PrerequisiteNodeId = salvageSpeedNode.Id,
            PrerequisiteMinLevel = 1,
            VisualPrerequisiteNodeId = null,
            PosX = -520f,
            PosY = -300f,
            EffectKind = SkillTreeEffectKind.AutoActivity,
            EffectParam = (uint)(byte)ActivityType.Salvage,
            BaseMaxLevel = 1,
            BaseCost = 800,
            BranchTier = 5
        });

        Log.Info("Seeding storage chest structure");

        ctx.Db.StructureDefinition.Insert(new StructureDefinition
        {
            Id = 0,
            Name = "Storage Chest",
            Cost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Wood, Amount = 30 },
                new ActivityCost { Type = ResourceType.Metal, Amount = 10 }
            },
            IndoorOnly = true
        });

        Log.Info("Seeding gear recipes and definitions");

        ctx.Db.GearDefinition.Insert(new GearDefinition
        {
            Id = 0,
            Name = "Tattered Vest",
            Slot = GearSlot.Chest,
            StatBonuses = new List<GearStatBonus>
            {
                new GearStatBonus { Stat = StatType.Perception, Value = 1 }
            },
            HealthBonus = 2,
            SetName = null,
            CraftingRecipeId = 0
        });

        var hoodRecipe = ctx.Db.CraftingRecipe.Insert(new CraftingRecipe
        {
            Id = 0,
            StructureDefinitionId = tailorStation.Id,
            Name = "Craft Survivor's Hood",
            InputCost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Fabric, Amount = 20 },
                new ActivityCost { Type = ResourceType.Parts, Amount = 5 }
            },
            OutputResource = ResourceType.Food,
            OutputAmount = 0,
            DurationMs = 5000,
            IsGearRecipe = true
        });

        ctx.Db.GearDefinition.Insert(new GearDefinition
        {
            Id = 0,
            Name = "Survivor's Hood",
            Slot = GearSlot.Head,
            StatBonuses = new List<GearStatBonus>
            {
                new GearStatBonus { Stat = StatType.Perception, Value = 2 }
            },
            HealthBonus = 5,
            SetName = "Survivor's Set",
            CraftingRecipeId = hoodRecipe.Id
        });

        var vestRecipe = ctx.Db.CraftingRecipe.Insert(new CraftingRecipe
        {
            Id = 0,
            StructureDefinitionId = tailorStation.Id,
            Name = "Craft Survivor's Vest",
            InputCost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Fabric, Amount = 30 },
                new ActivityCost { Type = ResourceType.Parts, Amount = 10 }
            },
            OutputResource = ResourceType.Food,
            OutputAmount = 0,
            DurationMs = 8000,
            IsGearRecipe = true
        });

        ctx.Db.GearDefinition.Insert(new GearDefinition
        {
            Id = 0,
            Name = "Survivor's Vest",
            Slot = GearSlot.Chest,
            StatBonuses = new List<GearStatBonus>
            {
                new GearStatBonus { Stat = StatType.Endurance, Value = 3 }
            },
            HealthBonus = 15,
            SetName = "Survivor's Set",
            CraftingRecipeId = vestRecipe.Id
        });

        var glovesRecipe = ctx.Db.CraftingRecipe.Insert(new CraftingRecipe
        {
            Id = 0,
            StructureDefinitionId = workbench.Id,
            Name = "Craft Survivor's Gloves",
            InputCost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Fabric, Amount = 10 },
                new ActivityCost { Type = ResourceType.Metal, Amount = 8 }
            },
            OutputResource = ResourceType.Food,
            OutputAmount = 0,
            DurationMs = 4000,
            IsGearRecipe = true
        });

        ctx.Db.GearDefinition.Insert(new GearDefinition
        {
            Id = 0,
            Name = "Survivor's Gloves",
            Slot = GearSlot.Arms,
            StatBonuses = new List<GearStatBonus>
            {
                new GearStatBonus { Stat = StatType.Dexterity, Value = 2 }
            },
            HealthBonus = 3,
            SetName = "Survivor's Set",
            CraftingRecipeId = glovesRecipe.Id
        });

        var pantsRecipe = ctx.Db.CraftingRecipe.Insert(new CraftingRecipe
        {
            Id = 0,
            StructureDefinitionId = tailorStation.Id,
            Name = "Craft Survivor's Pants",
            InputCost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Fabric, Amount = 25 },
                new ActivityCost { Type = ResourceType.Parts, Amount = 8 }
            },
            OutputResource = ResourceType.Food,
            OutputAmount = 0,
            DurationMs = 6000,
            IsGearRecipe = true
        });

        ctx.Db.GearDefinition.Insert(new GearDefinition
        {
            Id = 0,
            Name = "Survivor's Pants",
            Slot = GearSlot.Legs,
            StatBonuses = new List<GearStatBonus>
            {
                new GearStatBonus { Stat = StatType.Endurance, Value = 2 }
            },
            HealthBonus = 10,
            SetName = "Survivor's Set",
            CraftingRecipeId = pantsRecipe.Id
        });

        var bootsRecipe = ctx.Db.CraftingRecipe.Insert(new CraftingRecipe
        {
            Id = 0,
            StructureDefinitionId = workbench.Id,
            Name = "Craft Survivor's Boots",
            InputCost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Fabric, Amount = 15 },
                new ActivityCost { Type = ResourceType.Metal, Amount = 10 }
            },
            OutputResource = ResourceType.Food,
            OutputAmount = 0,
            DurationMs = 5000,
            IsGearRecipe = true
        });

        ctx.Db.GearDefinition.Insert(new GearDefinition
        {
            Id = 0,
            Name = "Survivor's Boots",
            Slot = GearSlot.Feet,
            StatBonuses = new List<GearStatBonus>
            {
                new GearStatBonus { Stat = StatType.Dexterity, Value = 1 },
                new GearStatBonus { Stat = StatType.Endurance, Value = 1 }
            },
            HealthBonus = 5,
            SetName = "Survivor's Set",
            CraftingRecipeId = bootsRecipe.Id
        });

        var swordRecipe = ctx.Db.CraftingRecipe.Insert(new CraftingRecipe
        {
            Id = 0,
            StructureDefinitionId = weaponStation.Id,
            Name = "Craft Iron Sword",
            InputCost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Metal, Amount = 20 },
                new ActivityCost { Type = ResourceType.Parts, Amount = 5 }
            },
            OutputResource = ResourceType.Food,
            OutputAmount = 0,
            DurationMs = 6000,
            IsGearRecipe = true
        });

        ctx.Db.GearDefinition.Insert(new GearDefinition
        {
            Id = 0,
            Name = "Iron Sword",
            Slot = GearSlot.Weapon,
            StatBonuses = new List<GearStatBonus>
            {
                new GearStatBonus { Stat = StatType.Strength, Value = 3 }
            },
            HealthBonus = 0,
            SetName = null,
            CraftingRecipeId = swordRecipe.Id
        });

        var scoutBladeRecipe = ctx.Db.CraftingRecipe.Insert(new CraftingRecipe
        {
            Id = 0,
            StructureDefinitionId = weaponStation.Id,
            Name = "Craft Scout's Blade",
            InputCost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Metal, Amount = 15 },
                new ActivityCost { Type = ResourceType.Parts, Amount = 10 }
            },
            OutputResource = ResourceType.Food,
            OutputAmount = 0,
            DurationMs = 5000,
            IsGearRecipe = true
        });

        ctx.Db.GearDefinition.Insert(new GearDefinition
        {
            Id = 0,
            Name = "Scout's Blade",
            Slot = GearSlot.Weapon,
            StatBonuses = new List<GearStatBonus>
            {
                new GearStatBonus { Stat = StatType.Dexterity, Value = 2 },
                new GearStatBonus { Stat = StatType.Perception, Value = 1 }
            },
            HealthBonus = 0,
            SetName = null,
            CraftingRecipeId = scoutBladeRecipe.Id
        });

        Log.Info("Seeding gear set bonuses");

        ctx.Db.GearSetBonus.Insert(new GearSetBonus
        {
            Id = 0,
            SetName = "Survivor's Set",
            PiecesRequired = 3,
            StatBonuses = new List<GearStatBonus>
            {
                new GearStatBonus { Stat = StatType.Endurance, Value = 3 }
            },
            HealthBonus = 10
        });

        ctx.Db.GearSetBonus.Insert(new GearSetBonus
        {
            Id = 0,
            SetName = "Survivor's Set",
            PiecesRequired = 5,
            StatBonuses = new List<GearStatBonus>
            {
                new GearStatBonus { Stat = StatType.Endurance, Value = 5 },
                new GearStatBonus { Stat = StatType.Strength, Value = 2 }
            },
            HealthBonus = 25
        });
    }

    [SpacetimeDB.Reducer]
    public static void Travel(ReducerContext ctx, LocationType destination)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
            throw new Exception("Player not found");

        if (player.Location == destination)
            throw new Exception("Already at that location");

        if (destination == LocationType.GuildHall)
        {
            if (ctx.Db.GuildMember.PlayerId.Find(ctx.Sender) is null)
                throw new Exception("You are not in a guild");
        }


        ctx.Db.Player.Identity.Update(player with { Location = destination });

        RemoveAllScheduledEventsForParticipant(ctx, ctx.Sender);

        if (destination == LocationType.Shelter || destination == LocationType.Wastes)
        {
            StartShelterSchedules(ctx, ctx.Sender);
        }
    }

    [SpacetimeDB.Reducer]
    public static void BuildStructure(ReducerContext ctx, ulong definitionId, int posX, int posY)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
            throw new Exception("Player not found");

        if (!IsLocationValid(LocationType.Shelter, player.Location))
            throw new Exception("You must be in a shelter to build structures");

        if (ctx.Db.StructureDefinition.Id.Find(definitionId) is not StructureDefinition definition)
            throw new Exception("Structure definition not found");

        var existing = ctx.Db.PlayerStructure.by_owner_and_definition
            .Filter((Owner: ctx.Sender, DefinitionId: definitionId));
        if (existing.Any())
            throw new Exception($"You already have a {definition.Name}");

        foreach (var cost in definition.Cost)
        {
            var resource = ctx.Db.ResourceTracker.by_owner_and_type
                .Filter((Owner: ctx.Sender, Type: cost.Type));
            if (!resource.Any() || resource.First().Amount < cost.Amount)
                throw new Exception($"Insufficient {cost.Type}");
        }

        foreach (var cost in definition.Cost)
        {
            var resource = ctx.Db.ResourceTracker.by_owner_and_type
                .Filter((Owner: ctx.Sender, Type: cost.Type)).First();
            resource.Amount -= cost.Amount;
            ctx.Db.ResourceTracker.Id.Update(resource);
        }

        ctx.Db.PlayerStructure.Insert(new PlayerStructure
        {
            Id = 0,
            Owner = ctx.Sender,
            DefinitionId = definitionId,
            Level = 1,
            BuiltAt = ctx.Timestamp,
            PosX = posX,
            PosY = posY
        });

        if (definition.Name == "Storage Chest")
        {
            ctx.Db.StorageChest.Insert(new StorageChest
            {
                Id = 0,
                Owner = ctx.Sender,
                Capacity = MaxInventorySlots
            });
        }
    }

    [SpacetimeDB.Reducer]
    public static void CraftRecipe(ReducerContext ctx, ulong recipeId)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
            throw new Exception("Player not found");

        if (ctx.Db.CraftingRecipe.Id.Find(recipeId) is not CraftingRecipe recipe)
            throw new Exception("Recipe not found");

        if (recipe.IsGearRecipe)
            throw new Exception("Use CraftGear for gear recipes");

        bool ownsStation = ctx.Db.PlayerStructure.by_owner_and_definition
            .Filter((Owner: ctx.Sender, DefinitionId: recipe.StructureDefinitionId)).Any();
        if (!ownsStation)
            throw new Exception("You don't own the required crafting station");

        foreach (var cost in recipe.InputCost)
        {
            var resource = ctx.Db.ResourceTracker.by_owner_and_type
                .Filter((Owner: ctx.Sender, Type: cost.Type));
            if (!resource.Any() || resource.First().Amount < cost.Amount)
                throw new Exception($"Insufficient {cost.Type}");
        }

        foreach (var cost in recipe.InputCost)
        {
            var resource = ctx.Db.ResourceTracker.by_owner_and_type
                .Filter((Owner: ctx.Sender, Type: cost.Type)).First();
            resource.Amount -= cost.Amount;
            ctx.Db.ResourceTracker.Id.Update(resource);
        }

        AddResourceToPlayer(ctx, ctx.Sender, recipe.OutputResource, recipe.OutputAmount);
    }
}
