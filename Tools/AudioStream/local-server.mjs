// Development-only adapter. The listener is loopback-only and exposes no file server.
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { Readable } from 'node:stream';
import worker from './worker.mjs';

const args = process.argv.slice(2);
const arg = (name, fallback) => { const i = args.indexOf(name); return i < 0 ? fallback : args[i + 1]; };
const storeArg = arg('--store');
const port = Number(arg('--port', '8788'));
if (!storeArg || !Number.isInteger(port) || port < 0 || port > 65535) throw new Error('Use --store <private HLS fixture directory> [--port <port>]');
const root = fs.realpathSync(storeArg);
function fileFor(key) {
  if (!/^stream\/track_00[1-5]\/(?:index\.json|segment-\d{5}\.ts)$/.test(key)) return null;
  const file = path.resolve(root, key);
  if (!file.startsWith(root + path.sep)) return null;
  try {
    const real = fs.realpathSync(file);
    return real.startsWith(root + path.sep) && fs.statSync(real).isFile() ? real : null;
  } catch { return null; }
}
const bucket = {
  async head(key) {
    const file = fileFor(key);
    return file ? { size: fs.statSync(file).size } : null;
  },
  async get(key, options) {
    const file = fileFor(key);
    if (!file) return null;
    const size = fs.statSync(file).size;
    if (key.endsWith('/index.json')) return { size, json: async () => JSON.parse(await fs.promises.readFile(file, 'utf8')) };
    const range = options?.range;
    const streamOptions = range ? { start: range.offset, end: range.offset + range.length - 1 } : {};
    return { size, body: Readable.toWeb(fs.createReadStream(file, streamOptions)) };
  },
};

const server = http.createServer(async (req, res) => {
  try {
    const request = new Request(`http://127.0.0.1:${server.address().port}${req.url}`, { method: req.method, headers: req.headers });
    const response = await worker.fetch(request, { STREAM_BUCKET: bucket });
    res.writeHead(response.status, Object.fromEntries(response.headers));
    if (response.body && req.method !== 'HEAD') Readable.fromWeb(response.body).pipe(res);
    else res.end();
  } catch { res.writeHead(500); res.end(); }
});
server.listen(port, '127.0.0.1', () => console.log(`Local audio test server listening on loopback port ${server.address().port}`));
