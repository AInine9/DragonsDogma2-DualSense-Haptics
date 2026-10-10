# Building

Requires the .NET 10 SDK on Windows x64.

```powershell
dotnet publish DragonsDogma2DualSense/DragonsDogma2DualSense.csproj -c Release -p:PublishDir=../work/publish-release/
./tools/package-release.ps1 -Publish work/publish-release -Output release/DragonsDogma2-DualSense
```

Use a new output directory. The package requires the .NET 10 x64 Runtime.

A prebuilt native audio guard is included. To rebuild it with Zig 0.15.2:

```powershell
./tools/build-native.ps1 -Zig <path-to-zig.exe>
```

The guard's source and its MinHook dependency are in `native`.

Native output tracing is disabled by default. For diagnostics, create
`reframework/data/dd2_output_diagnostics.enabled` in the game folder before
starting the game. Remove the marker and restart the game to disable tracing.

The catalog contains routing metadata and source hashes, not sound recordings. Run the packaged `Setup.cmd` against your own game installation to generate haptic data.

Catalog sources live in `distribution/catalog/`. `manifest.json` lists the parts
for banks, events, nearby rules, and nodes. Node files are grouped by kind, with
at most 256 entries per file. Each entry stays on one line for readable diffs.
Edit these parts; `package-release.ps1` automatically combines them into the
single `bin/catalog.json` expected by the runtime. Duplicate IDs within a section
are rejected. To generate just the runtime catalog:

```powershell
./tools/build-catalog.ps1 -Output work/catalog.json
```
