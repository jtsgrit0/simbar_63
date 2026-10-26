"""Execute the deployed asset cache against real Web Response streams in Node."""

import os
from pathlib import Path
import shutil
import subprocess
import unittest


ROOT = Path(__file__).resolve().parent.parent
FACTORY = (ROOT / "Tools/WebGLTemplate/asset-cache.js").read_text(encoding="utf-8")
FIXTURE = r"""
const assert = require('node:assert/strict');
const base = 'https://example.com/simbar/';
const data = 'Build/0123456789abcdef0123456789abcdef.data?v=0123456789abcdef';
const wasm = 'Build/abcdef0123456789abcdef0123456789.wasm?v=fedcba9876543210';
const absolute = value => new URL(value, base).href;
const tick = () => new Promise(resolve => setTimeout(resolve, 0));
function storageFixture() {
  const entries = new Map();
  const calls = [];
  const cache = {
    async match(url) { return entries.get(url)?.clone(); },
    async put(url, response) { entries.set(url, response.clone()); }
  };
  const storage = {async open(name) { calls.push(name); return cache; }};
  return {entries, calls, cache, storage};
}
"""


class AssetCacheTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.node = shutil.which("node")
        if not cls.node:
            editors = Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "Unity/Hub/Editor"
            installed = sorted(editors.glob(
                "*/Editor/Data/PlaybackEngines/WebGLSupport/BuildTools/Emscripten/node/node.exe"), reverse=True)
            cls.node = str(installed[0]) if installed else None
        if not cls.node:
            raise unittest.SkipTest("Node is required for the browser asset cache tests")

    def run_case(self, script):
        result = subprocess.run(
            [self.node, "-e", FACTORY + FIXTURE + "\n(async () => {\n" + script
             + "\n})().catch(error => { console.error(error); process.exitCode = 1; });"],
            capture_output=True, text=True, timeout=10)
        self.assertEqual(result.returncode, 0, result.stderr)

    def test_prime_is_consumed_once_without_duplicate_download(self):
        self.run_case(r"""
const f = storageFixture(); let downloads = 0;
const api = createSimbarAssetFetch(async () => {
  downloads++; return new Response(new Uint8Array([1, 2, 3, 255]));
}, f.storage, base, [data]);
api.prime(data); api.prime(data);
const first = await api.fetch(absolute(data), {method: 'GET'});
assert.deepEqual([...new Uint8Array(await first.arrayBuffer())], [1, 2, 3, 255]);
await tick();
const second = await api.fetch(data);
assert.deepEqual([...new Uint8Array(await second.arrayBuffer())], [1, 2, 3, 255]);
assert.equal(downloads, 1);
assert.equal(f.calls.length, 1);
assert.ok(f.calls[0].includes('/simbar/'));
""")

    def test_warm_wasm_is_a_streaming_response_with_original_mime(self):
        self.run_case(r"""
const f = storageFixture(); let downloads = 0;
const api = createSimbarAssetFetch(async () => {
  downloads++;
  return new Response(new Uint8Array([0,97,115,109,1,0,0,0]), {
    headers: {'Content-Type':'application/wasm', 'ETag':'"version"'}
  });
}, f.storage, base, [wasm]);
await WebAssembly.instantiateStreaming(api.fetch(wasm), {});
await tick();
const response = await api.fetch(wasm);
assert.equal(response.headers.get('content-type'), 'application/wasm');
assert.equal(response.headers.get('etag'), '"version"');
const module = await WebAssembly.instantiateStreaming(response, {});
assert.ok(module.instance instanceof WebAssembly.Instance);
assert.equal(downloads, 1);
""")

    def test_storage_failure_does_not_block_network(self):
        self.run_case(r"""
let downloads = 0;
const api = createSimbarAssetFetch(async () => {
  downloads++; return new Response('network');
}, {open() {throw new Error('storage disabled');}}, base, [data]);
assert.equal(await (await api.fetch(data)).text(), 'network');
assert.equal(await (await api.fetch(data)).text(), 'network');
assert.equal(downloads, 2);
""")

    def test_cache_read_or_write_errors_do_not_fail_a_valid_response(self):
        self.run_case(r"""
const cache = {match() {throw new Error('read');}, put() {return Promise.reject(new Error('quota'));}};
const api = createSimbarAssetFetch(async () => new Response('usable'),
  {open:async () => cache}, base, [data]);
assert.equal(await (await api.fetch(data)).text(), 'usable');
await tick();
""")

    def test_cache_write_is_not_on_the_critical_path(self):
        self.run_case(r"""
const cache = {match:async () => null, put:() => new Promise(() => {})};
const api = createSimbarAssetFetch(async () => new Response('stream'),
  {open:async () => cache}, base, [data]);
const response = await Promise.race([api.fetch(data), new Promise((_,reject) =>
  setTimeout(() => reject(new Error('waited for disk write')), 100))]);
assert.equal(await response.text(), 'stream');
""")

    def test_failed_prime_retries_when_the_game_requests_the_asset(self):
        self.run_case(r"""
const f = storageFixture(); let downloads = 0;
const api = createSimbarAssetFetch(async () => {
  if (++downloads === 1) throw new Error('temporary network error');
  return new Response('retried');
}, f.storage, base, [data]);
api.prime(data); await tick();
assert.equal(await (await api.fetch(data)).text(), 'retried');
assert.equal(downloads, 2);
""")

    def test_only_exact_get_build_urls_are_intercepted(self):
        self.run_case(r"""
const f = storageFixture(); const calls = [];
const native = async (input, init) => {calls.push([input,init]); return new Response('bypass');};
const api = createSimbarAssetFetch(native, f.storage, base, [data,wasm]);
for (const input of [
  'Build/Game.data?v=0123456789abcdef',
  data.replace('.data', '.data.unityweb'),
  data + '&extra=1',
  data.replace('v=0123456789abcdef', 'v=0000000000000000'),
  'https://elsewhere.com/simbar/' + data,
  'StreamingAssets/music.bundle'
].filter(value => value !== data)) {
  const options = {credentials:'omit', headers:{'X-Test':'preserved'}};
  await api.fetch(input, options);
  assert.equal(calls.at(-1)[0], input); assert.equal(calls.at(-1)[1], options);
}
for (const options of [{method:'POST',body:'body'}, {headers:{Range:'bytes=0-3'}}, {cache:'no-store'}]) {
  await api.fetch(data, options);
  assert.equal(calls.at(-1)[0], data); assert.equal(calls.at(-1)[1], options);
}
assert.equal(f.calls.length, 0);
""")

    def test_missing_storage_or_invalid_asset_identity_disables_wrapper(self):
        self.run_case(r"""
let downloads = 0;
const native = async () => {downloads++; return new Response('direct');};
const noStorage = createSimbarAssetFetch(native, null, base, [data]);
assert.equal(noStorage.enabled, false); noStorage.prime(data);
assert.equal(await (await noStorage.fetch(data)).text(), 'direct');
const invalid = createSimbarAssetFetch(native, {open() {throw new Error('not called');}}, base,
  ['Build/Game.data?v=0123456789abcdef', data + '&v=duplicate']);
assert.equal(invalid.enabled, false);
assert.equal(await (await invalid.fetch(data)).text(), 'direct');
assert.equal(downloads, 2);
""")

    def test_reload_honors_fetch_options_and_does_not_use_stale_response(self):
        self.run_case(r"""
const f = storageFixture(); f.entries.set(absolute(data), new Response('cached'));
const options = {cache:'reload',credentials:'same-origin',headers:{'X-Asset':'value'}};
let inputSeen, optionsSeen;
const api = createSimbarAssetFetch(async (input, init) => {
  inputSeen=input; optionsSeen=init; return new Response('fresh');
}, f.storage, base, [data]);
assert.equal(await (await api.fetch(data, options)).text(), 'fresh');
assert.equal(inputSeen,data); assert.equal(optionsSeen,options);
""")

    def test_http_errors_are_not_cached_and_a_bad_prime_is_retried(self):
        self.run_case(r"""
const f = storageFixture(); let downloads = 0;
const api = createSimbarAssetFetch(async () => {
  downloads++;
  return downloads === 1 ? new Response('temporary', {status:503}) : new Response('recovered');
}, f.storage, base, [data]);
api.prime(data); await tick();
assert.equal(await (await api.fetch(data)).text(), 'recovered');
assert.equal(downloads,2); await tick();
assert.equal(await (await f.cache.match(absolute(data))).text(), 'recovered');
""")

    def test_prime_does_not_replace_a_request_with_different_credentials(self):
        self.run_case(r"""
const calls=[];
const storage={open:async () => ({match:async () => null,put:async () => {}})};
const api = createSimbarAssetFetch(async (input,init) => {
  calls.push([input,init]); return new Response(init?.credentials || input?.credentials || 'default');
}, storage, base, [data]);
api.prime(data);
const request = new Request(absolute(data), {credentials:'omit'});
const result = await api.fetch(request);
assert.equal(await result.text(), 'omit');
const actualCall = calls.find(call => call[0] === request);
assert.ok(actualCall);
assert.equal(actualCall[1],undefined);
""")

    def test_pruning_is_scoped_nonblocking_and_keeps_current_puts_and_metadata(self):
        self.run_case(r"""
const f = storageFixture();
const old = absolute('Build/11111111111111111111111111111111.data?v=1111111111111111');
const metadata = absolute('cache-metadata.json');
f.entries.set(old,new Response('old'));
f.entries.set(absolute(data),new Response('current'));
f.entries.set(metadata,new Response('metadata'));
let releaseCleanup;
f.cache.keys = () => new Promise(resolve => {
  releaseCleanup = () => resolve([...f.entries.keys()].map(url => new Request(url)));
});
f.cache.delete = async request => f.entries.delete(request.url);
const expectedName = 'SimBarAssets_v1:https://example.com/simbar/';
const otherCache = new Map([[old,'other app data']]);
const opened = [];
const storage = {async open(name) {
  opened.push(name);
  assert.equal(name,expectedName);
  return f.cache;
}};
const api = createSimbarAssetFetch(async () => new Response('new wasm'),storage,base,[data,wasm]);
const response = await Promise.race([api.fetch(wasm),new Promise((_,reject) =>
  setTimeout(() => reject(new Error('cleanup blocked startup')),100))]);
assert.equal(await response.text(),'new wasm');
await tick();
// The new response was cached while enumeration of the old build was pending.
assert.ok(f.entries.has(absolute(wasm)));
releaseCleanup(); await tick();
assert.equal(f.entries.has(old),false);
assert.equal(await (await f.cache.match(absolute(data))).text(),'current');
assert.equal(await (await f.cache.match(absolute(wasm))).text(),'new wasm');
assert.equal(await (await f.cache.match(metadata)).text(),'metadata');
assert.equal(otherCache.get(old),'other app data');
assert.deepEqual(opened,[expectedName]);
""")


if __name__ == "__main__":
    unittest.main()
