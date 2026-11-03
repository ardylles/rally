import type { Meet } from "./types";
export function activeRound(meet: Pick<Meet, "matches">): number {
  const live = meet.matches.filter((m) => m.status === "live");
  if (live.length) return Math.min(...live.map((m) => m.round));
  const upcoming = meet.matches.filter((m) => m.status === "ready");
  if (upcoming.length) return Math.min(...upcoming.map((m) => m.round));
  return Math.max(0, ...meet.matches.map((m) => m.round));
}
export function matchesPlayed(
  meet: Pick<Meet, "matches">,
  playerId: string,
): number {
  return meet.matches.filter(
    (m) => m.status === "done" && m.teams.flat().includes(playerId),
  ).length;
}
