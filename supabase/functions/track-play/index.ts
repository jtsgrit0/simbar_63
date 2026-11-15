// Public app observations, not proof of a person's identity or listening.
// No dependencies, user login, or service credential is shipped to the client.
const QUALIFY_MS = 30_000;
const TICKET_LIFETIME_MS = 24 * 60 * 60 * 1000;
const MAX_BODY_BYTES = 4096;
const TRACK_IDS = new Set([
  "track_001", "track_002", "track_003", "track_004", "track_005",
]);
const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const encoder = new TextEncoder();
const decoder = new TextDecoder("utf-8", { fatal: true });

type Source = "web" | "windows";
type Ticket = {
  v: 1;
  playId: string;
  trackId: string;
  source: Source;
  issuedAt: number;
};
type JsonRecord = Record<string, unknown>;

function object(value: unknown): value is JsonRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function defaultKey(name: string): string | undefined {
  try {
    const keys: unknown = JSON.parse(Deno.env.get(name) || "{}");
    return object(keys) && typeof keys.default === "string" && keys.default
      ? keys.default
      : undefined;
  } catch {
    return undefined;
  }
}

const publishableKey = defaultKey("SUPABASE_PUBLISHABLE_KEYS");
const serverKey = defaultKey("SUPABASE_SECRET_KEYS") || Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
const supabaseUrl = Deno.env.get("SUPABASE_URL")?.replace(/\/$/, "");

// Domain separation prevents signatures being interchangeable with any other
// use of the server key. A dedicated secret may be rotated independently.
const ticketKey = (async () => {
  const secret = Deno.env.get("TRACK_PLAY_TOKEN_SECRET") || serverKey;
  if (!secret) return null;
  const material = await crypto.subtle.digest(
    "SHA-256", encoder.encode(`SIMBAR/track-play/ticket/v1\0${secret}`),
  );
  return crypto.subtle.importKey("raw", material, { name: "HMAC", hash: "SHA-256" }, false, [
    "sign", "verify",
  ]);
})();

function allowedOrigin(origin: string | null): boolean {
  if (origin === null) return true; // Server-to-server calls have no CORS origin.
  if (origin === "https://jtsgrit0.github.io" || origin === "simbar://app") return true;
  try {
    const url = new URL(origin);
    return url.origin === origin && ["http:", "https:"].includes(url.protocol) &&
      ["localhost", "127.0.0.1", "[::1]"].includes(url.hostname);
  } catch {
    return false;
  }
}

function headers(origin: string | null): Headers {
  const result = new Headers({
    "content-type": "application/json; charset=utf-8",
    "cache-control": "no-store",
    "vary": "Origin",
    "access-control-allow-methods": "POST, OPTIONS",
    "access-control-allow-headers": "apikey, content-type, authorization, x-client-info",
    "access-control-expose-headers": "Retry-After",
    "access-control-max-age": "600",
  });
  if (origin && allowedOrigin(origin)) result.set("access-control-allow-origin", origin);
  return result;
}

function json(origin: string | null, status: number, body: unknown, retryAfter?: number): Response {
  const responseHeaders = headers(origin);
  if (retryAfter !== undefined) responseHeaders.set("Retry-After", String(retryAfter));
  return new Response(JSON.stringify(body), { status, headers: responseHeaders });
}

async function sameKey(actual: string | null, expected: string): Promise<boolean> {
  if (!actual || actual.length > 1024) return false;
  const [left, right] = await Promise.all([
    crypto.subtle.digest("SHA-256", encoder.encode(actual)),
    crypto.subtle.digest("SHA-256", encoder.encode(expected)),
  ]);
  const a = new Uint8Array(left);
  const b = new Uint8Array(right);
  let difference = 0;
  for (let index = 0; index < a.length; index++) difference |= a[index] ^ b[index];
  return difference === 0;
}

function base64url(bytes: Uint8Array): string {
  return btoa(String.fromCharCode(...bytes)).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function fromBase64url(value: string): Uint8Array {
  if (!/^[A-Za-z0-9_-]+$/.test(value)) throw new Error("invalid base64url");
  const padded = value.replace(/-/g, "+").replace(/_/g, "/").padEnd(Math.ceil(value.length / 4) * 4, "=");
  return Uint8Array.from(atob(padded), (char) => char.charCodeAt(0));
}

async function signTicket(ticket: Ticket, key: CryptoKey): Promise<string> {
  const payload = base64url(encoder.encode(JSON.stringify(ticket)));
  const signature = await crypto.subtle.sign("HMAC", key, encoder.encode(payload));
  return `${payload}.${base64url(new Uint8Array(signature))}`;
}

async function readTicket(value: string, key: CryptoKey): Promise<Ticket | null> {
  try {
    const parts = value.split(".");
    if (parts.length !== 2 || value.length > 2048) return null;
    const signature = fromBase64url(parts[1]);
    if (signature.length !== 32) return null;
    const valid = await crypto.subtle.verify("HMAC", key, signature, encoder.encode(parts[0]));
    if (!valid) return null;
    const ticket: unknown = JSON.parse(decoder.decode(fromBase64url(parts[0])));
    if (!object(ticket) || ticket.v !== 1 || typeof ticket.playId !== "string" ||
      !UUID_RE.test(ticket.playId) || typeof ticket.trackId !== "string" ||
      !TRACK_IDS.has(ticket.trackId) || !["web", "windows"].includes(String(ticket.source)) ||
      typeof ticket.issuedAt !== "number" || !Number.isSafeInteger(ticket.issuedAt)) return null;
    return ticket as Ticket;
  } catch {
    return null;
  }
}

async function readJson(request: Request): Promise<JsonRecord | null> {
  if (!request.headers.get("content-type")?.toLowerCase().startsWith("application/json")) return null;
  const advertisedSize = request.headers.get("content-length");
  if (advertisedSize && Number(advertisedSize) > MAX_BODY_BYTES) return null;
  const reader = request.body?.getReader();
  if (!reader) return null;
  let size = 0;
  const chunks: Uint8Array[] = [];
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      size += value.byteLength;
      if (size > MAX_BODY_BYTES) {
        await reader.cancel();
        return null;
      }
      chunks.push(value);
    }
    const bytes = new Uint8Array(size);
    let offset = 0;
    for (const chunk of chunks) {
      bytes.set(chunk, offset);
      offset += chunk.byteLength;
    }
    const result: unknown = JSON.parse(decoder.decode(bytes));
    return object(result) ? result : null;
  } catch {
    return null;
  } finally {
    reader.releaseLock();
  }
}

export async function handler(request: Request): Promise<Response> {
  const origin = request.headers.get("origin");
  if (!allowedOrigin(origin)) return json(origin, 403, { code: "ORIGIN_NOT_ALLOWED" });
  if (request.method === "OPTIONS") return new Response(null, { status: 204, headers: headers(origin) });
  if (request.method !== "POST") return json(origin, 405, { code: "METHOD_NOT_ALLOWED" });

  const key = await ticketKey;
  if (!publishableKey || !serverKey || !supabaseUrl || !key) {
    return json(origin, 503, { code: "SERVICE_UNAVAILABLE", retryAfter: 30 }, 30);
  }
  if (!(await sameKey(request.headers.get("apikey"), publishableKey))) {
    return json(origin, 401, { code: "INVALID_API_KEY" });
  }

  const body = await readJson(request);
  if (!body || typeof body.playId !== "string" || !UUID_RE.test(body.playId) ||
    typeof body.trackId !== "string" || !TRACK_IDS.has(body.trackId) ||
    !["start", "qualify"].includes(String(body.action))) {
    return json(origin, 400, { code: "INVALID_INPUT" });
  }

  const playId = body.playId.toLowerCase();
  const trackId = body.trackId;
  const now = Date.now();
  if (body.action === "start") {
    if (body.source !== "web" && body.source !== "windows") {
      return json(origin, 400, { code: "INVALID_INPUT" });
    }
    const ticket = await signTicket({ v: 1, playId, trackId, source: body.source, issuedAt: now }, key);
    return json(origin, 200, {
      ticket, notBefore: now + QUALIFY_MS, expiresAt: now + TICKET_LIFETIME_MS,
    });
  }

  if (typeof body.ticket !== "string" || typeof body.seconds !== "number" ||
    !Number.isFinite(body.seconds) || body.seconds < 30 || body.seconds > 86400) {
    return json(origin, 400, { code: "INVALID_INPUT" });
  }
  const ticket = await readTicket(body.ticket, key);
  if (!ticket || ticket.playId !== playId || ticket.trackId !== trackId || ticket.issuedAt > now) {
    return json(origin, 400, { code: "INVALID_TICKET" });
  }
  if (now - ticket.issuedAt >= TICKET_LIFETIME_MS) {
    return json(origin, 410, { code: "EXPIRED_TICKET" });
  }
  if (now - ticket.issuedAt < QUALIFY_MS) {
    const retryAfter = Math.max(1, Math.ceil((QUALIFY_MS - (now - ticket.issuedAt)) / 1000));
    return json(origin, 409, { code: "TOO_EARLY", retryAfter }, retryAfter);
  }

  try {
    // New secret API keys belong on apikey, never in Authorization. Legacy
    // service-role JWTs additionally use Authorization for compatibility only.
    const databaseHeaders: Record<string, string> = {
      "apikey": serverKey,
      "content-type": "application/json",
    };
    if (!serverKey.startsWith("sb_secret_")) databaseHeaders.Authorization = `Bearer ${serverKey}`;
    const response = await fetch(`${supabaseUrl}/rest/v1/rpc/record_music_play`, {
      method: "POST",
      headers: databaseHeaders,
      body: JSON.stringify({
        p_play_id: playId,
        p_track_id: trackId,
        p_source: ticket.source,
        p_heard_seconds: body.seconds,
        p_ticket_issued_at: new Date(ticket.issuedAt).toISOString(),
      }),
      signal: AbortSignal.timeout(10_000),
    });
    const result: unknown = await response.json();
    if (!response.ok) {
      if (object(result) && result.code === "23505") {
        return json(origin, 409, { code: "PLAY_ID_CONFLICT" });
      }
      if (object(result) && result.code === "22023") {
        return json(origin, 400, { code: "INVALID_TRACK" });
      }
      return json(origin, 503, { code: "SERVICE_UNAVAILABLE", retryAfter: 5 }, 5);
    }
    const row: unknown = Array.isArray(result) ? result[0] : null;
    if (!object(row) || row.track_id !== trackId || typeof row.play_count !== "string" ||
      !/^\d+$/.test(row.play_count) || typeof row.counted !== "boolean") {
      return json(origin, 503, { code: "SERVICE_UNAVAILABLE", retryAfter: 5 }, 5);
    }
    return json(origin, 200, {
      trackId: row.track_id, playCount: row.play_count, counted: row.counted,
    });
  } catch {
    return json(origin, 503, { code: "SERVICE_UNAVAILABLE", retryAfter: 5 }, 5);
  }
}

Deno.serve(handler);
