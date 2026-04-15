using SpacetimeDB;

[SpacetimeDB.Type]
public enum GearSlot : byte
{
    Head,
    Chest,
    Arms,
    Legs,
    Feet,
    Weapon
}

[SpacetimeDB.Type]
public partial struct GearStatBonus
{
    public StatType Stat;
    public int Value;
}

public static partial class Module
{
    public const int MaxInventorySlots = 15;

    [SpacetimeDB.Table(Accessor = "GearDefinition", Public = true)]
    public partial struct GearDefinition
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Unique]
        public string Name;

        public GearSlot Slot;
        public List<GearStatBonus> StatBonuses;
        public int HealthBonus;
        public string? SetName;
        public ulong CraftingRecipeId;
    }

    [SpacetimeDB.Table(Accessor = "GearSetBonus", Public = true)]
    public partial struct GearSetBonus
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public string SetName;

        public int PiecesRequired;
        public List<GearStatBonus> StatBonuses;
        public int HealthBonus;
    }

    [SpacetimeDB.Table(Accessor = "InventoryItem", Public = true)]
    public partial struct InventoryItem
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public Identity Owner;

        public ulong GearDefinitionId;
        public Identity CraftedBy;
        public Timestamp CraftedAt;
    }

    [SpacetimeDB.Table(Accessor = "EquippedGear", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "by_equip_owner_slot", Columns = new[] { "Owner", "Slot" })]
    public partial struct EquippedGear
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public Identity Owner;

        public GearSlot Slot;
        public ulong InventoryItemId;
        public ulong GearDefinitionId;
        public Identity CraftedBy;
        public Timestamp CraftedAt;
    }

    [SpacetimeDB.Table(Accessor = "StorageChest", Public = true)]
    public partial struct StorageChest
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public Identity Owner;

        public int Capacity;
    }

    [SpacetimeDB.Table(Accessor = "ChestItem", Public = true)]
    public partial struct ChestItem
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public ulong ChestId;

        public ulong GearDefinitionId;
        public Identity CraftedBy;
        public Timestamp CraftedAt;
    }

    private static int CountInventoryItems(ReducerContext ctx, Identity owner)
    {
        int count = 0;
        foreach (var _ in ctx.Db.InventoryItem.Owner.Filter(owner))
            count++;
        return count;
    }

    private static int CountChestItems(ReducerContext ctx, ulong chestId)
    {
        int count = 0;
        foreach (var _ in ctx.Db.ChestItem.ChestId.Filter(chestId))
            count++;
        return count;
    }

    private static void ApplyGearStats(ReducerContext ctx, Identity owner, GearDefinition def, bool add)
    {
        int sign = add ? 1 : -1;

        foreach (var bonus in def.StatBonuses)
        {
            int current = GetStat(ctx, owner, bonus.Stat);
            SetStat(ctx, owner, bonus.Stat, current + bonus.Value * sign);
        }

        if (def.HealthBonus != 0)
        {
            int currentMax = GetStat(ctx, owner, StatType.MaxHealth);
            SetStat(ctx, owner, StatType.MaxHealth, currentMax + def.HealthBonus * sign);
        }
    }

    private static void RecalculateSetBonuses(ReducerContext ctx, Identity owner, string setName, bool wasEquipped)
    {
        int equippedSetCount = 0;
        var countedSlots = new HashSet<GearSlot>();
        foreach (var eq in ctx.Db.EquippedGear.Owner.Filter(owner))
        {
            if (ctx.Db.GearDefinition.Id.Find(eq.GearDefinitionId) is GearDefinition eqDef
                && eqDef.SetName == setName
                && countedSlots.Add(eqDef.Slot))
            {
                equippedSetCount++;
            }
        }

        int previousCount = wasEquipped ? equippedSetCount + 1 : equippedSetCount - 1;

        foreach (var setBonus in ctx.Db.GearSetBonus.Iter())
        {
            if (setBonus.SetName != setName) continue;

            bool wasActive = previousCount >= setBonus.PiecesRequired;
            bool isActive = equippedSetCount >= setBonus.PiecesRequired;

            if (wasActive && !isActive)
            {
                foreach (var b in setBonus.StatBonuses)
                    SetStat(ctx, owner, b.Stat, GetStat(ctx, owner, b.Stat) - b.Value);
                if (setBonus.HealthBonus != 0)
                    SetStat(ctx, owner, StatType.MaxHealth, GetStat(ctx, owner, StatType.MaxHealth) - setBonus.HealthBonus);
            }
            else if (!wasActive && isActive)
            {
                foreach (var b in setBonus.StatBonuses)
                    SetStat(ctx, owner, b.Stat, GetStat(ctx, owner, b.Stat) + b.Value);
                if (setBonus.HealthBonus != 0)
                    SetStat(ctx, owner, StatType.MaxHealth, GetStat(ctx, owner, StatType.MaxHealth) + setBonus.HealthBonus);
            }
        }
    }

    [SpacetimeDB.Reducer]
    public static void CraftGear(ReducerContext ctx, ulong recipeId)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
            throw new Exception("Player not found");

        if (ctx.Db.CraftingRecipe.Id.Find(recipeId) is not CraftingRecipe recipe)
            throw new Exception("Recipe not found");

        if (!recipe.IsGearRecipe)
            throw new Exception("This recipe does not produce gear");

        bool ownsStation = ctx.Db.PlayerStructure.by_owner_and_definition
            .Filter((Owner: ctx.Sender, DefinitionId: recipe.StructureDefinitionId)).Any();
        if (!ownsStation)
            throw new Exception("You don't own the required crafting station");

        if (CountInventoryItems(ctx, ctx.Sender) >= MaxInventorySlots)
            throw new Exception("Inventory is full");

        GearDefinition? gearDef = null;
        foreach (var def in ctx.Db.GearDefinition.Iter())
        {
            if (def.CraftingRecipeId == recipeId)
            {
                gearDef = def;
                break;
            }
        }
        if (gearDef is null)
            throw new Exception("No gear definition linked to this recipe");

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

        ctx.Db.InventoryItem.Insert(new InventoryItem
        {
            Id = 0,
            Owner = ctx.Sender,
            GearDefinitionId = gearDef.Value.Id,
            CraftedBy = ctx.Sender,
            CraftedAt = ctx.Timestamp
        });
    }

    [SpacetimeDB.Reducer]
    public static void EquipGear(ReducerContext ctx, ulong inventoryItemId)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
            throw new Exception("Player not found");

        if (ctx.Db.InventoryItem.Id.Find(inventoryItemId) is not InventoryItem item)
            throw new Exception("Item not found in inventory");

        if (item.Owner != ctx.Sender)
            throw new Exception("You don't own this item");

        if (ctx.Db.GearDefinition.Id.Find(item.GearDefinitionId) is not GearDefinition def)
            throw new Exception("Gear definition not found");

        var existingEquip = ctx.Db.EquippedGear.by_equip_owner_slot
            .Filter((Owner: ctx.Sender, Slot: def.Slot));

        if (existingEquip.Any())
        {
            var old = existingEquip.First();
            if (ctx.Db.GearDefinition.Id.Find(old.GearDefinitionId) is GearDefinition oldDef)
            {
                ApplyGearStats(ctx, ctx.Sender, oldDef, false);
                if (oldDef.SetName is not null)
                    RecalculateSetBonuses(ctx, ctx.Sender, oldDef.SetName, true);
            }

            ctx.Db.InventoryItem.Insert(new InventoryItem
            {
                Id = 0,
                Owner = ctx.Sender,
                GearDefinitionId = old.GearDefinitionId,
                CraftedBy = old.CraftedBy,
                CraftedAt = old.CraftedAt
            });

            ctx.Db.EquippedGear.Id.Delete(old.Id);
        }

        ctx.Db.InventoryItem.Id.Delete(inventoryItemId);

        ctx.Db.EquippedGear.Insert(new EquippedGear
        {
            Id = 0,
            Owner = ctx.Sender,
            Slot = def.Slot,
            InventoryItemId = 0,
            GearDefinitionId = item.GearDefinitionId,
            CraftedBy = item.CraftedBy,
            CraftedAt = item.CraftedAt
        });

        ApplyGearStats(ctx, ctx.Sender, def, true);
        if (def.SetName is not null)
            RecalculateSetBonuses(ctx, ctx.Sender, def.SetName, false);
    }

    [SpacetimeDB.Reducer]
    public static void UnequipGear(ReducerContext ctx, GearSlot slot)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
            throw new Exception("Player not found");

        var existing = ctx.Db.EquippedGear.by_equip_owner_slot
            .Filter((Owner: ctx.Sender, Slot: slot));

        if (!existing.Any())
            throw new Exception("No gear equipped in that slot");

        if (CountInventoryItems(ctx, ctx.Sender) >= MaxInventorySlots)
            throw new Exception("Inventory is full");

        var eq = existing.First();

        if (ctx.Db.GearDefinition.Id.Find(eq.GearDefinitionId) is GearDefinition def)
        {
            ApplyGearStats(ctx, ctx.Sender, def, false);
            if (def.SetName is not null)
                RecalculateSetBonuses(ctx, ctx.Sender, def.SetName, true);
        }

        ctx.Db.InventoryItem.Insert(new InventoryItem
        {
            Id = 0,
            Owner = ctx.Sender,
            GearDefinitionId = eq.GearDefinitionId,
            CraftedBy = eq.CraftedBy,
            CraftedAt = eq.CraftedAt
        });

        ctx.Db.EquippedGear.Id.Delete(eq.Id);
    }

    [SpacetimeDB.Reducer]
    public static void TransferToChest(ReducerContext ctx, ulong inventoryItemId, ulong chestId)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
            throw new Exception("Player not found");

        if (ctx.Db.InventoryItem.Id.Find(inventoryItemId) is not InventoryItem item)
            throw new Exception("Item not found in inventory");

        if (item.Owner != ctx.Sender)
            throw new Exception("You don't own this item");

        if (ctx.Db.StorageChest.Id.Find(chestId) is not StorageChest chest)
            throw new Exception("Chest not found");

        if (chest.Owner != ctx.Sender)
            throw new Exception("You don't own this chest");

        if (CountChestItems(ctx, chestId) >= chest.Capacity)
            throw new Exception("Chest is full");

        ctx.Db.InventoryItem.Id.Delete(inventoryItemId);

        ctx.Db.ChestItem.Insert(new ChestItem
        {
            Id = 0,
            ChestId = chestId,
            GearDefinitionId = item.GearDefinitionId,
            CraftedBy = item.CraftedBy,
            CraftedAt = item.CraftedAt
        });
    }

    [SpacetimeDB.Reducer]
    public static void TransferFromChest(ReducerContext ctx, ulong chestItemId)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
            throw new Exception("Player not found");

        if (ctx.Db.ChestItem.Id.Find(chestItemId) is not ChestItem chestItem)
            throw new Exception("Item not found in chest");

        if (ctx.Db.StorageChest.Id.Find(chestItem.ChestId) is not StorageChest chest)
            throw new Exception("Chest not found");

        if (chest.Owner != ctx.Sender)
            throw new Exception("You don't own this chest");

        if (CountInventoryItems(ctx, ctx.Sender) >= MaxInventorySlots)
            throw new Exception("Inventory is full");

        ctx.Db.ChestItem.Id.Delete(chestItemId);

        ctx.Db.InventoryItem.Insert(new InventoryItem
        {
            Id = 0,
            Owner = ctx.Sender,
            GearDefinitionId = chestItem.GearDefinitionId,
            CraftedBy = chestItem.CraftedBy,
            CraftedAt = chestItem.CraftedAt
        });
    }
}
