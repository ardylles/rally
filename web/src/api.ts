export async function api<T>(
  path: string,
  method = "GET",
  body?: unknown,
): Promise<T> {
  const r = await fetch("/api" + path, {
    method,
    credentials: "same-origin",
    headers:
      method === "GET"
        ? {}
        : { "Content-Type": "application/json", "X-Rally-Request": "1" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!r.ok) {
    const p = await r.json().catch(() => ({ message: "Request failed." }));
    throw new Error(p.message || p.title || "Request failed.");
  }
  return r.status === 204 ? (undefined as T) : r.json();
}
