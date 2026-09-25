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

The catalog contains routing metadata and source hashes, not sound recordings. Run the packaged `Setup.cmd` against your own game installation to generate haptic data.
