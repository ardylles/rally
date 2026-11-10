import { useState } from "react";
import type { FormEvent } from "react";
import type { ClubPlayer, Meet } from "./types";
import { api } from "./api";
import { SortableTable } from "./SortableTable";
export function ClubPlayersPage({
  clubId,
  profiles,
  organizer,
  meet,
  onReload,
  onMeet,
}: {
  clubId: string;
  profiles: ClubPlayer[];
  organizer: boolean;
  meet: Meet | null;
  onReload: () => Promise<void>;
  onMeet: (m: Meet) => void;
}) {
  const [source, setSource] = useState("");
  const sourcePlayer = meet?.players.find((p) => p.id === source);
  const [editing, setEditing] = useState<ClubPlayer | null | undefined>(),
    [linking, setLinking] = useState<ClubPlayer | null>(null),
    [photo, setPhoto] = useState<string | undefined>(),
    [photoBusy, setPhotoBusy] = useState(false),
    [busy, setBusy] = useState(false),
    [error, setError] = useState(""),
    [notice, setNotice] = useState(""),
    [selected, setSelected] = useState<string[]>([]);
  async function perform(work: () => Promise<void>) {
    setBusy(true);
    setError("");
    setNotice("");
    try {
      await work();
      await onReload();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  function edit(p: ClubPlayer | null) {
    setSource("");
    setEditing(p);
    setPhoto(undefined);
    setError("");
    setLinking(null);
  }
  async function image(file: File | undefined) {
    if (!file) return;
    setPhotoBusy(true);
    setError("");
    try {
      if (
        !["image/jpeg", "image/png", "image/webp"].includes(file.type) ||
        file.size > 10 * 1024 * 1024
      )
        throw Error("Choose a JPEG, PNG or WebP image under 10 MB.");
      const bitmap = await createImageBitmap(file);
      const canvas = document.createElement("canvas");
      const scale = Math.min(1, 256 / Math.max(bitmap.width, bitmap.height));
      canvas.width = Math.max(1, Math.round(bitmap.width * scale));
      canvas.height = Math.max(1, Math.round(bitmap.height * scale));
      const ctx = canvas.getContext("2d")!;
      ctx.fillStyle = "white";
      ctx.fillRect(0, 0, canvas.width, canvas.height);
      ctx.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
      bitmap.close();
      setPhoto(canvas.toDataURL("image/jpeg", 0.85));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setPhotoBusy(false);
    }
  }
  function save(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    void perform(async () => {
      await api(
        "/clubs/" + clubId + "/players" + (editing ? "/" + editing.id : ""),
        editing ? "PUT" : "POST",
        {
          revision: editing?.revision ?? 0,
          name: f.get("name"),
          reclubName: f.get("reclubName"),
          reclubUrl: f.get("reclubUrl"),
          side: f.get("side"),
          skill: f.get("skill") ? Number(f.get("skill")) : null,
          photoData: photo,
        },
      );
      setEditing(undefined);
      setNotice("Club player saved.");
    });
  }
  return (
    <>
      <div className="section-title">
        <h2>Club players</h2>
        <div className="row-actions">
          <button
            className="button"
            disabled={busy}
            onClick={() => void perform(async () => {})}
          >
            Refresh
          </button>
          {organizer && (
            <button
              className="button primary"
              disabled={busy || photoBusy}
              onClick={() => edit(null)}
            >
              Add club player
            </button>
          )}
        </div>
      </div>
      <p className="subtle">
        Create profiles before players register. Club names, playing sides and
        skills are used when adding them to new meets. Profile edits leave
        existing match records unchanged.
      </p>
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
      {notice && <p role="status">{notice}</p>}
      {editing !== undefined && organizer && (
        <section className="card">
          {!editing && meet && (
            <label className="field">
              <span>Start from a meet player (optional)</span>
              <select
                value={source}
                onChange={(e) => setSource(e.target.value)}
              >
                <option value="">New player</option>
                {meet.players
                  .filter((p) => !p.clubPlayerId)
                  .map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name}
                    </option>
                  ))}
              </select>
              <small>
                After saving, use Connect an existing meet player below to
                attach their history.
              </small>
            </label>
          )}
          <form key={editing?.id ?? "new-" + source} onSubmit={save}>
            <h3>{editing ? "Edit player" : "Add club player"}</h3>
            <div className="fields">
              <label className="field">
                <span>Club display name</span>
                <input
                  name="name"
                  required
                  maxLength={80}
                  defaultValue={editing?.name ?? sourcePlayer?.name}
                />
              </label>
              <label className="field">
                <span>Reclub name (optional)</span>
                <input
                  name="reclubName"
                  maxLength={80}
                  defaultValue={editing?.reclubName}
                />
              </label>
              <label className="field full">
                <span>Reclub profile link (optional)</span>
                <input
                  name="reclubUrl"
                  type="url"
                  placeholder="https://reclub.co/players/@username"
                  defaultValue={
                    editing?.reclubUrl ?? sourcePlayer?.reclubProfileUrl ?? ""
                  }
                />
              </label>
              <label className="field">
                <span>Playing side</span>
                <select
                  name="side"
                  defaultValue={editing?.side ?? sourcePlayer?.side ?? "both"}
                >
                  <option value="both">Both</option>
                  <option value="left">Left only</option>
                  <option value="right">Right only</option>
                </select>
              </label>
              <label className="field">
                <span>Skill (optional)</span>
                <input
                  name="skill"
                  type="number"
                  min="1"
                  max="5"
                  step=".5"
                  defaultValue={editing?.skill ?? sourcePlayer?.skill ?? ""}
                />
              </label>
              <label className="field full">
                <span>Profile photo (optional)</span>
                <input
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  disabled={busy || photoBusy}
                  onChange={(e) => void image(e.target.files?.[0])}
                />
              </label>
            </div>
            <p className="subtle">
              Left and Right are strict restrictions. Choose Both for
              flexibility. Photos are resized before saving and visible only to
              club members.
            </p>
            {(photo || (photo === undefined && editing?.photoUrl)) && (
              <img
                className="club-avatar"
                src={photo ?? editing!.photoUrl!}
                alt="Profile preview"
              />
            )}
            <div className="row-actions">
              <button
                type="button"
                className="button"
                disabled={busy || photoBusy}
                onClick={() => setPhoto("")}
              >
                Remove photo
              </button>
              <button
                type="button"
                className="button"
                disabled={busy || photoBusy}
                onClick={() => setEditing(undefined)}
              >
                Cancel
              </button>
              <button className="button primary" disabled={busy || photoBusy}>
                {photoBusy
                  ? "Preparing photo…"
                  : busy
                    ? "Saving…"
                    : "Save player"}
              </button>
            </div>
          </form>
        </section>
      )}
      {linking && organizer && (
        <section className="card">
          <form
            key={linking.id}
            onSubmit={(e) => {
              e.preventDefault();
              const email = new FormData(e.currentTarget).get("email");
              void perform(async () => {
                await api(
                  "/clubs/" + clubId + "/players/" + linking.id + "/account",
                  "PUT",
                  { revision: linking.revision, email },
                );
                setLinking(null);
                setNotice(
                  email
                    ? "Account linked; the user now has club member access."
                    : "Account unlinked. Existing club access is unchanged.",
                );
              });
            }}
          >
            <h3>Link account · {linking.name}</h3>
            <label className="field">
              <span>Registered account email</span>
              <input
                name="email"
                type="email"
                defaultValue={linking.email ?? ""}
              />
            </label>
            <p className="subtle">
              The player must register first. Linking grants member access, not
              organizer access. Leave empty to unlink; this does not remove
              existing club membership.
            </p>
            <div className="row-actions">
              <button
                className="button"
                type="button"
                disabled={busy}
                onClick={() => setLinking(null)}
              >
                Cancel
              </button>
              <button className="button primary" disabled={busy}>
                Save account link
              </button>
            </div>
          </form>
        </section>
      )}
      {organizer && meet && (
        <section className="card">
          <h3>Add selected profiles to {meet.name}</h3>
          <p className="subtle">
            Select{" "}
            {meet.mode === "fixed"
              ? "two players for a new fixed pair"
              : "one player"}{" "}
            below.
          </p>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              const regenerate =
                new FormData(e.currentTarget).get("regenerate") === "on";
              void perform(async () => {
                onMeet(
                  await api<Meet>(
                    "/meets/" + meet.id + "/club-players",
                    "POST",
                    {
                      revision: meet.revision,
                      profileIds: selected,
                      regenerate,
                    },
                  ),
                );
                setSelected([]);
                setNotice("Club player added to the meet.");
              });
            }}
          >
            <label>
              <input type="checkbox" name="regenerate" defaultChecked />{" "}
              Regenerate remaining matches
            </label>
            <button
              className="button"
              disabled={
                busy || selected.length !== (meet.mode === "fixed" ? 2 : 1)
              }
            >
              Add to current meet
            </button>
          </form>
        </section>
      )}
      <section className="card">
        {!profiles.length ? (
          <p>No club players yet. Add a profile to get started.</p>
        ) : (
          <div className="table-wrap">
            <SortableTable>
              <thead>
                <tr>
                  <th></th>
                  <th>Player</th>
                  <th>Reclub</th>
                  <th>Side</th>
                  <th>Skill</th>
                  <th>Account</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {profiles.map((p) => (
                  <tr key={p.id}>
                    <td>
                      {organizer && meet && (
                        <input
                          type="checkbox"
                          aria-label={"Select " + p.name}
                          checked={selected.includes(p.id)}
                          disabled={
                            busy ||
                            meet.players.some((x) => x.clubPlayerId === p.id)
                          }
                          onChange={(e) =>
                            setSelected(
                              e.target.checked
                                ? [...selected, p.id]
                                : selected.filter((id) => id !== p.id),
                            )
                          }
                        />
                      )}
                    </td>
                    <td>
                      <div className="club-player-name">
                        {p.photoUrl && (
                          <img
                            className="club-avatar"
                            src={p.photoUrl}
                            alt=""
                          />
                        )}
                        <strong>{p.name}</strong>
                      </div>
                    </td>
                    <td>
                      {p.reclubUrl ? (
                        <a href={p.reclubUrl} target="_blank" rel="noreferrer">
                          {p.reclubName || "View profile"}
                        </a>
                      ) : (
                        "—"
                      )}
                    </td>
                    <td>{p.side}</td>
                    <td>{p.skill ?? "—"}</td>
                    <td>{p.email ?? (p.userId ? "Linked" : "Not linked")}</td>
                    <td>
                      {organizer && (
                        <div className="row-actions">
                          <button
                            className="button"
                            disabled={busy || photoBusy}
                            onClick={() => edit(p)}
                          >
                            Edit
                          </button>
                          <button
                            className="button"
                            disabled={busy || photoBusy}
                            onClick={() => {
                              setLinking(p);
                              setEditing(undefined);
                              setError("");
                            }}
                          >
                            Link account
                          </button>
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </SortableTable>
          </div>
        )}
      </section>
      {organizer && meet && profiles.length > 0 && (
        <section className="card">
          <h3>Connect an existing meet player</h3>
          <p className="subtle">
            Attach the selected meet player to a club profile for future history
            and rankings. Their recorded name, playing side, scores and match
            assignments stay unchanged.
          </p>
          <form
            className="fields"
            onSubmit={(e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              void perform(async () => {
                onMeet(
                  await api<Meet>(
                    "/meets/" +
                      meet.id +
                      "/players/" +
                      f.get("player") +
                      "/profile",
                    "PUT",
                    { revision: meet.revision, clubPlayerId: f.get("profile") },
                  ),
                );
                setNotice("Meet player linked to club profile.");
              });
            }}
          >
            <label className="field">
              <span>Meet player</span>
              <select name="player">
                {meet.players.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="field">
              <span>Club player</span>
              <select name="profile">
                {profiles.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </select>
            </label>
            <button className="button" disabled={busy}>
              Connect profile
            </button>
          </form>
        </section>
      )}
    </>
  );
}
