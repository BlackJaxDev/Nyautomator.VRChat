# Nyautomator VRChat Module Decoupling TODO

Goal: this repository should build, test, and package the VRChat module from a standalone checkout while linking against Nyautomator-provided binary DLLs. The module may compile against Nyautomator host/SDK assemblies, but it must not require sibling Nyautomator source projects. Nyautomator itself must not reference, include, or define anything owned by the VRChat module.

Allowed dependency direction:

- [ ] `Nyautomator.VRChat` module projects may reference Nyautomator binary DLLs that users receive with Nyautomator.
- [ ] `Nyautomator.VRChat` module projects must not use `ProjectReference` paths into the Nyautomator source tree for distributed builds.
- [ ] Nyautomator host projects must not reference `Nyautomator.VRChat`, `Nyautomator.Module.VRChat`, or `Nyautomator.Automation.VRChat`.
- [ ] Nyautomator core/platform projects must contain zero VRChat-specific type names, defaults, CLI flags, config merge branches, docs, controllers, runtime state fields, or registration code.
- [ ] Nyautomator host projects must expose neutral extension points instead of VRChat-specific types, options, or registration code.
- [ ] Modules must register and unregister their own configuration definitions dynamically when loaded or unloaded.
- [ ] The packed module should not include host DLLs that Nyautomator already provides at runtime, unless the module loader explicitly requires local copies for a stable SDK contract.

## Current Coupling Snapshot

- [ ] `src/Nyautomator.VRChat/Nyautomator.VRChat.csproj` references `Nyautomator.Core`.
- [ ] `src/Nyautomator.Automation.VRChat/Nyautomator.Automation.VRChat.csproj` references `Nyautomator.Automation`.
- [ ] `src/Nyautomator.Module.VRChat/Nyautomator.Module.VRChat.csproj` references `Nyautomator.Module.Abstractions` and `Nyautomator.Runtime.Abstractions` as sibling projects.
- [ ] Core VRChat code uses host-level `AppConfiguration`, including VRChat-specific options that should not live in the host.
- [ ] Core VRChat auth uses host-level `IntegrationTokenStore` and `IntegrationToken`.
- [ ] Dolly defaults use host-level `StoragePath`.
- [ ] Automation code uses host/static automation types such as `AutomationFlowEngine`, `AutomationHost`, `AutomationValueHelper`, and `AutomationTypeTokens`.
- [ ] Module code reaches into host runtime abstractions through source project references instead of binary references.
- [ ] Nyautomator core currently defines `AppConfiguration.VRChatOptions`, `AppConfiguration.VRChatDollyOptions`, and VRChat-specific config defaults.
- [ ] Nyautomator runtime currently has VRChat-specific command-line parsing and config merge behavior.
- [ ] Nyautomator UI/server currently has VRChat-specific runtime/dashboard state fields outside the module.
- [ ] Packaging output can include host-owned DLLs from the development workspace.

## Phase 1: Define The Public Contract Boundary

- [ ] Write down the intended dependency direction:
  - [ ] `Nyautomator.VRChat` contains VRChat API/OSC/Dolly logic and depends on Nyautomator binaries only where host services are truly needed.
  - [ ] `Nyautomator.Automation.VRChat` depends on Nyautomator automation binaries plus `Nyautomator.VRChat`.
  - [ ] `Nyautomator.Module.VRChat` depends on Nyautomator module/runtime binaries plus module-owned assemblies.
- [ ] Decide whether module builds use file-based DLL references, local NuGet packages, or published SDK packages.
- [ ] Decide which Nyautomator assemblies are host-provided at runtime and should be compile-only for this module.
- [ ] Identify any VRChat-specific types currently in Nyautomator host binaries that must move into the module or become generic host extension points.
- [ ] Define the module configuration lifecycle:
  - [ ] Module registers config schema/defaults during load.
  - [ ] Host persists module config under a module id, such as `modules.vrchat`.
  - [ ] Module reads typed config through a generic host service.
  - [ ] Module receives config change notifications scoped to its module id.
  - [ ] Module unregisters config UI/schema/watchers during unload.
  - [ ] Persisted module config remains on disk unless the user explicitly removes module data.
- [ ] Add a short architecture note to the repo once the boundary is agreed.

Done when: everyone agrees which Nyautomator binary references are allowed, which source references are forbidden, and which assemblies are forbidden in the module package.

## Phase 2: Move VRChat Configuration Into The Module

- [ ] Replace `AppConfiguration` usage in auth with a module-owned `VRChatAuthOptions` record.
- [ ] Replace `AppConfiguration.VRChatDollyOptions` usage with a module-owned `VRChatDollyOptionsInput` or equivalent options record.
- [ ] Add module-owned `VRChatModuleOptions` that contains:
  - [ ] Auth settings: `CookieTtlDays`, `AutoReconnect`.
  - [ ] Dolly settings: enabled state, track directory, frame rate, freshness windows, capture trigger settings, OpenVR companion setting, write confirmation setting.
  - [ ] OSC settings needed by this module: passthrough enabled/input/output ports and any VRChat-specific OSC filters.
- [ ] Replace `StoragePath.GetSettingsDirectory()` with an injected storage/root path provider.
- [ ] Introduce an auth/session persistence abstraction, for example `IVRChatSessionStore`.
- [ ] Move `IntegrationTokenStore` reads/writes/clears behind that session store.
- [ ] Keep the `VRChatAuthService` usable without a Nyautomator host by providing a default file/in-memory store only if appropriate.
- [ ] Register the VRChat module config schema/defaults from `Nyautomator.Module.VRChat` during module load.
- [ ] Read VRChat settings from generic module config storage instead of `AppConfiguration.VRChat`.
- [ ] Subscribe to module-scoped config change notifications and reconfigure auth/OSC/Dolly when VRChat config changes.
- [ ] Unsubscribe config listeners during module unload/disposal.
- [ ] Remove VRChat-specific settings from Nyautomator host/core configuration.
- [ ] Remove VRChat-specific command-line flags from Nyautomator runtime, or replace them with generic module option overrides such as `--module-option vrchat:key=value`.
- [ ] Replace the `Nyautomator.Core` source project reference in `Nyautomator.VRChat.csproj` with an allowed binary reference only if a neutral host contract is still needed.
- [ ] Build `Nyautomator.VRChat.csproj` by itself from this repo.

Done when: `Nyautomator.VRChat` has no compile-time dependency on any Nyautomator source project and Nyautomator host/core has no VRChat-specific code or types.

## Phase 3: Add Generic Dynamic Module Configuration To Nyautomator

These changes may happen in the Nyautomator host repo, but they must remain generic. The host can define contracts; it cannot depend on VRChat module types.

- [ ] Add or promote a stable module storage abstraction such as `IModuleStorage` or `IModuleDataPathProvider`.
- [ ] Add or promote a stable integration secret/session store abstraction such as `IIntegrationTokenStore`.
- [ ] Add or promote typed module options access, for example `IModuleOptionsProvider`.
- [ ] Add a generic module config registry, for example `IModuleConfigurationRegistry`.
- [ ] Add a generic module config contribution model:
  - [ ] Module id.
  - [ ] JSON schema or strongly typed options descriptor.
  - [ ] Default values.
  - [ ] Optional settings UI contribution metadata.
  - [ ] Optional validation callback.
- [ ] Store module config in a generic persisted location keyed by module id, not as hardcoded properties on `AppConfiguration`.
- [ ] Support module config load/unload:
  - [ ] Register config schema/defaults when a module loads.
  - [ ] Remove config schema/UI registrations when a module unloads.
  - [ ] Keep persisted user values until explicit deletion.
  - [ ] Notify module services when their scoped config changes.
- [ ] Add generic CLI/environment override support for module config without naming VRChat.
- [ ] Add or promote an automation event bus abstraction such as `IAutomationEventBus`.
- [ ] Add or promote an authenticated integration client abstraction for automation nodes.
- [ ] Ensure these abstractions live in Nyautomator assemblies users can reference as binaries.
- [ ] Publish, locally package, or copy these Nyautomator DLLs for standalone module builds.
- [ ] Add a host-side guard that prevents built-in host projects from referencing module projects.
- [ ] Add a host-side guard that fails if core/platform source contains module-specific identifiers such as `VRChat`, except in test fixtures explicitly scoped to module loading.

Done when: module authors can compile against Nyautomator binaries without referencing the Nyautomator source tree, and Nyautomator remains unaware of VRChat module types.

## Phase 4: Remove VRChat Names From Nyautomator Core/Platform

These changes happen in the Nyautomator host repo.

- [ ] Remove `AppConfiguration.VRChatOptions`.
- [ ] Remove `AppConfiguration.VRChatDollyOptions`.
- [ ] Remove `AppConfiguration.VRChat`.
- [ ] Remove VRChat-specific fields from `AppConfiguration.OscOptions` if they are not truly generic OSC settings.
- [ ] Replace host-level OSC passthrough config with either:
  - [ ] Generic OSC module config, if OSC is a platform feature.
  - [ ] VRChat module config, if the passthrough behavior is only used by VRChat.
- [ ] Remove VRChat-specific CLI parsing from `ConfigurationService`.
- [ ] Remove `MergeVRChatOptions` and `MergeVRChatDollyOptions` from `ConfigurationMerger`.
- [ ] Remove VRChat-specific runtime state fields from host dashboards/snapshots.
- [ ] Move or delete built-in host VRChat controllers/endpoints/docs that are superseded by module API handlers.
- [ ] Ensure generated docs no longer include built-in VRChat controller pages unless they are emitted from the module contribution system.
- [ ] Add tests proving Nyautomator host can start without any VRChat module assemblies present.
- [ ] Add tests proving Nyautomator host can load and unload the VRChat module dynamically without compile-time references.

Done when: searching Nyautomator core/platform source for `VRChat`, `vrchat`, or `VRC` yields no production host references outside generic test data and module manifests.

## Phase 5: Decouple Automation Assembly

- [ ] Replace `NyautomatorUI.Server.Automation` namespace dependencies with public automation SDK namespaces.
- [ ] Replace direct/static `AutomationFlowEngine` calls with a public automation event bus or registration contract.
- [ ] Replace direct/static `AutomationHost.SendAuthenticatedIntegrationRequest` usage with an injected/public authenticated integration client.
- [ ] Move or expose `AutomationValueHelper` and `AutomationTypeTokens` through the public SDK.
- [ ] Convert `Nyautomator.Automation.VRChat.csproj` from sibling project references to binary references or package references.
- [ ] Mark host-provided automation references as not copied into the plugin package.
- [ ] Build `Nyautomator.Automation.VRChat.csproj` from this standalone repo.

Done when: automation nodes build against Nyautomator-provided binaries and no host assembly is copied into the module package unintentionally.

## Phase 6: Decouple Module Host Layer

- [ ] Convert `Nyautomator.Module.VRChat.csproj` from sibling project references to binary references or package references.
- [ ] Keep references only to module/runtime contracts that the host promises to provide.
- [ ] Move all host configuration mapping into `Nyautomator.Module.VRChat`.
- [ ] Map dynamic module config into `VRChatAuthOptions`, `VRChatDollyOptions`, and OSC options before passing them to the core library.
- [ ] Pass host storage/session abstractions into the core VRChat services.
- [ ] Remove fallback construction paths that assume concrete host services are always available, or make them use SDK abstractions.
- [ ] Register VRChat module settings UI/schema contributions from `module.json` or runtime module registration.
- [ ] Ensure unloading the module unregisters VRChat settings UI/schema/API handlers/events.
- [ ] Build `Nyautomator.Module.VRChat.csproj` from this standalone repo.

Done when: the module layer is the only module project that knows about Nyautomator module/runtime contracts, and those contracts are referenced as binaries for standalone builds.

## Phase 7: Fix Packaging

- [ ] Restore or replace `scripts/pack-module.ps1`.
- [ ] Package only:
  - [ ] `Nyautomator.Module.VRChat.dll`
  - [ ] `Nyautomator.Automation.VRChat.dll`
  - [ ] `Nyautomator.VRChat.dll`
  - [ ] required third-party dependencies
  - [ ] `module.json`
  - [ ] `module/assets/**`
- [ ] Exclude host-provided assemblies that Nyautomator already loads, such as:
  - [ ] `Nyautomator.Core.dll`
  - [ ] `Nyautomator.Module.Abstractions.dll`
  - [ ] `Nyautomator.Runtime.Abstractions.dll`
  - [ ] `Nyautomator.Automation.dll`
  - [ ] host UI/server assemblies
- [ ] Add a package manifest or allowlist so accidental DLL drift fails loudly.
- [ ] Add a build/package check that fails if a Nyautomator host DLL is copied from a source-project build path.
- [ ] Ensure build and packaging output stays ignored by Git.

Done when: a clean package contains module-owned assemblies, required third-party dependencies, assets, and no accidental host-source DLL copies.

## Phase 8: Verification And CI

- [ ] Add a standalone build check for `Nyautomator.VRChat.slnx`.
- [ ] Add a check that no project contains `ProjectReference` paths outside this repo.
- [ ] Add a check that no Nyautomator host project references this VRChat module repo.
- [ ] Add a Nyautomator host source scan that fails on production VRChat-specific references.
- [ ] Add a dynamic module config registration/unregistration test.
- [ ] Add a pack check from a clean checkout.
- [ ] Add a package-content test that fails if forbidden `Nyautomator.*` host DLLs are included.
- [ ] Add a smoke test that loads the package into a Nyautomator host.
- [ ] Add a smoke test that unloads the package and confirms VRChat config UI/API handlers disappear while persisted config remains.
- [ ] Add an auth/session-store smoke test with a fake store.
- [ ] Add an options-mapping smoke test for auth, OSC, and Dolly settings.

Done when: CI proves both standalone package creation and host loading.

## Phase 9: Documentation And Migration

- [ ] Update `README.md` to say the module builds from a standalone checkout.
- [ ] Document required Nyautomator DLL/package versions.
- [ ] Document where users obtain Nyautomator binaries for standalone module builds.
- [ ] Document which Nyautomator assemblies are host-provided at runtime.
- [ ] Document the pack command and expected output.
- [ ] Document how to test the packed module in a local Nyautomator host.
- [ ] Add migration notes for moving from sibling project references to binary/package references.
- [ ] Add migration notes for moving existing `AppConfiguration.VRChat` values into module-scoped config.
- [ ] Document what happens to module config when the VRChat module is unloaded, disabled, reloaded, or uninstalled.

Done when: a contributor can clone this repo alone, build it, package it, and understand how it loads into Nyautomator.

## Final Acceptance Gate

- [ ] Fresh clone of this repo only, plus access to the expected Nyautomator binary DLLs/packages.
- [ ] `dotnet restore` succeeds.
- [ ] `dotnet build .\Nyautomator.VRChat.slnx` succeeds.
- [ ] Pack script succeeds.
- [ ] No project references Nyautomator source paths outside this repo.
- [ ] Nyautomator host does not reference this repo or any VRChat module assemblies.
- [ ] Nyautomator core/platform production source contains no VRChat-specific references.
- [ ] VRChat settings are registered dynamically by the module and removed from the active UI/API surface when the module unloads.
- [ ] Package does not include accidental Nyautomator host implementation DLLs.
- [ ] Package loads in Nyautomator.
- [ ] Auth UI, OSC tab, Dolly tab, automation nodes, graph templates, and authenticated VRChat requests still work.
