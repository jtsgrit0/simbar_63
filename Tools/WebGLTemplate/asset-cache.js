// The immutable URLs below refer to one exact build. Cache decoded HTTP
// responses so both Unity's data reader and streaming Wasm compiler can reuse
// them without downloading or decompressing the same build again.
function createSimbarAssetFetch(nativeFetch, cacheStorage, baseURI, assetUrls) {
  let base;
  try { base = new URL(baseURI); } catch (_) { base = null; }
  const allowed = new Set();
  function buildAssetUrl(value) {
    if (!base || !/^https?:$/.test(base.protocol)) return null;
    try {
      const url = new URL(value?.url || value, base);
      if (url.origin === base.origin && !url.hash
          && /\/[0-9a-f]{32}\.(data|wasm)$/.test(url.pathname)
          && [...url.searchParams].length === 1
          && /^[0-9a-f]{16}$/.test(url.searchParams.get("v") || "")) return url.href;
    } catch (_) { /* Invalid or unrelated URLs use the original fetch. */ }
    return null;
  }
  for (const value of assetUrls) {
    const url = buildAssetUrl(value);
    if (url) allowed.add(url);
  }
  const enabled = allowed.size > 0 && !!cacheStorage && typeof cacheStorage.open === "function";
  const primed = new Map();
  let cachePromise;

  function pruneOldBuilds(cache) {
    if (typeof cache.keys !== "function" || typeof cache.delete !== "function") return;
    // Cleanup is best effort and never awaited by a request. Restrict it to
    // versioned player assets in this cache, leaving metadata untouched.
    Promise.resolve().then(() => cache.keys()).then(keys => Promise.all(keys.map(key => {
      const url = buildAssetUrl(key);
      return url && !allowed.has(url) ? cache.delete(key) : undefined;
    }))).catch(() => {});
  }

  function candidate(input, init) {
    if (!enabled) return null;
    try {
      const request = typeof input === "string" || input instanceof URL ? null : input;
      const method = String(init?.method || request?.method || "GET").toUpperCase();
      const mode = init?.cache || request?.cache || "default";
      const signal = init?.signal || request?.signal;
      if (method !== "GET" || mode === "no-store" || signal?.aborted) return null;
      const headers = new Headers(init?.headers || request?.headers);
      if (headers.has("range")) return null;
      const url = new URL(request?.url || input, base);
      if (!allowed.has(url.href)) return null;
      return {
        url: url.href,
        reload: mode === "reload" || mode === "no-cache",
        signature: JSON.stringify([
          init?.credentials || request?.credentials || "same-origin",
          init?.mode || request?.mode || "cors",
          init?.redirect || request?.redirect || "follow",
          [...headers.entries()].sort()
        ])
      };
    } catch (_) { return null; }
  }

  function openCache() {
    if (!cachePromise) {
      const name = "SimBarAssets_v1:" + base.origin + new URL(".", base).pathname;
      cachePromise = Promise.resolve().then(() => cacheStorage.open(name)).then(cache => {
        pruneOldBuilds(cache);
        return cache;
      }).catch(() => null);
    }
    return cachePromise;
  }

  async function load(input, init, asset) {
    const cache = await openCache();
    if (cache && !asset.reload) {
      try {
        const response = await cache.match(asset.url);
        if (response && response.status === 200) return response;
      } catch (_) { /* Storage restrictions must not prevent the game loading. */ }
    }
    const response = await nativeFetch(input, init);
    if (cache && response.status === 200) {
      try {
        // Do not wait for disk writes on the startup path. The other stream
        // branch remains available immediately to the Wasm compiler or Unity.
        Promise.resolve(cache.put(asset.url, response.clone())).catch(() => {});
      } catch (_) { /* Quota or unsupported response: keep the network result. */ }
    }
    return response;
  }

  function fetchAsset(input, init) {
    const asset = candidate(input, init);
    if (!asset) return nativeFetch(input, init);
    const pending = primed.get(asset.url);
    if (!asset.reload && pending && pending.signature === asset.signature) {
      primed.delete(asset.url);
      return pending.response.then(response => response || load(input, init, asset));
    }
    return load(input, init, asset);
  }

  function prime(input, init) {
    const asset = candidate(input, init);
    if (!asset || asset.reload || primed.has(asset.url)) return;
    // Only one reader receives this response. A failed speculative request is
    // retried by the real reader instead of becoming an unhandled rejection.
    primed.set(asset.url, {
      signature: asset.signature,
      response: load(input, init, asset).then(response => response.status === 200 ? response : null).catch(() => null)
    });
  }

  return { fetch: fetchAsset, prime, enabled };
}
