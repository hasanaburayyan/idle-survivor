import React, { useEffect, useRef, useState } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
  Animated,
} from 'react-native';
import {
  ActivityType,
  ACTIVITY_LABELS,
  ResourceType,
  RESOURCE_LABELS,
  tagOf,
} from '../../spacetime/types';
import { useActivityStore, useResourceStore } from '../../stores';
import { reducers } from '../../spacetime/client';

const ACTIVITY_ICONS: Record<ActivityType, string> = {
  [ActivityType.Scavenge]:     '🔍',
  [ActivityType.ChopWood]:     '🪓',
  [ActivityType.Mine]:         '⛏️',
  [ActivityType.GatherFabric]: '🧵',
  [ActivityType.Forage]:       '🍃',
  [ActivityType.Salvage]:      '🔧',
};

const ACTIVITY_RESOURCE: Record<ActivityType, ResourceType> = {
  [ActivityType.Scavenge]:     ResourceType.Money,
  [ActivityType.ChopWood]:     ResourceType.Wood,
  [ActivityType.Mine]:         ResourceType.Metal,
  [ActivityType.GatherFabric]: ResourceType.Fabric,
  [ActivityType.Forage]:       ResourceType.Food,
  [ActivityType.Salvage]:      ResourceType.Parts,
};

interface Props {
  type: ActivityType;
}

export default function ActivityCard({ type }: Props) {
  const activity    = useActivityStore(s => s.getActivity(type));
  const isRunning   = useActivityStore(s => s.isRunning(type));
  const taskProgress = useActivityStore(s => s.taskProgress);
  const canAfford   = useResourceStore(s => s.canAfford);

  const progressAnim = useRef(new Animated.Value(0)).current;
  const [now, setNow] = useState(Date.now());

  // Poll current time for smooth progress bar
  useEffect(() => {
    if (!isRunning) return;
    const id = setInterval(() => setNow(Date.now()), 100);
    return () => clearInterval(id);
  }, [isRunning]);

  const progress = taskProgress(type, now);

  useEffect(() => {
    Animated.timing(progressAnim, {
      toValue:         progress,
      duration:        100,
      useNativeDriver: false,
    }).start();
  }, [progress, progressAnim]);

  if (!activity) {
    return (
      <View style={[styles.card, styles.lockedCard]}>
        <Text style={styles.lockedIcon}>🔒</Text>
        <Text style={styles.lockedLabel}>{ACTIVITY_LABELS[type]}</Text>
      </View>
    );
  }

  // activity.cost is Array<{ type: { tag: string }, amount: bigint }>
  const costs = activity.cost ?? [];
  const canUpgrade = canAfford(costs);
  const resourceType = ACTIVITY_RESOURCE[type];

  return (
    <View style={styles.card}>
      {/* Header */}
      <View style={styles.header}>
        <Text style={styles.icon}>{ACTIVITY_ICONS[type]}</Text>
        <View style={styles.titleGroup}>
          <Text style={styles.title}>{ACTIVITY_LABELS[type]}</Text>
          <Text style={styles.subtitle}>LVL {activity.level}</Text>
        </View>
        <Text style={styles.yieldLabel}>+{RESOURCE_LABELS[resourceType]}</Text>
      </View>

      {/* Progress bar */}
      <View style={styles.progressBg}>
        <Animated.View
          style={[
            styles.progressFill,
            {
              width: progressAnim.interpolate({
                inputRange:  [0, 1],
                outputRange: ['0%', '100%'],
              }),
            },
          ]}
        />
        {isRunning && (
          <Text style={styles.progressLabel}>
            {Math.round(progress * 100)}%
          </Text>
        )}
      </View>

      {/* Actions */}
      <View style={styles.actions}>
        <TouchableOpacity
          style={[styles.btn, styles.startBtn, isRunning && styles.btnDisabled]}
          disabled={isRunning}
          onPress={() => reducers.startActivity(type)}
        >
          <Text style={[styles.btnText, isRunning && styles.btnTextMuted]}>
            {isRunning ? 'RUNNING' : 'START'}
          </Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.btn, styles.upgradeBtn, !canUpgrade && styles.btnDisabled]}
          disabled={!canUpgrade}
          onPress={() => reducers.upgradeActivity(type)}
        >
          <Text style={[styles.btnText, styles.upgradeBtnText, !canUpgrade && styles.btnTextMuted]}>
            UPGRADE
          </Text>
        </TouchableOpacity>
      </View>

      {/* Upgrade costs */}
      {costs.length > 0 && (
        <View style={styles.costs}>
          {costs.map((cost, i) => (
            <Text key={i} style={styles.costText}>
              {RESOURCE_LABELS[tagOf(cost.type) as ResourceType] ?? tagOf(cost.type)}:{' '}
              {cost.amount != null ? String(cost.amount) : '0'}
            </Text>
          ))}
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: '#1e1e1e',
    borderRadius: 8,
    padding: 12,
    borderWidth: 1,
    borderColor: '#2e2e2e',
    marginBottom: 8,
  },
  lockedCard: {
    opacity: 0.4,
    alignItems: 'center',
    flexDirection: 'row',
    gap: 10,
    paddingVertical: 16,
  },
  lockedIcon:  { fontSize: 20 },
  lockedLabel: { color: '#6b6b6b', fontSize: 14, fontWeight: '600' },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 10,
    gap: 10,
  },
  icon:       { fontSize: 24 },
  titleGroup: { flex: 1 },
  title:      { color: '#e0e0e0', fontSize: 15, fontWeight: '700' },
  subtitle:   { color: '#6b6b6b', fontSize: 11, fontWeight: '500', letterSpacing: 1 },
  yieldLabel: { color: '#c8a84b', fontSize: 11, fontWeight: '600' },
  progressBg: {
    height: 6,
    backgroundColor: '#252525',
    borderRadius: 3,
    marginBottom: 10,
    overflow: 'hidden',
    justifyContent: 'center',
  },
  progressFill: {
    height: '100%',
    backgroundColor: '#c8a84b',
    borderRadius: 3,
  },
  progressLabel: {
    position: 'absolute',
    right: 4,
    fontSize: 9,
    color: '#0f0f0f',
    fontWeight: '700',
  },
  actions: {
    flexDirection: 'row',
    gap: 8,
  },
  btn: {
    flex: 1,
    paddingVertical: 8,
    alignItems: 'center',
    borderRadius: 4,
    borderWidth: 1,
  },
  startBtn: {
    borderColor: '#c8a84b',
    backgroundColor: 'rgba(200,168,75,0.1)',
  },
  upgradeBtn: {
    borderColor: '#27ae60',
    backgroundColor: 'rgba(39,174,96,0.1)',
  },
  btnDisabled:    { opacity: 0.35 },
  btnText:        { color: '#c8a84b', fontSize: 11, fontWeight: '800', letterSpacing: 2 },
  upgradeBtnText: { color: '#27ae60' },
  btnTextMuted:   { opacity: 0.5 },
  costs: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 6,
    marginTop: 8,
  },
  costText: {
    color: '#6b6b6b',
    fontSize: 10,
    backgroundColor: '#252525',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 3,
  },
});
