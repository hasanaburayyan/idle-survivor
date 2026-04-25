import React from 'react';
import { View, Text, StyleSheet, ScrollView } from 'react-native';
import { useResourceStore } from '../../stores';
import { ResourceType } from '../../spacetime/types';

const RESOURCE_COLORS: Record<ResourceType, string> = {
  [ResourceType.Food]:   '#27ae60',
  [ResourceType.Money]:  '#f1c40f',
  [ResourceType.Wood]:   '#8b5e3c',
  [ResourceType.Metal]:  '#7f8c8d',
  [ResourceType.Fabric]: '#9b59b6',
  [ResourceType.Parts]:  '#2980b9',
};

const RESOURCE_ICONS: Record<ResourceType, string> = {
  [ResourceType.Food]:   '🍖',
  [ResourceType.Money]:  '💰',
  [ResourceType.Wood]:   '🪵',
  [ResourceType.Metal]:  '⚙️',
  [ResourceType.Fabric]: '🧵',
  [ResourceType.Parts]:  '🔩',
};

const DISPLAY_ORDER: ResourceType[] = [
  ResourceType.Money,
  ResourceType.Food,
  ResourceType.Wood,
  ResourceType.Metal,
  ResourceType.Fabric,
  ResourceType.Parts,
];

function formatAmount(n: bigint | number | null | undefined): string {
  if (n == null) return '0';
  const num = Number(n);
  if (isNaN(num)) return '0';
  if (num >= 1_000_000) return `${(num / 1_000_000).toFixed(1)}M`;
  if (num >= 1_000)     return `${(num / 1_000).toFixed(1)}K`;
  return num.toString();
}

export default function ResourceBar() {
  const getAmount = useResourceStore(s => s.getAmount);

  return (
    <View style={styles.container}>
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={styles.scroll}
      >
        {DISPLAY_ORDER.map(type => (
          <View key={type} style={styles.chip}>
            <Text style={styles.icon}>{RESOURCE_ICONS[type]}</Text>
            <Text style={[styles.amount, { color: RESOURCE_COLORS[type] }]}>
              {formatAmount(getAmount(type))}
            </Text>
          </View>
        ))}
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    backgroundColor: '#1a1a1a',
    borderBottomWidth: 1,
    borderBottomColor: '#2e2e2e',
    paddingVertical: 8,
  },
  scroll: {
    paddingHorizontal: 12,
    gap: 8,
  },
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#252525',
    borderRadius: 6,
    paddingVertical: 5,
    paddingHorizontal: 10,
    gap: 5,
    borderWidth: 1,
    borderColor: '#2e2e2e',
  },
  icon: {
    fontSize: 14,
  },
  amount: {
    fontSize: 13,
    fontWeight: '700',
    letterSpacing: 0.5,
    minWidth: 32,
    textAlign: 'right',
  },
});
