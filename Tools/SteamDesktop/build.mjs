import fs from 'node:fs/promises';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {createRequire} from 'node:module';
import {fileURLToPath, pathToFileURL} from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const project = path.resolve(here, '../..');
const args = Object.fromEntries(process.argv.slice(2).reduce((items, value, index, all) => {
  if (value.startsWith('--')) items.push([value.slice(2), all[index + 1]]);
  return items;
}, []));
if (!args.content || !args.toolchain) throw new Error('Required: --content <docs> --toolchain <npm toolchain> [--output <directory>]');
const content = path.resolve(args.content), toolchain = path.resolve(args.toolchain);
const info = JSON.parse(await fs.readFile(path.join(here, 'package.json'), 'utf8'));
const index = await fs.readFile(path.join(content, 'index.html'), 'utf8');
const versions = [...new Set([...index.matchAll(/FastGame\/([0-9a-f]{16})\//g)].map(value => value[1]))];
if (versions.length !== 1 || index.includes('__SIMBAR_')) throw new Error('Expected one complete native game release.');
const version = versions[0], runtimeRelative = 'FastGame/' + version;
const manifest = JSON.parse(await fs.readFile(path.join(content, runtimeRelative, 'runtime-manifest.json'), 'utf8'));
if (manifest.version !== version) throw new Error('Runtime version mismatch.');
const out = path.resolve(args.output || path.join(project, 'Builds/Steam/Windows', version));
const staging = await fs.mkdtemp(path.join(project, 'Temp', 'steam-desktop-'));
const appDirectory = path.join(staging, 'app'), game = path.join(staging, 'game');
await fs.mkdir(appDirectory); await fs.mkdir(game);
const hash = buffer => createHash('sha256').update(buffer).digest('hex');
const copied = [];
async function copy(relative, expected) {
  const file = path.resolve(content, relative), target = path.resolve(game, relative);
  if (!file.startsWith(content + path.sep) || !target.startsWith(game + path.sep)) throw new Error('Unsafe content path.');
  const stat = await fs.lstat(file);
  if (!stat.isFile() || stat.isSymbolicLink()) throw new Error('Expected a regular bundled file: ' + relative);
  const data = await fs.readFile(file), digest = hash(data);
  if (expected && (data.length !== expected.bytes || digest !== expected.sha256)) throw new Error('Runtime hash mismatch: ' + relative);
  await fs.mkdir(path.dirname(target), {recursive: true}); await fs.writeFile(target, data);
  copied.push({path: relative.replaceAll('\\', '/'), bytes: data.length, sha256: digest});
}
await copy('index.html');
await copy('TemplateData/style.css'); await copy('TemplateData/favicon.ico');
await copy('playlist.json');
await copy(runtimeRelative + '/runtime-manifest.json');
for (const entry of manifest.files) await copy(runtimeRelative + '/' + entry.path, entry);
async function posters(relative) {
  for (const entry of await fs.readdir(path.join(content, relative), {withFileTypes: true})) {
    const child = path.posix.join(relative, entry.name);
    if (entry.isSymbolicLink()) throw new Error('Poster symlink is unsupported.');
    if (entry.isDirectory()) await posters(child);
    else if (entry.isFile() && !entry.name.endsWith('.meta')) await copy(child);
  }
}
try { await posters('StreamingAssets/ScoutPosters'); } catch (error) { if (error.code !== 'ENOENT') throw error; }
for (const match of index.matchAll(/(?:src|href)="([^"]+)"/g)) {
  const relative = match[1].split(/[?#]/)[0];
  if (!relative || /^(https?:|data:|#)/.test(relative)) continue;
  if (!(await fs.stat(path.join(game, relative))).isFile()) throw new Error('Missing page asset: ' + relative);
}
await fs.copyFile(path.join(here, 'main.cjs'), path.join(appDirectory, 'main.cjs'));
const {desktopToolchain, ...runtimeInfo} = info;
await fs.writeFile(path.join(appDirectory, 'package.json'), JSON.stringify(runtimeInfo, null, 2) + '\n');
const requireTool = createRequire(path.join(toolchain, 'package.json'));
const {packager} = await import(pathToFileURL(requireTool.resolve('@electron/packager')).href);
const results = await packager({
  dir: appDirectory, out, name: 'SimBar', platform: 'win32', arch: 'x64',
  electronVersion: desktopToolchain.electron, asar: true, overwrite: false,
  electronZipDir: args['electron-zip-dir'] ? path.resolve(args['electron-zip-dir']) : undefined,
  executableName: 'SimBar', extraResource: game, icon: path.join(game, 'TemplateData/favicon.ico'),
  appVersion: info.version, buildVersion: info.version,
  win32metadata: {FileDescription: 'SimBar', ProductName: 'SimBar', OriginalFilename: 'SimBar.exe'}
});
const result = {version: info.version, runtime: version, electron: desktopToolchain.electron,
  platform: 'win32', arch: 'x64', executable: path.join(results[0], 'SimBar.exe'), files: copied};
await fs.writeFile(path.join(results[0], 'game-content-manifest.json'), JSON.stringify(result, null, 2) + '\n');
await fs.writeFile(path.join(results[0], 'STEAM_UPLOAD.txt'),
  'SimBar Windows x64\n\nContent root: this entire folder.\nSteam launch executable: SimBar.exe\nKeep all DLLs, locales and resources beside the executable.\nUse your actual Steam AppID and Windows DepotID in SteamPipe.\nSteamworks integration and Steam account publishing are not included in this build.\nMusic streams over HTTPS and requires internet. Game content is bundled locally.\nSaves: %APPDATA%\\SimBar (the fixed simbar://app origin).\n');
console.log(JSON.stringify(result));
