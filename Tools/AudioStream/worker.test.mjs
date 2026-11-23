import { test } from 'node:test';
import assert from 'node:assert/strict';
import worker from './worker.mjs';

const entries = new Map([
  ['stream/track_001/index.json', JSON.stringify({ durations: [4, 1.25] })],
  ['stream/track_001/segment-00000.ts', 'abcdefghij'],
  ['stream/track_001/segment-00001.ts', 'klmnop'],
  ['stream/track_001/original.mp3', 'secret-original'],
  ['stream/track_001/segment-00002.ts', 'unlisted'],
]);
const bucket = {
  async head(key) { const v = entries.get(key); return v === undefined ? null : { size: Buffer.byteLength(v) }; },
  async get(key, options) {
    let value = entries.get(key);
    if (value === undefined) return null;
    const size = Buffer.byteLength(value);
    if (key.endsWith('index.json')) return { size, json: async () => JSON.parse(value) };
    if (options?.range) value = value.slice(options.range.offset, options.range.offset + options.range.length);
    return { size, body: new Response(value).body };
  },
};
const url = 'https://stream.example.test/v1/audio/track_001/';
const get = (path, options = {}) => worker.fetch(new Request(url + path, options), { STREAM_BUCKET: bucket });

test('manifest is generated only from validated durations', async () => {
  const response = await get('index.m3u8');
  const text = await response.text();
  assert.equal(response.status, 200);
  assert.match(response.headers.get('content-type'), /^application\/vnd\.apple\.mpegurl/);
  assert.match(text, /#EXTINF:4\.000000,\nsegment-00000\.ts/);
  assert.match(text, /#EXT-X-ENDLIST/);
  assert.doesNotMatch(text, /mp3|secret-original/);
  assert.equal(response.headers.get('cache-control'), 'private, no-store, max-age=0');
});
test('only licensed expected manifest and segments are reachable', async () => {
  for (const path of ['original.mp3', 'segment-00002.ts', 'index.json', '%2e%2e/original.mp3', 'segment-00000.ts%2f.mp3']) {
    assert.equal((await get(path)).status, 404);
  }
  assert.equal((await worker.fetch(new Request('https://stream.example.test/v1/audio/track_006/index.m3u8'), { STREAM_BUCKET: bucket })).status, 404);
  assert.equal((await get('index.m3u8', { method: 'POST' })).status, 405);
  assert.equal((await worker.fetch(new Request(url + 'index.m3u8'), {})).status, 503);
});
test('GET, HEAD, ranges, and CORS support browser HLS playback', async () => {
  const preflight = await get('index.m3u8', { method: 'OPTIONS' });
  assert.equal(preflight.status, 204);
  assert.equal(preflight.headers.get('access-control-allow-origin'), '*');
  const all = await get('segment-00000.ts');
  assert.equal(await all.text(), 'abcdefghij');
  assert.equal(all.headers.get('content-type'), 'video/mp2t');
  const partial = await get('segment-00000.ts', { headers: { Range: 'bytes=2-5' } });
  assert.equal(partial.status, 206);
  assert.equal(partial.headers.get('content-range'), 'bytes 2-5/10');
  assert.equal(await partial.text(), 'cdef');
  const suffix = await get('segment-00000.ts', { headers: { Range: 'bytes=-3' } });
  assert.equal(await suffix.text(), 'hij');
  const head = await get('segment-00000.ts', { method: 'HEAD', headers: { Range: 'bytes=2-' } });
  assert.equal(head.status, 206);
  assert.equal(head.headers.get('content-length'), '8');
  assert.equal(await head.text(), '');
  const invalid = await get('segment-00000.ts', { headers: { Range: 'bytes=10-11' } });
  assert.equal(invalid.status, 416);
  assert.equal(invalid.headers.get('content-range'), 'bytes */10');
  assert.equal((await get('segment-00000.ts', { headers: { Range: 'bytes=0-1,4-5' } })).status, 416);
});
test('invalid index and storage failures fail closed', async () => {
  entries.set('stream/track_001/index.json', JSON.stringify({ durations: [0] }));
  assert.equal((await get('index.m3u8')).status, 404);
  entries.set('stream/track_001/index.json', 'not json');
  assert.equal((await get('index.m3u8')).status, 503);
});
