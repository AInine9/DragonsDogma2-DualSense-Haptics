# Dragon's Dogma 2 DualSense Haptics

An unofficial DualSense haptics mod for the PC version of Dragon's Dogma 2.
It generates haptic files from your game's sound effects and plays them during supported actions.

This mod does not reproduce the PS5's official haptic feedback. Adaptive triggers are not implemented.

## Requirements

- Windows x64, Dragon's Dogma 2 on Steam, and one DualSense or DualSense Edge. USB is recommended; Bluetooth and Edge hardware support remain unverified for this mod.
- [.NET 10 Runtime for Windows x64](https://dotnet.microsoft.com/download/dotnet/10.0). The SDK is not required for installation.
- A game-compatible version of [REFramework](https://github.com/praydog/REFramework).
- For USB, keep the controller's audio device enabled in Windows.
- Close other applications that control DualSense output.

## Install

1. Install REFramework in the game folder.
2. Extract the mod into a permanent folder outside the game directory.
3. Close the game and run `Setup.cmd`.
4. If prompted, enter the game folder containing `DD2.exe`.
5. Wait while Setup downloads the required tools and generates haptic data from your local game files. Allow several GB of free space.
6. Connect your controller and run `Start-Mod.cmd`.

Game audio files are not included in the mod.

## Usage

Run `Start-Mod.cmd` whenever you want to use the mod. **The mod runs only while its console window is open.** Close the window or press Ctrl+C to stop it.

The game launches through Steam by default. The mod also stops after the game closes.

## Configuration

Edit `config.json`, then restart the mod:

- `gain` — Overall vibration strength, from `0.0` to `3.0`; default `1.25`. Existing explicit settings are preserved.
- `damage_gain` — Additional strength for supported flesh-impact sounds, from `0.0` to `3.0`; default `1.0`. Does not add missing hit events. The output limiter still applies.
- `auto_launch_game` — Set to `false` to launch the game yourself; default `true`.
- `require_focus` — Pause vibration while another window is active; default `true`.

## Uninstall

1. Close the game and the mod console.
2. Run `Uninstall.cmd`.
3. Delete the extracted mod folder if you no longer need it.

## Troubleshooting

If vibration does not work, check the controller connection, Windows audio device, and REFramework compatibility. Keep only one supported controller connected.

Not every action is supported. Game updates may require a mod update and regeneration of haptic data. DLC support is not automatic.

For troubleshooting, check `data/bridge.log` and `data/status.json`.

## Build

See [BUILD.md](BUILD.md) for building and packaging from source.

## Legal

This mod is unofficial and is not affiliated with Capcom, Sony, or the game developers. Source code is available under the [MIT License](LICENSE). Third-party dependencies are listed in [THIRD_PARTY_NOTICES.txt](distribution/THIRD_PARTY_NOTICES.txt).
