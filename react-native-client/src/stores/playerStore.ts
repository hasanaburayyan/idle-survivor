import { create } from 'zustand';
import { tagOf, type StatType, type UpgradeType } from '../spacetime/types';

// Row shapes from generated bindings (camelCase, tagged unions)
type TaggedType = { tag: string };
type PlayerRow    = { identity: any; displayName: string; email: string | null; online: boolean; location: TaggedType; [k: string]: any };
type PlayerLevelRow = { owner: any; level: number; xp: bigint; availableSkillPoints: number; [k: string]: any };
type PlayerStatRow  = { id: bigint; owner: any; stat: TaggedType; value: number; [k: string]: any };
type PlayerUpgradeRow = { id: bigint; owner: any; type: TaggedType; level: number; [k: string]: any };
type KillLootRow  = { id: bigint; owner: any; resource: TaggedType; amount: bigint; [k: string]: any };

interface PlayerState {
  player:   PlayerRow    | null;
  level:    PlayerLevelRow | null;
  stats:    Map<string, number>;
  upgrades: Map<string, number>;
  killLoot: KillLootRow[];

  setPlayer:      (p: PlayerRow | null)   => void;
  updatePlayer:   (p: PlayerRow)          => void;
  setLevel:       (l: PlayerLevelRow)     => void;
  setStat:        (s: PlayerStatRow)      => void;
  removeStat:     (id: bigint)            => void;
  setUpgrade:     (u: PlayerUpgradeRow)   => void;
  addKillLoot:    (loot: KillLootRow)     => void;
  removeKillLoot: (id: bigint)            => void;

  getStat:         (stat: StatType)    => number;
  getUpgradeLevel: (type: UpgradeType) => number;
  xpPercent:       ()                  => number;
}

export const usePlayerStore = create<PlayerState>((set, get) => ({
  player:   null,
  level:    null,
  stats:    new Map(),
  upgrades: new Map(),
  killLoot: [],

  setPlayer: p => set({ player: p }),

  updatePlayer: p =>
    set(state => ({
      player: state.player?.identity?.toHexString?.() === p.identity?.toHexString?.() ? p : state.player,
    })),

  setLevel: l => set({ level: l }),

  setStat: s =>
    set(state => {
      const next = new Map(state.stats);
      next.set(tagOf(s.stat), s.value);
      return { stats: next };
    }),

  removeStat: _id => {
    // Stats are keyed by type string — nothing to remove by ID here
  },

  setUpgrade: u =>
    set(state => {
      const next = new Map(state.upgrades);
      next.set(tagOf(u.type), u.level);
      return { upgrades: next };
    }),

  addKillLoot:    loot => set(state => ({ killLoot: [...state.killLoot, loot] })),
  removeKillLoot: id   => set(state => ({ killLoot: state.killLoot.filter(l => l.id !== id) })),

  getStat:         stat => get().stats.get(stat) ?? 0,
  getUpgradeLevel: type => get().upgrades.get(type) ?? 0,

  xpPercent: () => {
    const { level } = get();
    if (!level) return 0;
    const needed = Math.floor(20 * Math.pow(1.5, level.level ?? 0));
    const xp     = level.xp != null ? Number(level.xp) : 0;
    return needed > 0 ? xp / needed : 0;
  },
}));
