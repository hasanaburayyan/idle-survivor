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
            }
        });

        var workbench = ctx.Db.StructureDefinition.Insert(new StructureDefinition
        {
            Id = 0,
            Name = "Workbench",
            Cost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Wood, Amount = 20 },
                new ActivityCost { Type = ResourceType.Parts, Amount = 10 }
            }
        });

        var tailorStation = ctx.Db.StructureDefinition.Insert(new StructureDefinition
        {
            Id = 0,
            Name = "Tailor Station",
            Cost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Fabric, Amount = 30 },
                new ActivityCost { Type = ResourceType.Wood, Amount = 15 }
            }
        });

        var weaponStation = ctx.Db.StructureDefinition.Insert(new StructureDefinition
        {
            Id = 0,
            Name = "Weapon Station",
            Cost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Metal, Amount = 40 },
                new ActivityCost { Type = ResourceType.Parts, Amount = 20 }
            }
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
                new ActivityCost { Type = ResourceType.Metal, Amount = 5 },
                new ActivityCost { Type = ResourceType.Wood, Amount = 3 }
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

        Log.Info("Seeding skill definitions");

        var autoKill = ctx.Db.SkillDefinition.Insert(new SkillDefinition
        {
            Id = 0,
            Name = "Auto Kill Zombie",
            Description = "Your character automatically kills nearby zombies.",
            Cost = 1,
            RequiredLevel = null,
            PrerequisiteSkillId = null,
            PrerequisiteSkillId2 = null
        });

        var unlockWood = ctx.Db.SkillDefinition.Insert(new SkillDefinition
        {
            Id = 0,
            Name = "Unlock Wood Gathering",
            Description = "Unlocks the Chop Wood activity.",
            Cost = 1,
            RequiredLevel = null,
            PrerequisiteSkillId = autoKill.Id,
            PrerequisiteSkillId2 = null
        });

        var unlockMetal = ctx.Db.SkillDefinition.Insert(new SkillDefinition
        {
            Id = 0,
            Name = "Unlock Metal Gathering",
            Description = "Unlocks the Mine activity.",
            Cost = 1,
            RequiredLevel = null,
            PrerequisiteSkillId = autoKill.Id,
            PrerequisiteSkillId2 = null
        });

        var autoActivity = ctx.Db.SkillDefinition.Insert(new SkillDefinition
        {
            Id = 0,
            Name = "Automate Activity",
            Description = "Allows you to automate 1 activity at a time.",
            Cost = 1,
            RequiredLevel = null,
            PrerequisiteSkillId = unlockWood.Id,
            PrerequisiteSkillId2 = unlockMetal.Id
        });

        ctx.Db.SkillDefinition.Insert(new SkillDefinition
        {
            Id = 0,
            Name = "Automate Activity 2",
            Description = "Allows you to automate a second activity simultaneously.",
            Cost = 10,
            RequiredLevel = null,
            PrerequisiteSkillId = autoActivity.Id,
            PrerequisiteSkillId2 = null
        });

        ctx.Db.SkillDefinition.Insert(new SkillDefinition
        {
            Id = 0,
            Name = "Unlock Wastes",
            Description = "Placeholder — venture into the wastes.",
            Cost = 1,
            RequiredLevel = null,
            PrerequisiteSkillId = autoActivity.Id,
            PrerequisiteSkillId2 = null
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

        if (destination == LocationType.Wastes)
        {
            var wastesDef = ctx.Db.SkillDefinition.Name.Find("Unlock Wastes")
                ?? throw new Exception("Unlock Wastes skill not found");
            if (!ctx.Db.PlayerSkill.by_skill_owner_def
                .Filter((Owner: ctx.Sender, SkillDefinitionId: wastesDef.Id)).Any())
                throw new Exception("You haven't unlocked the Wastes");
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
    }

    [SpacetimeDB.Reducer]
    public static void CraftRecipe(ReducerContext ctx, ulong recipeId)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
            throw new Exception("Player not found");

        if (ctx.Db.CraftingRecipe.Id.Find(recipeId) is not CraftingRecipe recipe)
            throw new Exception("Recipe not found");

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
