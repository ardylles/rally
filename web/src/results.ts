import type { Meet, Match, Player } from "./types";
export type ResultMeet = Pick<Meet, "id" | "name" | "date" | "timezone"> & {
  players: Pick<Player, "id" | "name" | "userId" | "clubPlayerId">[];
  matches: Pick<Match, "id" | "status" | "teams" | "score" | "stats">[];
};
export type Standing = {
  key: string;
  name: string;
  played: number;
  wins: number;
  losses: number;
  gamesFor: number;
  gamesAgainst: number;
  winners: number | null;
  errors: number | null;
  ratio: number | null;
  meets: number;
  rank: number;
};
export function standings(
  meets: ResultMeet[],
  club = false,
  mergeNames = false,
): Standing[] {
  const profilesByAccount = new Map<string, Set<string>>();
  for (const meet of meets) for (const player of meet.players) {
    if (!player.userId || !player.clubPlayerId) continue;
    const profiles = profilesByAccount.get(player.userId) ?? new Set<string>();
    profiles.add(player.clubPlayerId);
    profilesByAccount.set(player.userId, profiles);
  }
  const rows = new Map<string, Standing>();
  for (const meet of meets) {
    const keys = new Map<string, string>();
    for (const p of meet.players) {
      const accountProfiles = p.userId ? profilesByAccount.get(p.userId) : undefined;
      const profileId = p.clubPlayerId ?? (accountProfiles?.size === 1 ? [...accountProfiles][0] : null);
      const key = club
        ? profileId
          ? "profile:" + profileId
          : p.userId
            ? "user:" + p.userId
            : mergeNames
              ? "name:" + p.name.trim().toLocaleLowerCase()
              : "meet:" + meet.id + ":" + p.id
        : p.id;
      keys.set(p.id, key);
      if (!rows.has(key))
        rows.set(key, {
          key,
          name: p.name,
          played: 0,
          wins: 0,
          losses: 0,
          gamesFor: 0,
          gamesAgainst: 0,
          winners: null,
          errors: null,
          ratio: null,
          meets: 0,
          rank: 0,
        });
    }
    const participated = new Set<string>();
    for (const m of meet.matches.filter(
      (m) => m.status === "done" && m.score?.length === 2,
    )) {
      for (let side = 0; side < 2; side++)
        for (const id of m.teams[side]) {
          const key = keys.get(id);
          if (!key) continue;
          const row = rows.get(key)!;
          participated.add(key);
          const own = m.score![side],
            other = m.score![1 - side];
          row.played++;
          row.wins += Number(own > other);
          row.losses += Number(own < other);
          row.gamesFor += own;
          row.gamesAgainst += other;
          const stat = m.stats[id];
          if (stat?.winners != null)
            row.winners = (row.winners ?? 0) + stat.winners;
          if (stat?.unforcedErrors != null)
            row.errors = (row.errors ?? 0) + stat.unforcedErrors;
          if (stat?.winners != null && stat.unforcedErrors != null)
            row.ratio = (row.ratio ?? 0) + stat.winners - stat.unforcedErrors;
        }
    }
    for (const key of participated) rows.get(key)!.meets++;
  }
  const sorted = [...rows.values()]
    .filter((r) => !club || r.played > 0)
    .sort(
      (a, b) =>
        b.wins - a.wins ||
        b.gamesFor - b.gamesAgainst - (a.gamesFor - a.gamesAgainst) ||
        b.gamesFor - a.gamesFor ||
        a.name.localeCompare(b.name),
    );
  sorted.forEach((r, i) => {
    const prev = sorted[i - 1];
    r.rank =
      prev &&
      r.wins === prev.wins &&
      r.gamesFor - r.gamesAgainst === prev.gamesFor - prev.gamesAgainst &&
      r.gamesFor === prev.gamesFor
        ? prev.rank
        : i + 1;
  });
  return sorted;
}
export function inPeriod(meet: ResultMeet, period: string, now = new Date()) {
  if (period === "all") return true;
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: meet.timezone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(now);
  const part = (type: string) => parts.find((p) => p.type === type)!.value;
  const today = part("year") + "-" + part("month") + "-" + part("day");
  if (period === "month")
    return meet.date.slice(0, 7) === today.slice(0, 7) && meet.date <= today;
  const start = new Date(today + "T00:00:00Z");
  start.setUTCDate(start.getUTCDate() - ((start.getUTCDay() + 6) % 7));
  return meet.date >= start.toISOString().slice(0, 10) && meet.date <= today;
}
