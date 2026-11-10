'use strict';

const {app, BrowserWindow, net, protocol, session, shell} = require('electron');
const fs = require('node:fs');
const path = require('node:path');
const {pathToFileURL} = require('node:url');

const ORIGIN = 'simbar://app';
const CONTENT_POLICY = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob: https:; media-src 'self' https:; connect-src 'self' https:; font-src 'self'; object-src 'none'; base-uri 'self'; frame-src 'none'; form-action 'none'";

app.setName('SimBar');
app.setAppUserModelId('SimBar.Desktop');
// A fixed origin and user-data directory retain the existing game saves.
// The optional CLI directory is useful for portable use and isolated QA.
const profileArg = process.argv.find(value => value.startsWith('--user-data-dir='));
if (profileArg) {
  const directory = profileArg.slice('--user-data-dir='.length);
  if (!path.isAbsolute(directory)) throw new Error('The user-data directory must be absolute.');
  fs.mkdirSync(directory, {recursive: true});
  app.setPath('userData', directory);
  app.setPath('sessionData', directory);
}
protocol.registerSchemesAsPrivileged([{
  scheme: 'simbar',
  privileges: {standard: true, secure: true, supportFetchAPI: true, corsEnabled: true, stream: true}
}]);

function localPath(address, root) {
  const url = new URL(address);
  if (url.protocol !== 'simbar:' || url.hostname !== 'app' || url.port || url.username || url.password) return null;
  let pathname;
  try { pathname = decodeURIComponent(url.pathname); } catch { return null; }
  if (pathname.includes('\0') || pathname.includes('\\')) return null;
  const file = path.resolve(root, '.' + (pathname === '/' ? '/index.html' : pathname));
  const relative = path.relative(root, file);
  return relative && !relative.startsWith('..' + path.sep) && relative !== '..' && !path.isAbsolute(relative) ? file : null;
}

function externalLink(address) {
  try {
    const url = new URL(address);
    if (url.protocol === 'https:' || url.protocol === 'http:') shell.openExternal(url.href);
  } catch { /* Ignore unsupported or malformed external destinations. */ }
}

function isLocalAddress(address) {
  try { const url = new URL(address); return url.protocol === 'simbar:' && url.hostname === 'app' && !url.port && !url.username && !url.password; }
  catch { return false; }
}

let window;
if (!app.requestSingleInstanceLock()) {
  app.quit();
} else {
  app.on('second-instance', () => {
    if (!window) return;
    if (window.isMinimized()) window.restore();
    window.focus();
  });
  app.whenReady().then(async () => {
    const game = path.join(process.resourcesPath, 'game');
    if (!fs.existsSync(path.join(game, 'index.html'))) throw new Error('The bundled game is missing.');
    protocol.handle('simbar', async request => {
      const file = localPath(request.url, game);
      if (!file || !fs.existsSync(file) || !fs.statSync(file).isFile()) return new Response('Not found', {status: 404});
      const result = await net.fetch(pathToFileURL(file).href);
      const headers = new Headers(result.headers);
      headers.set('Content-Security-Policy', CONTENT_POLICY);
      headers.set('X-Content-Type-Options', 'nosniff');
      return new Response(result.body, {status: result.status, headers});
    });
    session.defaultSession.setPermissionRequestHandler((_contents, permission, callback, details) => {
      callback(permission === 'fullscreen' && details.requestingUrl?.startsWith(ORIGIN + '/'));
    });
    session.defaultSession.setPermissionCheckHandler((_contents, permission, requestingOrigin) => {
      return permission === 'fullscreen' && requestingOrigin === ORIGIN;
    });
    window = new BrowserWindow({
      title: 'SimBar', width: 1600, height: 900, minWidth: 1024, minHeight: 640,
      backgroundColor: '#101417', show: false, autoHideMenuBar: true,
      webPreferences: {nodeIntegration: false, contextIsolation: true, sandbox: true, webSecurity: true}
    });
    window.removeMenu();
    window.webContents.setWindowOpenHandler(({url}) => { externalLink(url); return {action: 'deny'}; });
    window.webContents.on('will-navigate', (event, url) => {
      if (!isLocalAddress(url)) { event.preventDefault(); externalLink(url); }
    });
    window.webContents.on('will-attach-webview', event => event.preventDefault());
    window.once('ready-to-show', () => window.show());
    await window.loadURL(ORIGIN + '/index.html');
  }).catch(error => { console.error(error); app.quit(); });
  app.on('window-all-closed', () => app.quit());
}
