import React, { useMemo, useState, useCallback } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  ScrollView,
  StyleSheet,
  Dimensions,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useSkillStore } from '../stores/skillStore';
import { useResourceStore } from '../stores';
import { reducers } from '../spacetime/client';
import { tagOf, ResourceType } from '../spacetime/types';
import ResourceBar from '../components/ui/ResourceBar';

// ─── Layout constants ─────────────────────────────────────────────────────────

const SCREEN_W   = Dimensions.get('window').width;
const NODE_W     = 76;
const NODE_H     = 76;
const TIER_H     = 120;
const H_PAD      = 24;
const CANVAS_PAD = 40;

// ─── Resource helpers (matches ResourceBar display) ───────────────────────────

const RESOURCE_ICONS: Record<string, string> = {
  Money:  '💰',
  Wood:   '🪵',
  Metal:  '⚙️',
  Fabric: '🧵',
  Food:   '🍖',
  Parts:  '🔩',
};

// ─── Cost logic — exact port of SkillTree.cs ─────────────────────────────────

// TierResources[i] is required starting at costTier > i
const TIER_RESOURCES: ResourceType[] = [
  ResourceType.Wood,
  ResourceType.Metal,
  ResourceType.Fabric,
  ResourceType.Food,
  ResourceType.Parts,
];

function getCostTier(branchTier: number, effectKind: string, currentLevel: number): number {
  if (effectKind === 'ScavengeUnlock') {
    return branchTier > 0 ? branchTier - 1 : 0;
  }
  const nextLevel = currentLevel + 1;
  const band = Math.floor((nextLevel - 1) / 5);
  return branchTier + band;
}

type ResourceCost = { type: ResourceType; amount: bigint };

function getNextLevelCost(
  baseCost: bigint,
  branchTier: number,
  effectKind: string,
  currentLevel: number,
): ResourceCost[] {
  const perResource = BigInt(
    Math.max(1, Math.floor(Number(baseCost) * Math.pow(1.5, currentLevel)))
  );
  const costs: ResourceCost[] = [{ type: ResourceType.Money, amount: perResource }];
  const costTier = getCostTier(branchTier, effectKind, currentLevel);
  for (let i = 1; i <= costTier && i <= TIER_RESOURCES.length; i++) {
    costs.push({ type: TIER_RESOURCES[i - 1], amount: perResource });
  }
  return costs;
}

function getEffectiveMaxLevel(baseMaxLevel: number, branchTier: number, unlockedTiers: number): number {
  if (baseMaxLevel <= 1) return baseMaxLevel;
  if (unlockedTiers <= branchTier) return baseMaxLevel;
  return baseMaxLevel * (1 + unlockedTiers - branchTier);
}

// ─── Types ────────────────────────────────────────────────────────────────────

type NodeRow = {
  id: bigint;
  name: string;
  tooltip: string;
  posX: number;
  posY: number;
  branchTier: number;
  prerequisiteNodeId: bigint | null;
  prerequisiteMinLevel: number;
  visualPrerequisiteNodeId: bigint | null;
  effectKind: { tag: string };
  effectParam: number;
  baseMaxLevel: number;
  baseCost: bigint;
  [k: string]: any;
};

type NodeState = 'locked' | 'available' | 'unlocked' | 'maxed';

// ─── Helpers ──────────────────────────────────────────────────────────────────

function effectIcon(kind: string): string {
  const icons: Record<string, string> = {
    AutoKillEnable:       '⚡',
    UnlockUpgrade:        '🔓',
    ScavengeUnlock:       '🔍',
    UpgradeActivity:      '⬆️',
    AutoActivity:         '🔄',
    ActivitySpeedUpgrade: '⏩',
  };
  return icons[kind] ?? '✦';
}

function effectLabel(kind: string, param: number): string {
  switch (kind) {
    case 'AutoKillEnable':       return 'Auto Kill';
    case 'UnlockUpgrade':        return 'Unlock Upgrade';
    case 'ScavengeUnlock':       return 'Unlock Scavenge';
    case 'UpgradeActivity':      return `Activity Lv +${param}`;
    case 'AutoActivity':         return 'Auto Activity';
    case 'ActivitySpeedUpgrade': return `Speed -${param}ms`;
    default:                     return kind;
  }
}

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function SkillTreeScreen({ navigation }: any) {
  const nodes        = useSkillStore(s => s.nodes);
  const unlockedNodes= useSkillStore(s => s.unlockedNodes);
  const getNodeLevel = useSkillStore(s => s.getNodeLevel);
  const getAmount    = useResourceStore(s => s.getAmount);

  const [selectedId, setSelectedId] = useState<bigint | null>(null);

  // ── Count unlocked ScavengeUnlock tiers (affects effective max level) ────────
  const unlockedTiers = useMemo(() => {
    let count = 0;
    for (const [nodeId, unlock] of unlockedNodes) {
      if (unlock.level < 1) continue;
      const node = nodes.get(nodeId) as NodeRow | undefined;
      if (node && tagOf(node.effectKind) === 'ScavengeUnlock') count++;
    }
    return count;
  }, [nodes, unlockedNodes]);

  // ── Compute canvas layout ─────────────────────────────────────────────────

  const { layout, canvasH, canvasW } = useMemo(() => {
    const nodeArr = Array.from(nodes.values()) as NodeRow[];
    if (nodeArr.length === 0) return { layout: new Map(), canvasH: 0, canvasW: 0 };

    const tiers = new Map<number, NodeRow[]>();
    for (const n of nodeArr) {
      const t = n.branchTier ?? 0;
      if (!tiers.has(t)) tiers.set(t, []);
      tiers.get(t)!.push(n);
    }
    for (const arr of tiers.values()) arr.sort((a, b) => a.posX - b.posX);

    const sortedTiers = Array.from(tiers.keys()).sort((a, b) => a - b);
    const positions = new Map<bigint, { x: number; y: number }>();
    let maxX = SCREEN_W - H_PAD * 2;

    for (const tier of sortedTiers) {
      const tierNodes = tiers.get(tier)!;
      const y = CANVAS_PAD + tier * TIER_H;

      if (tierNodes.length === 1) {
        const x = (SCREEN_W - H_PAD * 2) / 2 - NODE_W / 2 + H_PAD;
        positions.set(tierNodes[0].id, { x, y });
      } else {
        const xs = tierNodes.map(n => n.posX);
        const minPX = Math.min(...xs);
        const maxPX = Math.max(...xs);
        const range = maxPX - minPX;
        const usableW = SCREEN_W - H_PAD * 2 - NODE_W;
        tierNodes.forEach((n, i) => {
          const x = range > 0
            ? H_PAD + ((n.posX - minPX) / range) * usableW
            : H_PAD + (i / (tierNodes.length - 1)) * usableW;
          maxX = Math.max(maxX, x + NODE_W);
          positions.set(n.id, { x, y });
        });
      }
    }

    const maxTier = Math.max(...sortedTiers);
    const h = CANVAS_PAD * 2 + maxTier * TIER_H + NODE_H;
    return { layout: positions, canvasH: h, canvasW: Math.max(maxX + H_PAD, SCREEN_W) };
  }, [nodes]);

  // ── Node state ────────────────────────────────────────────────────────────

  const getState = useCallback((node: NodeRow): NodeState => {
    const cur = getNodeLevel(node.id);
    const effectiveMax = getEffectiveMaxLevel(node.baseMaxLevel, node.branchTier, unlockedTiers);
    if (cur >= effectiveMax) return 'maxed';
    if (cur > 0)             return 'unlocked';
    if (node.prerequisiteNodeId != null) {
      const prereq = getNodeLevel(node.prerequisiteNodeId);
      if (prereq < (node.prerequisiteMinLevel ?? 1)) return 'locked';
    }
    return 'available';
  }, [getNodeLevel, unlockedTiers]);

  // ── Selected node ─────────────────────────────────────────────────────────

  const selectedNode  = selectedId != null ? (nodes.get(selectedId) as NodeRow | undefined) : undefined;
  const selectedState = selectedNode ? getState(selectedNode) : null;
  const selectedLevel = selectedId  != null ? getNodeLevel(selectedId) : 0;

  const nextCosts = selectedNode && selectedState !== 'maxed' && selectedState !== 'locked'
    ? getNextLevelCost(
        selectedNode.baseCost,
        selectedNode.branchTier,
        tagOf(selectedNode.effectKind),
        selectedLevel,
      )
    : [];

  const canPurchase =
    selectedNode != null &&
    selectedState !== 'locked' &&
    selectedState !== 'maxed' &&
    nextCosts.every(c => getAmount(c.type) >= c.amount);

  const nodeArr = Array.from(nodes.values()) as NodeRow[];

  // ── Empty state ───────────────────────────────────────────────────────────

  if (nodeArr.length === 0) {
    return (
      <SafeAreaView style={styles.container}>
        <Header navigation={navigation} />
        <ResourceBar />
        <View style={styles.emptyBox}>
          <Text style={styles.emptyText}>Loading skill tree…</Text>
        </View>
      </SafeAreaView>
    );
  }

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <SafeAreaView style={styles.container} edges={['top']}>
      <Header navigation={navigation} />
      <ResourceBar />

      <ScrollView
        style={styles.canvas}
        contentContainerStyle={{ width: canvasW, height: canvasH }}
        showsVerticalScrollIndicator={false}
      >
        {/* Connector lines */}
        {nodeArr.map(node => {
          const linkId = node.visualPrerequisiteNodeId ?? node.prerequisiteNodeId;
          if (linkId == null) return null;
          const from = layout.get(linkId);
          const to   = layout.get(node.id);
          if (!from || !to) return null;

          const fx = from.x + NODE_W / 2, fy = from.y + NODE_H / 2;
          const tx = to.x   + NODE_W / 2, ty = to.y   + NODE_H / 2;
          const dx = tx - fx, dy = ty - fy;
          const len = Math.sqrt(dx * dx + dy * dy);
          const ang = Math.atan2(dy, dx) * (180 / Math.PI);

          const parentNode  = nodes.get(linkId) as NodeRow | undefined;
          const parentState = parentNode ? getState(parentNode) : 'locked';
          const active = parentState === 'unlocked' || parentState === 'maxed';

          return (
            <View
              key={`line-${node.id}`}
              pointerEvents="none"
              style={{
                position: 'absolute',
                left: (fx + tx) / 2 - len / 2,
                top:  (fy + ty) / 2 - 1,
                width: len,
                height: 2,
                backgroundColor: active ? 'rgba(200,168,75,0.4)' : '#252525',
                transform: [{ rotate: `${ang}deg` }],
              }}
            />
          );
        })}

        {/* Nodes */}
        {nodeArr.map(node => {
          const pos = layout.get(node.id);
          if (!pos) return null;
          const state      = getState(node);
          const nodeLevel  = getNodeLevel(node.id);
          const isSelected = selectedId === node.id;
          const kind       = tagOf(node.effectKind);
          const effectiveMax = getEffectiveMaxLevel(node.baseMaxLevel, node.branchTier, unlockedTiers);

          const borderCol =
            isSelected         ? '#ffffff' :
            state === 'maxed'  ? '#e8c86a' :
            state === 'unlocked' ? '#c8a84b' :
            state === 'available' ? '#3d3d3d' : '#1e1e1e';

          const bgCol =
            state === 'maxed'     ? 'rgba(232,200,106,0.2)' :
            state === 'unlocked'  ? 'rgba(200,168,75,0.12)' :
            state === 'available' ? 'rgba(255,255,255,0.04)' : '#111111';

          return (
            <TouchableOpacity
              key={String(node.id)}
              activeOpacity={0.75}
              onPress={() => setSelectedId(selectedId === node.id ? null : node.id)}
              style={[
                styles.node,
                { left: pos.x, top: pos.y, backgroundColor: bgCol, borderColor: borderCol,
                  borderWidth: isSelected ? 2 : 1 },
              ]}
            >
              <Text style={[styles.nodeIcon, { color: state === 'locked' ? '#2a2a2a' : '#c8a84b' }]}>
                {effectIcon(kind)}
              </Text>
              <Text
                style={[styles.nodeName, { color: state === 'locked' ? '#333333' : '#e0e0e0' }]}
                numberOfLines={2}
              >
                {node.name}
              </Text>
              {effectiveMax > 1 && (
                <Text style={[styles.nodeLevel, { color: state === 'locked' ? '#2a2a2a' : '#c8a84b' }]}>
                  {nodeLevel}/{effectiveMax}
                </Text>
              )}
              {state === 'maxed' && (
                <View style={styles.maxBadge}><Text style={styles.maxBadgeText}>MAX</Text></View>
              )}
              {state === 'available' && <View style={styles.availableDot} />}
            </TouchableOpacity>
          );
        })}
      </ScrollView>

      {/* Detail panel */}
      {selectedNode && (
        <View style={styles.panel}>
          <View style={styles.panelTitleRow}>
            <Text style={styles.panelName}>{selectedNode.name}</Text>
            <Text style={styles.panelLevel}>
              Lv {selectedLevel} / {getEffectiveMaxLevel(selectedNode.baseMaxLevel, selectedNode.branchTier, unlockedTiers)}
            </Text>
          </View>

          <Text style={styles.panelTooltip}>{selectedNode.tooltip}</Text>
          <Text style={styles.panelEffect}>
            {effectIcon(tagOf(selectedNode.effectKind))}  {effectLabel(tagOf(selectedNode.effectKind), selectedNode.effectParam)}
          </Text>

          {/* Cost breakdown */}
          {nextCosts.length > 0 && (
            <View style={styles.costRow}>
              <Text style={styles.costLabel}>COST  </Text>
              {nextCosts.map((c, i) => {
                const have      = getAmount(c.type);
                const affordable = have >= c.amount;
                return (
                  <View key={i} style={[styles.costChip, !affordable && styles.costChipShort]}>
                    <Text style={styles.costIcon}>{RESOURCE_ICONS[c.type] ?? c.type}</Text>
                    <Text style={[styles.costAmount, !affordable && styles.costAmountShort]}>
                      {String(c.amount)}
                    </Text>
                  </View>
                );
              })}
            </View>
          )}

          <TouchableOpacity
            style={[styles.purchaseBtn, !canPurchase && styles.purchaseBtnDim]}
            disabled={!canPurchase}
            onPress={() => reducers.purchaseSkillTreeNode(selectedNode.id)}
          >
            <Text style={[styles.purchaseBtnText, !canPurchase && styles.purchaseBtnTextDim]}>
              {selectedState === 'maxed'   ? 'MAXED OUT' :
               selectedState === 'locked'  ? '🔒  LOCKED' :
               !canPurchase               ? 'NOT ENOUGH RESOURCES' :
               selectedLevel > 0          ? 'UPGRADE  +' : 'UNLOCK'}
            </Text>
          </TouchableOpacity>
        </View>
      )}
    </SafeAreaView>
  );
}

// ─── Header ───────────────────────────────────────────────────────────────────

function Header({ navigation }: { navigation: any }) {
  return (
    <View style={styles.header}>
      <TouchableOpacity onPress={() => navigation.goBack()} style={styles.back}>
        <Text style={styles.backText}>← Back</Text>
      </TouchableOpacity>
      <Text style={styles.title}>SKILL TREE</Text>
    </View>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#0f0f0f' },

  header: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#2e2e2e',
    gap: 12,
  },
  back:     { paddingVertical: 4 },
  backText: { color: '#c8a84b', fontSize: 14, fontWeight: '600' },
  title: {
    flex: 1,
    color: '#e0e0e0',
    fontSize: 15,
    fontWeight: '800',
    letterSpacing: 3,
  },

  canvas: { flex: 1 },

  node: {
    position: 'absolute',
    width: NODE_W,
    height: NODE_H,
    borderRadius: 10,
    alignItems: 'center',
    justifyContent: 'center',
    padding: 6,
    borderWidth: 1,
  },
  nodeIcon:  { fontSize: 16, marginBottom: 2 },
  nodeName:  { fontSize: 9, fontWeight: '700', textAlign: 'center', letterSpacing: 0.2, lineHeight: 12 },
  nodeLevel: { fontSize: 9, fontWeight: '600', marginTop: 2 },
  maxBadge:  {
    position: 'absolute', bottom: -5,
    backgroundColor: '#c8a84b', borderRadius: 3,
    paddingHorizontal: 4, paddingVertical: 1,
  },
  maxBadgeText:  { color: '#0f0f0f', fontSize: 7, fontWeight: '900', letterSpacing: 1 },
  availableDot:  {
    position: 'absolute', top: 5, right: 5,
    width: 6, height: 6, borderRadius: 3, backgroundColor: '#c8a84b',
  },

  emptyBox:  { flex: 1, alignItems: 'center', justifyContent: 'center' },
  emptyText: { color: '#6b6b6b', fontSize: 14 },

  panel: {
    backgroundColor: '#141414',
    borderTopWidth: 1,
    borderTopColor: '#2e2e2e',
    padding: 16,
    gap: 10,
  },
  panelTitleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  panelName:    { color: '#e0e0e0', fontSize: 16, fontWeight: '800', flex: 1 },
  panelLevel:   { color: '#6b6b6b', fontSize: 12, fontWeight: '600' },
  panelTooltip: { color: '#7a7a7a', fontSize: 13, lineHeight: 18 },
  panelEffect:  { color: '#c8a84b', fontSize: 12, fontWeight: '600' },

  costRow: {
    flexDirection: 'row',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: 6,
  },
  costLabel: { color: '#6b6b6b', fontSize: 10, fontWeight: '700', letterSpacing: 2 },
  costChip: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#1e1e1e',
    borderRadius: 6,
    borderWidth: 1,
    borderColor: '#2e2e2e',
    paddingHorizontal: 8,
    paddingVertical: 4,
    gap: 4,
  },
  costChipShort: { borderColor: '#c0392b' },
  costIcon:      { fontSize: 13 },
  costAmount:    { color: '#e0e0e0', fontSize: 12, fontWeight: '700' },
  costAmountShort: { color: '#e74c3c' },

  purchaseBtn: {
    backgroundColor: '#c8a84b',
    borderRadius: 6,
    paddingVertical: 13,
    alignItems: 'center',
  },
  purchaseBtnDim: {
    backgroundColor: 'transparent',
    borderWidth: 1,
    borderColor: '#2e2e2e',
  },
  purchaseBtnText:    { color: '#0f0f0f', fontSize: 13, fontWeight: '900', letterSpacing: 3 },
  purchaseBtnTextDim: { color: '#333333' },
});
