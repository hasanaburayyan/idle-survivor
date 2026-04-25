/**
 * SpacetimeDB connection manager.
 *
 * ─── SETUP (one-time) ────────────────────────────────────────────────────────
 * Before this file will connect to a real database, run:
 *
 *   npm run generate
 *
 * That generates src/spacetime/generated/ from the C# backend.
 * ─────────────────────────────────────────────────────────────────────────────
 */

import { DbConnection } from './generated';

import AsyncStorage from '@react-native-async-storage/async-storage';
import { useEffect, useReducer } from 'react';

import { tagged } from './types';

import { usePlayerStore }    from '../stores/playerStore';
import { useResourceStore }  from '../stores/resourceStore';
import { useActivityStore }  from '../stores/activityStore';
import { useSkillStore }     from '../stores/skillStore';
import { useEquipmentStore } from '../stores/equipmentStore';

// ─── Config ───────────────────────────────────────────────────────────────────

const SPACETIMEDB_URI = 'wss://maincloud.spacetimedb.com';
const MODULE_NAME     = 'idle-survivor';
const TOKEN_KEY       = 'stdb_auth_token';

// ─── Connection state ─────────────────────────────────────────────────────────

export type ConnectionStatus =
  | 'disconnected'
  | 'connecting'
  | 'connected'
  | 'subscribed'
  | 'error';

export interface ConnectionState {
  status:   ConnectionStatus;
  identity: string | null;
  token:    string | null;
  error:    string | null;
}

let _conn:     DbConnection | null = null;
let _status:   ConnectionStatus    = 'disconnected';
let _identity: string | null       = null;
let _token:    string | null       = null;
let _error:    string | null       = null;

const _listeners = new Set<() => void>();
const notify = () => _listeners.forEach(fn => fn());

// ─── Token persistence ────────────────────────────────────────────────────────

export const loadSavedToken = () =>
  AsyncStorage.getItem(TOKEN_KEY).catch(() => null);

export const saveToken = (t: string) =>
  AsyncStorage.setItem(TOKEN_KEY, t);

export const clearToken = () =>
  AsyncStorage.removeItem(TOKEN_KEY);

// ─── Connect ──────────────────────────────────────────────────────────────────

export async function initConnection(token?: string | null): Promise<void> {
  _status = 'connecting';
  notify();

  const savedToken = token ?? (await loadSavedToken());

  const builder = DbConnection.builder()
    .withUri(SPACETIMEDB_URI)
    .withDatabaseName(MODULE_NAME)
    .onConnect(async (conn, identity, newToken) => {
      _conn     = conn;
      _identity = identity.toHexString();
      _token    = newToken;
      _status   = 'connected';
      _error    = null;
      await saveToken(newToken);
      notify();
      wireTableCallbacks(conn);
      conn.subscriptionBuilder()
        .onApplied(() => {
          // Fires on V8 (web). On Hermes (iOS) this is silently swallowed due to
          // private-field access inside promise chains — see fallback below.
          _status = 'subscribed';
          notify();
        })
        .onError((ctx: any) => {
          console.warn('[SpacetimeDB] Subscription error:', ctx);
        })
        .subscribeToAllTables();

      // Hermes fallback: onApplied never fires on iOS due to an SDK v2.1 bug
      // (private fields accessed inside .then() chains lose context on Hermes).
      // After 1.5 s the connection is stable enough to decide new-vs-returning user.
      setTimeout(() => {
        if (_status === 'connected') {
          _status = 'subscribed';
          notify();
        }
      }, 1500);
    })
    .onDisconnect((_ctx, err) => {
      _conn   = null;
      _status = 'disconnected';
      _error  = err ? err.message : null;
      notify();
    })
    .onConnectError(async (_ctx, err) => {
      _conn = null;
      // A 401 means the saved token is stale — clear it and retry anonymously.
      const isAuthError =
        err.message.includes('401') ||
        err.message.toLowerCase().includes('unauthorized');
      if (isAuthError && savedToken) {
        console.warn('[SpacetimeDB] Stale token, clearing and retrying…');
        await clearToken();
        initConnection(null);
        return;
      }
      _status = 'error';
      _error  = err.message;
      notify();
    });

  if (savedToken) builder.withToken(savedToken);
  builder.build();
}

export function disconnect(): void {
  _conn?.disconnect?.();
  _conn   = null;
  _status = 'disconnected';
  _identity = null;
  notify();
}

export const getConnectionStatus = (): ConnectionState => ({
  status:   _status,
  identity: _identity,
  token:    _token,
  error:    _error,
});

export const isConnected = () => _status === 'connected' || _status === 'subscribed';

// ─── React hook ───────────────────────────────────────────────────────────────

export function useConnectionStatus(): ConnectionState {
  const [, rerender] = useReducer(x => x + 1, 0);
  useEffect(() => {
    _listeners.add(rerender);
    return () => { _listeners.delete(rerender); };
  }, []);
  return getConnectionStatus();
}

// ─── Table → store wiring ─────────────────────────────────────────────────────

function wireTableCallbacks(conn: DbConnection) {
  const {
    setPlayer, setLevel, setStat, setUpgrade,
    addKillLoot, removeKillLoot,
  } = usePlayerStore.getState();
  const { upsertTracker } = useResourceStore.getState();
  const { upsertActivity, upsertActiveTask, removeActiveTask } =
    useActivityStore.getState();
  const {
    upsertNode, upsertSkillDef, upsertNodeUnlock, removeNodeUnlock,
    addPlayerSkill, removePlayerSkill,
  } = useSkillStore.getState();
  const {
    upsertGearDef, upsertSetBonus, upsertRecipe,
    addInventoryItem, removeInventoryItem,
    upsertEquipped, removeEquipped,
    setChest, addChestItem, removeChestItem,
  } = useEquipmentStore.getState();

  // Player
  conn.db.Player.onInsert((_ctx, row) => setPlayer(row as any));
  conn.db.Player.onUpdate((_ctx, _old, row) => setPlayer(row as any));
  conn.db.Player.onDelete(() => setPlayer(null));

  // Player level
  conn.db.PlayerLevel.onInsert((_ctx, row) => setLevel(row as any));
  conn.db.PlayerLevel.onUpdate((_ctx, _old, row) => setLevel(row as any));

  // Stats
  conn.db.PlayerStat.onInsert((_ctx, row) => setStat(row as any));
  conn.db.PlayerStat.onUpdate((_ctx, _old, row) => setStat(row as any));

  // Upgrades
  conn.db.PlayerUpgrade.onInsert((_ctx, row) => setUpgrade(row as any));
  conn.db.PlayerUpgrade.onUpdate((_ctx, _old, row) => setUpgrade(row as any));

  // Resources
  conn.db.ResourceTracker.onInsert((_ctx, row) => upsertTracker(row as any));
  conn.db.ResourceTracker.onUpdate((_ctx, _old, row) => upsertTracker(row as any));

  // Activities
  conn.db.Activity.onInsert((_ctx, row) => upsertActivity(row as any));
  conn.db.Activity.onUpdate((_ctx, _old, row) => upsertActivity(row as any));

  // Active tasks
  conn.db.ActiveTask.onInsert((_ctx, row) => upsertActiveTask(row as any));
  conn.db.ActiveTask.onDelete((_ctx, row) => removeActiveTask((row as any).id));

  // Kill loot
  conn.db.KillLoot.onInsert((_ctx, row) => addKillLoot(row as any));
  conn.db.KillLoot.onDelete((_ctx, row) => removeKillLoot((row as any).id));

  // Skill tree — static definitions (same for all players)
  conn.db.SkillTreeNode.onInsert((_ctx, row) => upsertNode(row as any));
  conn.db.SkillTreeNode.onUpdate((_ctx, _old, row) => upsertNode(row as any));
  conn.db.SkillDefinition.onInsert((_ctx, row) => upsertSkillDef(row as any));
  conn.db.SkillDefinition.onUpdate((_ctx, _old, row) => upsertSkillDef(row as any));

  // Skill tree — per-player unlocks
  conn.db.PlayerSkillTreeUnlock.onInsert((_ctx, row) => upsertNodeUnlock(row as any));
  conn.db.PlayerSkillTreeUnlock.onUpdate((_ctx, _old, row) => upsertNodeUnlock(row as any));
  conn.db.PlayerSkillTreeUnlock.onDelete((_ctx, row) => removeNodeUnlock((row as any).id));
  conn.db.PlayerSkill.onInsert((_ctx, row) => addPlayerSkill(row as any));
  conn.db.PlayerSkill.onDelete((_ctx, row) => removePlayerSkill((row as any).id));

  // Equipment — static definitions
  conn.db.GearDefinition.onInsert((_ctx, row) => upsertGearDef(row as any));
  conn.db.GearDefinition.onUpdate((_ctx, _old, row) => upsertGearDef(row as any));
  conn.db.GearSetBonus.onInsert((_ctx, row) => upsertSetBonus(row as any));
  conn.db.GearSetBonus.onUpdate((_ctx, _old, row) => upsertSetBonus(row as any));
  conn.db.CraftingRecipe.onInsert((_ctx, row) => upsertRecipe(row as any));
  conn.db.CraftingRecipe.onUpdate((_ctx, _old, row) => upsertRecipe(row as any));

  // Equipment — per-player items
  conn.db.InventoryItem.onInsert((_ctx, row) => addInventoryItem(row as any));
  conn.db.InventoryItem.onDelete((_ctx, row) => removeInventoryItem((row as any).id));
  conn.db.EquippedGear.onInsert((_ctx, row) => upsertEquipped(row as any));
  conn.db.EquippedGear.onUpdate((_ctx, _old, row) => upsertEquipped(row as any));
  conn.db.EquippedGear.onDelete((_ctx, row) => removeEquipped((row as any).id));
  conn.db.StorageChest.onInsert((_ctx, row) => setChest(row as any));
  conn.db.StorageChest.onUpdate((_ctx, _old, row) => setChest(row as any));
  conn.db.ChestItem.onInsert((_ctx, row) => addChestItem(row as any));
  conn.db.ChestItem.onDelete((_ctx, row) => removeChestItem((row as any).id));
}

// ─── Reducer guard ────────────────────────────────────────────────────────────

function guard(name: string): boolean {
  if (!_conn || (_status !== 'connected' && _status !== 'subscribed')) {
    console.warn(`[SpacetimeDB] Cannot call '${name}': not connected`);
    return false;
  }
  return true;
}

// ─── Reducer calls ────────────────────────────────────────────────────────────
// Each reducer's param object matches the generated schema field names.

export const reducers = {
  createPlayer: () =>
    guard('createPlayer') && _conn!.reducers.createPlayer({}),

  setName: (name: string) =>
    guard('setName') && _conn!.reducers.setName({ name }),

  killZombie: () =>
    guard('killZombie') && _conn!.reducers.killZombie({}),

  ackKillLoot: (lootId: bigint) =>
    guard('ackKillLoot') && _conn!.reducers.ackKillLoot({ lootId }),

  startActivity: (type: string) =>
    guard('startActivity') && _conn!.reducers.startActivity({ type: tagged(type) as any }),

  upgradeActivity: (type: string) =>
    guard('upgradeActivity') && _conn!.reducers.upgradeActivity({ type: tagged(type) as any }),

  purchaseSkill: (skillDefinitionId: bigint) =>
    guard('purchaseSkill') && _conn!.reducers.purchaseSkill({ skillDefinitionId } as any),

  purchaseSkillTreeNode: (nodeId: bigint) =>
    guard('purchaseSkillTreeNode') && _conn!.reducers.purchaseSkillTreeNode({ nodeId }),

  debugLevelUp: () =>
    guard('debugLevelUp') && _conn!.reducers.debugLevelUp({}),

  debugGrantResources: () =>
    guard('debugGrantResources') && _conn!.reducers.debugGrantResources({}),

  craftGear: (recipeId: bigint) =>
    guard('craftGear') && _conn!.reducers.craftGear({ recipeId }),

  equipGear: (inventoryItemId: bigint) =>
    guard('equipGear') && _conn!.reducers.equipGear({ inventoryItemId }),

  unequipGear: (slot: string) =>
    guard('unequipGear') && _conn!.reducers.unequipGear({ slot: tagged(slot) as any }),

  transferToChest: (inventoryItemId: bigint, chestId: bigint) =>
    guard('transferToChest') && _conn!.reducers.transferToChest({ inventoryItemId, chestId }),

  transferFromChest: (chestItemId: bigint) =>
    guard('transferFromChest') && _conn!.reducers.transferFromChest({ chestItemId }),

  buildStructure: (definitionId: bigint, posX: number, posY: number) =>
    guard('buildStructure') && _conn!.reducers.buildStructure({ definitionId, posX, posY }),

  craftRecipe: (recipeId: bigint) =>
    guard('craftRecipe') && _conn!.reducers.craftRecipe({ recipeId }),

  purchaseUpgrade: (type: string) =>
    guard('purchaseUpgrade') && _conn!.reducers.purchaseUpgrade({ type: tagged(type) as any }),

  travel: (destination: string) =>
    guard('travel') && _conn!.reducers.travel({ destination: tagged(destination) as any }),

  sendFriendRequest: (targetPlayerName: string) =>
    guard('sendFriendRequest') && _conn!.reducers.sendFriendRequest({ targetPlayerName }),

  acceptFriendRequest: (requestId: bigint) =>
    guard('acceptFriendRequest') && _conn!.reducers.acceptFriendRequest({ requestId }),

  createGuild: (name: string) =>
    guard('createGuild') && _conn!.reducers.createGuild({ name }),

  createParty: () =>
    guard('createParty') && _conn!.reducers.createParty({}),

  inviteToParty: (targetIdentity: string) =>
    guard('inviteToParty') && _conn!.reducers.inviteToParty({ targetIdentity } as any),

  startAdventure: () =>
    guard('startAdventure') && _conn!.reducers.startAdventure({}),

  startRiskyBusiness: () =>
    guard('startRiskyBusiness') && _conn!.reducers.startRiskyBusiness({}),

  exitRiskyBusiness: () =>
    guard('exitRiskyBusiness') && _conn!.reducers.leaveRiskyBusiness({}),
};
