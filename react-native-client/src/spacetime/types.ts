/**
 * Canonical enum types for the idle-survivor client.
 *
 * These use string-literal values that match the tag names in the SpacetimeDB
 * generated tagged-union types.  This lets us use them as Map keys, compare
 * them directly against `row.someField.tag`, and pass `{ tag: value }` when
 * calling reducers.
 */

// ─── Enum constant objects ────────────────────────────────────────────────────

export const ActivityType = {
  Scavenge:     'Scavenge',
  ChopWood:     'ChopWood',
  Mine:         'Mine',
  GatherFabric: 'GatherFabric',
  Forage:       'Forage',
  Salvage:      'Salvage',
} as const;
export type ActivityType = typeof ActivityType[keyof typeof ActivityType];

export const ResourceType = {
  Food:   'Food',
  Money:  'Money',
  Wood:   'Wood',
  Metal:  'Metal',
  Fabric: 'Fabric',
  Parts:  'Parts',
} as const;
export type ResourceType = typeof ResourceType[keyof typeof ResourceType];

export const LocationType = {
  Shelter:   'Shelter',
  GuildHall: 'GuildHall',
  Wastes:    'Wastes',
} as const;
export type LocationType = typeof LocationType[keyof typeof LocationType];

export const StatType = {
  Health:        'Health',
  MaxHealth:     'MaxHealth',
  Strength:      'Strength',
  Intelligence:  'Intelligence',
  Perception:    'Perception',
  Wit:           'Wit',
  Endurance:     'Endurance',
  Dexterity:     'Dexterity',
  ZombiesKilled: 'ZombiesKilled',
  KillSpeed:     'KillSpeed',
} as const;
export type StatType = typeof StatType[keyof typeof StatType];

export const UpgradeType = {
  AttackSpeed:    'AttackSpeed',
  KillsPerClick:  'KillsPerClick',
  ZombieDensity:  'ZombieDensity',
  LootMultiplier: 'LootMultiplier',
} as const;
export type UpgradeType = typeof UpgradeType[keyof typeof UpgradeType];

export const GearSlot = {
  Head:   'Head',
  Chest:  'Chest',
  Arms:   'Arms',
  Legs:   'Legs',
  Feet:   'Feet',
  Weapon: 'Weapon',
} as const;
export type GearSlot = typeof GearSlot[keyof typeof GearSlot];

export const SkillTreeEffectKind = {
  None:                 'None',
  AutoKillEnable:       'AutoKillEnable',
  UnlockUpgrade:        'UnlockUpgrade',
  ScavengeUnlock:       'ScavengeUnlock',
  UpgradeActivity:      'UpgradeActivity',
  AutoActivity:         'AutoActivity',
  ActivitySpeedUpgrade: 'ActivitySpeedUpgrade',
} as const;
export type SkillTreeEffectKind = typeof SkillTreeEffectKind[keyof typeof SkillTreeEffectKind];

// ─── Helper: extract the tag string from a generated tagged union value ───────

/** e.g. { tag: 'Scavenge' }  →  'Scavenge' */
export function tagOf(v: { tag: string } | null | undefined): string {
  return v?.tag ?? '';
}

/** Build a tagged-union value from a plain tag string. */
export function tagged(tag: string): { tag: string } {
  return { tag };
}

// ─── Convenience label maps ───────────────────────────────────────────────────

export const RESOURCE_LABELS: Record<ResourceType, string> = {
  [ResourceType.Food]:   'Food',
  [ResourceType.Money]:  'Money',
  [ResourceType.Wood]:   'Wood',
  [ResourceType.Metal]:  'Metal',
  [ResourceType.Fabric]: 'Fabric',
  [ResourceType.Parts]:  'Parts',
};

export const ACTIVITY_LABELS: Record<ActivityType, string> = {
  [ActivityType.Scavenge]:     'Scavenge',
  [ActivityType.ChopWood]:     'Chop Wood',
  [ActivityType.Mine]:         'Mine',
  [ActivityType.GatherFabric]: 'Gather Fabric',
  [ActivityType.Forage]:       'Forage',
  [ActivityType.Salvage]:      'Salvage',
};

export const STAT_LABELS: Record<StatType, string> = {
  [StatType.Health]:        'Health',
  [StatType.MaxHealth]:     'Max Health',
  [StatType.Strength]:      'Strength',
  [StatType.Intelligence]:  'Intelligence',
  [StatType.Perception]:    'Perception',
  [StatType.Wit]:           'Wit',
  [StatType.Endurance]:     'Endurance',
  [StatType.Dexterity]:     'Dexterity',
  [StatType.ZombiesKilled]: 'Zombies Killed',
  [StatType.KillSpeed]:     'Kill Speed',
};

export const GEAR_SLOT_LABELS: Record<GearSlot, string> = {
  [GearSlot.Head]:   'Head',
  [GearSlot.Chest]:  'Chest',
  [GearSlot.Arms]:   'Arms',
  [GearSlot.Legs]:   'Legs',
  [GearSlot.Feet]:   'Feet',
  [GearSlot.Weapon]: 'Weapon',
};

export const LOCATION_LABELS: Record<LocationType, string> = {
  [LocationType.Shelter]:   'Shelter',
  [LocationType.GuildHall]: 'Guild Hall',
  [LocationType.Wastes]:    'Wastes',
};
