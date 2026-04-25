import { create } from 'zustand';
import { ResourceType, tagOf } from '../spacetime/types';

// Row shape from generated bindings
type TaggedType = { tag: string };
type ResourceTrackerRow = {
  id: bigint;
  owner: any;
  type: TaggedType;
  amount: bigint;
  [key: string]: any;
};

interface ResourceState {
  // Keyed by ResourceType tag string (e.g. 'Money')
  resources:  Map<string, bigint>;
  trackerIds: Map<string, bigint>;

  // Table callbacks
  upsertTracker: (t: ResourceTrackerRow) => void;
  removeTracker: (id: bigint)            => void;

  // Helpers
  getAmount: (type: ResourceType) => bigint;
  canAfford: (costs: Array<{ type: TaggedType; amount: bigint }>) => boolean;
}

export const useResourceStore = create<ResourceState>((set, get) => ({
  resources: new Map([
    [ResourceType.Food,   0n],
    [ResourceType.Money,  0n],
    [ResourceType.Wood,   0n],
    [ResourceType.Metal,  0n],
    [ResourceType.Fabric, 0n],
    [ResourceType.Parts,  0n],
  ]),
  trackerIds: new Map(),

  upsertTracker: t =>
    set(state => {
      const r   = new Map(state.resources);
      const ids = new Map(state.trackerIds);
      const key = tagOf(t.type);
      r.set(key, t.amount);
      ids.set(key, t.id);
      return { resources: r, trackerIds: ids };
    }),

  removeTracker: id =>
    set(state => {
      for (const [key, tid] of state.trackerIds) {
        if (tid === id) {
          const r = new Map(state.resources);
          r.set(key, 0n);
          return { resources: r };
        }
      }
      return {};
    }),

  getAmount: type => get().resources.get(type) ?? 0n,

  canAfford: costs => {
    const { resources } = get();
    return costs.every(c => (resources.get(tagOf(c.type)) ?? 0n) >= c.amount);
  },
}));
