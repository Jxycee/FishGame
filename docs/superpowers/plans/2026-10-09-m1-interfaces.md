# FishGame M1 — Interface Contract

- **Date:** 2026-10-09
- **Scope:** Milestone M1 Foundation only (spec `docs/superpowers/specs/2026-10-09-fishgame-design.md` §17.1 and the sections it cites).
- **Status:** Binding for every M1 task plan (T1–T17). If a task plan disagrees with this file, this file wins. If this file disagrees with the spec, §13 here records why.
- **Audience:** Sonnet implementers who see only their own task text, the Global Constraints, and this contract.

Every type, file, asset, scene object, constant, event and RPC that is used by **two or more tasks** is defined here exactly once, with an owner task. A task may add **private/internal** helpers freely. It must **not** add, rename or remove anything public that is listed here, and it must not edit a file owned by another task (exception: §2.6).

---

## 0. Contents

1. Verified facts (overrides of spec [verify] items)
2. Shared conventions (paths, namespaces, encoding, CLI, `$env:FG_OUT`, TDD batching, commits)
3. Execution order and dependency graph
4. Assemblies (`.asmdef` files, final content, T1 owns all)
5. Constants
6. Assets, prefabs, scenes and hierarchies
7. Types by task (full public signatures)
8. Event and callback catalogue
9. Runtime flows (boot, solo, direct, online, approval, spawn, leave, failure)
10. Test inventory (names, files, owners)
11. Commands reference (`Tools/unity.ps1` output contract)
12. Per-task file manifest
13. Resolved ambiguities and spec deviations
14. Plan-review amendments (adopted when the M1 plan was assembled)

---

## 1. Verified facts (checked in package source on 2026-10-09; treat as decided)

| Id | Fact | Source |
|---|---|---|
| F1 (V1) | The Sessions SDK's `GameObjectsNetcodeNetworkHandler` requires `NetworkManager.Singleton` to exist at create/join time (else `SessionException` with `SessionError.NetworkManagerNotInitialized`). It calls `StartHost()` / `StartClient()` / `Shutdown()` itself and calls `UnityTransport.SetRelayServerData(...)` itself. Our code never calls `StartHost`/`StartClient` on the online path. | multiplayer 2.3.3 `NetworkManagerSession.cs`, `GameObjectsNetcodeNetworkHandler.cs` |
| F2 (V2) | `IHostSession.IsLocked { get; set; }` + `Task SavePropertiesAsync()`; `Task RemovePlayerAsync(string playerId)`; `ISession.Code`, `ISession.LeaveAsync()`; `MultiplayerService.Instance.CreateSessionAsync(SessionOptions)` returns `Task<IHostSession>`; `JoinSessionByCodeAsync(string, JoinSessionOptions = default)` returns `Task<ISession>`; `SessionOptions { MaxPlayers, IsPrivate, IsLocked, Name }`; extension `WithRelayNetwork<T>(this T, string region = default)` in namespace `Unity.Services.Multiplayer`. | `Unity.Services.Multiplayer.api` |
| F3 | `SessionException.Error` is a `SessionError`. **Lobby "full" (16004) and "invalid join code" (16010) are converted to `SessionError.Unknown`** (only `LobbyNotFound` → `SessionError.SessionNotFound = 6`), and the inner `LobbyServiceException` is **not** preserved. FullLobby detection on join therefore relies on message text (see `UgsErrorMap`, T9). | `LobbyConverter.ToSessionException`, `SessionException.cs` |
| F4 | Authentication 3.8.0, in the editor only: if the command line contains `-editor-mode` and `-name <x>`, the auth profile becomes `<x>`. MPPM clones therefore get distinct anonymous PlayerIds automatically (still confirm at runtime, criterion 13). `InitializationOptions.SetProfile(string)` is an extension in namespace `Unity.Services.Authentication`. | `AuthenticationPackageInitializer.GetProfile` |
| F5 (V4) | `NetworkSceneManager.OnSynchronizeComplete(ulong clientId)` is invoked **on the server** for each client whose initial scene sync completes. Use it to spawn late joiners. | NGO 2.13.3 `NetworkSceneManager.cs` ~2486–2503 |
| F6 (V5) | NetworkObjects spawned with `destroyWithScene: false` are moved to the DontDestroyOnLoad scene before a `LoadSceneMode.Single` load, so `SessionState` survives Menu→Spot loads. | `DefaultSceneManagerHandler.MoveObjectsFromSceneToDontDestroyOnLoad` |
| F7 (V6) | MPPM 3.0.0 ships **no code and no asmdef**. `Unity.Multiplayer.PlayMode.CurrentPlayer` (capital **M** in `PlayMode`) lives in the engine module `UnityEngine.MultiplayerModule`. It needs no asmdef reference. Members: `static bool IsMainEditor { get; }`, `static ReadOnlyTags()` and `static Tags { get; }` which return the tag collection. **Always consume tags with `foreach (var tag in CurrentPlayer.ReadOnlyTags())`**, never `==` against a string. Wrap all use in `#if UNITY_EDITOR`. There is **no player-index API**. | `UnityEngine.MultiplayerModule.xml` |
| F8 (V8) | `InputSystem.actions` has a public setter in 1.20.0 that **throws in Play mode**. Call it only from editor code. `TestRunnerApi.SaveResultToFile(ITestResultAdaptor, string)` exists (static) in test-framework 1.8.0. | package sources |
| F9 | `NetworkConfig.ProtocolVersion` is `ushort` (we keep it 0). `NetworkConfig.TickRate` is `uint`. `NetworkManager.ConnectionApprovalCallback` can be reassigned freely. Only a multicast delegate throws. | NGO 2.13.3 `NetworkManager.cs` 749–760 |
| F10 | `ConnectionApprovalResponse` has `Approved`, `CreatePlayerObject`, `PlayerPrefabHash`, `Position`, `Rotation`, `Pending`, `Reason`. `ConnectionApprovalRequest` has `Payload` (byte[]) and `ClientNetworkId`. | NGO 2.13.3 |
| F11 | `NetworkSpawnManager.InstantiateAndSpawn(NetworkObject networkPrefab, ulong ownerClientId = ServerClientId, bool destroyWithScene = false, bool isPlayerObject = false, bool forceOverride = false, Vector3 position = default, Quaternion rotation = default)`. **Always call it with named arguments.** | NGO 2.13.3 |
| F12 | `UnityTransport.SetConnectionData(bool forceOverrideCommandLineArgs, string ipv4Address, ushort port, string listenAddress = null)`. `UnityTransport.Protocol` (enum `UnityTransport.ProtocolType { UnityTransport, RelayUnityTransport }`). `ConnectionData.Address`, `.Port`, `.ServerListenAddress`. Settable int properties: `ConnectTimeoutMS`, `MaxConnectAttempts`, `DisconnectTimeoutMS`, `HeartbeatTimeoutMS`. Namespace `Unity.Netcode.Transports.UTP`, assembly `Unity.Netcode.Runtime`. | NGO 2.13.3 |
| F13 | `NetworkTransform` is in namespace `Unity.Netcode.Components` inside assembly `Unity.Netcode.Runtime`. Fields: `AuthorityMode` (`NetworkTransform.AuthorityModes { Server, Owner }`), `SyncPositionX/Y/Z`, `SyncRotAngleX/Y/Z`, `SyncScaleX/Y/Z`, `InLocalSpace`, `Interpolate`, `PositionInterpolationType`/`RotationInterpolationType` (`NetworkTransform.InterpolationTypes { LegacyLerp, Lerp, SmoothDampening }`), `SwitchTransformSpaceWhenParented`, `UseUnreliableDeltas`. `Teleport(Vector3, Quaternion, Vector3)` is authority-only. | NGO 2.13.3 |
| F14 | `RpcAttribute(SendTo)` with named `InvokePermission` (`RpcInvokePermission { Everyone = 0, Server, Owner }`), `DeferLocal`, `Delivery`. `SendTo` values: `Owner, NotOwner, Server, NotServer, Me, NotMe, Everyone, ClientsAndHost, Authority, NotAuthority, SpecifiedInParams`. `RpcParams.Receive.SenderClientId`. `RequireOwnership` is `[Obsolete]`. | NGO 2.13.3 |
| F15 | `NetworkManager.OnConnectionEvent` is `Action<NetworkManager, ConnectionEventData>`. `ConnectionEventData { ConnectionEvent EventType; ulong ClientId; NativeArray<ulong> PeerClientIds; }`. `ConnectionEvent { ClientConnected, PeerConnected, ClientDisconnected, PeerDisconnected }`. Also `OnServerStarted` (Action), `OnServerStopped`/`OnClientStopped` (Action<bool>), `ShutdownInProgress`, `DisconnectReason`, `DisconnectClient(ulong, string)`. | NGO 2.13.3 |
| F16 | `NetworkSceneManager.OnLoadEventCompleted` is `(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)`. `LoadScene(string, LoadSceneMode)` returns `SceneEventProgressStatus` (check `== SceneEventProgressStatus.Started`). | NGO 2.13.3 |
| F17 | `NetcodeForGameObjectsProjectSettings.instance.GenerateDefaultNetworkPrefabs` (public field, editor asm `Unity.Netcode.Editor`, namespace `Unity.Netcode.Editor.Configuration`). `SaveSettings()` is **internal**. Invoke it by reflection (`BindingFlags.Instance | BindingFlags.NonPublic`). | NGO 2.13.3 |
| F18 | Built-in layer 4 is already named `Water`. User layers start at 6. | `ProjectSettings/TagManager.asset` |
| F19 | Exact UGS asmdef names: `Unity.Services.Core`, `Unity.Services.Authentication`, `Unity.Services.Multiplayer`. Network Simulator runtime asmdef: `Unity.Multiplayer.Tools.NetworkSimulator.Runtime` (not referenced in M1 code). | package tarballs |
| F20 | `.gitignore` already ignores `[Ll]ogs/`, `*.csproj`, `*.slnx`. It does **not** ignore `.vscode/` (T1 appends it). | repo |
| F21 | `NetworkList<T>` requires `T : unmanaged, IEquatable<T>`. Construct it in `Awake` (not in a field initializer), so an unspawned prefab instance doesn't leak native memory. | NGO 2.13.3 |

---

## 2. Shared conventions

### 2.1 Repository, paths, encoding
- Repo root: `C:\Dev\FishGame`. Gameplay code under `Assets/_Project/`. CLI wrapper `Tools/unity.ps1`.
- All text files are UTF-8 **without BOM** (`.cs .json .asmdef .inputactions .uxml .uss .tss .md .meta .ps1`). Use the Write tool. `Tools/unity.ps1` may contain only ASCII.
- One public MonoBehaviour, NetworkBehaviour or ScriptableObject per file, and the file name equals the type name. Other public types are one per file too, unless this contract lists several types in one file. Keep files under ~300 lines.
- RPC methods end in `Rpc`. NetworkVariable fields start with `Net`. Every networked class opens with a `// Authority: ...` class comment.
- `.meta` files for new `.cs`/`.asmdef`/folders are generated by Unity on the next batchmode run. Commit them together with their assets. Exception: `FishGameInput.inputactions.meta` is hand-written (T4).
- Never reference `Assembly-CSharp`. Never put runtime code outside an asmdef folder.
- Avoid `using System.Numerics;` in any file that also has `using UnityEngine;` (it causes the CS0104 `Vector2`/`Vector3` ambiguity). Core public types in this contract use plain `float` fields instead of vectors.
- Never write a bare `Input.` identifier in any `FishGame.*` namespace. The generated wrapper's namespace `FishGame.Input` shadows `UnityEngine.Input`, the same trap as CS0118. The legacy `UnityEngine.Input` is also unusable (Input-System-only project), so don't use it at all. Don't name any type or namespace `Input` or `Editor`.

### 2.2 Namespaces (one per assembly)

| Assembly | Root namespace |
|---|---|
| FishGame.Core | `FishGame.Core` |
| FishGame.Data | `FishGame.Data` |
| FishGame.Runtime | `FishGame.Runtime` (generated input wrapper only: `FishGame.Input`) |
| FishGame.Networking | `FishGame.Networking` |
| FishGame.Gameplay | `FishGame.Gameplay` |
| FishGame.Fishing | `FishGame.Fishing` |
| FishGame.Presentation | `FishGame.Presentation` |
| FishGame.UI | `FishGame.UI` |
| FishGame.App | `FishGame.App` |
| FishGame.Editor, FishGame.Editor.TestMenu | `FishGame.EditorTools` (never `FishGame.Editor`, which causes CS0118) |
| FishGame.Tests.EditMode | `FishGame.Tests.EditMode` |
| FishGame.Tests.PlayMode | `FishGame.Tests.PlayMode` |

Do not create sub-namespaces. Sub-folders are fine.

### 2.3 `$env:FG_OUT` (scratch output convention)
- `FG_OUT` is an **absolute** directory inside **your own session scratchpad**, e.g. `C:\Users\<you>\AppData\Local\Temp\claude\...\scratchpad\fg`. It must not contain `..` and must never be inside the repo.
- PowerShell tool state does not persist between calls, so **every** command that runs Unity begins by setting it:
  ```powershell
  $env:FG_OUT = '<absolute scratchpad path>\fg'; powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 test-edit
  ```
  Plans write the literal text `<absolute scratchpad path>`. The implementer substitutes its own scratchpad path (this is the only permitted placeholder).
- `Tools/unity.ps1` uses `-OutDir` if given, else `$env:FG_OUT`, else it exits 4 with a usage error.
- Call the PowerShell tool with `timeout: 600000` for every Unity run. For T1's first run after the manifest change, use `run_in_background: true` and wait for the completion notice. Package resolution can exceed 10 minutes.

### 2.4 TDD cadence (Unity launches cost minutes)
- One "red" run per step group: write **all** tests for the step, then run `test-edit -Filter <regex>` once. Red is either a compile error naming the missing type (`error CS0246`/`CS0103`) or failing assertions.
- Implement, then run the same command once for green.
- Run the full `test-edit` (no filter) once at the end of each task, before committing.
- Tasks with no EditMode-testable logic (T8, T10–T14) use `compile` as their gate. They are proven by T16's PlayMode tests and the manual checklist (T17).
- `-Filter` is passed to Unity's `-testFilter`, a regex over full test names, e.g. `-Filter "FishGame.Tests.EditMode.Pcg32Tests"`.

### 2.5 Commits
- Only at the commit step of each task. Branch `main`. Never force-push. Never `git add -A` or `git add .`.
- Command shape:
  ```powershell
  git add <explicit files and folders, including generated .meta>; git commit -m "m1(<area>): <summary>" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
  ```
- `<area>` is one of `project, core, net-core, runtime, input, audio, data, net, ugs, interact, session, player, ui, app, builders, tests, docs`.
- Before committing, `git status --porcelain` must show nothing under `Library/ Temp/ Logs/ UserSettings/` and no `*.csproj`, `*.slnx` or `.vscode/`.

### 2.6 Shared-file edits (the only cross-owner edits allowed)
- `Packages/manifest.json`, `Packages/packages-lock.json`: T1 only.
- All `.asmdef` files: T1 only. No later task edits an asmdef. If a later task needs a reference that is missing, that is a contract bug. Stop and report it.
- `CLAUDE.md`: T17 only (the `FishGame.Editor.` → `FishGame.EditorTools.` fix).
- `README.md` (new, repo root): T17 only.
- `Assets/InputSystem_Actions.inputactions`: deleted by T15 (`ProjectSettingsBuilder`), **after** clearing `InputSystem.actions`.
- Generated output (`Assets/_Project/Scenes/*.unity`, `Assets/_Project/Prefabs/*.prefab`, `Assets/_Project/Content/**`, `ProjectSettings/*`) is produced by T15's `BuildAll`. Any later task may regenerate it by running `build` and may commit the regenerated files.
- **Integration-fix rule (T15, T16, T17 only).** These tasks are the first to run the game end to end. They **may** fix defects in files owned by earlier tasks, and they **may add** (never remove) references in an asmdef, when that is needed to make a build or test pass.
  - Limits: the public surface listed in this contract stays exactly as written, and each such fix is named in the commit message (`fix(<file>): <what>`).
  - A fix that needs a listed signature, name or path to change is a contract change: stop and report instead.
  - Every other task fixes only its own files.

### 2.7 Editor-lock rule
`Tools/unity.ps1` checks `Temp/UnityLockfile` first. If it reports `LOCKED` (exit 2), stop the task and report back: the user has the editor open. Never kill Unity processes.

---

## 3. Execution order and dependency graph

Drafting may be parallel. **Execution is strictly serial**, because there is one Unity project lock and one git index.

| # | Task | Depends on (must be committed first) |
|---|---|---|
| 1 | **T1** Project cleanup, manifest, asmdefs, `unity.ps1`, compile hook, test menu | — |
| 2 | **T2** Core primitives + shared Core constants (`SceneNames`, `Layers`, `SlotPalette`) | T1 |
| 3 | **T3** Core connection logic + `NetworkConstants` + `DevFlags` + payload codec | T2 |
| 4 | **T5** Runtime services (registry, bus, settings, pause, instance profile) | T3 |
| 5 | **T4** Input (asset, router, adapter, fake) | T5 |
| 6 | **T6** Audio (Core recipes, Runtime generator/bus/ambience) + flat `WaterSurface` | T5 |
| 7 | **T7** Data (`RegistryIndex`, `SpotDef`, `ContentRegistry`) | T2 |
| 8 | **T8** Networking local (`INetworkSession`, bridge, approver, `LocalNetworkSession`, composite) | T3, T5 |
| 9 | **T9** Networking UGS (`UgsNetworkSession`, `UgsErrorMap`) | T8 |
| 10 | **T12** `NetworkInteractable`, `DockBell`, `NetSpawnAnchor` | T6 |
| 11 | **T10** `SessionState`, `PlayerSlot`, `NetPlayerId`, `PlayerSpawner`, `IServerPlayerInit` | T8, T12 |
| 12 | **T11** PlayerFP components, `WaterVolume`, `WaterExit`, `SceneCameraSwitch` | T4, T6, T10, T12 |
| 13 | **T14** UI Toolkit (UXML/USS/TSS, `UiFlowController`, presenters) | T11 |
| 14 | **T13** App (`AppBootstrap`, `SceneFlow`, `GameScope`, `DevPrefKeys`) | T9, T14 |
| 15 | **T15** Editor builders, validators, architecture test, start-scene hook, Auto Solo | T13 |
| 16 | **T16** PlayMode tests + full `check` | T15 |
| 17 | **T17** README, manual acceptance checklist, CLAUDE.md fix, final `check` | T16 |

Serial order: **T1 → T2 → T3 → T5 → T4 → T6 → T7 → T8 → T9 → T12 → T10 → T11 → T14 → T13 → T15 → T16 → T17.**

Rule: no task references a type owned by a task later in this order. The interfaces `IServerPlayerInit` (T10) and `IJoinContextProvider` (T8) exist only to keep that rule.

---

## 4. Assemblies (T1 creates every file below, with exactly this content)

All asmdefs use `"allowUnsafeCode": false`, `"autoReferenced": true` (tests: `false`), `"noEngineReferences": false` (Core: `true`), `"overrideReferences": false` (tests and TestMenu: `true`), `"precompiledReferences": []` unless shown, `"defineConstraints": []` unless shown, `"versionDefines": []`, `"excludePlatforms": []`, `"includePlatforms": []` unless shown. References are by **name** (not GUID).

| File | `name` / `rootNamespace` | `references` | Extra |
|---|---|---|---|
| `Assets/_Project/Core/FishGame.Core.asmdef` | FishGame.Core | — | `noEngineReferences: true` |
| `Assets/_Project/Data/FishGame.Data.asmdef` | FishGame.Data | FishGame.Core | |
| `Assets/_Project/Runtime/FishGame.Runtime.asmdef` | FishGame.Runtime | FishGame.Core, FishGame.Data, Unity.InputSystem | |
| `Assets/_Project/Networking/FishGame.Networking.asmdef` | FishGame.Networking | FishGame.Core, FishGame.Runtime, Unity.Netcode.Runtime, Unity.Networking.Transport, Unity.Collections, Unity.Services.Core, Unity.Services.Authentication, Unity.Services.Multiplayer | |
| `Assets/_Project/Gameplay/FishGame.Gameplay.asmdef` | FishGame.Gameplay | FishGame.Core, FishGame.Data, FishGame.Runtime, Unity.Netcode.Runtime, Unity.Collections | |
| `Assets/_Project/Fishing/FishGame.Fishing.asmdef` | FishGame.Fishing | FishGame.Core, FishGame.Data, FishGame.Runtime, FishGame.Gameplay, Unity.Netcode.Runtime, Unity.Collections | |
| `Assets/_Project/Presentation/FishGame.Presentation.asmdef` | FishGame.Presentation | FishGame.Core, FishGame.Data, FishGame.Runtime, FishGame.Gameplay, FishGame.Fishing, Unity.Netcode.Runtime, Unity.Collections, Unity.RenderPipelines.Universal.Runtime, Unity.RenderPipelines.Core.Runtime | |
| `Assets/_Project/UI/FishGame.UI.asmdef` | FishGame.UI | FishGame.Core, FishGame.Data, FishGame.Runtime, FishGame.Gameplay, FishGame.Fishing, FishGame.Presentation, Unity.InputSystem, Unity.Netcode.Runtime, Unity.Collections | |
| `Assets/_Project/App/FishGame.App.asmdef` | FishGame.App | FishGame.Core, FishGame.Data, FishGame.Runtime, FishGame.Networking, FishGame.Gameplay, FishGame.Fishing, FishGame.Presentation, FishGame.UI, Unity.Netcode.Runtime, Unity.Collections, Unity.InputSystem | |
| `Assets/_Project/Editor/FishGame.Editor.asmdef` | FishGame.Editor (rootNamespace `FishGame.EditorTools`) | FishGame.Core, FishGame.Data, FishGame.Runtime, FishGame.Networking, FishGame.Gameplay, FishGame.Fishing, FishGame.Presentation, FishGame.UI, FishGame.App, Unity.Netcode.Runtime, Unity.Netcode.Editor, Unity.Collections, Unity.InputSystem, Unity.RenderPipelines.Universal.Runtime, Unity.RenderPipelines.Core.Runtime, UnityEngine.UI | `includePlatforms: ["Editor"]` |
| `Assets/_Project/Editor/TestMenu/FishGame.Editor.TestMenu.asmdef` | FishGame.Editor.TestMenu (rootNamespace `FishGame.EditorTools`) | UnityEngine.TestRunner, UnityEditor.TestRunner, FishGame.Editor, FishGame.Core | `includePlatforms: ["Editor"]`, `overrideReferences: true`, `precompiledReferences: ["nunit.framework.dll"]`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`, `autoReferenced: false` |
| `Assets/_Project/Tests/EditMode/FishGame.Tests.EditMode.asmdef` | FishGame.Tests.EditMode | UnityEngine.TestRunner, UnityEditor.TestRunner, FishGame.Core, FishGame.Data, FishGame.Runtime, FishGame.Editor, Unity.InputSystem, Unity.Netcode.Runtime | `includePlatforms: ["Editor"]`, `overrideReferences: true`, `precompiledReferences: ["nunit.framework.dll"]`, `autoReferenced: false`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]` |
| `Assets/_Project/Tests/PlayMode/FishGame.Tests.PlayMode.asmdef` | FishGame.Tests.PlayMode | UnityEngine.TestRunner, FishGame.Core, FishGame.Data, FishGame.Runtime, FishGame.Networking, FishGame.Gameplay, FishGame.Fishing, FishGame.App, Unity.Netcode.Runtime, Unity.Collections, Unity.InputSystem | `includePlatforms: []`, `overrideReferences: true`, `precompiledReferences: ["nunit.framework.dll"]`, `autoReferenced: false`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]` |

**Assembly markers (T1).** Each runtime asmdef folder gets `AssemblyInfo.cs`, so every assembly compiles before its first real type lands:
```csharp
// Assembly marker (T1). Keeps this asmdef compiling before its first type exists. Do not delete.
namespace FishGame.Core { internal static class AssemblyMarker { } }
```
(The namespace changes per assembly: Core, Data, Runtime, Networking, Gameplay, Fishing, Presentation, UI, App. Editor uses `FishGame.EditorTools` with class name `EditorAssemblyMarker`, and TestMenu uses `TestMenuAssemblyMarker`.) `Tests/PlayMode/AssemblyInfo.cs` uses `namespace FishGame.Tests.PlayMode { internal static class AssemblyMarker { } }`. Fishing and Presentation keep only their marker in M1.

**Manifest (T1, `Packages/manifest.json`, no BOM).** Remove `com.unity.visualscripting` and `com.unity.collab-proxy`. Add `com.unity.netcode.gameobjects: 2.13.3`, `com.unity.services.multiplayer: 2.3.3`, `com.unity.services.authentication: 3.8.0`, `com.unity.services.core: 1.18.0`, `com.unity.multiplayer.playmode: 3.0.0`, `com.unity.multiplayer.tools: 2.2.12`, `com.unity.nuget.newtonsoft-json: 3.2.2`. Keep every other existing entry unchanged, including `com.unity.pipeline: 0.8.0-exp.1`. Never add the §4.4 "never add" packages. Commit `packages-lock.json`.

---

## 5. Constants (single source; owners in brackets)

### 5.1 `FishGame.Core.NetworkConstants` [T3] — `Assets/_Project/Core/Net/NetworkConstants.cs`
```csharp
public static class NetworkConstants
{
    public const ushort DefaultPort = 7777;          // solo first try; dev-direct host + clients
    public const ushort SoloPortRetryCount = 10;     // solo tries Port .. Port+9 (7777..7786 by default)
    public const ushort TestPort = 7790;             // PlayMode tests set LocalNetworkSession.Port = TestPort
    public const string LoopbackAddress = "127.0.0.1";
    public const uint TickRate = 30;
    public const int MaxPlayers = 4;
    public const ushort NgoProtocolVersion = 0;      // NetworkConfig.ProtocolVersion, never changes
    public const int ConnectTimeoutMS = 1000;
    public const int MaxConnectAttempts = 10;
    public const int DisconnectTimeoutMS = 10000;
    public const int LongDisconnectTimeoutMS = 120000; // -dev-long-timeout
    public const int HeartbeatTimeoutMS = 500;
    public const double AsyncTimeoutSeconds = 15.0;
    public const double VersionMismatchWindowSeconds = 8.0; // empty-reason drop sooner than this while Connecting => VersionMismatch
}
```

### 5.2 `FishGame.Core.SceneNames` [T2] — `Assets/_Project/Core/Game/SceneNames.cs`
```csharp
public static class SceneNames
{
    public const string Bootstrap = "Bootstrap";
    public const string Menu = "Menu";
    public const string SpotLakeDock = "Spot_LakeDock";
    public const string SpotPrefix = "Spot_";
    public static readonly string[] BuildOrder = { Bootstrap, Menu, SpotLakeDock };
    public static bool IsGameplay(string sceneName); // true when sceneName starts with SpotPrefix (ordinal)
}
```

### 5.3 `FishGame.Core.Layers` [T2] — `Assets/_Project/Core/Game/Layers.cs`
```csharp
public static class Layers
{
    public const int Default = 0;
    public const int Water = 4;          // built-in name "Water" (already present)
    public const int Player = 6;         // local owner's root (CharacterController)
    public const int RemotePlayer = 7;   // remote roots (CapsuleCollider)
    public const int Ground = 8;         // lakebed
    public const int Shore = 9;          // banks, rocks, trees
    public const int Structure = 10;     // dock, posts, ladders, ramp
    public const int Interactable = 11;  // DockBell collider
    public const int Fish = 12;          // M5, named now
    public const int Ragdoll = 13;       // M5, named now
    public static readonly int[] UserLayerIndices = { 6, 7, 8, 9, 10, 11, 12, 13 };
    public static readonly string[] UserLayerNames = { "Player", "RemotePlayer", "Ground", "Shore", "Structure", "Interactable", "Fish", "Ragdoll" };
    public static int Mask(int layer) => 1 << layer;
}
```
Physics matrix (T15 applies): Fish ignores Player and RemotePlayer; Ragdoll ignores Player, RemotePlayer and Fish. Everything else stays at the default (collide).

### 5.4 `FishGame.Core.SlotPalette` [T2] — `Assets/_Project/Core/Game/SlotPalette.cs`
```csharp
public static class SlotPalette
{
    public const int Count = 4;
    // slot 0 coral (0.90,0.36,0.24); 1 blue (0.22,0.52,0.90); 2 green (0.30,0.72,0.32); 3 yellow (0.95,0.78,0.20); skin (0.96,0.80,0.66)
    public static void GetRgb(int slot, out float r, out float g, out float b); // ArgumentOutOfRangeException outside 0..3
    public static void GetSkinRgb(out float r, out float g, out float b);
}
```

### 5.5 Movement, camera, water numbers — `FishGame.Core.MovementMath` [T2] (see §7.2)

### 5.6 Other single-owner constants
| Constant | Value | Owner / file |
|---|---|---|
| `BuildInfo.ProtocolVersion` | `1` (int; bump by hand on any wire/RPC/networked-prefab change) | T2 `Core/Versioning/BuildInfo.cs` |
| `BuildInfo.GameVersion` | `"0.1.0"` | T2 |
| `ContentRegistry` `dataVersion` default | `1` | T7 |
| `ContentIds.LakeDock` | `"lake_dock"` | T7 `Core/Content/ContentIds.cs` |
| `SfxRecipes.SampleRate` | `44100` | T6 |
| `DevPrefKeys.AutoSolo` | `"FishGame.AutoSolo"` | T13 `Runtime/Dev/DevPrefKeys.cs` |
| `InputActionNames.AssetPath` | `"Assets/_Project/Runtime/Input/FishGameInput.inputactions"` | T4 |
| `DisplayNameRules.MaxLength` | `16` | T3 |
| Settings default display name | `"Fisher"` | T5 |
| Lake-dock layout seed | `0xF15ED0C4UL` | T15 internal |

---

## 6. Assets, prefabs, scenes and hierarchies

### 6.1 Folder layout (created as needed by the owning task)
```
Assets/_Project/
  Core/{Random,Hashing,Versioning,Net,Player,Input,Audio,Content,Game}/   T2,T3,T4,T6,T7
  Data/                                   T7
  Content/{Spots,Materials,Network,UI,Meshes}/   T15 (generated) + ContentRegistry.asset
  Runtime/{Services,Settings,Input,Audio,World,Net,Dev}/   T5,T4,T6,T8,T13
  Networking/                             T8,T9
  Gameplay/{Interaction,Session,Spawning,Player,World}/    T12,T10,T11
  Fishing/  Presentation/                 (marker only)
  UI/{Uxml,Uss}/ + UI/*.cs + UI/FishGameTheme.tss          T14
  App/                                    T13
  Scenes/  Prefabs/                       T15 output
  Editor/{Builders,Validation,Dev}/ + Editor/*.cs          T1 (hooks), T15
  Editor/TestMenu/                        T1 (+ T15 adds one file)
  Tests/EditMode/ Tests/PlayMode/         all
Tools/unity.ps1                           T1
```

### 6.2 Asset paths

| Asset | Path | Created by | Notes |
|---|---|---|---|
| Input actions | `Assets/_Project/Runtime/Input/FishGameInput.inputactions` (+ hand-written `.meta`) | T4 | generated wrapper `Assets/_Project/Runtime/Input/FishGameInput.cs` (namespace `FishGame.Input`, class `FishGameInput`) is written by Unity on import. It is committed and **referenced by no code**. |
| Content registry | `Assets/_Project/Content/ContentRegistry.asset` | T15 ContentBuilder (create-if-missing) | type `FishGame.Data.ContentRegistry` |
| Lake-dock spot | `Assets/_Project/Content/Spots/lake_dock.asset` | T15 (create-if-missing) | `SpotDef` id `lake_dock`, displayName `Lake Dock`, sceneName `Spot_LakeDock` |
| Materials | `Assets/_Project/Content/Materials/M_Body_Slot0.mat` … `M_Body_Slot3.mat`, `M_Skin.mat`, `M_Water.mat`, `M_Wood.mat`, `M_WoodDark.mat`, `M_Rock.mat`, `M_Grass.mat`, `M_Sand.mat`, `M_Foliage.mat`, `M_Brass.mat` | T15 | shader `Universal Render Pipeline/Lit`, `_BaseColor` only. Slot colours from `SlotPalette`. Rebuilt in place. |
| Panel settings | `Assets/_Project/Content/UI/PanelSettings.asset` | T15 (create-if-missing) | Scale With Screen Size, 1920×1080, match 0.5 (`MatchWidthOrHeight`), `themeStyleSheet` = `FishGameTheme.tss` |
| UI theme | `Assets/_Project/UI/FishGameTheme.tss` | T14 | content: `@import url("unity-theme://default");` plus `@import url("Uss/App.uss");` |
| UI layout | `Assets/_Project/UI/Uxml/AppRoot.uxml` | T14 | single root document holding every screen |
| UI styles | `Assets/_Project/UI/Uss/App.uss` | T14 | |
| Network prefab list | `Assets/_Project/Content/Network/FishGameNetworkPrefabs.asset` | T15 | `Unity.Netcode.NetworkPrefabsList` containing PlayerFP, DockBell, SessionState |
| Player prefab | `Assets/_Project/Prefabs/PlayerFP.prefab` | T15 | §6.3 |
| Bell prefab | `Assets/_Project/Prefabs/DockBell.prefab` | T15 | §6.4 |
| Session state prefab | `Assets/_Project/Prefabs/SessionState.prefab` | T15 | root: `NetworkObject` + `SessionState` only |
| Scenes | `Assets/_Project/Scenes/Bootstrap.unity`, `Menu.unity`, `Spot_LakeDock.unity` | T15 | build order §5.2 |

Invariant 4.7 #7: after T15 nothing lives under `Assets/` except `Assets/_Project/**` and `Assets/Settings/**`.

### 6.3 `PlayerFP` prefab (built by T15 PrefabBuilder; T11 code relies on these names and components)

Root `PlayerFP` (layer set at runtime by `PlayerNetState`). Components, in this order:

| Component | Prefab state | Configuration |
|---|---|---|
| `NetworkObject` | — | default; never destroyed/re-added on rebuild |
| `NetworkTransform` | enabled | `AuthorityMode = Owner`; SyncPositionX/Y/Z true; SyncRotAngleX false, Y true, Z false; SyncScaleX/Y/Z false; `InLocalSpace = false`; `Interpolate = true`; `PositionInterpolationType = Lerp`; `RotationInterpolationType = Lerp`; `UseUnreliableDeltas = false`; `SwitchTransformSpaceWhenParented = true` |
| `CharacterController` | **disabled** | height 1.8, radius 0.35, center (0,0.9,0), stepOffset 0.35, slopeLimit 45, skinWidth 0.08 |
| `CapsuleCollider` | **disabled** | height 1.8, radius 0.35, center (0,0.9,0), direction Y |
| `PlayerNetState` | enabled | — |
| `PlayerActions` | enabled | — |
| `PlayerLocomotion` | **disabled** | — |
| `PlayerCamera` | **disabled** | — |
| `PlayerBody` | enabled | serialized `slotMaterials` = M_Body_Slot0..3, `skinMaterial` = M_Skin |
| `PlayerInteractor` | **disabled** | — |

Children (names are the `PlayerRigPaths` constants, §7.11). Positions are local to the parent. Primitive colliders are removed from every visual.

| Path | Kind | Local position | Local scale | Material |
|---|---|---|---|---|
| `HeadPivot` | empty | (0, 1.62, 0) | 1 | — |
| `HeadPivot/Shake` | empty | (0,0,0) | 1 | — |
| `HeadPivot/Shake/Camera` | `Camera` (disabled, near 0.05, far 400, tag `MainCamera`) + `AudioListener` (disabled) | (0,0,0) | 1 | — |
| `HeadPivot/Head` | sphere | (0,0,0) | 0.28 | M_Skin |
| `HeadPivot/Head/Nose` | cube | (0,0,0.5) | (0.18,0.18,0.3) | M_Skin |
| `Body` | empty | (0,0,0) | 1 | — |
| `Body/Torso` | cube | (0,1.2,0) | (0.42,0.55,0.24) | slot |
| `Body/ArmL`, `Body/ArmR` | empty pivots | (∓0.27, 1.42, 0) | 1 | — |
| `Body/ArmL/Mesh`, `Body/ArmR/Mesh` | capsule | (0,−0.25,0) | (0.12,0.25,0.12) | slot |
| `Body/LegL`, `Body/LegR` | empty pivots | (∓0.11, 0.8, 0) | 1 | — |
| `Body/LegL/Mesh`, `Body/LegR/Mesh` | capsule | (0,−0.4,0) | (0.16,0.4,0.16) | slot |
| `NameplateAnchor` | empty | (0, 2.05, 0) | 1 | — |

### 6.4 `DockBell` prefab (T15)
Root `DockBell`: `NetworkObject`, `DockBell`, `BoxCollider` (size (0.6,1.6,0.6), center (0,0.8,0), non-trigger), layer `Interactable`. Children: `Post` (cylinder, scale (0.12,0.8,0.12), pos (0,0.8,0), M_Wood), `Bell` (sphere, scale (0.35,0.4,0.35), pos (0,1.45,0), M_Brass). All child primitive colliders are removed, and every child is on layer Interactable.

### 6.5 Scenes (T15 SceneBuilder; exact root object names)

**Bootstrap** (build index 0)
```
App                    AppBootstrap, NetworkManager, UnityTransport, PlayerInputAdapter, AudioBus
  UI                   UIDocument (panelSettings = PanelSettings.asset, visualTreeAsset = AppRoot.uxml), UiFlowController
  EventSystem          EventSystem, InputSystemUIInputModule (default actions)
```
NetworkManager configured by the builder: `NetworkConfig.NetworkTransport` = the UnityTransport, `TickRate = 30`, `ConnectionApproval = true`, `EnableSceneManagement = true`, `PlayerPrefab = null`, `ProtocolVersion = 0`, `ForceSamePrefabs = true`, `Prefabs.NetworkPrefabsLists = [FishGameNetworkPrefabs.asset]`. `AppBootstrap.Configure(sessionStatePrefab, inputActions, contentRegistry)` is called by the builder.

**Menu** (index 1): `MenuCamera` (Camera, solid colour background, AudioListener, tag MainCamera), `Sun` (Directional Light).

**Spot_LakeDock** (index 2). Key numbers (lake centred at origin, water y = 0):

| Root object | Content | Layer |
|---|---|---|
| `GameScope` | `GameScope` (T13) | Default |
| `PlayerSpawner` | `PlayerSpawner` + children `Spawn0..Spawn3` on the dock at (0, 0.65, −50.5), (0, 0.65, −49.0), (0, 0.65, −47.5), (0, 0.65, −46.0), facing +z (yaw 0) | Default |
| `SceneCamera` | Camera (tag MainCamera) + AudioListener + `SceneCameraSwitch` (T11), at (0, 4, −60) looking at the dock | Default |
| `Sun` | Directional Light, rotation (50, −30, 0), soft shadows | Default |
| `Ambience` | `AmbienceBed` (T6) | Default |
| `WaterVisual` | Plane scaled to 120 × 120 m at y 0, M_Water, MeshCollider removed | Water |
| `WaterVolume` | `BoxCollider` trigger, size (120, 8, 120), centre (0, −4, 0) so the top face is y 0; `WaterVolume` (T11) | Water |
| `Lakebed` | procedural mesh + MeshCollider. Shoreline radius R = 50; depth at distance d inward from the shoreline = −6·clamp(d/40, 0, 1) (0 m at the shoreline, −6 m at 40 m out) | Ground |
| `Shore` | ring mesh from r 49 (y −0.2) rising to y 1.0 at r 56, flat to r 90 + MeshCollider; seeded rocks (r 48–54) and trees (r 58–85) as primitive children | Shore |
| `Dock` | deck 3 × 12 m (x ∈ [−1.5, 1.5], z ∈ [−52, −40]), top at y 0.6, thickness 0.1; posts every 2 m at x ±1.4 down to the lakebed; ladders at z −41.5 on both sides | Structure |
| `Exits` | three `WaterExit` (T11) trigger boxes: `LadderL` at (−2.1, −0.8, −41.5) size (1.2, 2.6, 1.2) with child `Top` at (−0.9, 0.65, −41.5); `LadderR` mirrored (x +2.1, Top x +0.9); `Ramp` at (12, −0.9, −44) size (3, 2.4, 4) (lakebed there ≈ −0.66 m, so wading players are inside it) with `Top` at (12, 0.45, −50.5); plus a sloped `RampDeck` box (layer Structure) from the lakebed at about (12, −0.9, −43) up to the shore at about (12, 0.4, −51) | Water (volumes) |
| `BellAnchor` | `NetSpawnAnchor` (T12), prefab = DockBell, at (1.1, 0.6, −40.6) | Default |

### 6.6 Editor menu items (single owners)
| Menu path | Owner | Method |
|---|---|---|
| `FishGame/Build All` | T15 | `ProjectBuilder.BuildAllMenu()` |
| `FishGame/Validate` | T15 | `ValidationRunner.RunMenu()` |
| `FishGame/Dev/Auto Solo` (checkable) | T15 | `AutoSoloMenu.Toggle()` / `ValidateToggle()` |
| `FishGame/Run EditMode Tests` | T1 | `TestMenu.RunEditMode()` |
| `FishGame/Run PlayMode Tests` | T1 | `TestMenu.RunPlayMode()` |

---

## 7. Types by task (public surface)

Notation: everything listed is `public` unless marked. "File" paths are relative to `Assets/_Project/`.

### 7.1 T1 — project foundation (Editor + tooling)

**`FishGame.EditorTools.CompileLogHook`** — `Editor/CompileLogHook.cs`, asm FishGame.Editor
```csharp
[InitializeOnLoad]
public static class CompileLogHook
{
    public const string RelativeLogPath = "Logs/FishGame/compile.txt";
    static CompileLogHook(); // subscribes CompilationPipeline.compilationStarted / assemblyCompilationFinished / compilationFinished
    // Writes (overwrites per compilation run) a UTF-8 file: first line "FG COMPILE <PASS|FAIL> errors=N warnings=M at=<ISO8601>",
    // then one line per CompilerMessage: "<Error|Warning> <file>(<line>,<col>): <message>".
}
```

**`FishGame.EditorTools.TestMenu`** — `Editor/TestMenu/TestMenu.cs`, asm FishGame.Editor.TestMenu
```csharp
[InitializeOnLoad]
public static class TestMenu
{
    public const string EditModeResultPath = "Logs/FishGame/editmode.xml";
    public const string PlayModeResultPath = "Logs/FishGame/playmode.xml";
    [MenuItem("FishGame/Run EditMode Tests")] public static void RunEditMode();
    [MenuItem("FishGame/Run PlayMode Tests")] public static void RunPlayMode();
    // Uses TestRunnerApi (ScriptableObject.CreateInstance<TestRunnerApi>()). A private static ctor registers one ICallbacks per
    // domain load (callbacks are dropped on domain reload); the pending run is kept in UnityEditor.SessionState. On RunFinished it calls
    // TestRunnerApi.SaveResultToFile(result, fullPath) and logs "FG TESTS <mode> passed=N failed=M".
}
```

**Smoke test** — `Tests/EditMode/SmokeTests.cs`: `public class SmokeTests { [Test] public void Smoke_EditModeAssembly_Runs() }` (asserts `1 + 1 == 2`).

**`Tools/unity.ps1`** — contract in §11.

### 7.2 T2 — Core primitives (asm FishGame.Core)

```csharp
// Core/Random/Pcg32.cs — PCG-XSH-RR 32 (O'Neill), seeding exactly like pcg32_srandom_r.
public struct Pcg32
{
    public const ulong ZeroSeedReplacement = 0x853C49E6748FEA9BUL;
    public Pcg32(ulong seed, ulong stream = 0);   // seed 0 -> ZeroSeedReplacement; inc = (stream << 1) | 1; state = 0; Step(); state += seed; Step();
    public uint NextUInt();                        // XSH-RR output; reference vector: new Pcg32(42, 54) -> 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e
    public float NextFloat();                      // [0,1): (NextUInt() >> 8) * (1f / 16777216f)
    public double NextDouble();                    // [0,1): (NextUInt() >> 5 combined with next >> 6) / 2^53 (any exact 53-bit method)
    public float Range(float minInclusive, float maxExclusive);
    public int Range(int minInclusive, int maxExclusive); // unbiased (rejection); ArgumentException if max <= min
}

// Core/Random/Rng.cs
public static class Rng
{
    public static ulong SplitMix64(ulong x); // z = x + 0x9E3779B97F4A7C15; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9; z = (z ^ (z >> 27)) * 0x94D049BB133111EB; return z ^ (z >> 31)
    public static ulong Derive(ulong runSeed, string streamId, ulong index); // z = SplitMix64(runSeed ^ Fnv1a64.Hash(streamId)); z = SplitMix64(z ^ index); return z == 0 ? 1UL : z
    public static Pcg32 Stream(ulong runSeed, string streamId, ulong index);  // new Pcg32(Derive(...))
}

// Core/Hashing/Fnv1a64.cs — mutable struct; use as a local.
public struct Fnv1a64
{
    public const ulong OffsetBasis = 14695981039346656037UL;
    public const ulong Prime = 1099511628211UL;
    public static Fnv1a64 Create();
    public ulong Value { get; }
    public void Append(byte b);
    public void Append(byte[] bytes);
    public void Append(string s);            // UTF-8 bytes of s (null treated as "")
    public void AppendInt32LE(int value);
    public static ulong Hash(string s);      // vectors: "" -> 0xcbf29ce484222325, "a" -> 0xaf63dc4c8601ec8c, "foobar" -> 0x85944171f73967e8
    public static ulong Hash(byte[] bytes);
}

// Core/Hashing/ContentHash.cs  (file also holds ContentCategoryIds)
public readonly struct ContentCategoryIds
{
    public readonly string Category; public readonly IReadOnlyList<string> Ids;
    public ContentCategoryIds(string category, IReadOnlyList<string> ids);
}
public static class ContentHash
{
    // Order-independent: categories sorted by Category (StringComparer.Ordinal); ids within each sorted ordinal.
    // For each category: Append(category); Append((byte)0x1F); for each id: Append(id); Append((byte)0x1E).
    // After all categories: Append((byte)0x1D); AppendInt32LE(dataVersion). Returns Value.
    public static ulong Compute(IReadOnlyList<ContentCategoryIds> categories, int dataVersion);
}

// Core/Versioning/BuildInfo.cs
public static class BuildInfo
{
    public const int ProtocolVersion = 1;
    public const string GameVersion = "0.1.0";
    public static int EffectiveProtocolVersion(int devProtocolOffset) => ProtocolVersion + devProtocolOffset;
}

// Core/Net/JoinCode.cs
public static class JoinCode
{
    public const int MaxLength = 16;
    public static string Normalize(string raw);        // null -> ""; Trim(); ToUpperInvariant()
    public static bool IsPlausible(string normalized); // 1..16 chars, each A-Z or 0-9
}

// Core/Player/PitchQuant.cs
public static class PitchQuant
{
    public const float LimitDeg = 85f;
    public const short SendThreshold = 50;           // 0.5 deg
    public static short Quantize(float pitchDeg);    // clamp to ±85, then (short)Math.Round(p * 100, MidpointRounding.AwayFromZero)
    public static float Dequantize(short q);         // q / 100f
    public static bool ShouldSend(short lastSent, short current); // |current - lastSent| >= SendThreshold
}

// Core/Player/MovementMath.cs
public static class MovementMath
{
    public const float WalkSpeed = 4.2f, SprintSpeed = 6.6f, AimSpeedMult = 0.6f, SwimSpeed = 2.2f;
    public const float GroundAccel = 14f, AirAccel = 4f, KnockbackDecayGround = 10f, KnockbackDecayAir = 2f;
    public const float Gravity = -22f, TerminalVelocity = -30f, GroundedStickVelocity = -2f;
    public const float JumpHeight = 1.1f, CoyoteTime = 0.1f, JumpBufferTime = 0.1f;
    public const float SwimGravity = -4f, SwimEnterDepth = 1.2f, SwimEyeAboveSurface = 0.15f, WadeDepth = 0.3f;
    public const float SwimBuoyancyStiffness = 12f, SwimBuoyancyDamping = 5f, SwimMaxVerticalSpeed = 3f;
    public const float ClimbDuration = 0.4f, WaterTimeoutSeconds = 60f, OutOfBoundsY = -20f;
    public const float EyeHeight = 1.62f, CapsuleHeight = 1.8f, CapsuleRadius = 0.35f, CapsuleCenterY = 0.9f;
    public const float StepOffset = 0.35f, SlopeLimit = 45f, SkinWidth = 0.08f;
    public const float PitchLimit = 85f, NearClip = 0.05f, FarClip = 400f;
    public const float DefaultHorizontalFov = 90f, SprintFovKickDeg = 4f, SprintFovKickTime = 0.25f;
    public const float HeadBobStride = 1.4f, HeadBobAmplitude = 0.03f;
    public const float TraumaDecayPerSecond = 1.2f, TraumaMaxAngleDeg = 2.5f, TraumaFrequency = 20f;
    public const float CarryMaxKg = 40f, CarryMinMult = 0.55f, CarryNoSprintKg = 15f;

    public static float JumpVelocity(float jumpHeight, float gravity);       // sqrt(2 * |gravity| * jumpHeight); (1.1, -22) ≈ 6.957
    public static float JumpPeakHeight(float launchVelocity, float gravity); // v^2 / (2|g|)
    public static float CarrySpeedMult(float kg);                            // clamp(1 - kg/40, 0.55, 1); (0) = 1, (40) = 0.55
    public static float WishSpeed(bool sprint, bool aiming, bool swimming, float carryKg);
        // swimming -> SwimSpeed; else (sprint && !aiming && carryKg <= CarryNoSprintKg ? SprintSpeed : WalkSpeed) * (aiming ? AimSpeedMult : 1) * CarrySpeedMult(carryKg)
        // callers pass sprint = sprintHeld && grounded (no sprint in the air)
    public static void Accelerate(ref float vx, ref float vz, float targetX, float targetZ, float accel, float dt);
        // moves (vx,vz) toward (targetX,targetZ) by at most accel*dt (vector length); exact arrival when closer
    public static void DecayKnockback(ref float kx, ref float kz, float decay, float dt); // reduces length by decay*dt, not below 0
    public static float VerticalStep(ref float vy, float gravity, float terminalVelocity, float dt);
        // returns dy = vy*dt + 0.5*gravity*dt*dt; then vy = max(vy + gravity*dt, terminalVelocity). Exact for constant g, so jump peak == JumpHeight.
    public static float SwimTargetRootY(float surfaceY);                     // surfaceY + SwimEyeAboveSurface - EyeHeight
    public static float SwimVerticalVelocity(float rootY, float surfaceY, float vy, float dt);
        // target = SwimTargetRootY(surfaceY); a = (target - rootY)*SwimBuoyancyStiffness - vy*SwimBuoyancyDamping + (rootY > target ? SwimGravity : 0);
        // return clamp(vy + a*dt, -SwimMaxVerticalSpeed, SwimMaxVerticalSpeed)
    public static bool IsSwimmingDepth(float rootY, float surfaceY);         // rootY < surfaceY - SwimEnterDepth
    public static bool IsInWater(float rootY, float surfaceY);               // rootY < surfaceY - WadeDepth (wading or swimming; gates WaterExit climbs)
}

// Core/Player/JumpAssist.cs — coyote time + jump buffer.
public struct JumpAssist
{
    public static JumpAssist Create();  // both timers = float.PositiveInfinity (a default(JumpAssist) would allow an immediate jump; always use Create)
    public float TimeSinceGrounded { get; }
    public float TimeSinceJumpPressed { get; }
    // Order per tick: if (grounded) sinceGrounded = 0 else sinceGrounded += dt;
    //                 if (jumpPressed) sinceJump = 0 else sinceJump += dt;
    //                 if (sinceJump <= JumpBufferTime && sinceGrounded <= CoyoteTime) { both = +Inf; return true; } return false;
    public bool Tick(float dt, bool grounded, bool jumpPressed);
}
```
Also owns §5.2 `SceneNames`, §5.3 `Layers`, §5.4 `SlotPalette`.

### 7.3 T3 — Core connection logic (asm FishGame.Core, folder `Core/Net/`)

```csharp
public enum ConnState : byte { Offline = 0, Connecting = 1, Connected = 2, ShuttingDown = 3, Failed = 4 }
public enum ConnEvent : byte { Begin = 0, Established = 1, Fail = 2, UnexpectedStop = 3, Leave = 4, ShutdownComplete = 5 }
public enum EndReason : byte { UserLeft = 0, HostLeft = 1, Kicked = 2, Timeout = 3, FullLobby = 4, JoinsClosed = 5,
                               VersionMismatch = 6, InvalidCode = 7, ServicesUnavailable = 8, TransportFailure = 9,
                               Unknown = 10, DuplicateIdentity = 11 }
public enum SessionMode : byte { None = 0, Solo = 1, DevDirect = 2, Online = 3 }
public enum IdentityKind : byte { Local = 0, Ugs = 1, Steam = 2 }
```
One file per enum: `ConnState.cs`, `ConnEvent.cs`, `EndReason.cs`, `SessionMode.cs`, `IdentityKind.cs`.

**Legal transitions (exactly 8; every other (state, event) pair of the 5 × 6 grid is illegal):**

| From | Event | To |
|---|---|---|
| Offline | Begin | Connecting |
| Connecting | Established | Connected |
| Connecting | Fail | Failed |
| Connected | UnexpectedStop | Failed |
| Connected | Leave | ShuttingDown |
| Failed | Leave | ShuttingDown |
| Failed | ShutdownComplete | Offline |
| ShuttingDown | ShutdownComplete | Offline |

```csharp
// InvalidTransitionException.cs
public sealed class InvalidTransitionException : InvalidOperationException
{
    public ConnState From { get; } public ConnEvent Event { get; }
    public InvalidTransitionException(ConnState from, ConnEvent ev); // message "Invalid connection transition: {from} --{ev}-->"
}

// ConnectionStateMachine.cs
public sealed class ConnectionStateMachine
{
    public ConnState State { get; }                 // starts Offline
    public EndReason FailureReason { get; }         // reset to Unknown on Begin; set to `reason` on Fail/UnexpectedStop
    public event Action<ConnState, ConnState> Changed; // (from, to), raised after State updates
    public void Fire(ConnEvent ev, EndReason reason = EndReason.Unknown); // throws InvalidTransitionException
    public bool TryFire(ConnEvent ev, EndReason reason = EndReason.Unknown);
    public static ConnState Next(ConnState from, ConnEvent ev);           // throws InvalidTransitionException
    public static bool TryNext(ConnState from, ConnEvent ev, out ConnState to);
}

// PlayerKey.cs
public readonly struct PlayerKey : IEquatable<PlayerKey>
{
    public const int MaxIdLength = 64;
    public readonly IdentityKind Kind; public readonly string Id;
    public PlayerKey(IdentityKind kind, string id);
    public bool IsValid { get; }                    // !string.IsNullOrEmpty(Id) && Id.Length <= MaxIdLength
    public bool Equals(PlayerKey other);            // Kind equal && string.Equals(Id, other.Id, StringComparison.Ordinal)
    public override bool Equals(object obj); public override int GetHashCode(); public override string ToString(); // "Local:abc"
    public static bool operator ==(PlayerKey a, PlayerKey b); public static bool operator !=(PlayerKey a, PlayerKey b);
}

// JoinRules.cs (file also holds JoinRequest, RosterEntry, JoinContext, JoinVerdict)
public readonly struct JoinRequest
{
    public readonly ulong ClientId; public readonly bool IsServerSelf; public readonly int ProtocolVersion;
    public readonly ulong ContentHash; public readonly PlayerKey Key; public readonly string Name;
    public JoinRequest(ulong clientId, bool isServerSelf, int protocolVersion, ulong contentHash, PlayerKey key, string name);
}
public readonly struct RosterEntry
{
    public readonly PlayerKey Key; public readonly byte Slot; public readonly bool Connected;
    public RosterEntry(PlayerKey key, byte slot, bool connected);
}
public readonly struct JoinContext
{
    public readonly SessionMode Mode; public readonly int ProtocolVersion; public readonly ulong ContentHash;
    public readonly bool JoinsOpen; public readonly int MaxPlayers; public readonly IReadOnlyList<RosterEntry> Roster;
    public JoinContext(SessionMode mode, int protocolVersion, ulong contentHash, bool joinsOpen, int maxPlayers, IReadOnlyList<RosterEntry> roster);
}
public readonly struct JoinVerdict
{
    public const byte NoSlot = 255;
    public readonly bool Approved; public readonly EndReason Reason; public readonly byte Slot; public readonly bool Reclaimed;
    public static JoinVerdict Approve(byte slot, bool reclaimed);  // Reason = UserLeft (unused when approved)
    public static JoinVerdict Reject(EndReason reason);            // Slot = NoSlot
}
public static class JoinRules
{
    // In order (spec 6.6):
    // 1 IsServerSelf -> Approve(ChooseSlot)               (skips 2-7)
    // 2 Mode == Solo -> Reject(JoinsClosed)
    // 3 ProtocolVersion differs -> Reject(VersionMismatch)
    // 4 ContentHash differs -> Reject(VersionMismatch)
    // 5 !JoinsOpen -> Reject(JoinsClosed)
    // 6 ConnectedCount(Roster) >= MaxPlayers -> Reject(FullLobby)
    // 7 a Connected entry has Key == request.Key -> Reject(DuplicateIdentity)
    // 8 slot = ChooseSlot; NoSlot -> Reject(FullLobby); else Approve(slot, reclaimed)
    public static JoinVerdict Evaluate(in JoinRequest request, in JoinContext context);
    public static int ConnectedCount(IReadOnlyList<RosterEntry> roster);
    // a) a disconnected entry with the same Key -> its Slot, reclaimed = true
    // b) else the lowest slot in [0, maxPlayers) held by no entry (connected or not)
    // c) else the lowest Slot among disconnected entries (taken over), reclaimed = false
    // d) else JoinVerdict.NoSlot
    public static byte ChooseSlot(PlayerKey key, IReadOnlyList<RosterEntry> roster, int maxPlayers, out bool reclaimed);
}

// ConnectPayload.cs (file also holds ConnectPayloadCodec). Wire format, little-endian (BinaryWriter):
// byte FormatVersion(=1) | int32 ProtocolVersion | uint64 ContentHash | byte Kind | byte idLen + idLen UTF-8 bytes | byte nameLen + nameLen UTF-8 bytes
// Encode truncates Id to 64 chars and Name to 16 chars (DisplayNameRules.MaxLength) before UTF-8; each byte length must be <= 64 (truncate by chars until it is).
public readonly struct ConnectPayload
{
    public readonly int ProtocolVersion; public readonly ulong ContentHash; public readonly PlayerKey Key; public readonly string Name;
    public ConnectPayload(int protocolVersion, ulong contentHash, PlayerKey key, string name);
}
public static class ConnectPayloadCodec
{
    public const byte FormatVersion = 1;
    public const int MaxBytes = 255;
    public static byte[] Encode(in ConnectPayload payload);              // result length <= MaxBytes
    public static bool TryDecode(byte[] data, out ConnectPayload payload); // false on null, short, wrong FormatVersion, bad lengths, trailing bytes, unknown Kind
}

// DisplayNameRules.cs
public static class DisplayNameRules
{
    public const int MaxLength = 16;
    public static string Sanitize(string raw, int slot); // remove char.IsControl chars; Trim; truncate to 16 then TrimEnd; empty -> Fallback(slot)
    public static string Fallback(int slot);             // "Fisher " + (slot + 1)
}

// DisconnectClassifier.cs
public static class DisconnectClassifier
{
    // 1 userInitiated -> UserLeft
    // 2 disconnectReason non-empty, first char a letter, Enum.TryParse<EndReason>(ignoreCase:false) and Enum.IsDefined -> that value
    // 3 wasConnected -> HostLeft
    // 4 secondsSinceStart < NetworkConstants.VersionMismatchWindowSeconds -> VersionMismatch
    // 5 else Timeout
    public static EndReason Classify(bool userInitiated, bool wasConnected, string disconnectReason, double secondsSinceStart);
}

// EndReasonText.cs — UI string keys (11.2 table)
public static class EndReasonText
{
    // UserLeft -> null (no toast); HostLeft -> "err.host_left"; Kicked, DuplicateIdentity -> "err.rejected"; Timeout -> "err.timeout";
    // FullLobby -> "err.session_full"; JoinsClosed -> "err.joins_closed"; VersionMismatch -> "err.version"; InvalidCode -> "err.code_not_found";
    // ServicesUnavailable -> "err.services"; TransportFailure -> "err.network"; Unknown -> "err.generic"
    public static string ToastKey(EndReason reason);
}

// DevFlags.cs — parses command-line style tokens; unknown tokens ignored; malformed values ignored (flag left at default).
public sealed class DevFlags
{
    public bool Solo { get; }            // -dev-solo
    public bool HostDirect { get; }      // -dev-host-direct
    public bool HasJoinDirect { get; }   // -dev-join-direct <ip[:port]>
    public string JoinDirectAddress { get; }  // default "127.0.0.1" when HasJoinDirect is false
    public ushort JoinDirectPort { get; }     // default NetworkConstants.DefaultPort (also when ":port" omitted)
    public int ProtocolOffset { get; }   // -dev-protocol-offset <int>, default 0
    public bool LongTimeout { get; }     // -dev-long-timeout
    public static DevFlags Parse(IReadOnlyList<string> args);
    public static readonly DevFlags None; // all defaults
}
```
Also owns §5.1 `NetworkConstants`.

### 7.4 T5 — Runtime services (asm FishGame.Runtime)

```csharp
// Runtime/Services/ServiceRegistry.cs (file also holds MissingServiceException)
public sealed class MissingServiceException : InvalidOperationException
{
    public Type ServiceType { get; }
    public MissingServiceException(Type serviceType); // "Service not registered: {FullName}"
}
public sealed class ServiceRegistry : IDisposable
{
    public void Register<T>(T instance) where T : class;   // ArgumentNullException; InvalidOperationException if T already registered
    public void Replace<T>(T instance) where T : class;    // registers or overwrites (tests swap IInputService)
    public T Get<T>() where T : class;                     // MissingServiceException
    public bool TryGet<T>(out T instance) where T : class;
    public bool Contains<T>() where T : class;
    public void Dispose(); // Dispose()s each distinct registered IDisposable instance once, in reverse registration order; then clears. Idempotent.
}

// Runtime/Services/ServiceRoots.cs — the only static service access point.
public static class ServiceRoots
{
    public static ServiceRegistry App { get; }   // set by AppBootstrap; null in EditMode tests
    public static ServiceRegistry Game { get; }  // set by GameScope in gameplay scenes; null elsewhere
    public static void SetApp(ServiceRegistry registry);
    public static void SetGame(ServiceRegistry registry);
    // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetStatics() -> both null
}

// Runtime/Services/EventBus.cs — process-local, main thread.
public sealed class EventBus
{
    public IDisposable Subscribe<T>(Action<T> handler);  // ArgumentNullException; Dispose stops delivery (idempotent)
    public void Publish<T>(T message);                   // iterates a snapshot; a throwing handler is logged (Debug.LogException) and others still run
    public int SubscriberCount<T>();
    public void Clear();
}

// Runtime/Services/ToastRequested.cs — EventBus message; UI shows it.
public readonly struct ToastRequested
{
    public readonly string Key; public readonly string Arg; public readonly float Seconds;
    public ToastRequested(string key, string arg = null, float seconds = 4f);
}

// Runtime/Services/IPauseState.cs + PauseState.cs — solo pause flag (never touches Time.timeScale).
public interface IPauseState { bool Paused { get; } event Action<bool> Changed; void SetPaused(bool paused); }
public sealed class PauseState : IPauseState { /* raises Changed only on a real change */ }

// Runtime/Settings/SettingsData.cs
[Serializable]
public sealed class SettingsData
{
    public int version = 1;
    public int masterVolume = 80, sfxVolume = 80, ambienceVolume = 70, musicVolume = 60, uiVolume = 80, voiceVolume = 80; // 0..100
    public string displayName = "Fisher";          // 1..16 chars after Clamp (empty -> "Fisher")
    public float mouseSensitivity = 0.12f;         // 0.04..0.30 deg/count
    public float gamepadLookSpeed = 1.0f;          // 0.2..3.0 (x 180 deg/s)
    public bool invertY = false;
    public float fieldOfView = 90f;                // horizontal, 80..110
    public float cameraMotion = 1f;                // 0..1
    public bool sprintToggle = false, aimToggle = false, streamerMode = false;
    public int uiScalePercent = 100;               // 80..150
    public string bindingOverridesJson = "";
    public string localPlayerId = "";              // GUID "N" format, created once per instance file
    public void Clamp();
    public SettingsData Clone();
}

// Runtime/Settings/ISettings.cs
public interface ISettings
{
    string FilePath { get; }
    SettingsData Current { get; }          // live, mutable; call NotifyChanged after editing
    event Action<SettingsData> Changed;
    void NotifyChanged();                  // Clamp + raise Changed (values apply live)
    void Save();                           // Clamp + atomic write (on panel close / quit; never per slider tick)
    string EnsureLocalPlayerId();          // creates + saves once if empty; returns the id
}

// Runtime/Settings/SettingsStore.cs
public sealed class SettingsStore : ISettings
{
    public SettingsStore(string filePath);
    public bool LoadedFromBackup { get; }
    public void Load();                    // main file -> JsonUtility.FromJsonOverwrite(new SettingsData()); parse failure/empty -> try FilePath + ".bak"; else defaults; then Clamp
    public static SettingsStore CreateForThisInstance(); // path = SettingsPaths.SettingsFile(Application.persistentDataPath, InstanceProfile.Current.Suffix); calls Load()
    // + ISettings members
}

// Runtime/Settings/SettingsPaths.cs
public static class SettingsPaths
{
    public const string SettingsBaseName = "settings";
    public static string SettingsFile(string directory, string suffix); // Path.Combine(directory, "settings" + suffix + ".json")
}

// Runtime/Settings/AtomicFile.cs
public static class AtomicFile
{
    public const string TempSuffix = ".tmp", BackupSuffix = ".bak";
    public static void WriteAllText(string path, string contents);
        // creates the directory; writes path+".tmp" (UTF-8 no BOM, flushed to disk);
        // if path exists: File.Replace(tmp, path, path + ".bak") else File.Move(tmp, path)
    public static string ReadAllTextOrNull(string path); // null when missing or unreadable
}

// Runtime/Settings/InstanceProfile.cs — per-instance file suffix (MPPM clones, several standalone builds).
public sealed class InstanceProfile
{
    public string Name { get; }    // sanitized; "" for the main instance
    public string Suffix { get; }  // "" or "_" + Name
    public bool IsMppmClone { get; }
    // Rules: "-dev-profile <x>" wins (IsMppmClone false); else if args contain "-editor-mode" and "-name <x>" -> x (IsMppmClone true); else "".
    public static InstanceProfile FromArgs(IReadOnlyList<string> args);
    public static InstanceProfile Current { get; }       // lazily FromArgs(Environment.GetCommandLineArgs())
    public static string Sanitize(string raw);           // lowercase; keep [a-z0-9_-]; max 30 chars
}
```

### 7.5 T4 — Input

Core (asm FishGame.Core, folder `Core/Input/`):
```csharp
public enum InputContext : byte { Free = 0, RodReady = 1, RodAiming = 2, RodWaiting = 3, RodFighting = 4, Carrying = 5,
                                  Staggered = 6, Downed = 7, Carried = 8, Swimming = 9, Climbing = 10, Menu = 11, Seated = 12 }
[Flags] public enum InputMaps : byte { None = 0, Player = 1, Fishing = 2, Boat = 4, UI = 8 }

// Raw per-frame sample (name per spec 4.5: never "PlayerInput").
public struct PlayerInputFrame
{
    public float MoveX, MoveY;                 // -1..1 (MoveY forward)
    public float LookYawDeg, LookPitchDeg;     // degrees this frame, already scaled/inverted by the adapter; pitch positive = look up
    public bool JumpPressed, SprintHeld;
    public bool PrimaryHeld, PrimaryPressed, PrimaryReleased;
    public bool SecondaryHeld, SecondaryPressed, SecondaryReleased;
    public bool InteractPressed, ShovePressed, DropPressed, EquipPressed;
    public bool PausePressed;                  // Player/Pause in gameplay contexts; UI/Cancel in Menu
    public float Steer; public bool Reel;      // Fishing map
}
public struct PlayerIntent
{
    public float MoveX, MoveY, LookYawDeg, LookPitchDeg;
    public bool JumpPressed, SprintHeld;
    public bool PrimaryHeld, PrimaryPressed, PrimaryReleased;
    public bool SecondaryHeld, SecondaryPressed, SecondaryReleased;
    public bool InteractPressed, ShovePressed, DropPressed, EquipPressed, PausePressed;
    public static readonly PlayerIntent None;
}
public readonly struct FightInput { public readonly float Steer; public readonly bool Reel; public FightInput(float steer, bool reel); }

public static class InputContextRouter
{
    // M1 rows (other contexts route exactly like Free until their milestone):
    // Free:     Move, Look, Jump, Sprint, Interact, Shove, Equip, Pause pass. Primary*, Secondary*, Drop zeroed.
    // Swimming: Move, Look, Jump, Interact (climb at exits), Pause pass. Sprint, Primary*, Secondary*, Shove, Drop, Equip zeroed.
    // Climbing: Look, Pause pass. Everything else zeroed.
    // Menu:     Pause passes. Everything else zeroed (look included).
    public static PlayerIntent Route(InputContext context, in PlayerInputFrame frame);
    public static FightInput RouteFight(InputContext context, in PlayerInputFrame frame); // RodFighting/Staggered: (Steer, Reel || PrimaryHeld); else default
    public static InputMaps EnabledMaps(InputContext context); // Menu -> UI; Seated -> Player|Boat; others -> Player|Fishing
}

public static class LookMath
{
    public const float StickDegPerSecond = 180f;
    public static float MouseToDegrees(float countsDelta, float sensitivityDegPerCount); // counts * sens (no dt)
    public static float StickToDegrees(float stickValue, float speedMult, float dt);     // sign(v) * v^2 * 180 * speedMult * dt
}
```

Runtime (asm FishGame.Runtime, folder `Runtime/Input/`):
```csharp
public enum InputDeviceKind : byte { KeyboardMouse = 0, Gamepad = 1 }

public static class InputActionNames
{
    public const string AssetPath = "Assets/_Project/Runtime/Input/FishGameInput.inputactions";
    public const string PlayerMap = "Player", FishingMap = "Fishing", UiMap = "UI";
    public const string Move = "Player/Move", Look = "Player/Look", Jump = "Player/Jump", Sprint = "Player/Sprint",
        Primary = "Player/Primary", Secondary = "Player/Secondary", Interact = "Player/Interact", Shove = "Player/Shove",
        Drop = "Player/Drop", Equip = "Player/Equip", Journal = "Player/Journal", Pause = "Player/Pause";
    public const string Reel = "Fishing/Reel", Steer = "Fishing/Steer";
    public const string Navigate = "UI/Navigate", Submit = "UI/Submit", Cancel = "UI/Cancel", Point = "UI/Point", Click = "UI/Click";
    public const string KeyboardMouseScheme = "Keyboard&Mouse", GamepadScheme = "Gamepad";
    public static readonly string[] PlayerActions = { "Move","Look","Jump","Sprint","Primary","Secondary","Interact","Shove","Drop","Equip","Journal","Pause" };
    public static readonly string[] FishingActions = { "Reel","Steer" };
    public static readonly string[] UiActions = { "Navigate","Submit","Cancel","Point","Click" };
}

public interface IInputService
{
    InputContext Context { get; }               // MenuOpen ? Menu : GameplayContext
    InputContext GameplayContext { get; set; }  // written by PlayerLocomotion (Free/Swimming/Climbing in M1)
    bool MenuOpen { get; set; }                 // written by UiFlowController (any modal / main menu visible)
    PlayerIntent Intent { get; }                // routed, recomputed once per frame before gameplay Update
    FightInput Fight { get; }
    bool HasFocus { get; }
    InputDeviceKind LastDevice { get; }
    event Action<InputContext> ContextChanged;
    void SetRumble(float low, float high);      // 0..1; no-op without a gamepad
    string GetBindingDisplayString(string actionPath); // e.g. "E" for InputActionNames.Interact on keyboard; "" if unknown
}

public interface IInputBindings
{
    InputActionAsset Actions { get; }           // the runtime copy used by the adapter
    void SaveBindingOverrides();                // SaveBindingOverridesAsJson -> settings.bindingOverridesJson; settings.Save()
    void ResetAllBindingOverrides();            // RemoveAllBindingOverrides(); SaveBindingOverrides()
}

// PlayerInputAdapter.cs — on the App root (Bootstrap). The only reader of InputActions.
[DefaultExecutionOrder(-1000)]
public sealed class PlayerInputAdapter : MonoBehaviour, IInputService, IInputBindings
{
    public void Initialize(InputActionAsset asset, ISettings settings);
        // actions = Instantiate(asset); LoadBindingOverridesFromJson(settings.Current.bindingOverridesJson) when non-empty;
        // enables maps per InputContextRouter.EnabledMaps(Context)
    // Update(): builds PlayerInputFrame (look scaling via LookMath + settings, invert Y, drop first look delta after focus regain,
    // sprint toggle, neutral frame when unfocused), Intent = InputContextRouter.Route(Context, frame), Fight = RouteFight(...).
    // Cursor policy (single owner): Menu -> unlocked + visible; else locked + hidden; re-applied on context change and OnApplicationFocus(true).
    // Rumble reset to 0 on focus loss and OnDisable.
}
```
Fake (asm FishGame.Tests.PlayMode): `Tests/PlayMode/Fakes/FakeInputService.cs`
```csharp
public sealed class FakeInputService : IInputService
{
    public PlayerInputFrame Frame;              // tests set this; Intent = InputContextRouter.Route(Context, Frame)
    public float LastRumbleLow { get; } public float LastRumbleHigh { get; }
    // HasFocus = true; LastDevice = KeyboardMouse; GetBindingDisplayString returns the action path; ContextChanged raised on change
}
```
Asset content (T4): maps `Player` (12 actions above; Move & Look `Value`/`Vector2`; others `Button`), `Fishing` (Reel `Button`, Steer `Value`/`Axis` with a 1D composite A = −1 / D = +1 and Left Stick X), `UI` (Navigate `PassThrough`/`Vector2`, Submit `Button`, Cancel `Button`, Point `PassThrough`/`Vector2`, Click `PassThrough`/`Button`). No `Boat` map in M1. Control schemes `Keyboard&Mouse` and `Gamepad`. Default bindings per spec §8.3 (Sprint = Left Shift / Left Stick Press; Shove = F / Right Stick Press; Pause = Escape / Start; Journal = J / Select). No action map is named `FishGameInput`. `.meta`: copy the shape of the existing `Assets/InputSystem_Actions.inputactions.meta`, which still exists at T4 time:
```yaml
fileFormatVersion: 2
guid: <new random 32 lowercase hex, e.g. [guid]::NewGuid().ToString('N')>
ScriptedImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 2
  userData: 
  assetBundleName: 
  assetBundleVariant: 
  script: {fileID: 11500000, guid: 8404be70184654265930450def6a9037, type: 3}
  generateWrapperCode: 1
  wrapperCodePath: 
  wrapperClassName: FishGameInput
  wrapperCodeNamespace: FishGame.Input
```

### 7.6 T6 — Audio and flat water

Core (folder `Core/Audio/`):
```csharp
public enum AudioCategory : byte { Sfx = 0, Ambience = 1, Music = 2, Ui = 3, Voice = 4 }
public static class SfxIds
{
    public const string UiClick = "Ui_Click", UiConfirm = "Ui_Confirm", UiError = "Ui_Error",
        WindAmbient = "Wind_Ambient", WaterLap = "Water_Lap", BellRing = "Bell_Ring";
}
public readonly struct SfxRecipe
{
    public readonly string Id; public readonly float LengthSeconds; public readonly bool Loop; public readonly AudioCategory Category;
    public readonly float BaseVolume; public readonly bool Spatial; public readonly float MaxDistance; public readonly float MinInterval;
    public SfxRecipe(string id, float lengthSeconds, bool loop, AudioCategory category, float baseVolume, bool spatial, float maxDistance, float minInterval);
}
public static class SfxRecipes
{
    public const int SampleRate = 44100;
    // M1 table: Ui_Click/Ui_Confirm/Ui_Error 0.08 s, Ui, base 0.6, 2D, minInterval 0.03;
    //           Wind_Ambient 6.0 s loop, Ambience, base 0.5, 2D;  Water_Lap 0.4 s, Ambience, base 0.5, 2D, minInterval 0.3;
    //           Bell_Ring 1.0 s, Sfx, base 0.9, spatial, maxDistance 30, minInterval 0.25
    public static IReadOnlyList<SfxRecipe> M1 { get; }
    public static bool TryGet(string id, out SfxRecipe recipe);
    public static int SampleCount(in SfxRecipe recipe, int sampleRate);   // (int)Math.Round(LengthSeconds * sampleRate)
    public static float[] Generate(string id, int sampleRate, ulong seed); // mono; length = SampleCount; max|s| < 1 (normalize to peak 0.9);
        // no NaN/Inf; loops: whole number of base periods and |s[last] - s[0]| < 0.05 (noise loops: 20 ms crossfade); deterministic per seed. ArgumentException for unknown id.
}
public static class AudioMath
{
    public const float PitchJitter = 0.07f, VolumeJitterDb = 1.5f;
    public static float EffectiveVolume(float cueBase, int categoryPercent, int masterPercent); // cueBase * (c/100)^2 * (m/100)^2, inputs clamped 0..100
    public static float DbToLinear(float db);                                                   // 10^(db/20)
}
```
Runtime (folder `Runtime/Audio/` and `Runtime/World/`):
```csharp
public sealed class SfxGenerator
{
    public SfxGenerator(int sampleRate = SfxRecipes.SampleRate);
    public void GenerateAll(ulong seed);              // every SfxRecipes.M1 entry -> AudioClip.Create(id, n, 1, sampleRate, false) + SetData; warns and skips on failure
    public bool TryGetClip(string id, out AudioClip clip);
}

// On the App root (Bootstrap).
public sealed class AudioBus : MonoBehaviour
{
    public void Initialize(SfxGenerator generator, ISettings settings); // subscribes settings.Changed -> ApplyVolumes()
    public void Register(AudioSource source, AudioCategory category, float cueBase);
    public void Unregister(AudioSource source);
    public void PlayUi(string sfxId);                                   // 2D, category Ui
    public void Play2D(string sfxId, AudioCategory category);
    public void PlayAt(string sfxId, Vector3 position);                 // pooled 3D source, logarithmic rolloff, recipe MaxDistance
    public void ApplyVolumes();
    // per play: pitch ±7%, volume ±1.5 dB (Pcg32); rate limit per id by recipe MinInterval; silent + warning when a clip is missing
}

// Scene component (Spot_LakeDock "Ambience"). Wind loop + Water_Lap every 0.6–1.4 s. Disables itself if ServiceRoots.App or AudioBus is missing.
public sealed class AmbienceBed : MonoBehaviour { }

// Runtime/World/WaterSurface.cs — flat until M2 WaveMath.
public static class WaterSurface
{
    public const float FlatHeight = 0f;
    public static float HeightAt(float x, float z); // returns FlatHeight
}
```

### 7.7 T7 — Content registry

Core (folder `Core/Content/`):
```csharp
public sealed class RegistryIndex
{
    public RegistryIndex(IEnumerable<string> ids);  // ArgumentException on null/empty/duplicate id or > 65535 ids; sorts with StringComparer.Ordinal
    public int Count { get; }
    public IReadOnlyList<string> SortedIds { get; }
    public ushort IndexOf(string id);               // KeyNotFoundException
    public bool TryGetIndex(string id, out ushort index);
    public string IdAt(ushort index);               // ArgumentOutOfRangeException
}
public static class ContentIds
{
    public const string LakeDock = "lake_dock";
    public static bool IsValidId(string id);        // ^[a-z0-9]+(_[a-z0-9]+)*$
}
```
Data (asm FishGame.Data):
```csharp
// Data/SpotDef.cs (M1 fields only; M3/M4 add more)
public sealed class SpotDef : ScriptableObject
{
    [SerializeField] private string id; [SerializeField] private string displayName; [SerializeField] private string sceneName;
    public string Id { get; } public string DisplayName { get; } public string SceneName { get; }
    public void Configure(string id, string displayName, string sceneName); // builder + tests
}

// Data/ContentRegistry.cs
public sealed class ContentRegistry : ScriptableObject
{
    public const string CategorySpots = "spots";
    [SerializeField] private int dataVersion = 1; [SerializeField] private List<SpotDef> spots = new List<SpotDef>();
    public int DataVersion { get; }
    public IReadOnlyList<SpotDef> Spots { get; }
    public void Configure(int dataVersion, IEnumerable<SpotDef> spots); // builder + tests
    public IReadOnlyList<string> Validate();          // errors: null entry, invalid id (ContentIds.IsValidId), duplicate id
    public void Build();                              // throws InvalidOperationException listing Validate() errors; builds RegistryIndex per category
    public ushort SpotIndex(string id);
    public SpotDef SpotAt(ushort index);
    public bool TryGetSpot(string id, out SpotDef def);
    public ulong ComputeContentHash();                // ContentHash.Compute([new ContentCategoryIds(CategorySpots, spotsIndex.SortedIds)], DataVersion); calls Build() if needed
}
```

### 7.8 T8 — Networking (local) and the network seam

Runtime (folder `Runtime/Net/`):
```csharp
public interface INetworkSession : IDisposable
{
    ConnState State { get; }
    SessionMode Mode { get; }                   // None while Offline
    bool IsHost { get; }
    event Action<ConnState, ConnState> StateChanged;
    event Action<EndReason> Ended;              // exactly once per attempt (Begin .. Offline); UserLeft for a normal leave or a cancel
    string JoinCode { get; }                    // null for solo/direct
    Task<string> HostOnlineAsync(CancellationToken ct);
    Task JoinOnlineAsync(string code, CancellationToken ct);
    Task StartLocalAsync(bool allowRemote, CancellationToken ct);  // false = Solo, true = DevDirect host (dev builds only)
    Task JoinDirectAsync(string ip, ushort port, CancellationToken ct);
    Task SetJoinableAsync(bool open);
    Task LeaveAsync();                          // idempotent; completes when Offline
}
// Start tasks complete when State reaches Connected. On failure they fault with NetworkSessionException(reason) (a cancel faults with UserLeft; never a Canceled task),
// AND Ended(reason) fires. The UI shows error toasts ONLY from Ended (via SceneFlow), never from the exception.

public sealed class NetworkSessionException : Exception
{
    public EndReason Reason { get; }
    public NetworkSessionException(EndReason reason, string message = null, Exception inner = null);
}

public interface IJoinContextProvider                 // implemented by SessionState (T10)
{
    bool JoinsOpen { get; }
    void GetRoster(List<RosterEntry> into);           // clears `into`, then adds every slot (connected or not)
    bool TryGetKey(ulong clientId, out PlayerKey key);
}

public readonly struct ApprovedJoin
{
    public readonly ulong ClientId; public readonly PlayerKey Key; public readonly string Name; public readonly byte Slot; public readonly bool Reclaimed;
    public ApprovedJoin(ulong clientId, PlayerKey key, string name, byte slot, bool reclaimed);
}

// App-scope service: approver (Networking) -> SessionState (Gameplay) handoff.
public sealed class JoinApprovalBridge
{
    public IJoinContextProvider Provider { get; set; }    // SessionState sets on server spawn, clears on despawn
    public void AddApproved(in ApprovedJoin join);         // replaces any pending entry for the same ClientId
    public bool TryTakeApproved(ulong clientId, out ApprovedJoin join); // removes it
    public void GetPending(List<ApprovedJoin> into);       // clears `into` first
    public void RemovePending(ulong clientId);
    public void Clear();                                   // pending only; Provider untouched
}
```
Networking (asm FishGame.Networking):
```csharp
public sealed class NetworkSessionConfig
{
    public NetworkSessionConfig(NetworkManager networkManager, JoinApprovalBridge bridge, ISettings settings,
                                int protocolVersion, ulong contentHash, bool longTimeout);
    public NetworkManager NetworkManager { get; } public JoinApprovalBridge Bridge { get; } public ISettings Settings { get; }
    public int ProtocolVersion { get; } public ulong ContentHash { get; } public bool LongTimeout { get; }
}

public sealed class ConnectionApprover
{
    public ConnectionApprover(NetworkSessionConfig config, SessionMode mode);
    public void Install();     // networkManager.NetworkConfig.ConnectionApproval = true; ConnectionApprovalCallback = Handle (assignment, never +=)
    public void Uninstall();   // sets the callback to null
    public void Handle(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response);
    // Handle: TryDecode payload, else reject VersionMismatch; invalid key -> reject VersionMismatch.
    // roster = Provider?.GetRoster (empty if null) + bridge pending (as Connected); joinsOpen = Provider?.JoinsOpen ?? true.
    // JoinRules.Evaluate(new JoinRequest(id, id == NetworkManager.ServerClientId, ...), new JoinContext(mode, config.ProtocolVersion, config.ContentHash, joinsOpen, NetworkConstants.MaxPlayers, roster)).
    // Approved: bridge.AddApproved(new ApprovedJoin(id, key, DisplayNameRules.Sanitize(name, slot), slot, reclaimed));
    //           response.Approved = true; CreatePlayerObject = false; Pending = false.
    // Rejected: response.Approved = false; response.Reason = verdict.Reason.ToString().
}

public static class TransportSetup
{
    public static UnityTransport Get(NetworkManager nm);       // nm.NetworkConfig.NetworkTransport as UnityTransport, else nm.GetComponent<UnityTransport>(); throws if none
    public static void ApplyTimeouts(UnityTransport utp, bool longTimeout); // §5.1 values; DisconnectTimeoutMS = long ? 120000 : 10000
    public static void ConfigureDirect(UnityTransport utp, string address, ushort port, string listenAddress);
        // utp.SetConnectionData(true, address, port, listenAddress) -> plain UnityTransport protocol, ignores -port/-ip args
}

public static class NetworkManagerAwait
{
    public static Task WaitForShutdownAsync(NetworkManager nm, CancellationToken ct); // awaits Task.Yield() until !nm.ShutdownInProgress
    public static Task ShutdownAsync(NetworkManager nm);                              // if IsListening or IsClient/IsServer: Shutdown(); then WaitForShutdownAsync
}

// Solo + dev-direct. Never touches UGS.
public sealed class LocalNetworkSession : INetworkSession
{
    public LocalNetworkSession(NetworkSessionConfig config);
    public ushort Port { get; set; }        // default NetworkConstants.DefaultPort; PlayMode tests set NetworkConstants.TestPort
    public ushort ActivePort { get; }       // the port actually bound/joined
    // StartLocalAsync(false): Mode Solo, listen and connect 127.0.0.1, ports Port..Port+SoloPortRetryCount-1 (retry after a failed StartHost + shutdown).
    // StartLocalAsync(true):  Mode DevDirect, 127.0.0.1:Port only; throws NetworkSessionException(Unknown) unless UNITY_EDITOR || DEVELOPMENT_BUILD.
    // JoinDirectAsync(ip, port): Mode DevDirect client.
    // Every start: Begin -> WaitForShutdownAsync -> ConfigureDirect + ApplyTimeouts -> NetworkConfig.ProtocolVersion = 0 ->
    //   NetworkConfig.ConnectionData = ConnectPayloadCodec.Encode(new ConnectPayload(cfg.ProtocolVersion, cfg.ContentHash,
    //   new PlayerKey(IdentityKind.Local, settings.EnsureLocalPlayerId()), settings.Current.displayName)) -> new ConnectionApprover(cfg, mode).Install() -> StartHost/StartClient.
    // Established: host -> right after StartHost() returns true (NGO 2.13.3 raises OnServerStarted synchronously inside StartHost, after host
    //   self-approval; do NOT fire Established from inside that callback, to avoid re-entrant scene loads); client -> OnConnectionEvent
    //   ClientConnected where ClientId == LocalClientId.
    // 15 s timeout / ct cancel -> Fail(Timeout / UserLeft) -> shutdown -> Offline.
    // Unexpected stop: reason = DisconnectClassifier.Classify(false, wasConnected, nm.DisconnectReason, secondsSinceBegin) (host: TransportFailure) -> UnexpectedStop/Fail -> Leave -> ShutdownComplete.
    // Host: OnConnectionEvent ClientDisconnected -> bridge.RemovePending(clientId). On Offline: bridge.Clear(); approver.Uninstall().
    // HostOnlineAsync / JoinOnlineAsync throw NotSupportedException. SetJoinableAsync returns Task.CompletedTask.
}

// The single INetworkSession the UI/App see.
public sealed class CompositeNetworkSession : INetworkSession
{
    public CompositeNetworkSession(LocalNetworkSession local, Func<INetworkSession> onlineFactory); // online backend created lazily on first online call
    public LocalNetworkSession Local { get; }
    public INetworkSession Active { get; }   // backend of the current or last attempt (null before the first)
    // Starting while State != Offline throws InvalidOperationException. StateChanged/Ended are re-raised from the active backend.
}
```

### 7.9 T9 — UGS session

Core: `Core/Net/UgsErrorMap.cs`
```csharp
public static class UgsErrorMap
{
    public const int SessionErrorUnknown = 1, SessionErrorSessionNotFound = 6, SessionErrorInvalidParameter = 12; // mirrors Unity.Services.Multiplayer.SessionError
    // 1 message contains "full" not followed by a letter (OrdinalIgnoreCase; "LobbyFull" yes, "fully"/"successfully" no) -> FullLobby
    // 2 code == SessionNotFound -> InvalidCode
    // 3 message contains "relay" (OrdinalIgnoreCase) -> ServicesUnavailable ("Failed to fetch relay join code" is an outage, not a bad code)
    // 4 message contains "not found" or "join code" or "invalid code" (OrdinalIgnoreCase) -> InvalidCode
    // 5 code == InvalidParameter -> InvalidCode
    // 6 else -> ServicesUnavailable
    public static EndReason Map(int sessionErrorCode, string message);
}
```
Networking: `Networking/UgsNetworkSession.cs`
```csharp
// Authority: host owns the Sessions lobby (lock state, player removal); SDK owns NGO start/stop on this path.
public sealed partial class UgsNetworkSession : INetworkSession   // three files, see §14
{
    public UgsNetworkSession(NetworkSessionConfig config); // must not touch UGS (lazy)
    // HostOnlineAsync: EnsureSignedInAsync -> ConnectionData payload with PlayerKey(Ugs, AuthenticationService.Instance.PlayerId)
    //   -> ApplyTimeouts -> new ConnectionApprover(cfg, SessionMode.Online).Install()
    //   -> MultiplayerService.Instance.CreateSessionAsync(new SessionOptions { MaxPlayers = NetworkConstants.MaxPlayers, IsPrivate = true }.WithRelayNetwork())
    //   -> JoinCode = session.Code (exactly as given) -> Established once CreateSessionAsync has returned and NetworkManager.IsServer && IsListening
    //   (the SDK polls IsListening/IsConnectedClient itself, F1) -> returns JoinCode.
    // JoinOnlineAsync: code = JoinCode.Normalize(code); JoinSessionByCodeAsync(code) -> Established on local ClientConnected.
    // Errors: SessionException -> UgsErrorMap.Map((int)e.Error, e.Message); other UGS/auth/init exceptions -> ServicesUnavailable; raw text logged only.
    // Host: OnConnectionEvent ClientDisconnected -> bridge.Provider?.TryGetKey(id) with Kind == Ugs -> hostSession.RemovePlayerAsync(key.Id) (errors logged).
    // SetJoinableAsync(open): host.IsLocked = !open; await host.SavePropertiesAsync().
    // LeaveAsync / Ended(HostLeft): await session.LeaveAsync() (errors ignored), then NetworkManagerAwait.ShutdownAsync.
    // Cancel/timeout: the SDK call can't be aborted; when it completes, LeaveAsync + shutdown, and the attempt ends Failed(UserLeft/Timeout).
    // StartLocalAsync / JoinDirectAsync throw NotSupportedException.
    // Class comment must contain the "Recorded findings (M1 criterion 13)" block (§13 D19).
}
internal static class UgsBootstrap
{
    // Once per process (cached Task): UnityServices.InitializeAsync(options) where options.SetProfile(InstanceProfile.Current.Name)
    // only when Name != "" && !IsMppmClone; then AuthenticationService.Instance.SignInAnonymouslyAsync() if !IsSignedIn.
    internal static Task EnsureSignedInAsync(CancellationToken ct);
}
```

### 7.10 T12 — Interaction framework (asm FishGame.Gameplay)

```csharp
// Gameplay/Interaction/NetworkInteractable.cs
// Authority: host decides (ServerInteract); clients only display GetPrompt/CanInteract.
public abstract class NetworkInteractable : NetworkBehaviour
{
    public virtual float Range => 3.0f;
    public virtual Vector3 InteractPoint => transform.position;   // server range check target
    public abstract string GetPrompt(ulong clientId);              // local display only, without the key ("Ring bell")
    public virtual bool CanInteract(ulong clientId) => true;       // server truth; also evaluated locally to grey the prompt
    public abstract void ServerInteract(ulong clientId);           // server only
}

// Gameplay/Interaction/DockBell.cs
// Authority: host validates and rings; the ring cue is broadcast to ClientsAndHost.
public sealed class DockBell : NetworkInteractable
{
    public const float CooldownSeconds = 1.0f;
    public int LocalRingCount { get; }          // increments on every peer when RingRpc runs
    public event Action Rang;                   // local, every peer
    public override string GetPrompt(ulong clientId);   // "Ring bell"
    public override bool CanInteract(ulong clientId);   // false within CooldownSeconds of the last ring (NetworkManager.ServerTime.Time)
    public override void ServerInteract(ulong clientId);// records time, RingRpc()
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void RingRpc();                     // AudioBus.PlayAt(SfxIds.BellRing, transform.position); swings child "Bell"; LocalRingCount++; Rang?.Invoke()
}

// Gameplay/Spawning/NetSpawnAnchor.cs — scene marker; host spawns `Prefab` here (destroyWithScene: true).
public sealed class NetSpawnAnchor : MonoBehaviour
{
    [SerializeField] private NetworkObject prefab;
    public NetworkObject Prefab { get; }
    public void Configure(NetworkObject prefab);       // builder
    public static IReadOnlyList<NetSpawnAnchor> All { get; } // maintained in OnEnable/OnDisable
}
```

### 7.11 T10 — Session roster and spawning (asm FishGame.Gameplay)

```csharp
// Gameplay/Session/NetPlayerId.cs — wire form of PlayerKey.
public struct NetPlayerId : INetworkSerializable, IEquatable<NetPlayerId>
{
    public byte Kind; public FixedString64Bytes Id;
    public static NetPlayerId From(PlayerKey key);
    public PlayerKey ToKey();
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter; // Kind, Id
    public bool Equals(NetPlayerId other); public override int GetHashCode();
}

// Gameplay/Session/PlayerSlot.cs
public struct PlayerSlot : INetworkSerializable, IEquatable<PlayerSlot>
{
    public ulong ClientId; public NetPlayerId Id; public FixedString32Bytes Name; public byte Slot; public bool Connected; public bool Ready;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter;
    public bool Equals(PlayerSlot other); public override int GetHashCode();
}

// Gameplay/Session/SessionState.cs
// Authority: host writes Roster and NetJoinsOpen; every peer reads. Spawned by AppBootstrap on OnServerStarted with destroyWithScene: false.
public sealed class SessionState : NetworkBehaviour, IJoinContextProvider
{
    public static SessionState Current { get; }                 // set in OnNetworkSpawn, cleared in OnNetworkDespawn (every peer)
    public static event Action<SessionState> CurrentChanged;    // null on despawn
    public NetworkList<PlayerSlot> Roster;                      // constructed in Awake: new NetworkList<PlayerSlot>(null, Everyone, Server)
    public readonly NetworkVariable<bool> NetJoinsOpen = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public event Action RosterChanged;                          // every peer; on any Roster or NetJoinsOpen change
    public bool JoinsOpen { get; }
    public int ConnectedCount { get; }
    public bool TryGetByClient(ulong clientId, out PlayerSlot slot);  // connected entries only
    public bool TryGetBySlot(byte slot, out PlayerSlot entry);
    public void GetRoster(List<RosterEntry> into);
    public bool TryGetKey(ulong clientId, out PlayerKey key);         // connected or disconnected entries
    public void ServerSetJoinsOpen(bool open);                        // host only (InvalidOperationException otherwise)
    public void ServerRenameClient(ulong clientId, string sanitizedName); // host only; updates Name of that client's entry
    // Server spawn: bridge = ServiceRoots.App.Get<JoinApprovalBridge>(); bridge.Provider = this;
    //   for each id in NetworkManager.ConnectedClientsIds: if bridge.TryTakeApproved(id) -> Upsert.
    //   OnConnectionEvent ClientConnected -> TryTakeApproved -> Upsert (entry with that Slot replaced; Connected = true, Ready = false).
    //   OnConnectionEvent ClientDisconnected -> entry Connected = false, Ready = false (slot kept for reclaim).
    // Server despawn: bridge.Provider = null (if it is this).
}

// Gameplay/Spawning/IServerPlayerInit.cs — implemented by PlayerNetState (T11).
public interface IServerPlayerInit { void ServerInit(byte slot, string displayName); }

// Gameplay/Spawning/PlayerSpawner.cs
// Authority: host only. Spawns players and NetSpawnAnchor prefabs; every peer can query spawn poses.
public sealed class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private NetworkObject playerPrefab; [SerializeField] private Transform[] spawnPoints; // 4
    public static PlayerSpawner Current { get; }                    // OnEnable/OnDisable
    public void Configure(NetworkObject playerPrefab, Transform[] spawnPoints); // builder
    public bool TryGetSpawnPose(byte slot, out Vector3 position, out Quaternion rotation);
    // Host: OnLoadEventCompleted(this scene) -> once: spawn each NetSpawnAnchor.All prefab (InstantiateAndSpawn(prefab, destroyWithScene: true, position:, rotation:));
    //   spawn a player for each clientsCompleted id; DisconnectClient(id, EndReason.Timeout.ToString()) for each clientsTimedOut.
    //   OnSynchronizeComplete(clientId) -> spawn that player if it has none.
    //   Player spawn: slot from SessionState.Current.TryGetByClient; InstantiateAndSpawn(playerPrefab, ownerClientId: id, destroyWithScene: true,
    //   isPlayerObject: true, position: pos, rotation: rot); then GetComponent<IServerPlayerInit>().ServerInit(slot, name).
}
```

### 7.12 T11 — Player and water (asm FishGame.Gameplay)

```csharp
// Gameplay/Player/PlayerRigPaths.cs — names shared with PrefabBuilder (§6.3).
public static class PlayerRigPaths
{
    public const string HeadPivot = "HeadPivot", Shake = "HeadPivot/Shake", Camera = "HeadPivot/Shake/Camera",
        Head = "HeadPivot/Head", Nose = "HeadPivot/Head/Nose", Body = "Body", Torso = "Body/Torso",
        ArmL = "Body/ArmL", ArmR = "Body/ArmR", ArmLMesh = "Body/ArmL/Mesh", ArmRMesh = "Body/ArmR/Mesh",
        LegL = "Body/LegL", LegR = "Body/LegR", LegLMesh = "Body/LegL/Mesh", LegRMesh = "Body/LegR/Mesh",
        NameplateAnchor = "NameplateAnchor";
}

// Gameplay/Player/PlayerNetState.cs
// Authority: owner writes NetPitchQ and NetSwimming; host writes NetSlot and NetDisplayName.
public sealed class PlayerNetState : NetworkBehaviour, IServerPlayerInit
{
    public readonly NetworkVariable<short> NetPitchQ = new NetworkVariable<short>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public readonly NetworkVariable<byte> NetSlot = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public readonly NetworkVariable<FixedString32Bytes> NetDisplayName = new NetworkVariable<FixedString32Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public readonly NetworkVariable<bool> NetSwimming = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public static IReadOnlyList<PlayerNetState> Active { get; }   // spawned players on this peer
    public static PlayerNetState Local { get; }                   // locally owned player or null
    public static event Action<PlayerNetState> LocalPlayerChanged; // arg = Local (null on despawn)
    public static event Action<PlayerNetState> Spawned;
    public static event Action<PlayerNetState> Despawned;
    public byte Slot { get; } public string DisplayName { get; } public bool IsSwimming { get; }
    public event Action<byte> SlotChanged; public event Action<string> DisplayNameChanged;
    public void ServerInit(byte slot, string displayName);       // host: NetSlot, NetDisplayName (DisplayNameRules.Sanitize)
    public void ServerSetDisplayName(string sanitizedName);
    public void OwnerSetPitch(float pitchDeg);                   // PitchQuant + ShouldSend; NetPitchQ has SetUpdateTraits(MinSecondsBetweenUpdates = 1/15)
    public void OwnerSetSwimming(bool swimming);
    // OnNetworkSpawn: root layer = IsOwner ? Layers.Player : Layers.RemotePlayer; owner enables CharacterController, PlayerLocomotion, PlayerCamera,
    //   PlayerInteractor, the Camera and AudioListener; remotes enable the CapsuleCollider only. GetComponent<PlayerBody>().Bind(this, IsOwner).
    //   Owner: Local = this; LocalPlayerChanged; GetComponent<PlayerActions>().SubmitNameRpc(settings displayName).
    //   Subscribers read .Value once here and unsubscribe in OnNetworkDespawn.
}

// Gameplay/Player/PlayerActions.cs
// Authority: owner sends intents (SendTo.Server, InvokePermission Owner); host sends replies (SendTo.Owner, InvokePermission Server).
public sealed class PlayerActions : NetworkBehaviour
{
    public const float InteractRangeSlack = 1.0f;
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void SubmitNameRpc(FixedString32Bytes name, RpcParams rpcParams = default);
        // server: s = DisplayNameRules.Sanitize(name.ToString(), NetSlot); netState.ServerSetDisplayName(s); SessionState.Current?.ServerRenameClient(OwnerClientId, s)
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void InteractRpc(NetworkBehaviourReference target, RpcParams rpcParams = default);
        // server: target.TryGet(out NetworkInteractable i); sender = rpcParams.Receive.SenderClientId; i.CanInteract(sender);
        // |(transform.position + up*EyeHeight) - i.InteractPoint| <= i.Range + InteractRangeSlack; then i.ServerInteract(sender). No LOS check.
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void TeleportRpc(Vector3 position, float yaw);   // owner: GetComponent<PlayerLocomotion>().ApplyTeleport(position, yaw)
}

// Gameplay/Player/PlayerLocomotion.cs — owner only (enabled by PlayerNetState).
public sealed class PlayerLocomotion : MonoBehaviour
{
    public Vector3 Velocity { get; }       // last applied velocity (incl. vertical), zero right after ApplyTeleport
    public bool IsGrounded { get; } public bool IsSwimming { get; } public bool IsClimbing { get; }
    public float SecondsInWater { get; }
    public void ApplyTeleport(Vector3 position, float yaw);
        // cc.enabled = false; NetworkTransform.Teleport(position, Quaternion.Euler(0, yaw, 0), Vector3.one) when spawned with authority (else SetPositionAndRotation);
        // velocity = knockback = 0; JumpAssist reset; cc.enabled = true
    public void AddKnockback(Vector3 velocity); // M5 hook
    // OnEnable: input = ServiceRoots.App.Get<IInputService>() (tests may Replace it before spawn); pause = ServiceRoots.Game?.TryGet<IPauseState>.
    // Update: Paused -> nothing. Yaw: transform.Rotate(0, Intent.LookYawDeg, 0). MovementMath for speeds/accel/gravity/jump (JumpAssist)/swim;
    //   swimming = WaterVolume.FindContaining(pos) != null && MovementMath.IsSwimmingDepth(y, WaterSurface.HeightAt(x,z)) -> OwnerSetSwimming;
    //   inside a WaterVolume && MovementMath.IsInWater(y, surface) (wading OR swimming) && WaterExit.FindContaining(pos) != null && (JumpPressed || InteractPressed || MoveY > 0.5)
    //     -> climb: lerp over ClimbDuration to exit.TopPoint (cc disabled, IsClimbing true), then cc enabled, not swimming;
    //   SecondsInWater > WaterTimeoutSeconds or y < OutOfBoundsY -> ApplyTeleport to PlayerSpawner.Current.TryGetSpawnPose(netState.Slot).
    //   input.GameplayContext = IsClimbing ? Climbing : IsSwimming ? Swimming : Free (write only on change).
}

// Gameplay/Player/PlayerCamera.cs — owner only.
public sealed class PlayerCamera : MonoBehaviour
{
    public Camera Camera { get; }
    public float PitchDeg { get; }
    public void AddTrauma(float amount);   // trauma = clamp01(trauma + amount)
    // pitch from Intent.LookPitchDeg clamped ±PitchLimit on HeadPivot; netState.OwnerSetPitch; horizontal FOV from settings via
    // Camera.HorizontalToVerticalFieldOfView(h, aspect) re-applied on aspect change; sprint kick; trauma shake on Shake in LateUpdate; head bob; all scaled by settings.cameraMotion.
}

// Gameplay/Player/PlayerBody.cs — all peers.
public sealed class PlayerBody : MonoBehaviour
{
    [SerializeField] private Material[] slotMaterials; [SerializeField] private Material skinMaterial;
    public void Configure(Material[] slotMaterials, Material skinMaterial);  // builder
    public Transform NameplateAnchor { get; }
    public void Bind(PlayerNetState state, bool isOwner);  // slot material on SlotChanged (sharedMaterial swap; no MaterialPropertyBlock);
        // owner: Head/Nose/Torso/legs/left arm -> ShadowCastingMode.ShadowsOnly, right arm stays visible posed in camera space;
        // remotes: smoothed pitch = lerp(pitch, NetPitchQ/100, 1 - exp(-15 dt)) on HeadPivot and right arm. Walk cycle on every peer.
    public void Unbind();
}

// Gameplay/Player/PlayerInteractor.cs — owner only.
public sealed class PlayerInteractor : MonoBehaviour
{
    public const float CastRange = 3.0f, CastRadius = 0.15f;
    public NetworkInteractable Current { get; }
    // Only while input.GameplayContext == Free and !MenuOpen: SphereCast from the camera, mask Layers.Mask(Layers.Interactable), QueryTriggerInteraction.Ignore;
    // target = hit.collider.GetComponentInParent<NetworkInteractable>(); publishes InteractPromptChanged on change (via ServiceRoots.App EventBus);
    // InteractPressed && Current != null && Current.CanInteract(localId) -> actions.InteractRpc(new NetworkBehaviourReference(Current)).
}

// Gameplay/Player/InteractPromptChanged.cs — EventBus message.
public readonly struct InteractPromptChanged
{
    public readonly string Prompt; public readonly bool CanInteract;   // Prompt null/empty = hide
    public InteractPromptChanged(string prompt, bool canInteract);
}

// Gameplay/World/WaterVolume.cs — requires a trigger BoxCollider on the same object.
public sealed class WaterVolume : MonoBehaviour
{
    public bool Contains(Vector3 point);                   // world AABB of the BoxCollider
    public static WaterVolume FindContaining(Vector3 point);
    public static IReadOnlyList<WaterVolume> All { get; }
}

// Gameplay/World/WaterExit.cs — trigger BoxCollider + child "Top".
public sealed class WaterExit : MonoBehaviour
{
    public const string TopChildName = "Top";
    public Vector3 TopPoint { get; }
    public bool Contains(Vector3 point);
    public static WaterExit FindContaining(Vector3 point);
    public static IReadOnlyList<WaterExit> All { get; }
}

// Gameplay/World/SceneCameraSwitch.cs — on each gameplay scene's SceneCamera.
public sealed class SceneCameraSwitch : MonoBehaviour { } // Camera + AudioListener enabled iff PlayerNetState.Local == null (LocalPlayerChanged)
```

### 7.13 T14 — UI Toolkit (asm FishGame.UI)

```csharp
public enum UiScreen : byte { None = 0, MainMenu = 1, Busy = 2, Settings = 3, Pause = 4, Hud = 5 }

// UI/UiFlowController.cs — on App/UI next to the UIDocument.
public sealed class UiFlowController : MonoBehaviour
{
    public void Initialize(ServiceRegistry app);
        // needs: INetworkSession, ISettings, IInputService, IInputBindings, EventBus, AudioBus.
        // UIDocument.panelSettings = Instantiate(original) (runtime copy) and scale = uiScalePercent / 100f.
        // SceneManager.activeSceneChanged: Menu -> MainMenu; SceneNames.IsGameplay -> Hud; Bootstrap -> None.
        // input.MenuOpen = screen is MainMenu, Busy, Settings or Pause. Escape (Intent.PausePressed) toggles Pause in gameplay.
        // Solo pause: ServiceRoots.Game IPauseState.SetPaused(true/false) when INetworkSession.Mode == Solo.
        // Subscribes EventBus ToastRequested (queued bottom-right) and InteractPromptChanged (HUD centre).
    public UiScreen CurrentScreen { get; }
    public void Show(UiScreen screen);
}

public static class UiStrings
{
    public static string Get(string key);            // unknown key -> the key itself
    public static string Format(string key, string arg);
    // keys include every EndReasonText key (err.*) plus "err.generic", "toast.join_code" ("Join code: {0}"), menu/pause/settings labels.
    // "err.host_left" = "Host ended the session."  "err.joins_closed" = "Run in progress. Join when the crew is in the Hub, at the Briefing or at Night Camp."
    // "err.version" = "Different game version — update your build (or the host's build differs)."
}
```
Internal presenter classes (T14-private; names reserved): `MainMenuPresenter`, `BusyPresenter`, `SettingsPresenter`, `RebindPresenter`, `PausePresenter`, `HudPresenter`, `ToastPresenter`, `NameplateOverlay`. Element names inside `AppRoot.uxml` are T14-private.
Behaviour the other tasks rely on:
- MainMenu buttons: Host (`HostOnlineAsync`, publishes `ToastRequested("toast.join_code", code, 10f)`), Join (`JoinOnlineAsync`), Solo (`StartLocalAsync(false)`), Settings, Quit; display-name field. Dev only (`UNITY_EDITOR || DEVELOPMENT_BUILD`): Host Direct (`StartLocalAsync(true)`), Join 127.0.0.1 (`JoinDirectAsync("127.0.0.1", 7777)`).
- Busy screen Cancel: cancels the attempt's `CancellationTokenSource`.
- Pause: Resume, Settings, Leave (`LeaveAsync`), join code + Copy (`GUIUtility.systemCopyBuffer`) + streamer mask `••••••` with hold-to-reveal (online only). Dev-only host toggle "Close joins"/"Open joins": `SessionState.Current.ServerSetJoinsOpen(v)` + `INetworkSession.SetJoinableAsync(v)`.
- Nameplates: `PlayerNetState.Active` remotes, projected with `Camera.main`, alpha fades 15→25 m.
- Settings save on panel close only (`ISettings.Save`), apply live via `NotifyChanged`.

### 7.14 T13 — App composition (asm FishGame.App)

```csharp
// App/AppBootstrap.cs — on the root "App" (no parent). Composition root.
[DefaultExecutionOrder(-2000)]
public sealed class AppBootstrap : MonoBehaviour
{
    [SerializeField] private NetworkObject sessionStatePrefab; [SerializeField] private InputActionAsset inputActions; [SerializeField] private ContentRegistry contentRegistry;
    public static AppBootstrap Current { get; }
    public static bool SuppressDevAutoStart { get; set; }   // PlayMode tests set true before loading Bootstrap
    public ServiceRegistry Services { get; }
    public NetworkManager NetworkManager { get; }
    public ulong ContentHashValue { get; }
    public DevFlags DevFlags { get; }
    public void Configure(NetworkObject sessionStatePrefab, InputActionAsset inputActions, ContentRegistry contentRegistry); // builder
    // Awake: duplicate guard (destroy self if Current exists); DontDestroyOnLoad(gameObject); Current = this.
    //   NetworkManager = GetComponent<NetworkManager>() (never NetworkManager.Singleton: this Awake runs at -2000, before NGO's own Awake/OnEnable).
    //   OnServerStarted is raised synchronously inside StartHost after host self-approval, so the SessionState spawn below runs while StartHost is still on the stack. That is safe (SpawnManager is ready).
    //   Registers in Services: EventBus, ISettings (SettingsStore.CreateForThisInstance + EnsureLocalPlayerId), IInputService + IInputBindings
    //   (GetComponent<PlayerInputAdapter>().Initialize), SfxGenerator (GenerateAll), AudioBus (GetComponent<AudioBus>().Initialize), ContentRegistry (Build; hash),
    //   JoinApprovalBridge, DevFlags, LocalNetworkSession, INetworkSession (= CompositeNetworkSession(local, () => new UgsNetworkSession(config))).
    //   ServiceRoots.SetApp(Services). Enforces NetworkConfig: TickRate 30, ConnectionApproval true, ProtocolVersion 0, PlayerPrefab null.
    //   NetworkManager.OnServerStarted += spawn sessionStatePrefab via SpawnManager.InstantiateAndSpawn(sessionStatePrefab, destroyWithScene: false).
    //   Creates SceneFlow. Composition failures are logged (Debug.LogException) and disable the component.
    // Start (coroutine): GetComponentInChildren<UiFlowController>().Initialize(Services); SceneManager.LoadScene(SceneNames.Menu);
    //   wait until Menu is the active scene plus one frame; then unless SuppressDevAutoStart: DevStartup.TryAutoStart.
    // Application.quitting: best-effort INetworkSession.LeaveAsync(). OnDestroy (if Current == this): Services.Dispose(); ServiceRoots.SetApp(null); Current = null.
    public void DestroyForTests();  // Shutdown NGO if running, Destroy(gameObject)
}

// App/SceneFlow.cs
public sealed class SceneFlow : IDisposable
{
    public SceneFlow(INetworkSession session, NetworkManager networkManager, EventBus bus);
    // StateChanged -> Connected && networkManager.IsServer: networkManager.SceneManager.LoadScene(SceneNames.SpotLakeDock, LoadSceneMode.Single);
    //   status != Started -> log error + LeaveAsync.
    // Ended(reason): key = EndReasonText.ToastKey(reason); key != null -> bus.Publish(new ToastRequested(key)).
    // StateChanged -> Offline: if active scene != Menu -> SceneManager.LoadScene(SceneNames.Menu).
}

// App/GameScope.cs — one per gameplay scene (root object "GameScope").
public sealed class GameScope : MonoBehaviour
{
    public ServiceRegistry Services { get; }
    // Awake: Services = new ServiceRegistry(); Register<IPauseState>(new PauseState()); ServiceRoots.SetGame(Services).
    // OnDestroy: Services.Dispose(); ServiceRoots.SetGame(null) if it is ours.
}

// App/DevStartup.cs
public static class DevStartup
{
    // Dev builds only (UNITY_EDITOR || DEVELOPMENT_BUILD). Args = Environment.GetCommandLineArgs() + (#if UNITY_EDITOR) each
    // CurrentPlayer.ReadOnlyTags() entry split on whitespace. Order: Solo -> StartLocalAsync(false); HostDirect -> StartLocalAsync(true);
    // HasJoinDirect -> JoinDirectAsync. Else #if UNITY_EDITOR: EditorPrefs.GetBool(DevPrefKeys.AutoSolo) && CurrentPlayer.IsMainEditor -> Solo.
    public static IReadOnlyList<string> CollectArgs();
    public static void TryAutoStart(INetworkSession session, DevFlags flags);
}
```
Runtime file owned by T13: `Runtime/Dev/DevPrefKeys.cs` → `public static class DevPrefKeys { public const string AutoSolo = "FishGame.AutoSolo"; }`.

Config wiring (T13): `protocolVersion = BuildInfo.EffectiveProtocolVersion(devBuild ? DevFlags.ProtocolOffset : 0)`; `longTimeout = devBuild && DevFlags.LongTimeout`.

### 7.15 T15 — Editor builders (asm FishGame.Editor, namespace FishGame.EditorTools)

| File | Type | Public members |
|---|---|---|
| `Editor/Builders/ProjectBuilder.cs` | `static class ProjectBuilder` | `[MenuItem("FishGame/Build All")] static void BuildAllMenu()`; `static void BuildAll()`, the batchmode entry (`-executeMethod FishGame.EditorTools.ProjectBuilder.BuildAll`, always run together with `-quit`). On success it logs exactly `FG BUILDALL PASS` and **returns normally**, so `-quit` exits and Unity prints `Exiting batchmode successfully`. On failure it logs `FG BUILDALL FAIL errors=N`, then one `FG BUILDALL ERROR <text>` line per error, then calls `EditorApplication.Exit(1)` when `Application.isBatchMode`. `static bool RunAll(List<string> errors)` |
| `Editor/Builders/ProjectSettingsBuilder.cs` | `static class ProjectSettingsBuilder` | `static void Apply()`: TagManager layers (§5.3), physics matrix, `EditorSettings.enterPlayModeOptionsEnabled = false`, URP `Assets/Settings/PC_RPAsset.asset` (MSAA 4, depth texture on, HDR on, soft shadows, 2 cascades, shadow distance 70, opaque texture off), `InputSystem.actions = null` then delete `Assets/InputSystem_Actions.inputactions`, `PlayerSettings.runInBackground = true`, `NetcodeForGameObjectsProjectSettings.instance.GenerateDefaultNetworkPrefabs = false` + reflective `SaveSettings()`, delete `Assets/DefaultNetworkPrefabs.asset` if present |
| `Editor/Builders/AssetPaths.cs` | `static class AssetPaths` | constants for every path in §6.2 |
| `Editor/Builders/MaterialLibrary.cs` | `static class MaterialLibrary` | `static void BuildAll()`; `static Material Get(string name)` (e.g. `"M_Body_Slot0"`) |
| `Editor/Builders/ContentBuilder.cs` | `static class ContentBuilder` | `static void Build()` (create-if-missing SpotDef + ContentRegistry, ensure `lake_dock` listed; materials; PanelSettings) |
| `Editor/Builders/PrefabBuilder.cs` | `static class PrefabBuilder` | `static void Build()`; `static void BuildPlayer()`; `static void BuildDockBell()`; `static void BuildSessionState()` |
| `Editor/Builders/NetworkPrefabsBuilder.cs` | `static class NetworkPrefabsBuilder` | `static void Build()` |
| `Editor/Builders/SceneBuilder.cs` | `static class SceneBuilder` | `static void BuildAll()`; `static void BuildBootstrap()`; `static void BuildMenu()`; `static void ApplyBuildSettings()` |
| `Editor/Builders/LakeDockBuilder.cs` | `static class LakeDockBuilder` | `static void Build()` (writes `Spot_LakeDock.unity`, §6.5) |
| `Editor/Validation/Validators.cs` | `static class Validators` | each returns `List<string>` errors: `ContentIds()`, `NetworkPrefabs()`, `BuildScenes()`, `InputActions()`, `Manifest()`, `ProjectSettingsInvariants()`, `AssetsLayout()` |
| `Editor/Validation/ValidationRunner.cs` | `static class ValidationRunner` | `static List<string> Run()`; `[MenuItem("FishGame/Validate")] static void RunMenu()` |
| `Editor/Dev/PlayModeStartScene.cs` | `[InitializeOnLoad] static class PlayModeStartScene` | `static void Apply()` (sets `EditorSceneManager.playModeStartScene` to Bootstrap unless `Application.isBatchMode`); `static void Clear()` |
| `Editor/Dev/AutoSoloMenu.cs` | `static class AutoSoloMenu` | `[MenuItem("FishGame/Dev/Auto Solo")] static void Toggle()`; `[MenuItem("FishGame/Dev/Auto Solo", true)] static bool ValidateToggle()` (sets the checkmark); pref key `DevPrefKeys.AutoSolo` |
| `Editor/TestMenu/TestRunStartSceneGuard.cs` (asm TestMenu) | `[InitializeOnLoad] static class TestRunStartSceneGuard` | registers `ICallbacks`: RunStarted → `PlayModeStartScene.Clear()`; RunFinished → `PlayModeStartScene.Apply()` |

### 7.16 T16 — PlayMode harness (asm FishGame.Tests.PlayMode)
```csharp
// Tests/PlayMode/PlayModeHarness.cs
public static class PlayModeHarness
{
    public const float DefaultTimeoutSeconds = 10f;
    public static IEnumerator LoadBootstrapAndWaitForMenu();   // AppBootstrap.SuppressDevAutoStart = true; SceneManager.LoadScene(SceneNames.Bootstrap); wait for active scene Menu
    public static IEnumerator StartSoloAndWaitForPlayer();     // Local.Port = NetworkConstants.TestPort; StartLocalAsync(false); wait for PlayerNetState.Local
    public static IEnumerator LeaveAndWaitForMenu();
    public static IEnumerator TearDownApp();                   // LeaveAsync if needed; AppBootstrap.Current?.DestroyForTests(); yield null x2
    public static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string failMessage); // Assert.Fail on timeout
    public static IEnumerator Await(Task task, float timeoutSeconds);
}
```

---

## 8. Event and callback catalogue

| Name | Kind | Defined in (owner) | Raised by | Consumed by |
|---|---|---|---|---|
| `ToastRequested` | EventBus message | Runtime (T5) | SceneFlow (T13), MainMenuPresenter (T14) | ToastPresenter (T14) |
| `InteractPromptChanged` | EventBus message | Gameplay (T11) | PlayerInteractor (T11) | HudPresenter (T14) |
| `IInputService.ContextChanged` | C# event | Runtime (T4) | PlayerInputAdapter / Fake | UI (T14), optional |
| `ISettings.Changed` | C# event | Runtime (T5) | SettingsStore | AudioBus (T6), PlayerInputAdapter (T4), PlayerCamera (T11), UiFlowController (T14) |
| `IPauseState.Changed` | C# event | Runtime (T5) | PauseState | PlayerLocomotion (T11) |
| `INetworkSession.StateChanged` / `Ended` | C# events | Runtime (T8) | Local/Ugs/Composite sessions | SceneFlow (T13), UiFlowController (T14) |
| `SessionState.CurrentChanged` / `RosterChanged` | C# events | Gameplay (T10) | SessionState | UI (T14) |
| `PlayerNetState.LocalPlayerChanged` / `Spawned` / `Despawned` | static C# events | Gameplay (T11) | PlayerNetState | SceneCameraSwitch (T11), NameplateOverlay (T14), PlayModeHarness (T16) |
| `PlayerNetState.SlotChanged` / `DisplayNameChanged` | C# events | Gameplay (T11) | PlayerNetState | PlayerBody (T11), NameplateOverlay (T14) |
| `DockBell.Rang` | C# event | Gameplay (T12) | DockBell.RingRpc | tests / manual |

**RPC summary (M1):**

| RPC | Class | `SendTo` | `InvokePermission` | Owner |
|---|---|---|---|---|
| `SubmitNameRpc(FixedString32Bytes name, RpcParams rpcParams = default)` | PlayerActions | Server | Owner | T11 |
| `InteractRpc(NetworkBehaviourReference target, RpcParams rpcParams = default)` | PlayerActions | Server | Owner | T11 |
| `TeleportRpc(Vector3 position, float yaw)` | PlayerActions | Owner | Server | T11 |
| `RingRpc()` (private) | DockBell | ClientsAndHost | Server | T12 |

**NetworkVariables (M1):**

| Field | Type | Read / Write | Class | Owner |
|---|---|---|---|---|
| `NetPitchQ` | `short` | Everyone / Owner (traits: min 1/15 s) | PlayerNetState | T11 |
| `NetSlot` | `byte` | Everyone / Server | PlayerNetState | T11 |
| `NetDisplayName` | `FixedString32Bytes` | Everyone / Server | PlayerNetState | T11 |
| `NetSwimming` | `bool` | Everyone / Owner | PlayerNetState | T11 |
| `NetJoinsOpen` | `bool` (default true) | Everyone / Server | SessionState | T10 |
| `Roster` | `NetworkList<PlayerSlot>` | Everyone / Server | SessionState | T10 |

---

## 9. Runtime flows (who does what, in order)

**Boot.** Bootstrap scene loads → `AppBootstrap.Awake` composes services (§7.14) → `Start` loads Menu (plain) → `UiFlowController` sees Menu → MainMenu, `MenuOpen = true` → optional dev auto-start.

**Solo.** UI calls `INetworkSession.StartLocalAsync(false)` → Composite routes to Local → Begin → wait for shutdown → transport 127.0.0.1:Port, timeouts, payload, approver → `StartHost` → host self-approval (rule 1, slot 0) puts an `ApprovedJoin` in the bridge → `OnServerStarted`: AppBootstrap spawns SessionState (destroyWithScene false). SessionState absorbs the pending host join and becomes `bridge.Provider` → Local fires Established → Connected → SceneFlow loads Spot_LakeDock through NetworkSceneManager → `GameScope.Awake` (IPauseState) → `PlayerSpawner.OnLoadEventCompleted`: spawns the DockBell at BellAnchor and the host player at Spawn0 → `PlayerNetState.OnNetworkSpawn` enables the owner components → `SceneCameraSwitch` disables the scene camera → UI shows Hud with `MenuOpen = false`.

**Dev-direct.** The host does the same with `StartLocalAsync(true)` (Mode DevDirect, port 7777 only). A client calls `JoinDirectAsync("127.0.0.1", 7777)` and its payload goes to the host's approver (rules 2–8). Approved → client `ClientConnected` → Established. The host's SessionState gets `ClientConnected` and upserts the slot. NGO scene sync loads Spot_LakeDock on the client. Server `OnSynchronizeComplete(id)` → PlayerSpawner spawns that client's player at `Spawn<slot>`.

**Online.** `HostOnlineAsync` → Composite creates UgsNetworkSession lazily → sign-in → payload/approver → `CreateSessionAsync(...WithRelayNetwork())`. The SDK sets the relay data and calls `StartHost`. From there it matches Solo (SessionState spawn, Established, scene load). Join is the same with `JoinSessionByCodeAsync`.

**Rejection.** Approver sets `Reason = EndReason.ToString()` → the client's NGO stops while Connecting → `DisconnectClassifier.Classify(false, false, nm.DisconnectReason, t)` → Fail(reason) → shutdown → Offline → Ended(reason) → SceneFlow publishes `ToastRequested(EndReasonText.ToastKey(reason))` and the UI shows MainMenu with the toast.

**Clean leave (client).** Pause → Leave → `LeaveAsync` → Leave → shutdown → Offline → Ended(UserLeft), with no toast. SceneFlow loads Menu. Host: `ClientDisconnected` → SessionState marks the slot disconnected. NGO destroys the leaver's player object (isPlayerObject). Ugs: `RemovePlayerAsync`.

**Host leaves.** Clients' NGO stops while Connected → `Classify` → HostLeft → UnexpectedStop → Leave → ShutdownComplete → Offline → Ended(HostLeft) → toast `err.host_left` → Menu.

**Process kill.** The host detects the drop within `DisconnectTimeoutMS` (10 s), then handles it as a clean leave. The same `localPlayerId` (from the clone's own settings file) reconnects → rule 8a reclaims the same slot and colour.

**Mode switch.** Every start awaits `WaitForShutdownAsync` and calls `ConfigureDirect` (plain UTP), so Relay data never leaks into Solo.

---

## 10. Test inventory (exact names; owners)

All EditMode tests live in `Assets/_Project/Tests/EditMode/` (namespace `FishGame.Tests.EditMode`), one test class per file, named `<Subject>Tests`. Test methods are named `Subject_Condition_Expected`.

| Owner | File | Required tests (others may be added) |
|---|---|---|
| T1 | `SmokeTests.cs` | `Smoke_EditModeAssembly_Runs` |
| T2 | `Pcg32Tests.cs` | `Pcg32_Seed42Stream54_MatchesReferenceVector`, `Pcg32_ZeroSeed_IsReplaced`, `Pcg32_RangeInt_StaysInBounds` |
| T2 | `RngTests.cs` | `Rng_Derive_IsDeterministicAndStreamIndependent`, `Rng_Derive_NeverReturnsZero` |
| T2 | `Fnv1a64Tests.cs` | `Fnv1a64_KnownVectors_Match` |
| T2 | `ContentHashTests.cs` | `ContentHash_InputOrder_DoesNotChangeHash`, `ContentHash_DataVersionChange_ChangesHash`, `ContentHash_IdChange_ChangesHash` |
| T2 | `JoinCodeTests.cs` | `JoinCode_Normalize_TrimsAndUppercases`, `JoinCode_Normalize_Null_ReturnsEmpty`, `JoinCode_IsPlausible_RejectsSymbolsAndOverlong` |
| T2 | `PitchQuantTests.cs` | `PitchQuant_Quantize_ClampsToLimit` (±90 → ±8500), `PitchQuant_RoundTrip_WithinOneHundredthDegree`, `PitchQuant_ShouldSend_UsesHalfDegreeThreshold` |
| T2 | `MovementMathTests.cs` | `MovementMath_JumpPeak_IsOnePointOneMeters` (simulated with `VerticalStep`, dt 1/240, 1.10 ± 0.01), `MovementMath_CarrySpeedMult_ZeroKg_IsOne`, `MovementMath_CarrySpeedMult_FortyKg_IsMinimum`, `MovementMath_WishSpeed_SwimAndSprintRules` |
| T2 | `JumpAssistTests.cs` | `JumpAssist_PressWithinCoyoteWindow_Jumps`, `JumpAssist_PressAfterCoyoteWindow_DoesNotJump`, `JumpAssist_BufferedPressBeforeLanding_JumpsOnLanding`, `JumpAssist_StaleBufferedPress_DoesNotJump` |
| T3 | `ConnectionStateMachineTests.cs` | `ConnectionStateMachine_LegalTransitions_ReachExpectedState` (8 cases), `ConnectionStateMachine_IllegalPairs_ThrowInvalidTransition` (every other pair of the 30), `ConnectionStateMachine_Fail_RecordsReason` |
| T3 | `JoinRulesTests.cs` | `JoinRules_HostSelf_ApprovedEvenWhenSolo`, `JoinRules_SoloRemote_RejectedJoinsClosed`, `JoinRules_ProtocolMismatch_RejectedVersionMismatch`, `JoinRules_ContentHashMismatch_RejectedVersionMismatch`, `JoinRules_JoinsClosed_Rejected`, `JoinRules_Full_RejectedFullLobby`, `JoinRules_DuplicateConnectedIdentity_Rejected`, `JoinRules_DisconnectedIdentity_ReclaimsSlot`, `JoinRules_NewIdentity_TakesLowestFreeSlot`, `JoinRules_RuleOrder_VersionCheckedBeforeJoinsOpen` |
| T3 | `ConnectPayloadCodecTests.cs` | `ConnectPayloadCodec_RoundTrip_PreservesFields`, `ConnectPayloadCodec_LongNameAndId_TruncatedUnderMaxBytes`, `ConnectPayloadCodec_Garbage_ReturnsFalse` |
| T3 | `DisplayNameRulesTests.cs` | `DisplayNameRules_Sanitize_StripsControlTrimsAndTruncates`, `DisplayNameRules_Sanitize_Empty_UsesSlotFallback` |
| T3 | `DisconnectClassifierTests.cs` | `DisconnectClassifier_ReasonString_ParsesEndReason`, `DisconnectClassifier_EmptyWhileConnected_IsHostLeft`, `DisconnectClassifier_EmptyEarlyWhileConnecting_IsVersionMismatch`, `DisconnectClassifier_EmptyLateWhileConnecting_IsTimeout`, `DisconnectClassifier_UserInitiated_IsUserLeft` |
| T3 | `DevFlagsTests.cs` | `DevFlags_Parse_AllFlags`, `DevFlags_Parse_JoinDirectWithoutPort_UsesDefault`, `DevFlags_Parse_MalformedOffset_IsZero` |
| T3 | `EndReasonTextTests.cs` | `EndReasonText_EveryReason_HasKeyExceptUserLeft` |
| T5 | `ServiceRegistryTests.cs` | `ServiceRegistry_MissingService_Throws`, `ServiceRegistry_DuplicateRegister_Throws`, `ServiceRegistry_Replace_Overwrites`, `ServiceRegistry_Dispose_DisposesInReverseOrderOnce` |
| T5 | `EventBusTests.cs` | `EventBus_DisposedSubscription_StopsReceiving`, `EventBus_ThrowingHandler_DoesNotBlockOthers` |
| T5 | `SettingsStoreTests.cs` | `SettingsStore_SaveThenLoad_RoundTripsAllFields`, `SettingsStore_CorruptMain_LoadsBackup`, `SettingsStore_OutOfRangeValues_AreClamped`, `SettingsStore_TwoInstances_DoNotCorruptEachOther`, `SettingsStore_EnsureLocalPlayerId_IsStable` |
| T5 | `AtomicFileTests.cs` | `AtomicFile_Write_LeavesNoTempFile`, `AtomicFile_SecondWrite_KeepsBackupOfPrevious`, `AtomicFile_Write_HasNoBom` |
| T5 | `InstanceProfileTests.cs` | `InstanceProfile_DevProfile_Wins`, `InstanceProfile_MppmEditorModeName_UsesName`, `InstanceProfile_NoArgs_IsMain`, `InstanceProfile_Sanitize_LowercasesAndFilters` |
| T4 | `InputContextRouterTests.cs` | `InputContextRouter_Free_PassesMovementAndInteract`, `InputContextRouter_Swimming_BlocksSprintAndShove`, `InputContextRouter_Climbing_PassesOnlyLookAndPause`, `InputContextRouter_Menu_ZeroesGameplay`, `InputContextRouter_EnabledMaps_MenuIsUiOnly` |
| T4 | `LookMathTests.cs` | `LookMath_Mouse_ScalesWithoutDt`, `LookMath_Stick_IsSquaredAndSigned` |
| T4 | `InputActionsAssetTests.cs` | `InputActions_FromJson_HasMapsActionsAndTypes` (maps Player, Fishing, UI; Move/Look Value Vector2; Steer Value Axis; other Player/Fishing actions Button; UI action names present) |
| T6 | `SfxRecipesTests.cs` | `SfxRecipes_EveryM1Recipe_HasCorrectSampleCount`, `SfxRecipes_EveryM1Recipe_PeakBelowOne`, `SfxRecipes_EveryM1Recipe_HasNoNaNOrInf`, `SfxRecipes_Loops_HaveSmoothSeam`, `SfxRecipes_SameSeed_IsDeterministic` |
| T6 | `AudioMathTests.cs` | `AudioMath_EffectiveVolume_IsSquaredCategoryTimesSquaredMaster` |
| T7 | `RegistryIndexTests.cs` | `Registry_Index_IsOrdinalSortedAndStable`, `RegistryIndex_Duplicate_Throws` |
| T7 | `ContentRegistryTests.cs` | `ContentRegistry_Hash_StableAcrossInstances`, `ContentRegistry_Validate_FlagsBadIds` |
| T9 | `UgsErrorMapTests.cs` | `UgsErrorMap_FullMessage_IsFullLobby`, `UgsErrorMap_SessionNotFound_IsInvalidCode`, `UgsErrorMap_Other_IsServicesUnavailable` |
| T15 | `ValidationTests.cs` | `Validation_ContentIds_Pass`, `Validation_NetworkPrefabs_Pass`, `Validation_BuildScenes_BootstrapFirst`, `Validation_InputActions_Pass`, `Validation_Manifest_HasPinsAndNoForbidden`, `Validation_ProjectSettings_Pass`, `Validation_AssetsLayout_OnlyProjectAndSettings` |
| T15 | `ArchitectureTests.cs` | `Architecture_GameplayFishingPresentationUi_DoNotUseServicesOrTransports` (scans `.cs` under Gameplay, Fishing, Presentation, UI for `Unity.Services` and `Unity.Netcode.Transports`; asserts those asmdefs list no `Unity.Services.*` or `Unity.Networking.Transport`) |

PlayMode (`Assets/_Project/Tests/PlayMode/`, T16, `[UnityTest]`, port 7790):

| File | Tests |
|---|---|
| `BootTests.cs` | `Bootstrap_Loads_Menu` |
| `SoloSessionTests.cs` | `Solo_Start_SpawnsOneOwnedPlayerWithCamera` (exactly one `PlayerNetState`, `IsOwner`, Camera + AudioListener enabled, CapsuleCollider disabled, CharacterController enabled); `Solo_LeaveThenRestart_Works` (asserts `ShutdownInProgress == false` before each start, `UnityTransport.Protocol == ProtocolType.UnityTransport`, `ConnectionData.Address == "127.0.0.1"`) |
| `PlayerMovementTests.cs` | `FakeInput_MoveForward_MovesPlayerMoreThanOneMeterInOneSecond`; `TeleportRpc_OnDeck_LandsWithinFiveCentimetersWithZeroVelocity` |

---

## 11. Commands reference — `Tools/unity.ps1` (T1)

```
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\FishGame\Tools\unity.ps1 <command> [-OutDir <abs dir>] [-Filter <regex>] [-TimeoutMinutes <int>]
<command> = compile | build | test-edit | test-play | check
```
- Parameter names: `$Command` (positional 0, checked by hand against the five commands, because a ValidateSet failure under `powershell -File` exits 1, not 4), `$OutDir` (default `$env:FG_OUT`), `$Filter` (default empty), `$TimeoutMinutes` (default 5; T1's first run passes 15). Never name a variable `$args`. Use `$unityArgs`.
- Pre-flight: if `Temp/UnityLockfile` exists and `[IO.File]::Open(p,'Open','ReadWrite','None')` throws, print the LOCKED result line and exit 2.
- Unity: `C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe`, always with `-batchmode -nographics -projectPath "C:\Dev\FishGame" -logFile "<OutDir>\<command>.log"`.
  - compile: `-quit`.
  - build: `-quit -executeMethod FishGame.EditorTools.ProjectBuilder.BuildAll`.
  - test-edit: `-runTests -testPlatform EditMode -testResults "<OutDir>\editmode.xml" -dev-profile fgtest` (+ `-testFilter "<Filter>"`). **Never `-quit`.**
  - test-play: the same with `-testPlatform PlayMode` and `playmode.xml`.
  - check: build → test-edit → test-play, stopping at the first failure.
- `$p = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru -NoNewWindow; $null = $p.Handle; $p.WaitForExit($ms)`. On timeout print the TIMEOUT line with the PID and exit 3. Never kill.
- **Success:** compile: exit 0 **and** the log contains `Exiting batchmode successfully` **and** no `error CS`. build: the same **plus** the log contains `FG BUILDALL PASS`. A log containing `FG BUILDALL FAIL` is a failure, and its `FG BUILDALL ERROR` lines are printed. Tests: the XML exists, the root `test-run` has `failed="0"` and `total` > 0, and the log line `Test run completed. Exiting with code 0` is present.
- **Output contract** (the last line of each sub-run; plans quote these as expected output):
  ```
  FG RESULT compile PASS log=<path>
  FG RESULT compile FAIL log=<path>
  FG RESULT test-edit PASS total=N passed=N failed=0 skipped=K log=<path>
  FG RESULT test-edit FAIL total=N passed=P failed=F skipped=K log=<path>
  FG RESULT test-edit FAIL total=0 (no tests matched or compile errors) log=<path>
  FG RESULT check PASS
  FG RESULT <command> LOCKED (close the Unity editor, then rerun)
  FG RESULT <command> TIMEOUT pid=<pid> log=<path>
  ```
  - On compile/build FAIL, it first prints up to 40 log lines matching `error CS|Exception|Scripts have compiler errors|Compilation failed` (prefix `FG LOG `).
  - On test FAIL, it first prints each failed test as `FG FAILED <fullname> :: <first line of message>`.
- Exit codes: 0 PASS, 1 FAIL, 2 LOCKED, 3 TIMEOUT, 4 usage error (no OutDir / bad command).

---

## 12. Per-task file manifest (owned files; all paths under `Assets/_Project/` unless rooted)

| Task | Files created or edited |
|---|---|
| T1 | `Packages/manifest.json`, `Packages/packages-lock.json` (Unity-written), `.gitignore` (append `.vscode/`), `Tools/unity.ps1`, all 13 `.asmdef` (§4), all `AssemblyInfo.cs` markers, `Editor/CompileLogHook.cs`, `Editor/TestMenu/TestMenu.cs`, `Tests/EditMode/SmokeTests.cs`. Deletes `Assets/TutorialInfo/`, `Assets/Readme.asset`, `Assets/Scenes/` (SampleScene + folder), `Assets/Settings/SampleSceneProfile.asset` (with `.meta` files). |
| T2 | `Core/Random/{Pcg32,Rng}.cs`, `Core/Hashing/{Fnv1a64,ContentHash}.cs`, `Core/Versioning/BuildInfo.cs`, `Core/Net/JoinCode.cs`, `Core/Player/{PitchQuant,MovementMath,JumpAssist}.cs`, `Core/Game/{SceneNames,Layers,SlotPalette}.cs`, T2 tests |
| T3 | `Core/Net/{ConnState,ConnEvent,EndReason,SessionMode,IdentityKind,InvalidTransitionException,ConnectionStateMachine,PlayerKey,JoinRules,ConnectPayload,DisplayNameRules,DisconnectClassifier,EndReasonText,DevFlags,NetworkConstants}.cs`, T3 tests |
| T5 | `Runtime/Services/{ServiceRegistry,ServiceRoots,EventBus,ToastRequested,IPauseState,PauseState}.cs`, `Runtime/Settings/{SettingsData,ISettings,SettingsStore,SettingsPaths,AtomicFile,InstanceProfile}.cs`, T5 tests |
| T4 | `Core/Input/{InputContext,InputMaps,PlayerInputFrame,PlayerIntent,FightInput,InputContextRouter,LookMath}.cs`, `Runtime/Input/{FishGameInput.inputactions,FishGameInput.inputactions.meta,FishGameInput.cs (Unity-generated),InputActionNames,InputDeviceKind,IInputService,IInputBindings,PlayerInputAdapter}.cs`, `Tests/PlayMode/Fakes/FakeInputService.cs`, T4 tests |
| T6 | `Core/Audio/{AudioCategory,SfxIds,SfxRecipe,SfxRecipes,AudioMath}.cs`, `Runtime/Audio/{SfxGenerator,AudioBus,AmbienceBed}.cs`, `Runtime/World/WaterSurface.cs`, T6 tests |
| T7 | `Core/Content/{RegistryIndex,ContentIds}.cs`, `Data/{SpotDef,ContentRegistry}.cs`, T7 tests |
| T8 | `Runtime/Net/{INetworkSession,NetworkSessionException,IJoinContextProvider,ApprovedJoin,JoinApprovalBridge}.cs`, `Networking/{NetworkSessionConfig,ConnectionApprover,TransportSetup,NetworkManagerAwait,LocalNetworkSession,CompositeNetworkSession}.cs` |
| T9 | `Core/Net/UgsErrorMap.cs`, `Networking/{UgsNetworkSession,UgsBootstrap}.cs`, T9 tests |
| T12 | `Gameplay/Interaction/{NetworkInteractable,DockBell}.cs`, `Gameplay/Spawning/NetSpawnAnchor.cs` |
| T10 | `Gameplay/Session/{NetPlayerId,PlayerSlot,SessionState}.cs`, `Gameplay/Spawning/{IServerPlayerInit,PlayerSpawner}.cs` |
| T11 | `Gameplay/Player/{PlayerRigPaths,PlayerNetState,PlayerActions,PlayerLocomotion,PlayerCamera,PlayerBody,PlayerInteractor,InteractPromptChanged}.cs`, `Gameplay/World/{WaterVolume,WaterExit,SceneCameraSwitch}.cs` |
| T14 | `UI/{UiScreen,UiFlowController,UiStrings,MainMenuPresenter,BusyPresenter,SettingsPresenter,RebindPresenter,PausePresenter,HudPresenter,ToastPresenter,NameplateOverlay}.cs`, `UI/Uxml/AppRoot.uxml`, `UI/Uss/App.uss`, `UI/FishGameTheme.tss` |
| T13 | `App/{AppBootstrap,SceneFlow,GameScope,DevStartup}.cs`, `Runtime/Dev/DevPrefKeys.cs` |
| T15 | everything in §7.15, `Tests/EditMode/{ValidationTests,ArchitectureTests}.cs`. Generated: `Scenes/*.unity`, `Prefabs/*.prefab`, `Content/**`, `ProjectSettings/*` changes. Deletes `Assets/InputSystem_Actions.inputactions`. |
| T16 | `Tests/PlayMode/{PlayModeHarness,BootTests,SoloSessionTests,PlayerMovementTests}.cs` |
| T17 | `README.md` (repo root, section "Local multiplayer testing" + "Generated scenes are disposable"), `docs/superpowers/plans/2026-10-09-m1-acceptance.md` (manual checklist, criteria 1–13 + 7.8 player acceptance + user actions), `CLAUDE.md` (executeMethod namespace fix only) |

---

## 13. Resolved ambiguities and spec deviations

| # | Topic | Decision | Reason |
|---|---|---|---|
| D1 | Where `ConnState`/`EndReason` live | **Core**, not Runtime. `INetworkSession` (Runtime) uses them. | `ConnectionStateMachine` and `JoinRules` are Core and need them. |
| D2 | Duplicate identity (rule 7) reason | New `EndReason.DuplicateIdentity = 11` (shown as `err.rejected`). | The spec enum has no fitting value. |
| D3 | Connect payload | Core `ConnectPayload` + `ConnectPayloadCodec` (BinaryWriter, < 256 B) instead of an `INetworkSerializable` struct with `FastBufferWriter`. `NetPlayerId` (Gameplay) is only the roster/wire form. | Networking can't see Gameplay. The codec is EditMode-testable. The bytes on the wire are equivalent. |
| D4 | Approval → roster handoff | Runtime `JoinApprovalBridge` + `IJoinContextProvider` (implemented by SessionState). With no provider: empty roster, joins open. Pending approvals count as connected for rules 6 and 7. | The spec gives no seam. This prevents a 5th player sneaking in during simultaneous joins. |
| D5 | Who spawns `SessionState` | `AppBootstrap`, on `NetworkManager.OnServerStarted`, `destroyWithScene: false`. | It holds the prefab reference and runs for every mode, including the SDK-started host. |
| D6 | Service access from scene/network objects | Static `ServiceRoots.App` / `ServiceRoots.Game` (Runtime), reset on SubsystemRegistration. `ServiceRegistry` itself is an instance class. Tests swap services with `Replace<T>`. | Gameplay/UI can't see App. NetworkBehaviours are created by NGO, so injection isn't possible. |
| D7 | One session object for the UI | `CompositeNetworkSession` (Networking) routes to `LocalNetworkSession` or a lazily created `UgsNetworkSession`. `INetworkSession` gains `Mode` and `IsHost`. | Solo never constructs or touches UGS. UI pause needs to know Solo. |
| D8 | Error toasts | Shown only from `Ended` (through SceneFlow → `ToastRequested`). Start tasks still fault with `NetworkSessionException` for callers. A cancel ends with `UserLeft` and no toast. | Avoids double toasts. |
| D9 | `ConnEvent` table | 8 legal pairs (§7.3). It adds `Failed --Leave--> ShuttingDown` and `Failed --ShutdownComplete--> Offline`, as implied by 6.4. | A test enumerates the full 30-pair grid. |
| D10 | MPPM | No MPPM asmdef reference anywhere (F7). Clone detection for file suffixes uses `-editor-mode` + `-name` (same rule as Authentication). `-dev-profile` wins. `CurrentPlayer` is used only in App (tags, IsMainEditor) under `#if UNITY_EDITOR`. | The MPPM package has no code. There is no player-index API. |
| D11 | Settings file name | `settings{Suffix}.json` with `Suffix = "_" + sanitized profile` (e.g. `settings_player2.json`). The spec's `_p2` was illustrative. | It follows the real clone name. |
| D12 | Asmdef additions beyond spec 4.3 | Networking + `Unity.Collections` (`ConnectionEventData.PeerClientIds` is a NativeArray). UI + `Unity.Netcode.Runtime`, `Unity.Collections` (touching NetworkBehaviour-derived Gameplay types otherwise causes CS0012). Fishing/Presentation/App + `Unity.Collections`. Editor + `Unity.InputSystem`, URP, `UnityEngine.UI`, `Unity.Collections`. EditMode tests + `Unity.Netcode.Runtime`. PlayMode tests + `Unity.Collections`, `Unity.InputSystem` (`AppBootstrap.Configure` exposes `InputActionAsset`). | The architecture test still forbids `Unity.Services.*` and `Unity.Netcode.Transports` usage in Gameplay/Fishing/Presentation/UI. |
| D13 | Test-runner menu items | A separate asmdef `FishGame.Editor.TestMenu` (`UNITY_INCLUDE_TESTS`, nunit) holds `TestMenu` and the start-scene guard. | Keeps NUnit/TestRunner references out of the main Editor asm. |
| D14 | Fishing / Presentation in M1 | Asmdefs created with a marker file only. | Their references resolve and the asmdef set is final from T1 on. |
| D15 | Input wrapper | The generated `FishGameInput.cs` is committed but **unused**. `PlayerInputAdapter` uses a serialized `InputActionAsset` (wired by SceneBuilder onto AppBootstrap) plus `InputActionNames`, on a runtime `Instantiate` copy. | No first-import compile-order hazard. Rebind overrides never dirty the asset. |
| D16 | Boat map | Not shipped in M1 (`InputMaps.Boat` enum value reserved). | YAGNI. M6. |
| D17 | `.inputactions` type test | Types are asserted only for Player and Fishing. UI is checked by action names. | UI actions are PassThrough/Vector2, which contradicts "others Button". |
| D18 | Router for non-M1 contexts | Rod*, Carrying, Staggered, Downed, Carried and Seated route like Free in M1. | No M1 code sets them. The rows land with their milestones. |
| D19 | UGS findings (criterion 13) | Pre-recorded from source: SDK starts/stops NGO and sets relay data; NetworkManager must exist before `CreateSessionAsync`; the inspector Protocol Type is irrelevant on the Sessions path; LobbyFull/InvalidJoinCode arrive as `SessionError.Unknown` (message-text mapping); MPPM clones' auth profile comes from `-name`. Still to confirm at runtime: code format/case (V3), distinct PlayerIds, Solo after a Sessions run, perf baseline. | F1–F4. |
| D20 | Join-code case | `JoinCode.Normalize` trims and upper-cases (required by test `JoinCode_Normalize_TrimsAndUppercases`). If V3 proves codes are case-sensitive, change only `Normalize`. | Spec test name. |
| D21 | Online display name | M1 uses the settings name on every path (payload + `SubmitNameRpc`). | Anonymous UGS players have no Sessions name. |
| D22 | Dev-direct listen address | 127.0.0.1 (not 0.0.0.0). | MPPM-only use in M1. No Windows Firewall prompt. LAN play is out of M1 scope. |
| D23 | Dev-direct port | Host binds exactly 7777 (no retry). Solo retries `Port..Port+9`. Tests use 7790. | Clients must know the port. |
| D24 | NetSpawnAnchor "prefab id" | A direct serialized `NetworkObject prefab` reference (validated to be in the network list). | Simpler, and still validated by invariant 4.7 #2. |
| D25 | Water/climb detection | Static registries + collider bounds (`WaterVolume.FindContaining`, `WaterExit.FindContaining`), not OnTrigger callbacks. | Deterministic with a CharacterController and no Rigidbody. |
| D26 | Swim buoyancy | Spring-damper toward `SwimTargetRootY`. `SwimGravity −4` applies only while above the target. Constants are in MovementMath. | The spec gives targets, not a formula. |
| D27 | Pause key | `PlayerIntent.PausePressed` (Player/Pause in gameplay, UI/Cancel in Menu). | One field for the UI to read. |
| D28 | Cursor lock owner | `PlayerInputAdapter` (from the effective context), not PlayerCamera. | One owner for Menu and gameplay. |
| D29 | UI structure | One persistent `UIDocument` under `App/UI` (DontDestroyOnLoad) with every screen. Bootstrap adds `EventSystem` + `InputSystemUIInputModule` (V9 fallback adopted up front). PanelSettings is copied at runtime before setting `scale`. | Toasts survive scene loads. Keyboard/gamepad navigation works in Input-System-only mode. The asset is never modified in Play mode. |
| D30 | Test isolation | `unity.ps1` passes `-dev-profile fgtest` to test runs (separate settings file). `AppBootstrap.SuppressDevAutoStart`. The start-scene guard clears `playModeStartScene` during Test Runner runs. | Tests never touch the user's settings or auto-solo. |
| D31 | Physics/layers | `Water` = built-in layer 4. Other layers are pinned at 6–13 (§5.3). | F18. |
| D32 | PlayerSlot field names | PascalCase public fields (`ClientId`, `Id`, `Name`, `Slot`, `Connected`, `Ready`). | C# convention. Same fields as spec 6.2. |
| D33 | Commit trailer | `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` (implementers are Sonnet), as the workflow instructs. This differs from spec 4.1 #4 (Opus). **Resolved 2026-10-09: Sonnet; spec 4.1 #4 updated to match.** | Attribution follows the actual author. |
| D36 | Water exits | A `WaterExit` climb triggers while wading **or** swimming (`MovementMath.IsInWater`, root below surface − 0.3 m), not only while swimming. The ramp volume sits where the lakebed is about −0.66 m. | With the spec's lakebed slope, a swimmer touches bottom about 8 m before the ramp, so a swim-only exit would never fire there. |
| D37 | "Established" on the host | Fired after `StartHost()` returns true (Local), or after `CreateSessionAsync` returns with `IsServer && IsListening` (UGS). Not inside `OnServerStarted`. The `SessionState` spawn does hook `OnServerStarted`. | NGO raises `OnServerStarted` synchronously inside `StartHost` (2.13.3 `HostServerInitialize`). Firing Established there would start a scene load re-entrantly. |
| D38 | SDK and our approval seam | Verified that multiplayer 2.3.3 never assigns `ConnectionApprovalCallback` or `NetworkConfig.ConnectionData` (it touches only the transport). Our approver and payload therefore survive the online path. | grep of the package source |
| D39 | Integration fixes | T15–T17 may fix earlier-task files and add asmdef references (§2.6), with the public surface frozen. | Integration bugs first surface in T15/T16. Without this rule they would deadlock. |
| D40 | `BuildAll` exit convention | Returns normally on success (log `FG BUILDALL PASS`). `Exit(1)` only on failure. `unity.ps1 build` requires both markers. | `EditorApplication.Exit(0)` could end the process before `Exiting batchmode successfully` is printed. |
| D34 | Lakebed numbers | Shoreline radius 50 m, depth −6·clamp(d/40) inward; dock z −52 → −40 (end depth −1.5 m, swimmable); ladders at z −41.5; ramp at x 12. | Satisfies 17.1 #9 with a swimmable dock end. |
| D35 | Online `SetJoinableAsync` | `IHostSession.IsLocked = !open` + `SavePropertiesAsync()` (F2). Local: no-op. | Verified API. |

---

## 14. Plan-review amendments (adopted when the M1 plan was assembled)

These deviations came out of the per-task reviews and the plan self-review. They are binding in the same way as the sections above. Where an earlier section disagrees, this section wins. No public name or signature listed in §4–§7 is renamed or removed; the additions below are new internal helpers, new files, extra tests or behaviour hardening inside the listed signatures. The plan is `docs/superpowers/plans/2026-10-09-m1-foundation.md`.

| # | Task | Amendment |
|---|---|---|
| A1 | T1 | `Tools/unity.ps1` checks `$Command` by hand (§11 updated). Extra output lines: a second `usage: <detail>` line, `FG LOG no log file at <path>`, `FG LOG could not start Unity: <msg>`, `FG LOG could not parse <xml>`, `FG RESULT check FAIL`, `FG RESULT check LOCKED (...)`, `FG RESULT check TIMEOUT`. An `-OutDir` inside `C:\Dev\FishGame` exits 4 (§2.3 rule). All §11 lines are unchanged. |
| A2 | T1 | `TestMenu` is `[InitializeOnLoad]` and registers its result saver once per domain load (§7.1 updated). Public members unchanged. |
| A3 | T2 | Hardening: `PitchQuant.Quantize(NaN)` returns 0; `ContentCategoryIds` ctor throws `ArgumentNullException` for a null category or ids; `ContentHash.Compute` throws `ArgumentException` for an entry with null `Ids`. Extra test classes `BuildInfoTests`, `SceneNamesTests`, `LayersTests`, `SlotPaletteTests` and extra methods in the §10 classes. |
| A4 | T3 | T3 is one task in two step groups: part 1 (state machine, `PlayerKey`, `JoinRules`, `NetworkConstants`) and part 2 (`ConnectPayload` + codec, `DisplayNameRules`, `DisconnectClassifier`, `EndReasonText`, `DevFlags`), committed together. Hardening: the codec never splits a surrogate pair and `TryDecode` also rejects invalid UTF-8 and never throws; `DisplayNameRules.Sanitize` never leaves a lone surrogate at the cut; `DisconnectClassifier` rule 2 also requires the text to contain no comma (`Enum.TryParse("FullLobby, Kicked")` = 6 = `VersionMismatch`); `DevFlags` flag names are case-insensitive, a missing or malformed value is ignored and not consumed, `-dev-join-direct` takes a dotted-decimal IPv4 with an optional port 1..65535. Extra tests: `ConnectPayloadCodec_Encode_IsLittleEndianWithFormatByteFirst`, `ConnectPayloadCodec_MultiByteAndInvalidUtf8_HandledSafely`, `DisplayNameRules_Sanitize_DoesNotSplitSurrogatePair`, `DisconnectClassifier_NonExactReasonText_FallsThrough`, `DevFlags_None_AndNullArgs_AreDefaults`, `DevFlags_Parse_MalformedJoinDirect_IsIgnoredAndNextFlagKept`. |
| A5 | T4 | `FakeInputService.LastRumbleLow/High` have private setters. The asset JSON has `"version": 1`. UI Point/Click bind only `<Mouse>/position` / `<Mouse>/leftButton`. Extra tests `InputActions_FromJson_DefaultBindingsMatchSpec`, `InputContextRouter_RouteFight_OnlyWhileFightingOrStaggered`, `InputContextRouter_NonM1Contexts_RouteLikeFree`. `PlayerInputAdapter` privately subscribes to `ISettings.Changed` to reset toggle latches. The hand-written meta GUID is `b40c1ffe6431466eb35f7002829a6e02`. |
| A6 | T5 | Extra test file `PauseStateTests.cs` and extra tests (`EventBus_Publish_DeliversOnlyToMatchingType`, `AtomicFile_Write_CreatesDirectory`, `AtomicFile_ReadAllTextOrNull_MissingFile_ReturnsNull`, `SettingsStore_LongDisplayName_TruncatesTo16`, `SettingsStore_NotifyChanged_ClampsRaisesAndDoesNotWrite`). The default name and profile length are `private const`. `SettingsStore.Save` logs (does not throw) on IO / access errors; `SettingsData.Clamp` strips control characters from `displayName`. |
| A7 | T6 | Internal `AudioBus.IsReady` and `AudioBus.TryGetClip` (used by `AmbienceBed`); `[DisallowMultipleComponent]` on `AudioBus` and `AmbienceBed`. Extra tests and the file `WaterSurfaceTests.cs`. `AppBootstrap` registers `AudioBus` under its concrete type (`services.Register(audio)`), which `AmbienceBed` and `DockBell` resolve. SFX seed is a private `AppBootstrap` constant (1). |
| A8 | T8 | Extra file `Tests/EditMode/JoinApprovalBridgeTests.cs` (5 tests). `TransportSetup.Get` also assigns `NetworkConfig.NetworkTransport` when found only by `GetComponent`. `ConnectionApprover.Uninstall` clears the callback only while it is still its own; `Handle` rejects with `Unknown` on an exception. Each start shuts NGO down first if it is still listening. `ActivePort` has a private setter; `IsHost` is false while Offline. `CompositeNetworkSession` forwards events from either backend (only one can be outside Offline). **A cancel or a leave while Connecting faults the start task with `NetworkSessionException(UserLeft)`**, the same as T9 (§7.8 comment updated). |
| A9 | T9 | `UgsNetworkSession` is `public sealed partial class` in `UgsNetworkSession.cs`, `UgsNetworkSession.Teardown.cs` and `UgsNetworkSession.NgoEvents.cs`, plus internal `Networking/UgsAsync.cs` (timeouts). A compile-time guard constant checks the mirrored `SessionError` values. `SetJoinableAsync` is bounded and only logs failures. A cancel faults with `NetworkSessionException(UserLeft)`. On a client, a reason recorded when NGO stopped while Connecting wins over the SDK's follow-up error. `DisconnectClassifier` timing counts from NGO `OnClientStarted`. **`UgsErrorMap` rules changed** (§7.9 updated): "full" must not be followed by a letter, and a new rule maps "relay" messages to `ServicesUnavailable`. Extra tests `UgsErrorMap_FullTextBeatsSessionNotFoundCode_IsFullLobby`, `UgsErrorMap_CodeTextWithUnknownCode_IsInvalidCode`, `UgsErrorMap_InvalidParameter_IsInvalidCode`, `UgsErrorMap_FullInsideLongerWord_IsNotFullLobby`, `UgsErrorMap_RelayJoinCodeFailure_IsServicesUnavailable`. |
| A10 | T10 | Internal `Gameplay/Session/SessionText.cs` (byte-safe FixedString conversion) and internal `SessionState.ServerAbsorbApproved(ulong)`, called by `PlayerSpawner` because NGO raises `OnSynchronizeComplete` before `ClientConnected`. `SessionState` resolves the bridge with `TryGet` and logs an error if it is missing. |
| A11 | T11 | Internal `Gameplay/Player/NameWire.cs` and internal `PlayerLocomotion.IsSprinting`. `PlayerNetState` overrides `OnDestroy` for static cleanup. `WaterVolume`, `WaterExit` and `PlayerLocomotion` have no `[RequireComponent]`; T15 adds the trigger `BoxCollider`s and the `CharacterController`. Swim time for the 60 s water timeout counts swimming only. |
| A12 | T13 | `UiFlowController.Initialize(Services)` is called at the start of `AppBootstrap.Start`, which is a coroutine that waits for Menu plus one frame before `DevStartup.TryAutoStart` (§7.14 updated). `AppBootstrap` also enforces `EnableSceneManagement = true`, fills a null `NetworkTransport`, sets `Application.runInBackground = true`, logs composition failures and disables itself, and resets `Current` on SubsystemRegistration. `GameScope` has `[DefaultExecutionOrder(-1500)]`. `DevFlags` are parsed from `DevStartup.CollectArgs()` (command line plus MPPM tags). |
| A13 | T14 | Internal `UI/UiQuery.cs`. `PanelSettings` is copied in `UiFlowController.Start` when the `UIDocument` is bound. `UiStrings` keeps the contract's non-ASCII strings (`••••••` and the em dash in `err.version`), which conflicts with spec 11.1 "UI text is ASCII"; T17 checks that the default runtime font renders them, and only the `UiStrings` values change if it does not. |
| A14 | T15 | The UIDocument property is `visualTreeAsset` (§6.5 updated). Extra public `AssetPaths` constants (folders, mesh assets, settings files, `DefaultVolumeProfile`, template paths). New generated folder `Assets/_Project/Content/Meshes/` (`LakeDock_Lakebed.asset`, `LakeDock_Shore.asset`; §6.1 updated). Internal `BuilderUtil`, `LakeDockLayout`, `LakeDockMeshes`, `ValidationText`. `TestRunStartSceneGuard` also hooks `playModeStateChanged` (ExitingEditMode). `PlayModeStartScene.Apply` clears the start scene in batchmode. `ProjectSettingsBuilder` also removes the `com.unity.input.settings.actions` config object and repoints `PC_RPAsset`'s dangling `m_VolumeProfile` (left by T1's deletion of `SampleSceneProfile.asset`) to `DefaultVolumeProfile.asset`. `PrefabBuilder` sets `CharacterController.minMoveDistance = 0`. The validators also check every spec 4.4 pin, a direct `com.unity.transport` entry, non-zero distinct `GlobalObjectIdHash` values on disk, missing scripts in prefabs, scene-text GUID references, the absent project-wide actions entry, orphan root `.meta` files and the deleted volume-profile GUID. Layout choices: ladders at x ±1.58, 14 rocks, 30 trees, streams `lake_dock.rocks` / `lake_dock.trees`. |
| A15 | T16 | `LoadBootstrapAndWaitForMenu` asserts that no App or NetworkManager leaked; `TearDownApp` waits (bounded) for `AppBootstrap.Current`, `NetworkManager.Singleton` and `PlayerNetState.Active` to clear and fails only after `DestroyForTests`. |
| A16 | T17 | Comment-only integration fix allowed in `UgsNetworkSession.cs` if T9 left out the "Recorded findings (M1 criterion 13)" block. The README tells the user to create the optional dev asset `Assets/_Project/Content/Dev/NetSim_150ms_2pct.asset` by hand (inside `_Project`, so invariant 4.7 #7 holds). |
| A17 | all | Untracked `?? docs/...` plan files and, from T1 until T15, `?? Assets/DefaultNetworkPrefabs.asset(.meta)` are expected in `git status`; they are never staged and never a reason to stop. |

Still open (needs the user or a runtime run): package resolution and `multiplayer.tools` 2.2.12 compiling against NGO 2.13.3 (first proven in T1 step 8); `com.unity.transport` resolving to 6.6.0 versus NGO's declared 2.6.0 (read `packages-lock.json` after T1); whether `PanelSettings.scale` composes with Scale With Screen Size (T17, criterion 11); the real join-code format and LobbyFull message text (criterion 12); MPPM tag text accepting spaces and colons (T17).
