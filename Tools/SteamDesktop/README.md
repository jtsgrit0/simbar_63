# Windows PC build

The Windows player bundles the reviewed native game locally, including its
campaign. It uses the same JavaScript and art as the public site; only music and
remote artwork need an internet connection. Unity scene and Editor settings are
not changed by this build.

```powershell
python Tools/prepare_steam_desktop.py --content docs --node C:/path/to/node.exe
```

The helper installs the pinned official Electron packaging tools under ignored
`Temp/SteamDesktopToolchain`, verifies the active native runtime hashes and
copies only that release, its host page, playlist and ScoutPosters. It excludes
the old Unity WebGL player and historical native releases. Sources and download
versions are retained in `package.json`; `toolchain-lock.json` retains the exact
npm dependency resolution used for this build.

Output is `Builds/Steam/Windows/<runtime>/SimBar-win32-x64/SimBar.exe`, the entire
application folder and a ZIP beside it. Keep every DLL, locale and resources
folder with the executable. The ZIP is a Windows x64 distribution archive, not
an installer. No system-wide install is needed.

Rebuilds retain existing output. Use `--output Builds/Steam/Windows/<new-name>`
to select a fresh destination; do not overwrite a previously reviewed archive.
`build-manifest.json` records the runtime, bundled files, exact `app.asar` shell
hash and ZIP hash. A completed build still needs an executable smoke test.

`simbar://app` is a fixed secure custom origin. Saves persist in
`%APPDATA%/SimBar` between launches. `--user-data-dir=<absolute directory>` can
select an isolated profile for QA. The renderer is sandboxed with Node disabled
and context isolation enabled. External HTTP(S) links open in the user's normal
browser. The app serves its bundled files through Electron's official custom
protocol API; it does not load the published site to run the game.

For SteamPipe, use the complete application folder as the Windows depot content
root and `SimBar.exe` as the launch executable. Real AppID, DepotID and Steamworks
account access are required for upload. No sample AppID, Steamworks integration,
achievement support, Steam Cloud mapping or store publication is included.

Official references: [Electron security](https://www.electronjs.org/docs/latest/tutorial/security),
[protocol](https://www.electronjs.org/docs/latest/api/protocol),
[@electron/packager](https://github.com/electron/packager).
