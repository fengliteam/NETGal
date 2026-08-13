# NETGal

NETGal is a small, beginner-friendly visual novel engine built with .NET 10.
It keeps game data in readable JSON, includes a local browser editor, supports instant preview, and can export both a portable static game package and native executable players.

The architecture is deliberately split into a platform-neutral core and platform hosts:

- `NETGal.Engine`: pure .NET story model, runtime, validation, and AOT-friendly source-generated JSON
- `NETGal`: browser editor and static web export host
- `NETGal.Player`: NativeAOT command-line player for Windows, Linux, and macOS targets
- `NETGal.Player.Maui`: native MAUI player foundation for Android, iOS, Mac Catalyst, and Windows
- `NETGal.Studio.Maui`: native Windows project editor; no WebView or browser runtime
- `NETGal.Player.Windows`: no-workload WPF native Windows player for local Windows builds
- `NETGal.Studio.Windows`: no-workload WPF native Windows editor for local Windows builds

The same `game.json` format is used everywhere. Gameplay rules are not tied to a browser, so a native host can replace the UI, storage, audio, and lifecycle services without forking the engine.

Chinese user, developer, plugin, and AI prompt documentation is in [`docs/`](docs/). The repository is released under [Apache License 2.0](LICENSE). Most of the initial implementation was produced with assistance from AI programming tools such as GitHub Copilot and OpenAI Codex, then reviewed, integrated, and tested in this repository. Contributors may use AI tools for modifications, but remain responsible for correctness, security, licensing, and disclosure in pull requests.

## Requirements

- .NET 10 SDK for development and local builds
- A modern browser

Released native artifacts are self-contained and do not require the .NET SDK or runtime on the player's machine. The SDK and platform workloads are only needed on the build machine.

## Quick start

```powershell
dotnet run --project src/NETGal -- new samples/MyGame "My First GalGame"
dotnet run --project src/NETGal -- play samples/MyGame
```

Open the printed local URL. The editor lets you edit scenes, preview the story, validate the project, and save changes.

For a completely native Windows authoring experience, use the MAUI Studio:

```powershell
dotnet workload install maui
dotnet run --project src/NETGal.Studio.Maui -f net10.0-windows10.0.19041.0
```

Studio opens `game.json` with the native Windows file picker and edits scenes, dialogue, choices, and save data with MAUI controls. The browser editor remains an optional lightweight authoring path only.

For a Windows machine where MAUI workloads are not installed or C: drive space is limited, use the WPF native Windows tools. They compile against the installed `Microsoft.WindowsDesktop.App` and keep NuGet/build caches under this repository's `work` directory:

```powershell
dotnet run --project src/NETGal.Studio.Windows -- samples/Starfall
dotnet run --project src/NETGal.Player.Windows -- samples/Starfall
```

The WPF player and Studio are fully native Windows applications. They share `NETGal.Engine` and the same `game.json` project format as the MAUI targets.

## Build a game package

```powershell
dotnet run --project src/NETGal -- build samples/MyGame dist/MyGame
```

This creates:

- `dist/MyGame/index.html`: the standalone player
- `dist/MyGame/game.json`: the game data
- `dist/MyGame/assets/`: copied game assets
- `dist/MyGame.zip`: a shareable zip package

## Native publish targets

The desktop editor also exposes a single publish command for native game builds. It reads the selected project's `game.json` and injects that project and its `assets` into the native MAUI app before compiling the requested target:

```powershell
dotnet run --project src/NETGal -- publish samples/MyGame windows Release dist/MyGame/windows
dotnet run --project src/NETGal -- publish samples/MyGame android Release dist/MyGame/android
dotnet run --project src/NETGal -- publish samples/MyGame ios Release dist/MyGame/ios
dotnet run --project src/NETGal -- publish samples/MyGame maccatalyst Release dist/MyGame/maccatalyst
```

The native game interface is implemented with .NET MAUI controls (`Grid`, `Image`, `Label`, `Button`, `ScrollView`, and `ActivityIndicator`). The runtime does not use WebView, HTML, JavaScript, or a browser shell. The browser editor is only a creation tool; exported games use the native player.

Target mapping:

- `windows` -> `net10.0-windows10.0.19041.0`, unpackaged native Windows app by default
- `android` -> `net10.0-android`, APK output by default
- `ios` -> `net10.0-ios`, device/simulator output according to the selected SDK and signing profile
- `maccatalyst` -> `net10.0-maccatalyst`, native Mac Catalyst app

Before using the command on a clean development machine, install the workload once:

```powershell
dotnet workload install maui
```

For Android release signing, pass the standard MAUI/MSBuild signing properties or configure them in your CI secret store. For iOS and Mac Catalyst, configure the Apple signing identity and provisioning profile on a macOS build host.

The terminal player can use NativeAOT for a self-contained executable. WPF and MAUI GUI releases are self-contained native Windows/mobile applications and do not require the .NET runtime on the player's machine. The terminal target can be changed for your release pipeline:

```powershell
dotnet publish src/NETGal.Player -c Release -r win-x64 -p:PublishAot=true --self-contained true -o dist/native/win-x64
dotnet publish src/NETGal.Player -c Release -r linux-x64 -p:PublishAot=true --self-contained true -o dist/native/linux-x64
dotnet publish src/NETGal.Player -c Release -r osx-arm64 -p:PublishAot=true --self-contained true -o dist/native/osx-arm64
```

### Protected native game packages

The editor source project remains editable as `game.json`. Native game publishing creates an authenticated encrypted `game.pkg` containing the story and assets, and embeds a per-build key into the native player. Native players refuse missing, modified, or corrupted packages, so the shipped output does not expose readable story JSON or loose game assets.

Use the explicit package command when you need a protected content file for another native host:

```powershell
dotnet run --project src/NETGal -- protect samples/MyGame work/native-content work/native-content/game.key
```

The key is for the build pipeline and must not be distributed as a separate player file. The `publish`/`native` command creates the package and embeds its key automatically. This raises the cost of casual editing and extraction; no client-side game can be made absolutely impossible to reverse engineer because a playable client must contain the code and key needed to decrypt its own content. Browser/static web exports remain inherently inspectable and should not be used for secret content.

The current native player is a terminal host, useful for validating the AOT runtime and project format. The MAUI host is the native UI path for touch devices:

```powershell
dotnet workload install maui
dotnet publish src/NETGal.Player.Maui -f net10.0-android -c Release
dotnet publish src/NETGal.Player.Maui -f net10.0-ios -c Release
dotnet publish src/NETGal.Player.Maui -f net10.0-windows10.0.19041.0 -c Release
```

Android/iOS signing, provisioning profiles, SDKs, and store packaging remain platform-specific prerequisites. The repository includes the shared MAUI host and a bundled sample `game.json`; device assets can be added under `Resources/Raw`.

## GitHub Actions build

The repository includes `.github/workflows/windows.yml`. A push, pull request, or manual workflow run on GitHub uses `windows-latest` to install the .NET 10 MAUI workload, compile the core and CLI, publish the WPF native Windows tools, publish the MAUI Windows player and Studio, and upload all Windows artifacts. This keeps the large MAUI SDK and native build output off a development machine with limited C: drive space.

The package is static and can be hosted on any static web server. For local testing, open `index.html` through a static server because some browsers restrict `fetch` from `file://` URLs.

## Project format

Each project has a `game.json` file:

```json
{
  "title": "My First GalGame",
  "startScene": "intro",
  "scenes": [
    {
      "id": "intro",
      "title": "Opening",
      "background": "",
      "speaker": "Narrator",
      "text": "The story begins...",
      "choices": [
        { "id": "continue", "text": "Continue", "next": "ending" }
      ]
    }
  ]
}
```

The editor currently focuses on the core visual-novel loop: backgrounds, dialogue, and branching choices. The JSON format is intentionally open so additional node types can be added without changing the basic project workflow.

## Repository layout

- `src/NETGal.Engine`: platform-neutral project model, validation, runtime, and package builder
- `src/NETGal`: ASP.NET Core editor host and CLI
- `src/NETGal.Player`: NativeAOT player host
- `src/NETGal.Player.Maui`: native mobile/desktop UI host
- `samples/Starfall`: example project generated for first launch
- `tests/NETGal.Engine.Smoke`: dependency-light engine, save, parser, and package integrity tests
- `docs`: Chinese user, developer, plugin, and AI prompt documentation
