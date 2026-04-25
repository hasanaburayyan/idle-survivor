import { create } from 'zustand';
import { tagOf } from '../spacetime/types';

// Row shapes inferred from generated schema (camelCase fields)
type TaggedType = { tag: string };

type GuildRow = {
  id: bigint;
  founderId: any;
  name: string;
  createdAt: any;
  [k: string]: any;
};

type GuildMemberRow = {
  id: bigint;
  guildId: bigint;
  playerId: any;  // Identity
  role: TaggedType;
  joinedAt: any;
  [k: string]: any;
};

type GuildResourceTrackerRow = {
  id: bigint;
  guildId: bigint;
  type: TaggedType;
  amount: bigint;
  [k: string]: any;
};

type GuildInviteRow = {
  id: bigint;
  guildId: bigint;
  inviterId: any;
  inviteeId: any;
  createdAt: any;
  [k: string]: any;
};

type GuildJoinRequestRow = {
  id: bigint;
  guildId: bigint;
  requesterId: any;
  createdAt: any;
  [k: string]: any;
};

type PartyRow = {
  id: bigint;
  leaderId: any;
  createdAt: any;
  [k: string]: any;
};

type PartyMemberRow = {
  id: bigint;
  partyId: bigint;
  playerId: any;
  joinedAt: any;
  [k: string]: any;
};

type PartyInviteRow = {
  id: bigint;
  partyId: bigint;
  inviterId: any;
  inviteeId: any;
  createdAt: any;
  [k: string]: any;
};

type FriendRequestRow = {
  id: bigint;
  senderId: any;
  receiverId: any;
  createdAt: any;
  [k: string]: any;
};

type FriendshipRow = {
  id: bigint;
  playerA: any;
  playerB: any;
  createdAt: any;
  [k: string]: any;
};

type PlayerRow = {
  identity: any;
  displayName: string;
  [k: string]: any;
};

interface SocialState {
  // Guild
  guild:               GuildRow | null;
  guildMembers:        Map<string, GuildMemberRow>;     // hex identity → member
  guildResources:      Map<string, bigint>;             // resource tag → amount
  pendingGuildInvites: GuildInviteRow[];
  pendingGuildRequests:GuildJoinRequestRow[];

  // Party
  party:               PartyRow | null;
  partyMembers:        Map<string, PartyMemberRow>;     // hex identity → member
  pendingPartyInvites: PartyInviteRow[];

  // Friends
  friends:                 Map<string, FriendshipRow>;  // id string → friendship
  incomingFriendRequests:  FriendRequestRow[];
  outgoingFriendRequests:  FriendRequestRow[];

  // Player lookup cache
  playerCache: Map<string, PlayerRow>;

  // Guild callbacks
  setGuild:           (g: GuildRow | null)        => void;
  upsertGuildMember:  (m: GuildMemberRow)         => void;
  removeGuildMember:  (id: bigint)                => void;
  upsertGuildResource:(t: GuildResourceTrackerRow)=> void;
  addGuildInvite:     (i: GuildInviteRow)         => void;
  removeGuildInvite:  (id: bigint)                => void;
  addGuildRequest:    (r: GuildJoinRequestRow)    => void;
  removeGuildRequest: (id: bigint)                => void;

  // Party callbacks
  setParty:           (p: PartyRow | null)        => void;
  upsertPartyMember:  (m: PartyMemberRow)         => void;
  removePartyMember:  (id: bigint)                => void;
  addPartyInvite:     (i: PartyInviteRow)         => void;
  removePartyInvite:  (id: bigint)                => void;

  // Friend callbacks
  addFriendship:      (f: FriendshipRow)          => void;
  removeFriendship:   (id: bigint)                => void;
  addFriendRequest:   (r: FriendRequestRow, myIdentityHex: string) => void;
  removeFriendRequest:(id: bigint)                => void;

  // Player cache
  cachePlayer: (p: PlayerRow) => void;

  // Helpers
  isFriendsWith: (identityHex: string) => boolean;
  inGuild:       ()                    => boolean;
  inParty:       ()                    => boolean;
}

export const useSocialStore = create<SocialState>((set, get) => ({
  guild:                null,
  guildMembers:         new Map(),
  guildResources:       new Map(),
  pendingGuildInvites:  [],
  pendingGuildRequests: [],
  party:                null,
  partyMembers:         new Map(),
  pendingPartyInvites:  [],
  friends:              new Map(),
  incomingFriendRequests: [],
  outgoingFriendRequests: [],
  playerCache:          new Map(),

  setGuild: g => set({ guild: g }),

  upsertGuildMember: m =>
    set(state => {
      const next = new Map(state.guildMembers);
      next.set(m.playerId?.toHexString?.() ?? String(m.playerId), m);
      return { guildMembers: next };
    }),

  removeGuildMember: id =>
    set(state => {
      const next = new Map(state.guildMembers);
      for (const [key, m] of next) {
        if (m.id === id) { next.delete(key); break; }
      }
      return { guildMembers: next };
    }),

  upsertGuildResource: t =>
    set(state => {
      const next = new Map(state.guildResources);
      next.set(tagOf(t.type), t.amount);
      return { guildResources: next };
    }),

  addGuildInvite: i =>
    set(state => ({ pendingGuildInvites: [...state.pendingGuildInvites, i] })),

  removeGuildInvite: id =>
    set(state => ({
      pendingGuildInvites: state.pendingGuildInvites.filter(i => i.id !== id),
    })),

  addGuildRequest: r =>
    set(state => ({
      pendingGuildRequests: [...state.pendingGuildRequests, r],
    })),

  removeGuildRequest: id =>
    set(state => ({
      pendingGuildRequests: state.pendingGuildRequests.filter(r => r.id !== id),
    })),

  setParty: p => set({ party: p }),

  upsertPartyMember: m =>
    set(state => {
      const next = new Map(state.partyMembers);
      next.set(m.playerId?.toHexString?.() ?? String(m.playerId), m);
      return { partyMembers: next };
    }),

  removePartyMember: id =>
    set(state => {
      const next = new Map(state.partyMembers);
      for (const [key, m] of next) {
        if (m.id === id) { next.delete(key); break; }
      }
      return { partyMembers: next };
    }),

  addPartyInvite: i =>
    set(state => ({ pendingPartyInvites: [...state.pendingPartyInvites, i] })),

  removePartyInvite: id =>
    set(state => ({
      pendingPartyInvites: state.pendingPartyInvites.filter(i => i.id !== id),
    })),

  addFriendship: f =>
    set(state => {
      const next = new Map(state.friends);
      next.set(f.id.toString(), f);
      return { friends: next };
    }),

  removeFriendship: id =>
    set(state => {
      const next = new Map(state.friends);
      next.delete(id.toString());
      return { friends: next };
    }),

  addFriendRequest: (r, myIdentityHex) =>
    set(state => {
      const receiverHex = r.receiverId?.toHexString?.() ?? String(r.receiverId);
      if (receiverHex === myIdentityHex) {
        return { incomingFriendRequests: [...state.incomingFriendRequests, r] };
      }
      return { outgoingFriendRequests: [...state.outgoingFriendRequests, r] };
    }),

  removeFriendRequest: id =>
    set(state => ({
      incomingFriendRequests: state.incomingFriendRequests.filter(r => r.id !== id),
      outgoingFriendRequests: state.outgoingFriendRequests.filter(r => r.id !== id),
    })),

  cachePlayer: p =>
    set(state => {
      const next = new Map(state.playerCache);
      next.set(p.identity?.toHexString?.() ?? String(p.identity), p);
      return { playerCache: next };
    }),

  isFriendsWith: (identityHex: string) => {
    for (const f of get().friends.values()) {
      const a = f.playerA?.toHexString?.() ?? String(f.playerA);
      const b = f.playerB?.toHexString?.() ?? String(f.playerB);
      if (a === identityHex || b === identityHex) return true;
    }
    return false;
  },

  inGuild: () => get().guild !== null,
  inParty: () => get().party !== null,
}));
