import { useEffect, useState } from "react";
import { api } from "./api";
import type { Meet, User } from "./types";

type PublicMeet = Pick<
  Meet,
  | "name"
  | "venue"
  | "date"
  | "time"
  | "timezone"
  | "rounds"
  | "courtAvailability"
  | "matches"
> & { players: { id: string; name: string }[] };
export function PublicMeetPage({ token }: { token: string }) {
  const [meet, setMeet] = useState<PublicMeet | null>(null),
    [error, setError] = useState("");
  useEffect(() => {
    let active = true;
    const refresh = () =>
      api<PublicMeet>("/public/" + encodeURIComponent(token))
        .then((m) => {
          if (active) {
            setMeet(m);
            setError("");
          }
        })
        .catch((e) => {
          if (active) {
            setMeet(null);
            setError(e.message);
          }
        });
    void refresh();
    const timer = setInterval(refresh, 15000);
    return () => {
      active = false;
      clearInterval(timer);
    };
  }, [token]);
  return (
    <main className="public-page">
      <a className="brand" href="/">
        rally.
      </a>
      {error ? (
        <p role="alert" className="error">
          {error}
        </p>
      ) : !meet ? (
        <p>Loading matches…</p>
      ) : (
        <>
          <div className="page-heading">
            <div>
              <div className="eyebrow">PUBLIC MEET · READ ONLY</div>
              <h1>{meet.name}</h1>
              <p>
                {meet.date} · {meet.time} · {meet.timezone} · {meet.venue}
              </p>
            </div>
          </div>
          <p className="subtle">Updates every 15 seconds.</p>
          {Array.from({ length: meet.rounds }, (_, r) => (
            <section className="schedule-round" key={r}>
              <h2>Round {r + 1}</h2>
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Court / time</th>
                      <th>Teams</th>
                      <th>Status / score</th>
                    </tr>
                  </thead>
                  <tbody>
                    {meet.matches
                      .filter((m) => m.round === r)
                      .map((m) => (
                        <tr key={m.id}>
                          <td>
                            {meet.courtAvailability[m.court - 1]?.name ||
                              "Court " + m.court}
                            <div className="subtle">
                              {m.plannedStart}–{m.plannedEnd}
                            </div>
                          </td>
                          <td>
                            {m.teams
                              .map((t) =>
                                t
                                  .map(
                                    (id) =>
                                      meet.players.find((p) => p.id === id)
                                        ?.name || "Player",
                                  )
                                  .join(" & "),
                              )
                              .join(" vs ")}
                          </td>
                          <td>
                            {m.status === "done"
                              ? m.score?.join(" – ")
                              : m.status === "live"
                                ? "On court"
                                : "Upcoming"}
                          </td>
                        </tr>
                      ))}
                  </tbody>
                </table>
              </div>
              {!meet.matches.some((m) => m.round === r) && (
                <p>No matches planned.</p>
              )}
            </section>
          ))}
        </>
      )}
    </main>
  );
}
export function Sharing({
  meet,
  onChange,
}: {
  meet: Meet;
  onChange: (m: Meet) => void;
}) {
  const [path, setPath] = useState<string | null>(null),
    [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false);
  useEffect(() => {
    let active = true;
    api<{ path: string | null }>("/meets/" + meet.id + "/sharing")
      .then((s) => {
        if (active) setPath(s.path);
      })
      .catch((e) => {
        if (active) setMessage(e.message);
      });
    return () => {
      active = false;
    };
  }, [meet.id, meet.revision]);
  async function save(enabled: boolean, reset = false) {
    if (
      reset &&
      !window.confirm(
        "Reset the public link? The previous link will stop working.",
      )
    )
      return;
    setBusy(true);
    setMessage("");
    try {
      onChange(
        await api<Meet>("/meets/" + meet.id + "/sharing", "PUT", {
          revision: meet.revision,
          enabled,
          reset,
        }),
      );
    } catch (e) {
      setMessage((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  const url = path ? window.location.origin + path : "";
  return (
    <section className="card feature-card">
      <h3>Public match view</h3>
      <p>
        Anyone with the link can view player display names, courts, matches and
        scores without signing in.
      </p>
      <label className="checkbox">
        <input
          type="checkbox"
          checked={meet.publicEnabled || false}
          disabled={busy}
          onChange={(e) => void save(e.target.checked)}
        />
        Enable public view
      </label>
      {meet.publicEnabled && path && (
        <>
          <input aria-label="Public meet link" value={url} readOnly />
          <div className="row-actions">
            <button
              className="button"
              disabled={busy}
              onClick={async () => {
                try {
                  await navigator.clipboard.writeText(url);
                  setMessage("Public link copied.");
                } catch {
                  setMessage("Select and copy the link above.");
                }
              }}
            >
              Share public link
            </button>
            <a className="button" href={path} target="_blank" rel="noreferrer">
              Open public view
            </a>
            <button
              className="button"
              disabled={busy}
              onClick={() => void save(true, true)}
            >
              Reset link
            </button>
          </div>
        </>
      )}
      {message && <p role="status">{message}</p>}
    </section>
  );
}
type AdminUser = User & { disabled: boolean };
type AdminMeet = { id: string; name: string; club: string };
type Audit = { action: string; actor: string; target: string; at: string };
export function AdminPanel({
  onMeetDeleted,
}: {
  onMeetDeleted: (id: string) => void;
}) {
  const [users, setUsers] = useState<AdminUser[]>([]),
    [meets, setMeets] = useState<AdminMeet[]>([]),
    [audit, setAudit] = useState<Audit[]>([]),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false);
  async function load() {
    const [u, m, a] = await Promise.all([
      api<AdminUser[]>("/admin/users"),
      api<AdminMeet[]>("/admin/meets"),
      api<Audit[]>("/admin/audit"),
    ]);
    setUsers(u);
    setMeets(m);
    setAudit(a);
  }
  useEffect(() => {
    void load().catch((e) => setError(e.message));
  }, []);
  async function act(
    path: string,
    method: string,
    prompt: string,
    body?: unknown,
    meetId?: string,
  ) {
    if (!window.confirm(prompt)) return;
    setBusy(true);
    setError("");
    try {
      await api(path, method, body);
      if (meetId) onMeetDeleted(meetId);
      await load();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section>
      <h1>Application administration</h1>
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
      <h2>Users</h2>
      <p>
        Deleting an account preserves participant names, match history and
        statistics. Disabling immediately blocks access and invalidates existing
        sessions.
      </p>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>User</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.id}>
                <td>
                  {u.name}
                  <div className="subtle">{u.email}</div>
                </td>
                <td>
                  {u.isAdmin ? "Administrator · " : ""}
                  {u.disabled ? "Disabled" : "Active"}
                </td>
                <td>
                  <div className="row-actions">
                    <button
                      className="button"
                      disabled={busy}
                      onClick={() =>
                        void act(
                          "/admin/users/" + u.id,
                          "PUT",
                          (u.disabled ? "Re-enable " : "Disable ") +
                            u.email +
                            "?",
                          { disabled: !u.disabled },
                        )
                      }
                    >
                      {u.disabled ? "Re-enable" : "Disable"}
                    </button>
                    <button
                      className="button"
                      disabled={busy}
                      onClick={() =>
                        void act(
                          "/admin/users/" + u.id,
                          "DELETE",
                          "Permanently delete account " +
                            u.email +
                            "? Participant records will remain.",
                        )
                      }
                    >
                      Delete account
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <h2 className="spaced-title">Meets</h2>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Meet</th>
              <th>Club</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {meets.map((m) => (
              <tr key={m.id}>
                <td>{m.name}</td>
                <td>{m.club}</td>
                <td>
                  <button
                    className="button"
                    disabled={busy}
                    onClick={() =>
                      void act(
                        "/admin/meets/" + m.id,
                        "DELETE",
                        "Permanently delete " +
                          m.name +
                          " and its match history? This also revokes its public link.",
                        undefined,
                        m.id,
                      )
                    }
                  >
                    Delete meet
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <h2 className="spaced-title">Administration history</h2>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>When</th>
              <th>Administrator</th>
              <th>Action</th>
              <th>Target</th>
            </tr>
          </thead>
          <tbody>
            {audit.map((a, i) => (
              <tr key={i}>
                <td>{new Date(a.at).toLocaleString()}</td>
                <td>{a.actor}</td>
                <td>{a.action}</td>
                <td>{a.target}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
