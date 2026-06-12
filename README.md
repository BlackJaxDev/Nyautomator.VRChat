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
- Standalone/distributed mode uses Nyautomator-provided DLLs. Set `UseNyautomatorProjectReferences=false` and point `NyautomatorReferencePath` at the folder containing the host SDK DLLs.

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

## Host Contributions

When packed and loaded, the module contributes:

- Dashboard cards for VRChat API status and OSC sending/listening/passthrough.
- A settings card for VRChat sign-in, verification, cookie import, status refresh, and logout.
- Integration tabs for VRChat OSC parameters/events and VRChat Dolly.
- Authenticated integration metadata for `https://api.vrchat.cloud/api/1`.
- Automation assemblies and templates for OSC avatar controls, chatbox output, Twitch redeems, stream stats, speech-to-chatbox, and camera-trigger automation.

## VRChat Setup Notes

- Enable OSC in VRChat before using OSC send/listen features.
- Switch avatars after enabling OSC if avatar parameters do not appear immediately.
- Use passthrough if another OSC tool needs to communicate with VRChat while Nyautomator is listening.
- VRChat may require authenticator or email verification during API sign-in; the module surfaces those steps in its settings UI.
