import { create } from 'zustand';

// Row shapes inferred from generated schema (camelCase fields)
type TaggedType = { tag: string };

type SkillTreeNodeRow = {
  id: bigint;
  name: string;
  tooltip: string;
  prerequisiteNodeId: bigint | null;
  prerequisiteMinLevel: number;
  visualPrerequisiteNodeId: bigint | null;
  posX: number;
  posY: number;
  effectKind: TaggedType;
  effectParam: number;
  baseMaxLevel: number;
  baseCost: bigint;
  branchTier: number;
  [k: string]: any;
};

type SkillDefinitionRow = {
  id: bigint;
  name: string;
  description: string;
  cost: number;
  requiredLevel: number | null;
  prerequisiteSkillId: bigint | null;
  prerequisiteSkillId2: bigint | null;
  [k: string]: any;
};

type PlayerSkillTreeUnlockRow = {
  id: bigint;
  owner: any;
  nodeId: bigint;
  level: number;
  [k: string]: any;
};

type PlayerSkillRow = {
  id: bigint;
  owner: any;
  skillDefinitionId: bigint;
  [k: string]: any;
};

interface SkillState {
  nodes:            Map<bigint, SkillTreeNodeRow>;
  skillDefinitions: Map<bigint, SkillDefinitionRow>;
  unlockedNodes:    Map<bigint, PlayerSkillTreeUnlockRow>; // nodeId → unlock
  purchasedSkills:  Set<bigint>;                          // skillDefinitionIds

  upsertNode:       (n: SkillTreeNodeRow)           => void;
  upsertSkillDef:   (s: SkillDefinitionRow)         => void;
  upsertNodeUnlock: (u: PlayerSkillTreeUnlockRow)   => void;
  removeNodeUnlock: (id: bigint)                    => void;
  addPlayerSkill:   (s: PlayerSkillRow)             => void;
  removePlayerSkill:(id: bigint)                    => void;

  getNodeLevel:   (nodeId: bigint)    => number;
  isNodeUnlocked: (nodeId: bigint)    => boolean;
  hasSkill:       (skillDefId: bigint) => boolean;
  allNodes:       ()                  => SkillTreeNodeRow[];
}

export const useSkillStore = create<SkillState>((set, get) => ({
  nodes:            new Map(),
  skillDefinitions: new Map(),
  unlockedNodes:    new Map(),
  purchasedSkills:  new Set(),

  upsertNode: n =>
    set(state => {
      const next = new Map(state.nodes);
      next.set(n.id, n);
      return { nodes: next };
    }),

  upsertSkillDef: s =>
    set(state => {
      const next = new Map(state.skillDefinitions);
      next.set(s.id, s);
      return { skillDefinitions: next };
    }),

  upsertNodeUnlock: u =>
    set(state => {
      const next = new Map(state.unlockedNodes);
      next.set(u.nodeId, u);
      return { unlockedNodes: next };
    }),

  removeNodeUnlock: id =>
    set(state => {
      const next = new Map(state.unlockedNodes);
      for (const [nodeId, u] of next) {
        if (u.id === id) { next.delete(nodeId); break; }
      }
      return { unlockedNodes: next };
    }),

  addPlayerSkill: s =>
    set(state => {
      const next = new Set(state.purchasedSkills);
      next.add(s.skillDefinitionId);
      return { purchasedSkills: next };
    }),

  removePlayerSkill: _id => {
    // PlayerSkills are never removed (no unlearn mechanic)
  },

  getNodeLevel:   nodeId => get().unlockedNodes.get(nodeId)?.level ?? 0,
  isNodeUnlocked: nodeId => get().unlockedNodes.has(nodeId),
  hasSkill:       id     => get().purchasedSkills.has(id),
  allNodes:       ()     => Array.from(get().nodes.values()),
}));
