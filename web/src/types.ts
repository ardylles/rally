export type User = { id: string; name: string; email: string };
export type Club = { id: string; name: string; role: string };
export type Member = User & { role: string };
export type Player = {
  id: string;
  name: string;
  skill: number | null;
  userId: string | null;
};
export type Stats = {
  winners: number | null;
  unforcedErrors: number | null;
  padelRatio?: number | null;
};
export type Match = {
  id: string;
  round: number;
  court: number;
  plannedStart?: string | null;
  plannedEnd?: string | null;
  teams: string[][];
  format: string;
  status: "ready" | "live" | "done";
  startedAt: string | null;
  endedAt: string | null;
  score: number[] | null;
  stats: Record<string, Stats>;
  changes: unknown[];
};
export type Meet = {
  id: string;
  clubId: string;
  revision: number;
  name: string;
  venue: string;
  date: string;
  time: string;
  timezone: string;
  durationMinutes: number;
  courts: number;
  courtAvailability: { name: string; start: string; end: string }[];
  slotMinutes: number;
  rounds: number;
  mode: string;
  format: string;
  timing: boolean;
  players: Player[];
  fixedPairs: string[][];
  matches: Match[];
  createdAt: string;
};
export type MeetSummary = Pick<
  Meet,
  "id" | "name" | "date" | "mode" | "revision"
> & { canManage: boolean };
