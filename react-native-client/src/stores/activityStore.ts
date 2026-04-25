import { create } from 'zustand';
import { ActivityType, tagOf } from '../spacetime/types';

// Row shapes from generated bindings (camelCase fields, tagged unions)
type TaggedType = { tag: string };
type ActivityRow = {
  id: bigint;
  participant: any;
  type: TaggedType;
  cost: Array<{ type: TaggedType; amount: bigint }>;
  durationMs: bigint;
  level: number;
  [key: string]: any;
};
type ActiveTaskRow = {
  id: bigint;
  participant: any;
  type: TaggedType;
  startedAt: { toMillis(): bigint };
  completesAt: { toMillis(): bigint };
  [key: string]: any;
};
type ActivityScheduleRow = {
  scheduleId: bigint;
  type: TaggedType;
  [key: string]: any;
};

interface ActivityState {
  // Keyed by ActivityType tag string (e.g. 'Scavenge')
  activities: Map<string, ActivityRow>;
  activeTasks: Map<string, ActiveTaskRow>;
  schedules: Map<string, ActivityScheduleRow>;

  // Table callbacks
  upsertActivity:   (a: ActivityRow)         => void;
  removeActivity:   (id: bigint)             => void;
  upsertActiveTask: (t: ActiveTaskRow)       => void;
  removeActiveTask: (id: bigint)             => void;
  upsertSchedule:   (s: ActivityScheduleRow) => void;
  removeSchedule:   (id: bigint)             => void;

  // Helpers
  isRunning:    (type: ActivityType) => boolean;
  isScheduled:  (type: ActivityType) => boolean;
  taskProgress: (type: ActivityType, nowMs: number) => number; // 0-1
  getActivity:  (type: ActivityType) => ActivityRow | undefined;
}

export const useActivityStore = create<ActivityState>((set, get) => ({
  activities: new Map(),
  activeTasks: new Map(),
  schedules: new Map(),

  upsertActivity: a =>
    set(state => {
      const next = new Map(state.activities);
      next.set(tagOf(a.type), a);
      return { activities: next };
    }),

  removeActivity: id =>
    set(state => {
      const next = new Map(state.activities);
      for (const [key, act] of next) {
        if (act.id === id) { next.delete(key); break; }
      }
      return { activities: next };
    }),

  upsertActiveTask: t =>
    set(state => {
      const next = new Map(state.activeTasks);
      next.set(tagOf(t.type), t);
      return { activeTasks: next };
    }),

  removeActiveTask: id =>
    set(state => {
      const next = new Map(state.activeTasks);
      for (const [key, task] of next) {
        if (task.id === id) { next.delete(key); break; }
      }
      return { activeTasks: next };
    }),

  upsertSchedule: s =>
    set(state => {
      const next = new Map(state.schedules);
      next.set(tagOf(s.type), s);
      return { schedules: next };
    }),

  removeSchedule: id =>
    set(state => {
      const next = new Map(state.schedules);
      for (const [key, sched] of next) {
        if (sched.scheduleId === id) { next.delete(key); break; }
      }
      return { schedules: next };
    }),

  isRunning:   type => get().activeTasks.has(type),
  isScheduled: type => get().schedules.has(type),

  taskProgress: (type, nowMs) => {
    const task = get().activeTasks.get(type);
    if (!task) return 0;
    const start = Number(task.startedAt.toMillis());
    const end   = Number(task.completesAt.toMillis());
    if (end <= start) return 1;
    return Math.min(1, Math.max(0, (nowMs - start) / (end - start)));
  },

  getActivity: type => get().activities.get(type),
}));
