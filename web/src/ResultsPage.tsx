import { useEffect, useState } from "react";
import { api } from "./api";
import { standings, inPeriod } from "./results";
import type { ResultMeet, Standing } from "./results";
function ResultsTable({ rows }: { rows: Standing[] }) {
  return rows.length ? (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            {[
              "Rank",
              "Player",
              "Played",
              "Won",
              "Lost",
              "Win %",
              "Games + / −",
              "Difference",
              "Meets",
              "Winners",
              "Errors",
              "Padel ratio",
            ].map((t) => (
              <th key={t}>{t}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.key}>
              <td>{r.played ? r.rank : "—"}</td>
              <td>
                <strong>{r.name}</strong>
              </td>
              <td>{r.played}</td>
              <td>{r.wins}</td>
              <td>{r.losses}</td>
              <td>
                {r.played ? Math.round((r.wins / r.played) * 100) + "%" : "—"}
              </td>
              <td>
                {r.gamesFor} / {r.gamesAgainst}
              </td>
              <td>{r.gamesFor - r.gamesAgainst}</td>
              <td>{r.meets}</td>
              <td>{r.winners ?? "—"}</td>
              <td>{r.errors ?? "—"}</td>
              <td>{r.ratio ?? "—"}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  ) : (
    <p className="empty">No completed matches in this period yet.</p>
  );
}
const note =
  "Completed matches only. Ranked by wins, then game difference, then games won. Optional statistics total entered values only; padel ratio includes matches with both winners and errors entered.";
export function MeetResults({ meet }: { meet: ResultMeet }) {
  return (
    <section className="card">
      <div className="section-title">
        <h2>Meet results · {meet.name}</h2>
        <span>
          {meet.matches.filter((m) => m.status === "done").length} completed
          matches
        </span>
      </div>
      <p className="subtle">{note}</p>
      <ResultsTable rows={standings([meet])} />
    </section>
  );
}
export function ClubResults({
  clubId,
  onOpen,
}: {
  clubId: string;
  onOpen: (id: string) => void;
}) {
  const [data, setData] = useState<ResultMeet[] | null>(null),
    [error, setError] = useState(""),
    [period, setPeriod] = useState("week"),
    [retry, setRetry] = useState(0);
  useEffect(() => {
    let active = true;
    setData(null);
    setError("");
    api<ResultMeet[]>("/clubs/" + clubId + "/results")
      .then((x) => {
        if (active) setData(x);
      })
      .catch((e) => {
        if (active) setError(e.message);
      });
    return () => {
      active = false;
    };
  }, [clubId, retry]);
  if (error)
    return (
      <div className="card">
        <p role="alert">{error}</p>
        <button className="button" onClick={() => setRetry((x) => x + 1)}>
          Retry
        </button>
      </div>
    );
  if (!data) return <p role="status">Loading club results…</p>;
  const selected = data.filter((m) => inPeriod(m, period));
  return (
    <>
      <div className="section-title">
        <h2>Club history & rankings</h2>
        <button className="button" onClick={() => setRetry((x) => x + 1)}>
          Refresh
        </button>
      </div>
      <div className="tabs" aria-label="Ranking period">
        {[
          ["week", "This week"],
          ["month", "This month"],
          ["all", "All time"],
        ].map(([value, label]) => (
          <button
            key={value}
            className={period === value ? "selected" : ""}
            onClick={() => setPeriod(value)}
          >
            {label}
          </button>
        ))}
      </div>
      <section className="card">
        <h3>Player rankings</h3>
        <p className="subtle">
          {note} Weeks start Monday; dates follow each meet’s time zone.
        </p>
        <p className="subtle">
          Linked accounts are combined across meets. Unlinked players with the
          same name are combined within this club; use distinct names for
          different people.
        </p>
        <ResultsTable rows={standings(selected, true, true)} />
      </section>
      <section className="card">
        <h3>Meet history</h3>
        {!selected.length ? (
          <p>No meets in this period.</p>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Meet</th>
                  <th>Players</th>
                  <th>Completed</th>
                  <th>Status</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {selected.map((m) => {
                  const done = m.matches.filter(
                    (x) => x.status === "done",
                  ).length;
                  return (
                    <tr key={m.id}>
                      <td>{m.date}</td>
                      <td>{m.name}</td>
                      <td>{m.players.length}</td>
                      <td>
                        {done} / {m.matches.length}
                      </td>
                      <td>
                        {m.matches.length && done === m.matches.length
                          ? "Finished"
                          : m.matches.some((x) => x.status !== "ready")
                            ? "In progress"
                            : "Upcoming"}
                      </td>
                      <td>
                        <button className="button" onClick={() => onOpen(m.id)}>
                          View results
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </>
  );
}
