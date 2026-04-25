import React from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  ScrollView,
  StyleSheet,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import type { ShelterScreenProps } from '../types/navigation';
import { usePlayerStore } from '../stores';
import { ActivityType, LocationType, LOCATION_LABELS, tagOf } from '../spacetime/types';
import { reducers } from '../spacetime/client';
import ResourceBar from '../components/ui/ResourceBar';
import ActivityCard from '../components/activity/ActivityCard';

const ALL_ACTIVITIES: ActivityType[] = [
  ActivityType.Scavenge,
  ActivityType.ChopWood,
  ActivityType.Mine,
  ActivityType.GatherFabric,
  ActivityType.Forage,
  ActivityType.Salvage,
];

const LOCATIONS: LocationType[] = [
  LocationType.Shelter,
  LocationType.GuildHall,
  LocationType.Wastes,
];

const LOCATION_ICONS: Record<LocationType, string> = {
  [LocationType.Shelter]:   '🏠',
  [LocationType.GuildHall]: '⚔️',
  [LocationType.Wastes]:    '☠️',
};

const NAV_ITEMS = [
  { screen: 'Character', icon: '👤', label: 'Character' },
  { screen: 'Gear',      icon: '🛡️', label: 'Gear' },
  { screen: 'SkillTree', icon: '🌳', label: 'Skills' },
  { screen: 'Structures',icon: '🏗️', label: 'Build' },
  { screen: 'Social',    icon: '👥', label: 'Social' },
] as const;

export default function ShelterScreen({ navigation }: ShelterScreenProps) {
  const player    = usePlayerStore(s => s.player);
  const level     = usePlayerStore(s => s.level);
  const xpPercent = usePlayerStore(s => s.xpPercent());

  // player.location is a tagged union { tag: 'Shelter' | 'GuildHall' | 'Wastes' }
  const currentLocation = (tagOf(player?.location) || LocationType.Shelter) as LocationType;

  return (
    <SafeAreaView style={styles.container} edges={['top']}>

      {/* ── Top bar: player info + location ── */}
      <View style={styles.topBar}>
        <View style={styles.playerInfo}>
          <Text style={styles.playerName}>{player?.displayName ?? '…'}</Text>
          <View style={styles.xpRow}>
            <Text style={styles.levelText}>LVL {level?.level ?? 0}</Text>
            <View style={styles.xpBarBg}>
              <View style={[styles.xpBarFill, { width: `${xpPercent * 100}%` }]} />
            </View>
            <Text style={styles.xpText}>
              {level?.xp != null ? String(level.xp) : '0'} XP
            </Text>
          </View>
        </View>

        {/* Debug buttons */}
        <TouchableOpacity
          style={styles.debugButton}
          onPress={reducers.debugGrantResources}
          activeOpacity={0.7}
        >
          <Text style={styles.debugIcon}>💰</Text>
          <Text style={styles.debugLabel}>RES</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={styles.debugButton}
          onPress={reducers.debugLevelUp}
          activeOpacity={0.7}
        >
          <Text style={styles.debugIcon}>⬆️</Text>
          <Text style={styles.debugLabel}>LVL</Text>
        </TouchableOpacity>

        {/* Zombie kill button */}
        <TouchableOpacity
          style={styles.killButton}
          onPress={reducers.killZombie}
          activeOpacity={0.7}
        >
          <Text style={styles.killIcon}>🧟</Text>
          <Text style={styles.killLabel}>KILL</Text>
        </TouchableOpacity>
      </View>

      {/* ── Resource counters ── */}
      <ResourceBar />

      {/* ── Location selector ── */}
      <View style={styles.locationRow}>
        {LOCATIONS.map(loc => (
          <TouchableOpacity
            key={loc}
            style={[
              styles.locationBtn,
              currentLocation === loc && styles.locationBtnActive,
            ]}
            onPress={() => reducers.travel(loc)}
          >
            <Text style={styles.locationIcon}>{LOCATION_ICONS[loc]}</Text>
            <Text
              style={[
                styles.locationLabel,
                currentLocation === loc && styles.locationLabelActive,
              ]}
            >
              {LOCATION_LABELS[loc]}
            </Text>
          </TouchableOpacity>
        ))}
      </View>

      {/* ── Activities ── */}
      <ScrollView
        style={styles.activityList}
        contentContainerStyle={styles.activityContent}
        showsVerticalScrollIndicator={false}
      >
        <Text style={styles.sectionTitle}>ACTIVITIES</Text>
        {ALL_ACTIVITIES.map(type => (
          <ActivityCard key={type} type={type} />
        ))}
        <View style={styles.listFooter} />
      </ScrollView>

      {/* ── Bottom navigation ── */}
      <View style={styles.bottomNav}>
        {NAV_ITEMS.map(item => (
          <TouchableOpacity
            key={item.screen}
            style={styles.navItem}
            onPress={() => navigation.navigate(item.screen as any)}
          >
            <Text style={styles.navIcon}>{item.icon}</Text>
            <Text style={styles.navLabel}>{item.label}</Text>
          </TouchableOpacity>
        ))}
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0f0f0f',
  },

  // ── Top bar ──────────────────────────────────────────────────────────────
  topBar: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#2e2e2e',
    gap: 12,
  },
  playerInfo: {
    flex: 1,
  },
  playerName: {
    color: '#e0e0e0',
    fontSize: 18,
    fontWeight: '800',
    letterSpacing: 1,
    marginBottom: 4,
  },
  xpRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  levelText: {
    color: '#c8a84b',
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1,
    width: 44,
  },
  xpBarBg: {
    flex: 1,
    height: 4,
    backgroundColor: '#252525',
    borderRadius: 2,
    overflow: 'hidden',
  },
  xpBarFill: {
    height: '100%',
    backgroundColor: '#c8a84b',
    borderRadius: 2,
  },
  xpText: {
    color: '#6b6b6b',
    fontSize: 10,
    width: 52,
    textAlign: 'right',
  },
  debugButton: {
    alignItems: 'center',
    backgroundColor: '#1e1e1e',
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#2a5a2a',
    paddingVertical: 8,
    paddingHorizontal: 10,
    gap: 2,
  },
  debugIcon:  { fontSize: 16 },
  debugLabel: {
    color: '#4a9a4a',
    fontSize: 9,
    fontWeight: '800',
    letterSpacing: 2,
  },
  killButton: {
    alignItems: 'center',
    backgroundColor: '#1e1e1e',
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#c0392b',
    paddingVertical: 8,
    paddingHorizontal: 14,
    gap: 2,
  },
  killIcon:  { fontSize: 22 },
  killLabel: {
    color: '#e74c3c',
    fontSize: 9,
    fontWeight: '800',
    letterSpacing: 2,
  },

  // ── Location row ─────────────────────────────────────────────────────────
  locationRow: {
    flexDirection: 'row',
    paddingHorizontal: 12,
    paddingVertical: 8,
    gap: 8,
    borderBottomWidth: 1,
    borderBottomColor: '#2e2e2e',
  },
  locationBtn: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    paddingVertical: 8,
    borderRadius: 6,
    borderWidth: 1,
    borderColor: '#2e2e2e',
    backgroundColor: '#1a1a1a',
  },
  locationBtnActive: {
    borderColor: '#c8a84b',
    backgroundColor: 'rgba(200,168,75,0.1)',
  },
  locationIcon: { fontSize: 14 },
  locationLabel: {
    color: '#6b6b6b',
    fontSize: 12,
    fontWeight: '600',
  },
  locationLabelActive: { color: '#c8a84b' },

  // ── Activities ───────────────────────────────────────────────────────────
  activityList: { flex: 1 },
  activityContent: { padding: 12 },
  sectionTitle: {
    color: '#6b6b6b',
    fontSize: 10,
    fontWeight: '700',
    letterSpacing: 3,
    marginBottom: 10,
  },
  listFooter: { height: 20 },

  // ── Bottom nav ───────────────────────────────────────────────────────────
  bottomNav: {
    flexDirection: 'row',
    borderTopWidth: 1,
    borderTopColor: '#2e2e2e',
    backgroundColor: '#1a1a1a',
  },
  navItem: {
    flex: 1,
    alignItems: 'center',
    paddingVertical: 10,
    gap: 3,
  },
  navIcon:  { fontSize: 20 },
  navLabel: {
    color: '#6b6b6b',
    fontSize: 9,
    fontWeight: '600',
    letterSpacing: 1,
  },
});
