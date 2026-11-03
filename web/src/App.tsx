import { useEffect, useState, type FormEvent, type ReactNode } from "react";
import {
  Activity,
  ArrowUpRight,
  CalendarDays,
  Check,
  ChevronLeft,
  ChevronRight,
  Clock3,
  Flag,
  Grid2X2,
  History,
  LogOut,
  Plus,
  RefreshCw,
  Settings2,
  ShieldCheck,
  Users,
  X,
} from "lucide-react";
import { PublicMeetPage, Sharing, AdminPanel } from "./Features";
import { api } from "./api";
import { activeRound, matchesPlayed } from "./meetView";
import type {
  Club,
  Meet,
  MeetSummary,
  Member,
  User,
  Match,
  Player,
  Stats,
} from "./types";
function remembered(key: string) {
  try {
    return sessionStorage.getItem(key) || "";
  } catch {
    return "";
  }
}
function remember(key: string, value: string) {
  try {
    sessionStorage.setItem(key, value);
  } catch {
    /* Selection still works when storage is unavailable. */
  }
}
const modes: Record<string, string> = {
  rotating: "Rotating · variety",
  balanced: "Rotating · skill balance",
  fixed: "Fixed partners",
  mixed: "Mixed · fixed & rotating",
};
const format = (v: string) =>
  v === "5" ? "Best of 5 games" : "Race to " + v + " games";
const initials = (v: string) =>
  v
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((s) => s[0])
    .join("");
const short = (v: string) => {
  const p = v.split(" ");
  return p[0] + (p[1] ? " " + p[1][0] + "." : "");
};
type ReclubPreview = {
  url: string;
  name: string;
  players: { id: string; name: string; status: number }[];
};
type Modal = { kind: string; match?: Match; player?: Player };
function Field({
  label,
  children,
  full = false,
}: {
  label: string;
  children: ReactNode;
  full?: boolean;
}) {
  return (
    <label className={"field " + (full ? "full" : "")}>
      <span>{label}</span>
      {children}
    </label>
  );
}
function Button({
  children,
  onClick,
  disabled = false,
  primary = false,
  type = "button",
}: {
  children: ReactNode;
  onClick?: () => void;
  disabled?: boolean;
  primary?: boolean;
  type?: "button" | "submit";
}) {
  return (
    <button
      type={type}
      className={"button " + (primary ? "primary" : "")}
      disabled={disabled}
      onClick={onClick}
    >
      {children}
    </button>
  );
}
function Dialog({
  title,
  children,
  onClose,
  onSubmit,
  busy,
  readOnly = false,
  label = "Save",
}: {
  title: string;
  children: ReactNode;
  onClose: () => void;
  onSubmit: (e: FormEvent<HTMLFormElement>) => void;
  busy: boolean;
  readOnly?: boolean;
  label?: string;
}) {
  useEffect(() => {
    const listener = (e: KeyboardEvent) => {
      if (e.key === "Escape" && !busy) onClose();
    };
    document.addEventListener("keydown", listener);
    return () => document.removeEventListener("keydown", listener);
  }, [onClose, busy]);
  return (
    <div className="modal-backdrop">
      <section
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className="dialog"
      >
        <form onSubmit={onSubmit}>
          <div className="modal-heading">
            <div>
              <div className="eyebrow">RALLY · YOUR CLUB</div>
              <h2>{title}</h2>
            </div>
            <button
              type="button"
              className="icon-button"
              aria-label="Close"
              onClick={onClose}
              disabled={busy}
            >
              <X size={21} />
            </button>
          </div>
          {children}
          <div className="modal-actions">
            <Button disabled={busy} onClick={onClose}>
              Cancel
            </Button>
            {!readOnly && (
              <Button type="submit" primary disabled={busy}>
                {busy ? "Saving…" : label}
              </Button>
            )}
          </div>
        </form>
      </section>
    </div>
  );
}
function Auth({ onLogin }: { onLogin: (u: User) => void }) {
  const [register, setRegister] = useState(false),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false);
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    setBusy(true);
    setError("");
    try {
      onLogin(
        await api<User>(register ? "/auth/register" : "/auth/login", "POST", {
          name: f.get("name") || "",
          email: f.get("email"),
          password: f.get("password"),
        }),
      );
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="auth-page">
      <div className="auth-art">
        <div className="brand">
          <span className="brand-icon">r</span>rally.
        </div>
        <span className="eyebrow">GOOD GAMES. GREAT COMPANY.</span>
        <h1>
          A fair share
          <br />
          of the court.
        </h1>
        <p>
          Your players, your courts, your next great match.
          <br />
          Make more room for the game.
        </p>
        <div className="large-court">
          <span>PLAY MORE. PLAN LESS.</span>
        </div>
      </div>
      <div className="auth-panel">
        <form onSubmit={submit}>
          <div className="eyebrow">WELCOME TO RALLY</div>
          <h2>
            {register ? "Start your next rally." : "Good to see you again."}
          </h2>
          <p className="subtitle">
            {register
              ? "Create your account, then create or join your club."
              : "Sign in to organize your next padel meet."}
          </p>
          {register && (
            <Field label="Your name">
              <input name="name" required maxLength={80} autoComplete="name" />
            </Field>
          )}
          <Field label="Email">
            <input type="email" name="email" required autoComplete="email" />
          </Field>
          <Field label="Password">
            <input
              type="password"
              name="password"
              required
              minLength={register ? 12 : 1}
              maxLength={128}
              autoComplete={register ? "new-password" : "current-password"}
            />
          </Field>
          {register && <p className="subtle">Use at least 12 characters.</p>}
          {error && (
            <p className="error" role="alert">
              {error}
            </p>
          )}
          <Button primary type="submit" disabled={busy}>
            {busy ? "Please wait…" : register ? "Create account" : "Sign in"}
            <ArrowUpRight size={16} />
          </Button>
          <button
            type="button"
            className="text-button"
            onClick={() => {
              setRegister(!register);
              setError("");
            }}
          >
            {register
              ? "Already have an account? Sign in"
              : "New here? Create an account"}
          </button>
        </form>
      </div>
    </div>
  );
}
export default function App() {
  const token = window.location.pathname.match(/^\/public\/([^/]+)$/)?.[1];
  return token ? <PublicMeetPage token={token} /> : <Workspace />;
}
function Workspace() {
  const [user, setUser] = useState<User | null>(null),
    [boot, setBoot] = useState(true),
    [clubs, setClubs] = useState<Club[]>([]),
    [clubId, setClubId] = useState(""),
    [summaries, setSummaries] = useState<MeetSummary[]>([]),
    [meet, setMeet] = useState<Meet | null>(null),
    [view, setView] = useState("overview"),
    [round, setRound] = useState(0),
    [modal, setModal] = useState<Modal | null>(null),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false),
    [members, setMembers] = useState<Member[]>([]),
    [organizers, setOrganizers] = useState<User[]>([]),
    [toast, setToast] = useState(""),
    [tick, setTick] = useState(Date.now()),
    [draftMode, setDraftMode] = useState("rotating"),
    [statsDraft, setStatsDraft] = useState<Record<string, Stats>>({});
  const [reclubUrl, setReclubUrl] = useState("");
  const [reclubPreview, setReclubPreview] = useState<ReclubPreview | null>(
    null,
  );
  const [importIds, setImportIds] = useState<string[]>([]);
  const [playersText, setPlayersText] = useState("");
  const [subOutgoing, setSubOutgoing] = useState("");
  const [subIncoming, setSubIncoming] = useState("");
  const [extraCount, setExtraCount] = useState(1);
  const [extraDuration, setExtraDuration] = useState(120);
  const [draftStart, setDraftStart] = useState("16:00");
  const [draftDuration, setDraftDuration] = useState("120");
  const endMinute =
    Number(draftStart.slice(0, 2)) * 60 +
    Number(draftStart.slice(3)) +
    Number(draftDuration);
  const draftEnd =
    draftStart && draftDuration && Number.isFinite(endMinute)
      ? String(Math.floor(endMinute / 60) % 24).padStart(2, "0") +
        ":" +
        String(endMinute % 60).padStart(2, "0") +
        (endMinute >= 1440 ? " (next day)" : "")
      : "—";
  const remainingRound = meet
    ? Math.max(
        0,
        ...meet.matches.filter((m) => m.status !== "ready").map((m) => m.round),
      )
    : 0;
  const canRegenerateRemaining =
    !!meet &&
    (meet.matches.some(
      (m) => m.status === "ready" && m.round >= remainingRound,
    ) ||
      (meet.players.some(
        (p) =>
          p.unavailableFromRound != null || (p.availableFromRound || 0) > 0,
      ) &&
        remainingRound < meet.rounds - 1));
  const isAvailable = (p: Player, r: number) =>
    r >= (p.availableFromRound || 0) &&
    (p.unavailableFromRound == null || r < p.unavailableFromRound);
  const courtName = (court: number) =>
    meet?.courtAvailability?.[court - 1]?.name ||
    "Court " + String(court).padStart(2, "0");
  const plannedCapacity = meet
    ? Array.from({ length: meet.rounds }, (_, r) => {
        const minute = (s: string) =>
          Number(s.slice(0, 2)) * 60 + Number(s.slice(3));
        const start =
          minute(meet.time) +
          r *
            (meet.slotMinutes ||
              Math.floor(meet.durationMinutes / meet.rounds));
        const end =
          start +
          (meet.slotMinutes || Math.floor(meet.durationMinutes / meet.rounds));
        return meet.courtAvailability?.length
          ? meet.courtAvailability.filter(
              (c) => minute(c.start) <= start && minute(c.end) >= end,
            ).length
          : meet.courts;
      }).reduce((a, b) => a + b, 0)
    : 0;
  async function previewReclub() {
    setBusy(true);
    setError("");
    setReclubPreview(null);
    try {
      const result = await api<ReclubPreview>(
        "/clubs/" + clubId + "/reclub/preview",
        "POST",
        { url: reclubUrl },
      );
      setReclubPreview(result);
      setImportIds(
        result.players.filter((p) => p.status === 1).map((p) => p.id),
      );
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  const club = clubs.find((c) => c.id === clubId),
    clubAdmin = club?.role === "organizer",
    canManage =
      !!clubAdmin ||
      summaries.find((m) => m.id === meet?.id)?.canManage === true;
  const player = (id: string) => meet?.players.find((p) => p.id === id);
  const team = (ids: string[]) =>
    ids.map((id) => short(player(id)?.name || "Unknown")).join(" & ");
  useEffect(() => {
    api<User>("/auth/me")
      .then(setUser)
      .catch(() => {})
      .finally(() => setBoot(false));
  }, []);
  useEffect(() => {
    if (!user) return;
    api<Club[]>("/clubs")
      .then((c) => {
        setClubs(c);
        setClubId((old) =>
          c.some((x) => x.id === old)
            ? old
            : c.find((x) => x.id === remembered("rally.club." + user.id))?.id ||
              c[0]?.id ||
              "",
        );
      })
      .catch((e) => setError(e.message));
  }, [user]);
  useEffect(() => {
    if (!clubId) return;
    let active = true;
    setMeet(null);
    setSummaries([]);
    setMembers([]);
    setRound(0);
    Promise.all([
      api<MeetSummary[]>("/clubs/" + clubId + "/meets"),
      api<Member[]>("/clubs/" + clubId + "/members"),
    ])
      .then(async ([ms, mem]) => {
        if (!active) return;
        setSummaries(ms);
        setMembers(mem);
        const preferred =
          ms.find((m) => m.id === remembered("rally.meet." + clubId)) || ms[0];
        if (preferred) {
          const m = await api<Meet>("/meets/" + preferred.id);
          if (active) {
            setMeet(m);
            setRound(activeRound(m));
          }
        }
      })
      .catch((e) => {
        if (active) setError(e.message);
      });
    return () => {
      active = false;
    };
  }, [clubId]);
  useEffect(() => {
    if (!meet) return;
    let active = true;
    api<User[]>("/meets/" + meet.id + "/organizers")
      .then((o) => {
        if (active) setOrganizers(o);
      })
      .catch((e) => setError(e.message));
    return () => {
      active = false;
    };
  }, [meet?.id, meet?.revision]);
  useEffect(() => {
    const id = setInterval(() => setTick(Date.now()), 1000);
    return () => clearInterval(id);
  }, []);
  useEffect(() => {
    if (!toast) return;
    const id = setTimeout(() => setToast(""), 4000);
    return () => clearTimeout(id);
  }, [toast]);
  async function reload() {
    if (!clubId) return;
    setError("");
    try {
      const [ms, mem] = await Promise.all([
        api<MeetSummary[]>("/clubs/" + clubId + "/meets"),
        api<Member[]>("/clubs/" + clubId + "/members"),
      ]);
      setSummaries(ms);
      setMembers(mem);
      if (meet) {
        const [m, o] = await Promise.all([
          api<Meet>("/meets/" + meet.id),
          api<User[]>("/meets/" + meet.id + "/organizers"),
        ]);
        setMeet(m);
        setRound(activeRound(m));
        setOrganizers(o);
      }
    } catch (e) {
      setError((e as Error).message);
    }
  }
  async function selectMeet(id: string) {
    setError("");
    setBusy(true);
    try {
      const selected = await api<Meet>("/meets/" + id);
      setMeet(selected);
      remember("rally.meet." + clubId, id);
      setRound(activeRound(selected));
      setView("overview");
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function change(path: string, body: unknown, method = "POST") {
    if (!meet) return;
    setBusy(true);
    setError("");
    try {
      const m = await api<Meet>("/meets/" + meet.id + path, method, {
        revision: meet.revision,
        ...(body as object),
      });
      setMeet(m);
      setModal(null);
      setToast("Saved to your club.");
      return m;
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  function open(kind: string, match?: Match, p?: Player) {
    setError("");
    if (kind === "create") {
      setPlayersText("");
      setReclubPreview(null);
      setReclubUrl("");
      setImportIds([]);
    }
    if (kind === "create" || kind === "settings") {
      setDraftMode(kind === "settings" ? meet?.mode || "rotating" : "rotating");
      setDraftStart(kind === "settings" ? meet?.time || "16:00" : "16:00");
      setDraftDuration(
        String(kind === "settings" ? meet?.durationMinutes || 120 : 120),
      );
    }
    if (kind === "extraRounds" && meet) {
      setExtraCount(1);
      setExtraDuration(meet.durationMinutes);
    }
    if (kind === "substitute" && match) {
      setSubOutgoing(match.teams[0][0]);
      setSubIncoming("");
    }
    if (kind === "result") setStatsDraft({ ...match?.stats });
    setModal({ kind, match, player: p });
  }
  const elapsed = (m: Match) => {
    if (!m.startedAt) return "Timer ready";
    const seconds = Math.max(
      0,
      Math.floor(
        ((m.endedAt ? Date.parse(m.endedAt) : tick) - Date.parse(m.startedAt)) /
          1000,
      ),
    );
    return (
      Math.floor(seconds / 60)
        .toString()
        .padStart(2, "0") +
      ":" +
      (seconds % 60).toString().padStart(2, "0")
    );
  };
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget),
      kind = modal?.kind;
    if (!kind) return;
    setError("");
    try {
      if (kind === "club") {
        setBusy(true);
        const c = await api<Club>("/clubs", "POST", { name: f.get("name") });
        setClubs([...clubs, c]);
        setClubId(c.id);
        setModal(null);
      } else if (kind === "create" || kind === "settings") {
        const editing = kind === "settings";
        const players = editing
          ? meet!.players.map((p) => ({
              ...p,
              skill:
                draftMode === "balanced"
                  ? Number(f.get("skill-" + p.id))
                  : p.skill,
            }))
          : String(f.get("players"))
              .split("\n")
              .map((s) => s.trim())
              .filter(Boolean)
              .map((line) => {
                const [name, value] = line.split("|");
                return {
                  id: crypto.randomUUID(),
                  name: name.trim(),
                  skill: value?.trim() ? Number(value.trim()) : null,
                  userId: null,
                };
              });
        if (players.length < 4 || players.length > 24)
          throw Error("Enter 4–24 players, one per line.");
        let fixedPairs: string[][] = [];
        if (draftMode === "fixed") {
          if (players.length % 2)
            throw Error("Fixed mode needs an even number of players.");
          for (let i = 0; i < players.length; i += 2)
            fixedPairs.push([players[i].id, players[i + 1].id]);
        }
        if (draftMode === "mixed")
          fixedPairs = [[players[0].id, players[1].id]];
        if (
          editing &&
          meet!.mode === draftMode &&
          ["fixed", "mixed"].includes(draftMode)
        )
          fixedPairs = meet!.fixedPairs;
        const body = {
          ...(editing ? meet : {}),
          name: f.get("name"),
          venue: f.get("venue"),
          date: f.get("date"),
          time: f.get("time"),
          timezone: "Asia/Jakarta",
          durationMinutes: Number(f.get("duration")),
          courts: Number(f.get("courts")),
          slotMinutes: Number(f.get("slotMinutes")),
          courtAvailability: String(f.get("courtAvailability") || "")
            .split("\n")
            .map((s) => s.trim())
            .filter(Boolean)
            .map((line) => {
              const parts = line.split("|").map((s) => s.trim());
              if (parts.length !== 3)
                throw Error("Use Court name | HH:mm | HH:mm for each court.");
              return { name: parts[0], start: parts[1], end: parts[2] };
            }),
          rounds: Number(f.get("rounds")),
          mode: draftMode,
          format: f.get("format"),
          timing: f.has("timing"),
          players,
          fixedPairs,
        };
        if (editing) await change("/settings", body, "PUT");
        else {
          setBusy(true);
          const m = await api<Meet>(
            "/clubs/" + clubId + "/meets",
            "POST",
            body,
          );
          setMeet(m);
          setSummaries([{ ...m, canManage: true }, ...summaries]);
          setRound(0);
          setView("overview");
          setModal(null);
          setToast("Your meet is ready.");
        }
      } else if (kind === "generateMatch") {
        const oldIds = new Set(meet!.matches.map((m) => m.id));
        const updated = await change("/generate-match", {
          court: Number(f.get("court")),
        });
        if (updated) {
          const added = updated.matches.find((m) => !oldIds.has(m.id));
          if (added) setRound(added.round);
          setView("overview");
        }
      } else if (kind === "regenerateAll") {
        const updated = await change("/regenerate-all", {
          confirmed: f.has("confirmed"),
        });
        if (updated) {
          setRound(0);
          setView("overview");
        }
      } else if (kind === "restartMatch" || kind === "deleteMatch") {
        await change(
          "/matches/" +
            modal!.match!.id +
            (kind === "restartMatch" ? "/restart" : "/delete"),
          { confirmed: f.has("confirmed") },
        );
      } else if (kind === "extraRounds") {
        const oldRounds = meet!.rounds;
        const updated = await change("/extra-rounds", {
          count: extraCount,
          durationMinutes: extraDuration,
          courtAvailability: meet!.courtAvailability.map((c, i) => ({
            ...c,
            end: String(f.get("courtEnd-" + i)),
          })),
        });
        if (updated) {
          setRound(oldRounds);
          setView("overview");
          setToast("Extra rounds added. Existing matches are unchanged.");
        }
      } else if (kind === "arrival") {
        await change("/arrivals", {
          playerId: modal!.player!.id,
          fromRound: Number(f.get("fromRound")),
        });
      } else if (kind === "side") {
        await change(
          "/sides",
          { playerId: modal!.player!.id, side: f.get("side") },
          "PUT",
        );
      } else if (kind === "departure") {
        await change("/departures", {
          playerId: modal!.player!.id,
          fromRound: Number(f.get("fromRound")),
        });
      } else if (kind === "regenerateRemaining") {
        await change("/regenerate-remaining", {});
      } else if (kind === "result")
        await change(
          "/matches/" + modal!.match!.id + "/result",
          {
            score: [Number(f.get("score0")), Number(f.get("score1"))],
            stats: statsDraft,
          },
          "PUT",
        );
      else if (kind === "substitute")
        await change("/matches/" + modal!.match!.id + "/substitute", {
          outgoingId: f.get("outgoing"),
          incomingId: f.get("incoming"),
        });
      else if (kind === "replace")
        await change("/replacements", {
          outgoingId: f.get("outgoing"),
          name: f.get("name"),
          scope: f.get("scope"),
          side: f.get("side"),
          skill: f.get("skill") ? Number(f.get("skill")) : null,
        });
      else if (kind === "skill")
        await change(
          "/skills",
          { playerId: modal!.player!.id, skill: Number(f.get("skill")) },
          "PUT",
        );
      else if (kind === "pairs") {
        const pairs: string[][] = [];
        for (let i = 0; i < Math.floor(meet!.players.length / 2); i++) {
          const a = f.get("a" + i),
            b = f.get("b" + i);
          if (!a && !b) continue;
          if (!a || !b) throw Error("Choose both players in each fixed pair.");
          pairs.push([String(a), String(b)]);
        }
        await change("/pairs", { pairs }, "PUT");
      } else if (kind === "organizers") {
        setBusy(true);
        await api("/meets/" + meet!.id + "/organizers", "PUT", {
          userIds: f.getAll("organizer"),
        });
        setOrganizers(await api<User[]>("/meets/" + meet!.id + "/organizers"));
        setModal(null);
        await reload();
      } else if (kind === "member") {
        setBusy(true);
        await api("/clubs/" + clubId + "/members", "POST", {
          email: f.get("email"),
          role: f.get("role"),
        });
        setModal(null);
        await reload();
      }
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  if (boot)
    return (
      <div className="loading">
        <span className="brand">rally.</span>
        <p>Opening your club…</p>
      </div>
    );
  if (!user) return <Auth onLogin={setUser} />;
  const counts =
    meet?.players.map((p) => ({
      p,
      planned: meet.matches.filter((m) => m.teams.flat().includes(p.id)).length,
      done: meet.matches.filter(
        (m) => m.status === "done" && m.teams.flat().includes(p.id),
      ).length,
    })) || [];
  const min = counts.length ? Math.min(...counts.map((c) => c.planned)) : 0,
    max = counts.length ? Math.max(...counts.map((c) => c.planned)) : 0;
  const completed = meet?.matches.filter((m) => m.status === "done") || [],
    locked =
      meet?.players.some(
        (p) =>
          p.unavailableFromRound != null || (p.availableFromRound || 0) > 0,
      ) ||
      meet?.matches.some((m) => m.status !== "ready" || m.changes.length > 0);
  function matchManagement(m: Match) {
    if (!canManage) return null;
    return (
      <div className="match-management">
        {m.status !== "ready" && (
          <button
            type="button"
            className="text-button"
            disabled={busy}
            onClick={() => open("restartMatch", m)}
          >
            Restart match
          </button>
        )}
        <button
          type="button"
          className="text-button"
          disabled={busy}
          onClick={() => open("deleteMatch", m)}
        >
          {m.status === "ready" ? "Remove upcoming match" : "Delete match"}
        </button>
      </div>
    );
  }
  function matchTable(matches: Match[]) {
    return (
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>COURT</th>
              <th>MATCHUP</th>
              <th>STATUS</th>
              <th>ACTIONS</th>
            </tr>
          </thead>
          <tbody>
            {matches.map((m) => (
              <tr key={m.id}>
                <td>
                  <strong>{courtName(m.court)}</strong>
                  <div className="subtle">
                    {m.plannedStart} {m.plannedEnd ? "– " + m.plannedEnd : ""}
                  </div>
                </td>
                <td>
                  <div className="matchup">
                    <strong>{team(m.teams[0])}</strong>
                    <span>vs</span>
                    <strong>{team(m.teams[1])}</strong>
                  </div>
                </td>
                <td>
                  <span className={"pill " + m.status}>
                    {m.status === "done"
                      ? m.score?.join(" – ")
                      : m.status === "live"
                        ? "On court"
                        : "Upcoming"}
                  </span>
                </td>
                <td>
                  <div className="row-actions">
                    {canManage && m.status === "ready" && (
                      <button
                        className="text-button"
                        onClick={() => open("substitute", m)}
                      >
                        Substitute
                      </button>
                    )}
                    <button
                      className="text-button"
                      onClick={() => open("result", m)}
                    >
                      {m.status === "done"
                        ? "View result"
                        : canManage
                          ? "Enter result"
                          : "Match details"}
                    </button>
                  </div>
                  {matchManagement(m)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  }
  function rest(r: number) {
    const ids = meet!.matches
        .filter((m) => m.round === r)
        .flatMap((m) => m.teams.flat()),
      resting = meet!.players.filter(
        (p) =>
          isAvailable(p, r) && !p.matchOnlyReplacement && !ids.includes(p.id),
      );
    return (
      <div className="rest">
        <Clock3 size={15} />
        <strong>
          {resting.length ? "Taking a breather" : "Everyone on court"}
        </strong>
        <span>{resting.map((p) => short(p.name)).join(" · ")}</span>
      </div>
    );
  }
  function resultBody(m: Match) {
    return (
      <>
        <div className="hint">
          Round {m.round + 1} · Court {m.court} · {format(m.format)}
          <br />
          Record games won. Small points and tiebreak details aren’t needed.
        </div>
        {canManage && m.status === "ready" && (
          <Button onClick={() => open("substitute", m)}>
            Substitute a registered player
          </Button>
        )}
        {m.teams.map((t, i) => (
          <label className="score-entry" key={i}>
            <strong>{team(t)}</strong>
            <input
              aria-label={"Team " + (i + 1) + " games"}
              name={"score" + i}
              type="number"
              min="0"
              max={m.format === "5" ? 3 : Number(m.format)}
              required
              defaultValue={m.score?.[i] ?? ""}
              disabled={!canManage}
            />
          </label>
        ))}
        <h3 className="spaced-title">Optional statistics</h3>
        <div className="table-wrap">
          <table className="stats-entry">
            <thead>
              <tr>
                <th>PLAYER</th>
                <th>WINNERS</th>
                <th>UNFORCED ERRORS</th>
                <th>W − UE</th>
              </tr>
            </thead>
            <tbody>
              {m.teams.flat().map((id) => {
                const s = statsDraft[id] || {
                  winners: null,
                  unforcedErrors: null,
                };
                return (
                  <tr key={id}>
                    <td>{short(player(id)?.name || "")}</td>
                    {(["winners", "unforcedErrors"] as const).map((k) => (
                      <td key={k}>
                        <input
                          aria-label={player(id)?.name + " " + k}
                          type="number"
                          min="0"
                          max="10000"
                          value={s[k] ?? ""}
                          disabled={!canManage}
                          onChange={(e) =>
                            setStatsDraft({
                              ...statsDraft,
                              [id]: {
                                ...s,
                                [k]:
                                  e.target.value === ""
                                    ? null
                                    : Number(e.target.value),
                              },
                            })
                          }
                        />
                      </td>
                    ))}
                    <td>
                      {s.winners !== null && s.unforcedErrors !== null
                        ? s.winners - s.unforcedErrors
                        : "—"}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        <p className="subtle">
          Padel ratio = winners − unforced errors. Blank means unknown; zero
          means none.
        </p>
      </>
    );
  }
  return (
    <div className="app">
      <aside className="sidebar">
        <a href="/" className="brand">
          <span className="brand-icon">r</span>rally
          <span className="brand-dot">.</span>
        </a>
        <div className="workspace">
          <span className="eyebrow">YOUR CLUB</span>
          <select
            aria-label="Select club"
            value={clubId}
            onChange={(e) => {
              remember("rally.club." + user.id, e.target.value);
              setClubId(e.target.value);
            }}
          >
            {clubs.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </div>
        <div className="nav-label">WORKSPACE</div>
        {[
          ["overview", "Meet dashboard", Grid2X2],
          ["players", "Players", Users],
          ["history", "Match history", History],
          ["organizers", "Organizers", ShieldCheck],
        ].map(([id, label, Icon]) => {
          const I = Icon as typeof Users;
          return (
            <button
              key={String(id)}
              className={"nav " + (view === id ? "active" : "")}
              onClick={() => setView(String(id))}
            >
              <I size={18} />
              {String(label)}
            </button>
          );
        })}
        {user.isAdmin && (
          <button
            className={"nav " + (view === "admin" ? "active" : "")}
            onClick={() => setView("admin")}
          >
            <ShieldCheck size={18} />
            Administration
          </button>
        )}
        <div className="nav-label meets-label">
          YOUR MEETS{" "}
          {clubAdmin && (
            <button aria-label="Create meet" onClick={() => open("create")}>
              <Plus size={15} />
            </button>
          )}
        </div>
        <div className="meet-list">
          {summaries.map((m) => (
            <button
              key={m.id}
              className={"meet-link " + (meet?.id === m.id ? "selected" : "")}
              onClick={() => selectMeet(m.id)}
            >
              <i />
              {m.name}
            </button>
          ))}
        </div>
        <button className="text-button new-club" onClick={() => open("club")}>
          ＋ Create a club
        </button>
        <div className="sidebar-bottom">
          <div className="mini-court" />
          <strong>Good games. Great company.</strong>
          <p>
            A little structure.
            <br />A lot more court time.
          </p>
          <div className="profile">
            <span className="avatar">{initials(user.name)}</span>
            <div>
              <strong>{user.name}</strong>
              <small>{clubAdmin ? "Club organizer" : "Club member"}</small>
            </div>
            <button
              aria-label="Sign out"
              onClick={async () => {
                await api("/auth/logout", "POST");
                setUser(null);
                setClubs([]);
                setMeet(null);
                setClubId("");
              }}
            >
              <LogOut size={16} />
            </button>
          </div>
        </div>
      </aside>
      <div className="shell">
        <header className="topbar">
          <span>
            Workspace <b>/</b> {club?.name || "Your clubs"} <b>/</b>{" "}
            <strong>{meet?.name || "Welcome"}</strong>
          </span>
          <span className="server-tag">
            <i /> PostgreSQL connected
          </span>
        </header>
        <main>
          {error && !modal && (
            <div role="alert" className="error banner">
              {error}
              <button onClick={reload}>Refresh data</button>
            </div>
          )}
          <div className="page-heading">
            <div>
              <div className="eyebrow">LET’S GET EVERYONE ON COURT</div>
              <h1>{meet?.name || "Your next great match starts here."}</h1>
              <p>
                {meet
                  ? meet.date +
                    " · " +
                    meet.time +
                    " · " +
                    meet.durationMinutes / 60 +
                    "-hour session · " +
                    meet.venue
                  : "Create a club and bring your players together."}
              </p>
            </div>
            {clubAdmin && (
              <Button primary onClick={() => open("create")}>
                <Plus size={16} />
                Create meet
              </Button>
            )}
          </div>
          {view === "admin" && user.isAdmin ? (
            <AdminPanel
              onMeetDeleted={(id) => {
                setSummaries((ms) => ms.filter((m) => m.id !== id));
                if (meet?.id === id) setMeet(null);
              }}
            />
          ) : !club ? (
            <div className="empty card">
              <Users size={38} />
              <h2>A home for your padel crew.</h2>
              <p>
                Create a club to organize meets, or ask an organizer to add your
                registered email.
              </p>
              <Button primary onClick={() => open("club")}>
                Create your club
              </Button>
              <Button
                onClick={async () => {
                  const c = await api<Club[]>("/clubs");
                  setClubs(c);
                  setClubId(c[0]?.id || "");
                }}
              >
                Check membership
              </Button>
            </div>
          ) : !meet ? (
            <div className="empty card">
              <Flag size={38} />
              <h2>Let’s plan the first meet.</h2>
              <p>
                {clubAdmin
                  ? "Add your roster, choose the courts, and we’ll arrange the rotation."
                  : "Your club organizer can create the first meet."}
              </p>
              {clubAdmin && (
                <Button primary onClick={() => open("create")}>
                  Create a meet
                </Button>
              )}
              {clubAdmin && (
                <Button onClick={() => open("member")}>
                  Add a club member
                </Button>
              )}
            </div>
          ) : (
            <>
              <div className="meet-toolbar">
                <div className="tabs">
                  {[
                    "overview",
                    "schedule",
                    "players",
                    "history",
                    "organizers",
                  ].map((v) => (
                    <button
                      key={v}
                      className={view === v ? "selected" : ""}
                      onClick={() => setView(v)}
                    >
                      {v[0].toUpperCase() + v.slice(1)}
                      {v === "players" && <span>{meet.players.length}</span>}
                    </button>
                  ))}
                </div>
                <div className="row-actions">
                  <button
                    className="icon-button"
                    aria-label="Refresh meet"
                    onClick={reload}
                  >
                    <RefreshCw size={16} />
                  </button>
                  {canManage && (
                    <button
                      className="button quiet"
                      onClick={() => open("settings")}
                    >
                      <Settings2 size={15} />
                      Meet settings
                    </button>
                  )}
                </div>
              </div>
              {view === "overview" && (
                <>
                  <div className="stats">
                    {[
                      [
                        "Players registered",
                        meet.players.length,
                        "in the roster",
                        Users,
                      ],
                      ["Courts available", meet.courts, "courts", Grid2X2],
                      [
                        "Session progress",
                        completed.length + " / " + meet.matches.length,
                        "matches completed",
                        Clock3,
                      ],
                      [
                        "Match opportunities",
                        min === max ? min : min + "–" + max,
                        "planned per player",
                        Activity,
                      ],
                    ].map(([label, value, detail, Icon]) => {
                      const I = Icon as typeof Users;
                      return (
                        <div className="stat" key={String(label)}>
                          <div className="stat-top">
                            {String(label)}
                            <I size={18} />
                          </div>
                          <strong>{String(value)}</strong>
                          <small>{String(detail)}</small>
                        </div>
                      );
                    })}
                  </div>
                  <div className="layout">
                    <div>
                      <div className="section-title court-section-heading">
                        <h2>On the courts</h2>
                        {canManage && (
                          <Button
                            disabled={busy}
                            onClick={() => open("generateMatch")}
                          >
                            <Plus size={15} />
                            Generate match
                          </Button>
                        )}
                        <div className="round-controls">
                          <button
                            aria-label="Previous round"
                            disabled={round === 0}
                            onClick={() => setRound(round - 1)}
                          >
                            <ChevronLeft size={17} />
                          </button>
                          <span>
                            {round + 1} / {meet.rounds}
                          </span>
                          <button
                            aria-label="Next round"
                            disabled={round === meet.rounds - 1}
                            onClick={() => setRound(round + 1)}
                          >
                            <ChevronRight size={17} />
                          </button>
                        </div>
                      </div>
                      <div className="courts">
                        {meet.matches
                          .filter((m) => m.round === round)
                          .map((m) => (
                            <article
                              className="court-card compact-court"
                              key={m.id}
                              aria-label={courtName(m.court)}
                            >
                              <header className="compact-court-heading">
                                <h3>{courtName(m.court)}</h3>
                                <span className={"pill " + m.status}>
                                  {m.status === "live"
                                    ? "Live"
                                    : m.status === "done"
                                      ? "Completed"
                                      : "Upcoming"}
                                </span>
                              </header>
                              <p className="compact-court-meta">
                                {m.plannedStart && (
                                  <span>
                                    {m.plannedStart}–{m.plannedEnd}
                                  </span>
                                )}
                                <span>{format(m.format)}</span>
                              </p>
                              <div
                                className="court-lineup"
                                aria-label="Match teams and score"
                              >
                                {m.teams.map((t, i) => (
                                  <div className="court-team-row" key={i}>
                                    <div className="court-team-names">
                                      <small>Team {i + 1}</small>
                                      <strong>
                                        {t
                                          .map(
                                            (id) =>
                                              player(id)?.name || "Unknown",
                                          )
                                          .join(" & ")}
                                      </strong>
                                    </div>
                                    <span
                                      className="court-team-score"
                                      aria-label={
                                        "Team " +
                                        (i + 1) +
                                        " games: " +
                                        (m.score?.[i] ?? "not recorded")
                                      }
                                    >
                                      {m.score?.[i] ?? "—"}
                                    </span>
                                  </div>
                                ))}
                              </div>
                              {meet.timing && m.startedAt && (
                                <p className="compact-court-timer">
                                  <Clock3 size={14} />
                                  {elapsed(m)}
                                  {m.status === "done"
                                    ? " · Final time"
                                    : " · Elapsed"}
                                </p>
                              )}
                              <div className="compact-court-actions">
                                {canManage && m.status === "ready" && (
                                  <Button
                                    disabled={busy}
                                    onClick={() => open("substitute", m)}
                                  >
                                    Substitute
                                  </Button>
                                )}
                                <Button
                                  primary={canManage && m.status !== "done"}
                                  disabled={busy}
                                  onClick={() =>
                                    canManage &&
                                    m.status === "ready" &&
                                    meet.timing
                                      ? change(
                                          "/matches/" + m.id + "/start",
                                          {},
                                        )
                                      : open("result", m)
                                  }
                                >
                                  {!canManage
                                    ? "Match details"
                                    : m.status === "ready"
                                      ? meet.timing
                                        ? "Start match"
                                        : "Enter result"
                                      : m.status === "live"
                                        ? "End match"
                                        : "View result"}
                                </Button>
                              </div>
                              {matchManagement(m)}
                            </article>
                          ))}
                      </div>
                      {rest(round)}
                      <section className="next-section">
                        <div className="section-title">
                          <h2>
                            {round < meet.rounds - 1 ? "Up next" : "This round"}
                          </h2>
                          <button
                            className="text-button"
                            onClick={() => setView("schedule")}
                          >
                            Full schedule ↗
                          </button>
                        </div>
                        {matchTable(
                          meet.matches.filter(
                            (m) =>
                              m.round === Math.min(round + 1, meet.rounds - 1),
                          ),
                        )}
                      </section>
                    </div>
                    <aside>
                      <div className="card fair-card">
                        <div className="card-title">
                          A fair share of the court <Activity size={16} />
                        </div>
                        <div className="fair-score">
                          <strong>
                            {min === max ? "Even" : max - min + " apart"}
                          </strong>
                          <span>match allocation</span>
                        </div>
                        <p>
                          {min === max
                            ? "Everyone has the same number of planned matches."
                            : "Compare individual counts in Players."}
                        </p>
                        <div className="bar">
                          <i
                            style={{
                              width: (max ? (min / max) * 100 : 0) + "%",
                            }}
                          />
                        </div>
                        <div className="metric">
                          <span>Matches per player</span>
                          <b>{min === max ? min : min + "–" + max}</b>
                        </div>
                        <div className="metric">
                          <span>Partner preference</span>
                          <b>
                            {meet.mode === "mixed"
                              ? "Fixed + rotating"
                              : meet.mode === "fixed"
                                ? "Fixed teams"
                                : meet.mode === "balanced"
                                  ? "Skill balance"
                                  : "More variety"}
                          </b>
                        </div>
                        <div className="fair-note">
                          <Check size={13} /> Fair participation comes first.
                          {meet.matches.length < plannedCapacity && (
                            <p>
                              {plannedCapacity - meet.matches.length} booked
                              court slots unused because of player numbers or
                              fairness.
                            </p>
                          )}
                          <p>
                            Best-effort schedule. Actual match lengths vary.
                          </p>
                        </div>
                      </div>
                      <div className="card details-card">
                        <div className="card-title">The game plan</div>
                        {[
                          [Users, "PARTNERS", modes[meet.mode]],
                          [Flag, "MATCH FORMAT", format(meet.format)],
                          [
                            Clock3,
                            "MATCH TIMING",
                            meet.timing
                              ? "Start & end controls"
                              : "Timing disabled",
                          ],
                          [CalendarDays, "TIMEZONE", meet.timezone],
                        ].map(([Icon, label, value]) => {
                          const I = Icon as typeof Users;
                          return (
                            <div className="detail-row" key={String(label)}>
                              <I size={17} />
                              <div>
                                <small>{String(label)}</small>
                                <strong>{String(value)}</strong>
                              </div>
                            </div>
                          );
                        })}
                      </div>
                    </aside>
                  </div>
                </>
              )}
              {view === "schedule" && (
                <>
                  <div className="section-title">
                    <h2>Your court rotation</h2>
                    {canManage && (
                      <div className="row-actions">
                        <Button
                          disabled={busy}
                          onClick={() => open("generateMatch")}
                        >
                          <Plus size={15} />
                          Generate match
                        </Button>
                        <Button
                          disabled={busy || meet.rounds >= 20}
                          onClick={() => open("extraRounds")}
                        >
                          Extra rounds
                        </Button>
                        <Button
                          disabled={busy}
                          onClick={() => open("regenerateAll")}
                        >
                          Regenerate all
                        </Button>
                        <Button
                          disabled={busy || !canRegenerateRemaining}
                          onClick={() => open("regenerateRemaining")}
                        >
                          <RefreshCw size={14} />
                          Regenerate remaining
                        </Button>
                      </div>
                    )}
                  </div>
                  <div className="hint">
                    {meet.rounds} rounds · {modes[meet.mode]}. Regenerating
                    remaining preserves started matches and eligible
                    substitutions; Regenerate all replaces every match after
                    confirmation. Planned times use {meet.timezone}. Allow for
                    changeovers; actual match durations can overrun the planned
                    slots.
                  </div>
                  <div className="hint">
                    {meet.courtAvailability?.length
                      ? meet.courtAvailability
                          .map((c) => c.name + ": " + c.start + "–" + c.end)
                          .join(" · ")
                      : "All courts use the full session window."}
                  </div>
                  {Array.from({ length: meet.rounds }, (_, r) => (
                    <section className="schedule-round" key={r}>
                      <h3>Round {String(r + 1).padStart(2, "0")}</h3>
                      {matchTable(meet.matches.filter((m) => m.round === r))}
                      {rest(r)}
                    </section>
                  ))}
                </>
              )}
              {["overview", "schedule", "players"].includes(view) &&
                !!meet.schedulingNotes?.length && (
                  <div className="hint">
                    {meet.schedulingNotes.map((note, i) => (
                      <p key={i}>{note}</p>
                    ))}
                  </div>
                )}
              {view === "players" && (
                <>
                  <div className="section-title">
                    <h2>The people make the game</h2>
                    {canManage && (
                      <div className="row-actions">
                        {["mixed", "fixed"].includes(meet.mode) && (
                          <Button
                            disabled={locked}
                            onClick={() => open("pairs")}
                          >
                            Choose fixed pairs
                          </Button>
                        )}
                        <Button primary onClick={() => open("replace")}>
                          Add replacement player
                        </Button>
                      </div>
                    )}
                  </div>
                  <p className="subtitle">
                    {meet.players.length} registered players ·{" "}
                    {modes[meet.mode]}
                  </p>
                  <div className="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>PLAYER</th>
                          <th>PLAYING SIDE</th>
                          {meet.mode === "balanced" && <th>SKILL</th>}
                          {["fixed", "mixed"].includes(meet.mode) && (
                            <th>PARTNER</th>
                          )}
                          <th>PLANNED</th>
                          <th>COMPLETED</th>
                          <th>AVAILABILITY</th>
                        </tr>
                      </thead>
                      <tbody>
                        {counts.map(({ p, planned, done }) => {
                          const pair = meet.fixedPairs.find((t) =>
                            t.includes(p.id),
                          );
                          return (
                            <tr key={p.id}>
                              <td>
                                <div className="player-name">
                                  <span className="avatar">
                                    {initials(p.name)}
                                  </span>
                                  <strong>{p.name}</strong>
                                </div>
                              </td>
                              <td>
                                {canManage ? (
                                  <button
                                    className="text-button"
                                    disabled={busy}
                                    onClick={() => open("side", undefined, p)}
                                  >
                                    {p.side || "both"} · Edit
                                  </button>
                                ) : (
                                  p.side || "both"
                                )}
                              </td>
                              {meet.mode === "balanced" && (
                                <td>
                                  {canManage ? (
                                    <button
                                      className="text-button"
                                      onClick={() =>
                                        open("skill", undefined, p)
                                      }
                                    >
                                      {p.skill ?? "Unknown"} · Edit
                                    </button>
                                  ) : (
                                    (p.skill ?? "Unknown")
                                  )}
                                </td>
                              )}
                              {["fixed", "mixed"].includes(meet.mode) && (
                                <td>
                                  {pair
                                    ? "Fixed with " +
                                      short(
                                        player(pair.find((id) => id !== p.id)!)
                                          ?.name || "",
                                      )
                                    : "Rotating partners"}
                                </td>
                              )}
                              <td>
                                <strong>{planned}</strong>
                                <span className="count-track">
                                  {Array.from({ length: planned }, (_, i) => (
                                    <i key={i} />
                                  ))}
                                </span>
                              </td>
                              <td>{done}</td>
                              <td>
                                <div>
                                  {(p.availableFromRound || 0) >= meet.rounds
                                    ? "Waiting for arrival"
                                    : (p.availableFromRound || 0) > 0
                                      ? "Arrives round " +
                                        ((p.availableFromRound || 0) + 1)
                                      : "Present from start"}
                                </div>
                                {canManage &&
                                  !meet.matches.some(
                                    (m) =>
                                      m.status !== "ready" &&
                                      m.teams.flat().includes(p.id),
                                  ) && (
                                    <Button
                                      disabled={busy}
                                      onClick={() =>
                                        open("arrival", undefined, p)
                                      }
                                    >
                                      Late / arrival
                                    </Button>
                                  )}
                                <span>
                                  {p.unavailableFromRound == null
                                    ? "Until session end"
                                    : "Unavailable from round " +
                                      (p.unavailableFromRound + 1)}
                                </span>
                                {canManage && canRegenerateRemaining && (
                                  <Button
                                    disabled={busy}
                                    onClick={() =>
                                      open("departure", undefined, p)
                                    }
                                  >
                                    {p.unavailableFromRound == null
                                      ? "Leave early"
                                      : "Edit departure"}
                                  </Button>
                                )}
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                  <div className="hint spaced">
                    New players are registered here. Substitute any existing
                    meet player directly from a match; their other assignments
                    stay unchanged. Left/Right restrictions apply to every team.
                    Teams list the left-side player first.
                  </div>
                </>
              )}
              {view === "history" && (
                <>
                  <div className="section-title">
                    <h2>Every match, remembered</h2>
                    <span className="subtle">{completed.length} completed</span>
                  </div>
                  {!completed.length ? (
                    <div className="card empty">
                      <History size={34} />
                      <h2>Your first match is waiting.</h2>
                      <p>
                        Save a result to see it here, with optional player
                        statistics.
                      </p>
                    </div>
                  ) : (
                    completed.map((m) => (
                      <section className="schedule-round" key={m.id}>
                        <h3>
                          Round {m.round + 1} · Court {m.court}
                          {m.startedAt ? " · " + elapsed(m) : ""}
                        </h3>
                        {matchTable([m])}
                        <div className="table-wrap spaced">
                          <table>
                            <thead>
                              <tr>
                                <th>PLAYER</th>
                                <th>WINNERS</th>
                                <th>UNFORCED ERRORS</th>
                                <th>PADEL RATIO</th>
                              </tr>
                            </thead>
                            <tbody>
                              {m.teams.flat().map((id) => (
                                <tr key={id}>
                                  <td>{player(id)?.name}</td>
                                  <td>{m.stats[id]?.winners ?? "—"}</td>
                                  <td>{m.stats[id]?.unforcedErrors ?? "—"}</td>
                                  <td>{m.stats[id]?.padelRatio ?? "—"}</td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                      </section>
                    ))
                  )}
                </>
              )}
              {view === "overview" && canManage && (
                <Sharing key={meet.id} meet={meet} onChange={setMeet} />
              )}
              {view === "organizers" && (
                <>
                  <div className="section-title">
                    <h2>A shared effort</h2>
                    {clubAdmin && (
                      <Button onClick={() => open("member")}>
                        Add a club member
                      </Button>
                    )}
                  </div>
                  <p className="subtitle">
                    Club organizers oversee all club meets. Meet organizers
                    manage only their assigned meets.
                  </p>
                  <div className="organizer-grid">
                    <div className="card">
                      <div className="card-title">Club organizers</div>
                      {members.map((u) => (
                        <div className="organizer-row" key={u.id}>
                          <span className="avatar">{initials(u.name)}</span>
                          <div>
                            <strong>{u.name}</strong>
                            <small>{u.email}</small>
                          </div>
                          {clubAdmin ? (
                            <select
                              aria-label={u.name + " club role"}
                              value={u.role}
                              onChange={async (e) => {
                                try {
                                  await api(
                                    "/clubs/" + clubId + "/roles",
                                    "PUT",
                                    { userId: u.id, role: e.target.value },
                                  );
                                  const cs = await api<Club[]>("/clubs");
                                  setClubs(cs);
                                  await reload();
                                } catch (err) {
                                  setError((err as Error).message);
                                }
                              }}
                            >
                              <option value="member">Member</option>
                              <option value="organizer">Club organizer</option>
                            </select>
                          ) : (
                            <span className="pill">{u.role}</span>
                          )}
                        </div>
                      ))}
                    </div>
                    <div className="card">
                      <div className="section-title">
                        <h2>Meet organizers</h2>
                        {clubAdmin && (
                          <Button primary onClick={() => open("organizers")}>
                            Assign
                          </Button>
                        )}
                      </div>
                      <p className="subtle">{meet.name} only</p>
                      {organizers.map((u) => (
                        <div className="organizer-row" key={u.id}>
                          <span className="avatar">{initials(u.name)}</span>
                          <div>
                            <strong>{u.name}</strong>
                            <small>Meet organizer</small>
                          </div>
                        </div>
                      ))}
                      {!organizers.length && (
                        <p className="subtitle">
                          No additional meet organizers. Club organizers can
                          already manage this meet.
                        </p>
                      )}
                    </div>
                  </div>
                </>
              )}
            </>
          )}
          <footer>
            <span>
              <b>r.</b> Made for the way you play.
            </span>
            <span>Rally · First application release</span>
          </footer>
        </main>
      </div>
      {modal && (
        <Dialog
          title={
            {
              club: "Create your club",
              create: "Plan your next meet",
              settings: "Meet settings",
              result: "Match result",
              substitute: "Substitute for this match",
              replace: "Add a replacement player",
              departure: "Player leaving early",
              arrival: "Late player / arrival",
              side: "Playing side",
              regenerateRemaining: "Regenerate remaining matches",
              extraRounds: "Generate extra rounds",
              generateMatch: "Generate a new match",
              regenerateAll: "Regenerate all matches",
              restartMatch: "Restart match",
              deleteMatch:
                modal.match?.status === "ready"
                  ? "Remove upcoming match"
                  : "Delete match",
              pairs: "Choose fixed partners",
              skill: "Edit player skill",
              organizers: "Assign meet organizers",
              member: "Add a club member",
            }[modal.kind] || ""
          }
          onClose={() => {
            setModal(null);
            setError("");
          }}
          onSubmit={submit}
          busy={busy}
          readOnly={modal.kind === "result" && !canManage}
          label={
            modal.kind === "generateMatch"
              ? "Generate match"
              : modal.kind === "regenerateAll"
                ? "Replace all matches"
                : modal.kind === "restartMatch"
                  ? "Restart match"
                  : modal.kind === "deleteMatch"
                    ? "Remove match"
                    : modal.kind === "extraRounds"
                      ? "Generate extra rounds"
                      : [
                            "departure",
                            "regenerateRemaining",
                            "arrival",
                            "side",
                          ].includes(modal.kind)
                        ? "Save & regenerate remaining"
                        : ["pairs", "settings"].includes(modal.kind)
                          ? "Save & generate"
                          : modal.kind === "create"
                            ? "Create meet"
                            : "Save"
          }
        >
          {error && (
            <p role="alert" className="error">
              {error}
            </p>
          )}
          {["departure", "regenerateRemaining"].includes(modal.kind) &&
            meet && (
              <div>
                {modal.kind === "departure" && (
                  <>
                    <p>
                      <strong>{modal.player?.name}</strong> will be excluded
                      from new matches starting with the selected round.
                    </p>
                    <Field label="Unavailable from round">
                      <select
                        name="fromRound"
                        defaultValue={Math.max(
                          remainingRound,
                          modal.player?.unavailableFromRound ?? remainingRound,
                        )}
                      >
                        {Array.from(
                          { length: meet.rounds - remainingRound },
                          (_, i) => remainingRound + i,
                        ).map((r) => (
                          <option key={r} value={r}>
                            Round {r + 1}
                            {meet.matches.find((m) => m.round === r)
                              ?.plannedStart
                              ? " · " +
                                meet.matches.find((m) => m.round === r)
                                  ?.plannedStart
                              : ""}
                          </option>
                        ))}
                      </select>
                    </Field>
                  </>
                )}
                <p className="hint">
                  Completed and live matches, scores and statistics stay intact.
                  An ongoing match can finish. Earlier rounds and eligible
                  substitutions stay in place; other unstarted matches are
                  rebuilt from round {remainingRound + 1} using previous match
                  counts and court bookings.
                </p>
                {meet.mode === "fixed" && (
                  <p className="hint">
                    Fixed pairs stay together. A partner without an available
                    teammate sits out; add a remaining-meet replacement from
                    Players to restore the pair.
                  </p>
                )}
                {meet.mode === "mixed" && (
                  <p className="hint">
                    If one fixed partner leaves, the remaining partner joins the
                    rotating pool.
                  </p>
                )}
                <p className="subtle">
                  There may be fewer matches or unused courts when fewer players
                  remain.
                </p>
              </div>
            )}
          {modal.kind === "generateMatch" && meet && (
            <>
              <p className="hint">
                Choose a court. We’ll generate one fair match, keeping every
                existing match unchanged. It joins the last round if players and
                court are unassigned there, otherwise a new round is added.
              </p>
              <Field label="Court">
                <select name="court" required>
                  {Array.from({ length: meet.courts }, (_, i) => (
                    <option value={i + 1} key={i}>
                      {courtName(i + 1)}
                    </option>
                  ))}
                </select>
              </Field>
              <p className="subtle">
                No fixed start time. Start when the court and players are free;
                booking times are guidance. Attendance, fixed pairs and strict
                playing sides still apply.
              </p>
            </>
          )}
          {modal.kind === "regenerateAll" && meet && (
            <>
              <div className="error" role="alert">
                <strong>This replaces the entire schedule.</strong>
                <p>
                  All {meet.matches.length} existing matches will be removed,
                  including{" "}
                  {meet.matches.filter((m) => m.status === "done").length}{" "}
                  completed and{" "}
                  {meet.matches.filter((m) => m.status === "live").length} live
                  matches. Scores, player statistics, substitutions and timers
                  will be cleared from the active meet. New matches will be
                  Upcoming.
                </p>
              </div>
              <p className="hint">
                Player attendance, fixed pairs, playing sides and meet
                configuration are kept. A before-change snapshot is retained in
                the audit history. Use Regenerate remaining to preserve played
                and live matches.
              </p>
              <label className="checkbox">
                <input type="checkbox" name="confirmed" required />I understand
                that all existing matches and their results will be replaced.
              </label>
            </>
          )}
          {["restartMatch", "deleteMatch"].includes(modal.kind) &&
            modal.match && (
              <>
                <p className="subtitle">
                  Round {modal.match.round + 1} · {courtName(modal.match.court)}{" "}
                  · {modal.match.teams.map(team).join(" vs ")}
                </p>
                <div className="error" role="alert">
                  {modal.kind === "restartMatch"
                    ? "Restart clears this match’s score, player statistics and timer, and returns it to Upcoming. Teams and court stay the same. Press Start match when ready."
                    : modal.match.status === "ready"
                      ? "Remove this upcoming match from the schedule? Other matches will stay unchanged."
                      : "Delete this match, including its score, player statistics and timer? Other matches will stay unchanged."}
                </div>
                <p className="subtle">
                  The previous meet version is retained in the audit history.
                </p>
                <label className="checkbox">
                  <input type="checkbox" name="confirmed" required />
                  Confirm{" "}
                  {modal.kind === "restartMatch" ? "restart" : "removal"} of
                  this match.
                </label>
              </>
            )}
          {modal.kind === "extraRounds" && meet && (
            <>
              <p className="hint">
                Append rounds after round {meet.rounds}. Existing matches,
                substitutions, scores and timers stay unchanged. New matches use
                previous participation, playing sides and attendance.
              </p>
              <div className="fields">
                <Field label="Extra rounds">
                  <input
                    type="number"
                    min="1"
                    max={20 - meet.rounds}
                    required
                    value={extraCount}
                    onChange={(e) => {
                      const count = Number(e.target.value);
                      setExtraCount(count);
                    }}
                  />
                </Field>
                <Field label="Total session minutes">
                  <input
                    type="number"
                    required
                    min={meet.durationMinutes}
                    max="480"
                    value={extraDuration}
                    onChange={(e) => setExtraDuration(Number(e.target.value))}
                  />
                </Field>
              </div>
              <p className="subtle">
                {meet.slotMinutes ||
                  Math.floor(meet.durationMinutes / meet.rounds)}{" "}
                minutes per round · {meet.rounds + extraCount} rounds in total.
                Extend the session and relevant court bookings if needed;
                existing round times will not move.
              </p>
              {meet.courtAvailability.length > 0 ? (
                <>
                  <h3 className="spaced-title">Court closing times</h3>
                  <p className="subtle">
                    Keep courts that close early unchanged. Booking times are
                    guidance and do not block extra rounds. Only extend a
                    booking if the court is available.
                  </p>
                  <div className="fields">
                    {meet.courtAvailability.map((c, i) => (
                      <Field key={i} label={c.name + " · opens " + c.start}>
                        <input
                          name={"courtEnd-" + i}
                          type="time"
                          required
                          min={c.end}
                          defaultValue={c.end}
                        />
                      </Field>
                    ))}
                  </div>
                </>
              ) : (
                <p className="hint">
                  All courts use the full session window, including any
                  extension you enter above.
                </p>
              )}
              <p className="subtle">
                Players marked waiting or departed stay out of automatic extra
                rounds. Limits: 20 total rounds, 480 minutes, within the same
                day.
              </p>
            </>
          )}
          {modal.kind === "arrival" && meet && (
            <>
              <p className="hint">
                {modal.player?.name} receives a fair share from arrival onward.
                Completed and live matches stay unchanged. Select “Waiting for
                arrival” to exclude this player until they arrive.
              </p>
              <Field label="Available from">
                <select
                  name="fromRound"
                  defaultValue={Math.max(
                    remainingRound,
                    modal.player?.availableFromRound || 0,
                  )}
                >
                  {Array.from(
                    { length: meet.rounds - remainingRound },
                    (_, i) => remainingRound + i,
                  ).map((r) => (
                    <option key={r} value={r}>
                      Round {r + 1}
                    </option>
                  ))}
                  <option value={meet.rounds}>Waiting for arrival</option>
                </select>
              </Field>
            </>
          )}
          {modal.kind === "side" && (
            <>
              <p className="hint">
                Left and Right are strict restrictions. Choose Both for a
                flexible player. Saving rebuilds compatible unstarted matches;
                completed matches stay unchanged.
              </p>
              <Field label="Playing side">
                <select name="side" defaultValue={modal.player?.side || "both"}>
                  <option value="both">Both</option>
                  <option value="left">Left only</option>
                  <option value="right">Right only</option>
                </select>
              </Field>
            </>
          )}
          {modal.kind === "club" && (
            <Field label="Club name">
              <input
                name="name"
                required
                maxLength={100}
                autoFocus
                placeholder="Weekend Padel Club"
              />
            </Field>
          )}
          {["create", "settings"].includes(modal.kind) && (
            <>
              <div className="fields">
                <Field label="Meet name" full>
                  <input
                    name="name"
                    required
                    maxLength={80}
                    defaultValue={modal.kind === "settings" ? meet?.name : ""}
                    placeholder="Saturday Social"
                    autoFocus
                  />
                </Field>
                <Field label="Venue" full>
                  <input
                    name="venue"
                    required
                    maxLength={150}
                    defaultValue={meet?.venue || ""}
                    placeholder="Your padel venue"
                  />
                </Field>
                <Field label="Date">
                  <input
                    type="date"
                    name="date"
                    required
                    defaultValue={
                      modal.kind === "settings"
                        ? meet?.date
                        : new Date().toLocaleDateString("en-CA")
                    }
                  />
                </Field>
                <Field label="Start time">
                  <input
                    type="time"
                    name="time"
                    required
                    value={draftStart}
                    onChange={(e) => setDraftStart(e.target.value)}
                  />
                </Field>
                <Field label="Courts">
                  <input
                    type="number"
                    name="courts"
                    required
                    min="1"
                    max="6"
                    defaultValue={meet?.courts || 2}
                  />
                </Field>
                <Field label="Rounds">
                  <input
                    type="number"
                    name="rounds"
                    required
                    min="1"
                    max="20"
                    defaultValue={meet?.rounds || 5}
                  />
                </Field>
                <Field label="Session minutes">
                  <input
                    type="number"
                    name="duration"
                    required
                    min="30"
                    max="480"
                    value={draftDuration}
                    onChange={(e) => setDraftDuration(e.target.value)}
                  />
                </Field>
                <Field label="Planned minutes per round">
                  <input
                    type="number"
                    name="slotMinutes"
                    min="5"
                    max="120"
                    required
                    defaultValue={
                      modal.kind === "settings"
                        ? meet?.slotMinutes ||
                          Math.floor(
                            (meet?.durationMinutes || 120) /
                              (meet?.rounds || 5),
                          )
                        : 24
                    }
                  />
                </Field>
                <div className="hint full" role="status">
                  Session:{" "}
                  <strong>
                    {draftStart || "—"}–{draftEnd}
                  </strong>{" "}
                  ·{" "}
                  {modal.kind === "settings" ? meet?.timezone : "Asia/Jakarta"}.
                  Court booking times are planning guidance. For the 18:00–21:00
                  example, set Start time to 18:00 and Session minutes to 180.
                </div>
                <Field label="Court availability · one court per line" full>
                  <textarea
                    name="courtAvailability"
                    rows={4}
                    defaultValue={
                      modal.kind === "settings"
                        ? meet?.courtAvailability
                            ?.map(
                              (c) => c.name + " | " + c.start + " | " + c.end,
                            )
                            .join("\n")
                        : ""
                    }
                    placeholder={
                      "Court A | 18:00 | 21:00\nCourt B | 18:00 | 21:00\nCourt C | 18:00 | 20:00"
                    }
                  />
                  <small>
                    Leave blank for all courts to use the full session.
                    Otherwise enter one line per court. Times use the meet
                    timezone. Round times are estimates; allow time for
                    changeovers.
                  </small>
                </Field>
                <Field label="Match format">
                  <select name="format" defaultValue={meet?.format || "4"}>
                    <option value="4">Race to 4 games</option>
                    <option value="6">Race to 6 games</option>
                    <option value="5">Best of 5 games</option>
                  </select>
                </Field>
                <Field label="Partner mode" full>
                  <select
                    value={draftMode}
                    onChange={(e) => setDraftMode(e.target.value)}
                  >
                    {Object.entries(modes).map(([v, t]) => (
                      <option key={v} value={v}>
                        {t}
                      </option>
                    ))}
                  </select>
                </Field>
                {modal.kind === "create" && (
                  <div className="field full">
                    <span>Import players from Reclub</span>
                    <input
                      aria-label="Reclub meet URL"
                      type="url"
                      value={reclubUrl}
                      onChange={(e) => setReclubUrl(e.target.value)}
                      placeholder="https://reclub.co/m/PATO94"
                    />
                    <Button
                      disabled={busy || !reclubUrl}
                      onClick={previewReclub}
                    >
                      Preview players
                    </Button>
                    {reclubPreview && (
                      <div className="import-preview">
                        <strong>{reclubPreview.name}</strong>
                        <p className="subtle">
                          Review the public roster. Confirmed players are
                          selected; other statuses require your review.
                          Importing does not link accounts.
                        </p>
                        {reclubPreview.players.map((p) => (
                          <label key={p.id} className="import-player">
                            <input
                              type="checkbox"
                              checked={importIds.includes(p.id)}
                              onChange={(e) =>
                                setImportIds(
                                  e.target.checked
                                    ? [...importIds, p.id]
                                    : importIds.filter((id) => id !== p.id),
                                )
                              }
                            />
                            <span>{p.name}</span>
                            <small>
                              {p.status === 1
                                ? "Confirmed"
                                : "Other status (" + p.status + ")"}
                            </small>
                          </label>
                        ))}
                        <Button
                          disabled={!importIds.length || busy}
                          onClick={() => {
                            setPlayersText(
                              reclubPreview.players
                                .filter((p) => importIds.includes(p.id))
                                .map((p) => p.name)
                                .join("\n"),
                            );
                            setToast(
                              "Selected players copied to the editable list below.",
                            );
                          }}
                        >
                          Use selected players ({importIds.length})
                        </Button>
                        <p className="subtle">
                          This replaces the draft player list below. Add skill
                          ratings there if skill balancing is enabled.
                        </p>
                      </div>
                    )}
                  </div>
                )}
                {modal.kind === "create" && (
                  <Field
                    label={
                      draftMode === "balanced"
                        ? "Players · one per line: Name | skill (1–5)"
                        : "Players · one name per line"
                    }
                    full
                  >
                    <textarea
                      name="players"
                      value={playersText}
                      onChange={(e) => setPlayersText(e.target.value)}
                      rows={7}
                      required
                      placeholder={
                        draftMode === "balanced"
                          ? "Ardy | 3.5\nNadia | 3\nRizky | 4\nSarah | 3"
                          : "Ardy\nNadia\nRizky\nSarah"
                      }
                    />
                  </Field>
                )}
                {modal.kind === "settings" &&
                  draftMode === "balanced" &&
                  meet!.players.map((p) => (
                    <Field key={p.id} label={p.name + " · skill"}>
                      <input
                        name={"skill-" + p.id}
                        type="number"
                        min="1"
                        max="5"
                        step="0.5"
                        required
                        defaultValue={p.skill ?? ""}
                      />
                    </Field>
                  ))}
              </div>
              <label className="checkbox">
                <input
                  name="timing"
                  type="checkbox"
                  defaultChecked={meet?.timing ?? true}
                />
                Enable optional start / end timing
              </label>
              {["fixed", "mixed"].includes(draftMode) && (
                <div className="hint">
                  {draftMode === "mixed"
                    ? "The first two players start as a fixed pair; everyone else rotates."
                    : "Players are initially paired in roster order."}{" "}
                  Choose your fixed pairs in Players after creating the meet.
                </div>
              )}
              {modal.kind === "settings" && locked && (
                <p className="error">
                  This meet has started or has substitutions. Its configuration
                  is protected; create a new meet for a different setup.
                </p>
              )}
            </>
          )}
          {modal.kind === "result" && modal.match && resultBody(modal.match)}
          {modal.kind === "substitute" && modal.match && (
            <>
              <div className="hint">
                Round {modal.match.round + 1} · Court {modal.match.court}.
                Choose any registered player. Selecting someone in this match
                swaps their positions. Selecting someone in another upcoming
                match in this round swaps both players between matches. Live and
                completed matches are never changed. Strict playing sides still
                apply.
              </div>
              <Field label="Player being replaced">
                <select
                  name="outgoing"
                  value={subOutgoing}
                  onChange={(e) => {
                    setSubOutgoing(e.target.value);
                    setSubIncoming("");
                  }}
                >
                  {modal.match.teams.flat().map((id) => (
                    <option key={id} value={id}>
                      {player(id)?.name} · {matchesPlayed(meet!, id)} played
                    </option>
                  ))}
                </select>
              </Field>
              <Field label="Registered substitute">
                <select
                  name="incoming"
                  required
                  value={subIncoming}
                  onChange={(e) => setSubIncoming(e.target.value)}
                >
                  <option value="" disabled>
                    Choose a player
                  </option>
                  {meet!.players.map((p) => (
                    <option
                      key={p.id}
                      value={p.id}
                      disabled={p.id === subOutgoing}
                    >
                      {p.name} · {matchesPlayed(meet!, p.id)} played
                      {p.id === subOutgoing
                        ? " · selected player"
                        : modal.match!.teams.flat().includes(p.id)
                          ? " · swap in this match"
                          : meet!.matches.some(
                                (m) =>
                                  m.id !== modal.match!.id &&
                                  m.round === modal.match!.round &&
                                  m.status === "ready" &&
                                  m.teams.flat().includes(p.id),
                              )
                            ? " · swap from another match"
                            : ""}
                      {!isAvailable(p, modal.match!.round)
                        ? " · attendance override"
                        : ""}
                    </option>
                  ))}
                </select>
              </Field>
              <p className="subtle">
                Other rounds stay unchanged. “Played” counts completed matches
                only. Add a new player from the Players list.
              </p>
            </>
          )}
          {modal.kind === "replace" && (
            <>
              <div className="hint">
                Register a new player for the next unstarted match or remaining
                meet. Previous participation and statistics stay with the
                original player. For someone already marked unavailable, choose
                remaining meet.
              </div>
              <Field label="Player being replaced">
                <select name="outgoing" required>
                  {meet!.players
                    .filter(
                      (p) =>
                        p.unavailableFromRound != null ||
                        meet!.matches.some(
                          (m) =>
                            m.status === "ready" &&
                            m.teams.flat().includes(p.id),
                        ),
                    )
                    .map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name}
                      </option>
                    ))}
                </select>
              </Field>
              <Field label="Playing side">
                <select name="side" defaultValue="both">
                  <option value="both">Both</option>
                  <option value="left">Left only</option>
                  <option value="right">Right only</option>
                </select>
              </Field>
              <Field label="New player's name">
                <input name="name" required maxLength={80} />
              </Field>
              {meet!.mode === "balanced" && (
                <Field label="Skill">
                  <input
                    type="number"
                    name="skill"
                    min="1"
                    max="5"
                    step="0.5"
                    required
                  />
                </Field>
              )}
              <Field label="Replacement scope">
                <select name="scope">
                  <option value="one">Next unstarted match only</option>
                  <option value="meet">Remaining meet</option>
                </select>
              </Field>
            </>
          )}
          {modal.kind === "skill" && (
            <>
              <p className="subtitle">
                {modal.player?.name}. Existing assignments stay unchanged;
                regenerate an unstarted schedule to use this rating.
              </p>
              <Field label="Skill level (1–5)">
                <input
                  name="skill"
                  type="number"
                  min="1"
                  max="5"
                  step="0.5"
                  required
                  defaultValue={modal.player?.skill ?? ""}
                />
              </Field>
            </>
          )}
          {modal.kind === "pairs" && (
            <>
              <div className="hint">
                Fixed partners play and rest together.{" "}
                {meet!.mode === "mixed"
                  ? "Leave at least two players in the rotating pool."
                  : "Assign every player to a pair."}{" "}
                Saving regenerates the schedule.
              </div>
              {Array.from(
                {
                  length: Math.floor(
                    (meet!.players.length - (meet!.mode === "mixed" ? 2 : 0)) /
                      2,
                  ),
                },
                (_, i) => (
                  <div className="fields" key={i}>
                    {["a", "b"].map((k, j) => (
                      <Field
                        key={k}
                        label={"Pair " + (i + 1) + " · player " + (j + 1)}
                      >
                        <select
                          name={k + i}
                          defaultValue={meet!.fixedPairs[i]?.[j] || ""}
                        >
                          <option value="">No selection</option>
                          {meet!.players.map((p) => (
                            <option key={p.id} value={p.id}>
                              {p.name}
                            </option>
                          ))}
                        </select>
                      </Field>
                    ))}
                  </div>
                ),
              )}
            </>
          )}
          {modal.kind === "organizers" && (
            <>
              <div className="hint">
                Assign registered club members to manage this meet. A player can
                also hold this role.
              </div>
              {members.map((u) => (
                <label className="checkbox" key={u.id}>
                  <input
                    type="checkbox"
                    name="organizer"
                    value={u.id}
                    defaultChecked={organizers.some((o) => o.id === u.id)}
                  />
                  {u.name}
                  <span className="subtle">{u.email}</span>
                </label>
              ))}
            </>
          )}
          {modal.kind === "member" && (
            <>
              <div className="hint">
                Add someone who has already created a Rally account. This grants
                club access; no invitation email is sent.
              </div>
              <Field label="Registered email">
                <input type="email" name="email" required />
              </Field>
              <Field label="Club role">
                <select name="role">
                  <option value="member">Member</option>
                  <option value="organizer">Club organizer</option>
                </select>
              </Field>
            </>
          )}
        </Dialog>
      )}
      {toast && (
        <div role="status" className="toast">
          {toast}
        </div>
      )}
    </div>
  );
}
