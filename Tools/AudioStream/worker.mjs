// The original recordings belong in a separate, private bucket which is never
// bound to this Worker. Only licensed, transcoded HLS assets are accessible.
const LICENSED_TRACKS = new Set(['track_001', 'track_002', 'track_003', 'track_004', 'track_005']);
const MAX_INDEX_BYTES = 256 * 1024;
const headers = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET, HEAD, OPTIONS',
  'Access-Control-Allow-Headers': 'Range',
  'Access-Control-Expose-Headers': 'Accept-Ranges, Content-Length, Content-Range',
  'Cross-Origin-Resource-Policy': 'cross-origin',
  'X-Content-Type-Options': 'nosniff',
  'Cache-Control': 'private, no-store, max-age=0',
};

function reply(status, body = null, extra = {}) {
  return new Response(body, { status, headers: { ...headers, ...extra } });
}

async function indexFor(bucket, id) {
  const object = await bucket.get(`stream/${id}/index.json`);
  if (!object || object.size > MAX_INDEX_BYTES || object.size < 1) return null;
  const data = await object.json();
  if (!Array.isArray(data.durations) || data.durations.length < 1 || data.durations.length > 10000 ||
      data.durations.some((d) => typeof d !== 'number' || !Number.isFinite(d) || d <= 0 || d > 60)) return null;
  return data.durations;
}

function parseRange(value, size) {
  const match = /^bytes=(\d*)-(\d*)$/.exec(value);
  if (!match || (!match[1] && !match[2]) || !size) return null;
  let start, end;
  if (!match[1]) {
    const suffix = Number(match[2]);
    if (!Number.isSafeInteger(suffix) || suffix <= 0) return null;
    start = Math.max(0, size - suffix);
    end = size - 1;
  } else {
    start = Number(match[1]);
    end = match[2] ? Number(match[2]) : size - 1;
    if (!Number.isSafeInteger(start) || !Number.isSafeInteger(end) || start >= size || end < start) return null;
    end = Math.min(end, size - 1);
  }
  return { start, end };
}

async function serve(request, env) {
  if (request.method === 'OPTIONS') return reply(204);
  if (request.method !== 'GET' && request.method !== 'HEAD') return reply(405, null, { Allow: 'GET, HEAD, OPTIONS' });
  // Reject encoded paths outright, including encoded separators and dot segments.
  const rawPath = request.url.split('?')[0].split('#')[0];
  if (rawPath.includes('%')) return reply(404);
  const path = new URL(request.url).pathname;
  const match = /^\/v1\/audio\/(track_00[1-5])\/(index\.m3u8|segment-(\d{5})\.ts)$/.exec(path);
  if (!match || !LICENSED_TRACKS.has(match[1])) return reply(404);
  if (!env.STREAM_BUCKET) return reply(503);
  const [, id, resource, segment] = match;
  const durations = await indexFor(env.STREAM_BUCKET, id);
  if (!durations) return reply(404);
  if (resource === 'index.m3u8') {
    const lines = ['#EXTM3U', '#EXT-X-VERSION:3', `#EXT-X-TARGETDURATION:${Math.ceil(Math.max(...durations))}`, '#EXT-X-MEDIA-SEQUENCE:0', '#EXT-X-PLAYLIST-TYPE:VOD'];
    for (let i = 0; i < durations.length; i++) lines.push(`#EXTINF:${durations[i].toFixed(6)},`, `segment-${String(i).padStart(5, '0')}.ts`);
    lines.push('#EXT-X-ENDLIST', '');
    const body = lines.join('\n');
    const type = { 'Content-Type': 'application/vnd.apple.mpegurl; charset=utf-8', 'Content-Length': String(new TextEncoder().encode(body).length) };
    return reply(200, request.method === 'HEAD' ? null : body, type);
  }
  const number = Number(segment);
  if (number >= durations.length) return reply(404);
  const key = `stream/${id}/segment-${segment}.ts`;
  const meta = await env.STREAM_BUCKET.head(key);
  if (!meta || !Number.isSafeInteger(meta.size) || meta.size <= 0) return reply(404);
  const rangeHeader = request.headers.get('Range');
  const range = rangeHeader ? parseRange(rangeHeader, meta.size) : null;
  if (rangeHeader && !range) return reply(416, null, { 'Content-Range': `bytes */${meta.size}`, 'Accept-Ranges': 'bytes' });
  const length = range ? range.end - range.start + 1 : meta.size;
  const segmentHeaders = { 'Content-Type': 'video/mp2t', 'Content-Length': String(length), 'Accept-Ranges': 'bytes' };
  if (range) segmentHeaders['Content-Range'] = `bytes ${range.start}-${range.end}/${meta.size}`;
  if (request.method === 'HEAD') return reply(range ? 206 : 200, null, segmentHeaders);
  const obj = await env.STREAM_BUCKET.get(key, range ? { range: { offset: range.start, length } } : undefined);
  if (!obj || !obj.body) return reply(404);
  return reply(range ? 206 : 200, obj.body, segmentHeaders);
}

export default {
  async fetch(request, env) {
    try { return await serve(request, env); }
    catch { return reply(503); }
  },
};
