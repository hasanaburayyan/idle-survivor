import { create } from 'zustand';
import { GearSlot, tagOf } from '../spacetime/types';

// Row shapes inferred from generated schema (camelCase fields)
type TaggedType = { tag: string };

type GearDefinitionRow = {
  id: bigint;
  name: string;
  slot: TaggedType;
  statBonuses: Array<any>;
  healthBonus: number;
  setName: string | null;
  craftingRecipeId: bigint;
  [k: string]: any;
};

type GearSetBonusRow = {
  id: bigint;
  setName: string;
  [k: string]: any;
};

type InventoryItemRow = {
  id: bigint;
  owner: any;
  gearDefinitionId: bigint;
  craftedBy: any;
  craftedAt: any;
  [k: string]: any;
};

type EquippedGearRow = {
  id: bigint;
  owner: any;
  slot: TaggedType;
  inventoryItemId: bigint;
  gearDefinitionId: bigint;
  [k: string]: any;
};

type StorageChestRow = {
  id: bigint;
  owner: any;
  capacity: number;
  [k: string]: any;
};

type ChestItemRow = {
  id: bigint;
  chestId: bigint;
  gearDefinitionId: bigint;
  [k: string]: any;
};

type CraftingRecipeRow = {
  id: bigint;
  name: string;
  inputCost: Array<{ type: TaggedType; amount: bigint }>;
  outputResource: TaggedType;
  outputAmount: bigint;
  durationMs: bigint;
  isGearRecipe: boolean;
  [k: string]: any;
};

interface EquipmentState {
  gearDefinitions: Map<bigint, GearDefinitionRow>;
  setBonuses:      Map<string, GearSetBonusRow[]>;
  craftingRecipes: Map<bigint, CraftingRecipeRow>;
  inventory:       Map<bigint, InventoryItemRow>;
  equipped:        Map<string, EquippedGearRow>;   // slot tag → row
  chest:           StorageChestRow | null;
  chestItems:      Map<bigint, ChestItemRow>;

  upsertGearDef:       (d: GearDefinitionRow)  => void;
  upsertSetBonus:      (b: GearSetBonusRow)    => void;
  upsertRecipe:        (r: CraftingRecipeRow)  => void;
  addInventoryItem:    (i: InventoryItemRow)   => void;
  removeInventoryItem: (id: bigint)            => void;
  upsertEquipped:      (e: EquippedGearRow)    => void;
  removeEquipped:      (id: bigint)            => void;
  setChest:            (c: StorageChestRow)    => void;
  addChestItem:        (i: ChestItemRow)       => void;
  removeChestItem:     (id: bigint)            => void;

  getGearDef:     (id: bigint)     => GearDefinitionRow | undefined;
  inventoryArray: ()               => InventoryItemRow[];
  equippedInSlot: (slot: GearSlot) => EquippedGearRow | undefined;
  activeSets:     ()               => string[];
}

export const useEquipmentStore = create<EquipmentState>((set, get) => ({
  gearDefinitions: new Map(),
  setBonuses:      new Map(),
  craftingRecipes: new Map(),
  inventory:       new Map(),
  equipped:        new Map(),
  chest:           null,
  chestItems:      new Map(),

  upsertGearDef: d =>
    set(state => {
      const next = new Map(state.gearDefinitions);
      next.set(d.id, d);
      return { gearDefinitions: next };
    }),

  upsertSetBonus: b =>
    set(state => {
      const next = new Map(state.setBonuses);
      const arr  = next.get(b.setName) ?? [];
      const idx  = arr.findIndex(x => x.id === b.id);
      if (idx >= 0) arr[idx] = b; else arr.push(b);
      next.set(b.setName, arr);
      return { setBonuses: next };
    }),

  upsertRecipe: r =>
    set(state => {
      const next = new Map(state.craftingRecipes);
      next.set(r.id, r);
      return { craftingRecipes: next };
    }),

  addInventoryItem: i =>
    set(state => {
      const next = new Map(state.inventory);
      next.set(i.id, i);
      return { inventory: next };
    }),

  removeInventoryItem: id =>
    set(state => {
      const next = new Map(state.inventory);
      next.delete(id);
      return { inventory: next };
    }),

  upsertEquipped: e =>
    set(state => {
      const next = new Map(state.equipped);
      next.set(tagOf(e.slot), e);
      return { equipped: next };
    }),

  removeEquipped: id =>
    set(state => {
      const next = new Map(state.equipped);
      for (const [slot, eq] of next) {
        if (eq.id === id) { next.delete(slot); break; }
      }
      return { equipped: next };
    }),

  setChest:    c => set({ chest: c }),

  addChestItem: i =>
    set(state => {
      const next = new Map(state.chestItems);
      next.set(i.id, i);
      return { chestItems: next };
    }),

  removeChestItem: id =>
    set(state => {
      const next = new Map(state.chestItems);
      next.delete(id);
      return { chestItems: next };
    }),

  getGearDef:     id   => get().gearDefinitions.get(id),
  inventoryArray: ()   => Array.from(get().inventory.values()),
  equippedInSlot: slot => get().equipped.get(slot),

  activeSets: () => {
    const { equipped, gearDefinitions } = get();
    const counts = new Map<string, number>();
    for (const eq of equipped.values()) {
      const def = gearDefinitions.get(eq.gearDefinitionId);
      if (def?.setName) {
        counts.set(def.setName, (counts.get(def.setName) ?? 0) + 1);
      }
    }
    return Array.from(counts.keys());
  },
}));
