# Nyautomator VRChat Module

VRChat integration module for Nyautomator. It adds VRChat API authentication, OSC controls and events, avatar parameter automation, chatbox output, and Dolly camera tooling to the Nyautomator host.

## Features

- Sign in to the VRChat API, restore stored sessions, and send authenticated API requests through Nyautomator.
- Read and write VRChat OSC avatar parameters, including built-in parameters and the active avatar config.
- Send VRChat input endpoints, camera controls, and chatbox messages over OSC.
- Listen for OSC avatar, parameter, and user camera events.
- Use OSC passthrough ports when another app also needs to exchange OSC traffic with VRChat.
- Capture and play VRChat Dolly camera paths from live camera pose and settings.
- Register VRChat graph nodes, reactions, events, option providers, dashboard cards, settings UI, integration tabs, and graph templates.

## Source Layout

- `src/Nyautomator.VRChat`: Core VRChat API, auth, OSC, and Dolly helpers.
- `src/Nyautomator.Automation.VRChat`: Automation nodes, reactions, events, and option providers.
- `src/Nyautomator.Module.VRChat`: Module entrypoint, runtime participant, API handlers, authenticated request support, and host registration.
- `module/module.json`: Nyautomator module contribution manifest.
- `module/assets`: Module icons, settings tab, OSC tab, Dolly tab, and graph templates.

## Local Development

This module can build in two modes:

- Local development mode uses `ProjectReference` entries to a sibling Nyautomator checkout. Set `NyautomatorSourceRoot` if the host repo is not at `..\Nyautomator\`.
- Standalone/distributed mode uses privately distributed Nyautomator DLLs. Retrieve them through Nyautomator's private developer channels, then set `UseNyautomatorProjectReferences=false` and point `NyautomatorReferencePath` at the folder containing those DLLs.

The module may compile against these Nyautomator host/SDK assemblies, but Nyautomator itself must not compile against this module:

- `Nyautomator.Core`
- `Nyautomator.Runtime.Abstractions`
- `Nyautomator.Automation`
- `Nyautomator.Module.Abstractions`

Host-provided assemblies are compile-time references for this module and are excluded from the packed module.

## Build

From this module directory:

```powershell
dotnet build .\Nyautomator.VRChat.slnx
```

Standalone binary-reference build:

```powershell
dotnet restore .\Nyautomator.VRChat.slnx -p:UseNyautomatorProjectReferences=false -p:NyautomatorReferencePath=C:\Path\To\NyautomatorDlls\
dotnet build .\Nyautomator.VRChat.slnx --no-restore -p:UseNyautomatorProjectReferences=false -p:NyautomatorReferencePath=C:\Path\To\NyautomatorDlls\
```

## Packaging

From this module directory:

```powershell
.\scripts\pack-module.ps1 -Configuration Release -UseNyautomatorProjectReferences false -NyautomatorReferencePath C:\Path\To\NyautomatorDlls\
```

The package script publishes the module, copies `module.json`, `module/assets/**`, module-owned assemblies, and required third-party dependencies, then fails if a host-provided `Nyautomator.*` implementation DLL is included.

## Testing In A Local Host

1. Retrieve the compatible Nyautomator DLLs through the private developer channel. If you also have an internal sibling Nyautomator checkout, a local host build output can be used as the DLL source:

```powershell
dotnet build ..\Nyautomator\apps\NyautomatorUI\NyautomatorUI.Server\NyautomatorUI.Server.csproj -c Debug
```

2. Build and pack this module in standalone mode, substituting the private DLL folder you received:

```powershell
.\scripts\pack-module.ps1 -Configuration Debug -UseNyautomatorProjectReferences false -NyautomatorReferencePath C:\Path\To\NyautomatorDlls\
```

3. Start the Nyautomator host and install `artifacts/modules/vrchat-0.1.0.zip` from the Modules tab.

After installation the module should contribute the VRChat dashboard cards, settings card, OSC and Dolly tabs, graph templates, automation assembly, runtime participant, and authenticated integration metadata. Disabling or unloading the module removes those active contributions while leaving module-scoped options stored under the host configuration so they are available again when the module is re-enabled.

## Host Contributions

When packed and loaded, the module contributes:

- Dashboard cards for VRChat API status and OSC sending/listening/passthrough.
- A settings card for VRChat sign-in, verification, cookie import, status refresh, and logout.
- Integration tabs for VRChat OSC parameters/events and VRChat Dolly.
- Authenticated integration metadata for `https://api.vrchat.cloud/api/1`.
- Automation assemblies and templates for OSC avatar controls, chatbox output, Twitch redeems, stream stats, speech-to-chatbox, and camera-trigger automation.

## Migration Notes

Local contributors can keep using sibling project references while editing host and module code together. For standalone validation or package work, switch to binary-reference mode with `UseNyautomatorProjectReferences=false` and a privately supplied `NyautomatorReferencePath` that contains `Nyautomator.Core.dll`, `Nyautomator.Module.Abstractions.dll`, `Nyautomator.Runtime.Abstractions.dll`, and `Nyautomator.Automation.dll`.

VRChat settings now live in the module option document for module id `vrchat`. Existing host-level `AppConfiguration.VRChat` values should be copied into the module options shape: `auth`, `osc`, and `dolly`. The module defaults place Dolly tracks under the module data directory at `dolly/tracks` unless `dolly.trackDirectory` is set explicitly.

The dependency boundary is intentionally one-way: this repo may compile against the host SDK assemblies, but the Nyautomator host must not compile against `Nyautomator.VRChat`, `Nyautomator.Automation.VRChat`, or `Nyautomator.Module.VRChat`. Runtime integration happens through `module.json`, `INyautomatorModule`, module API handlers, contribution registries, module options, and authenticated integration adapters.

## VRChat Setup Notes

- Enable OSC in VRChat before using OSC send/listen features.
- Switch avatars after enabling OSC if avatar parameters do not appear immediately.
- Use passthrough if another OSC tool needs to communicate with VRChat while Nyautomator is listening.
- VRChat may require authenticator or email verification during API sign-in; the module surfaces those steps in its settings UI.
