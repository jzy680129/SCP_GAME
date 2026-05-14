# Project Structure And Asset Pipeline

This document is the project-level rule for asset placement, imports, and future cleanup. The current baseline is a Unity 6 URP third-person cooperative survival/building project with a maximum target of 4 players.

## Current Baseline

- Unity: `6000.4.3f1`
- Render pipeline: URP
- Current development scene: `Assets/_Project/Scenes/Dev/SCN_Dev_Locomotion.unity`
- Build Settings scene policy: only project-owned scenes under `Assets/_Project/Scenes` should be enabled for game builds.
- Netcode generated prefab list: keep the single `Assets/DefaultNetworkPrefabs.asset` root asset unless Unity Netcode changes its generated path behavior.
- Main multiplayer packages:
  - `com.unity.netcode.gameobjects` `2.11.2`
  - `com.unity.transport` `2.7.3`
  - `com.unity.services.authentication` `3.6.1`
  - `com.unity.services.lobby` `1.3.0`
  - `com.unity.services.relay` `1.2.0`
  - `com.unity.multiplayer.playmode` `2.0.2`
  - `com.unity.multiplayer.tools` `2.2.8`

## Current Folder Classification

| Path | Classification | Rule |
| --- | --- | --- |
| `Assets/_Project/Scripts` | Project runtime code | Gameplay, camera, animation, networking, UI, building, inventory, persistence, and rendering code. |
| `Assets/_Project/Editor` | Project editor tools | Editor-only automation and import helpers. Do not reference from runtime code. |
| `Assets/_Project/Tests` | Project tests | EditMode and future PlayMode tests. |
| `Assets/_Project/Scenes/Dev` | Development scenes | Current integration scenes for movement, camera, UI, networking, and prototypes. |
| `Assets/_Project/Scenes/Game` | Production game scenes | Create when the first real playable map or bootstrap scene is introduced. |
| `Assets/_Project/Scenes/Test` | Test scenes | Create focused scenes for PlayMode and systems tests. |
| `Assets/_Project/Scenes/Template` | Unity template scenes | Keep isolated from production builds unless deliberately promoted. |
| `Assets/_Project/Input` | Input assets | Input System action maps and future input configuration. |
| `Assets/_Project/UI` | Project UI | Runtime UI Toolkit documents, USS files, panel settings, sprites, and UI prefabs. |
| `Assets/_Project/Settings` | Project settings/assets | URP assets, lighting files, post-process profiles, render settings, and project-owned configuration assets. |
| `Assets/_Project/Art/Common` | Project visual resources | Runtime-ready reusable shaders, materials, textures, and support art. |
| `Assets/_Project/Art/Characters` | Project-owned character content | Runtime-ready character materials/textures/models that have been promoted for use. |
| `Assets/_Project/Pipeline/Raw` | Raw import staging | Source-side assets that are not final runtime content. |
| `Assets/_Project/Pipeline/Processed` | Pipeline outputs | Processed Mixamo/tool outputs. Promote selected assets before production use. |
| `Assets/_Project/Tools` | Project pipeline tools | Blender/import/build helper scripts and pipeline docs. |
| `Assets/_ThirdParty/Knife/RealBlood` | Imported vendor pack | Treat as third-party source. Do not edit directly unless unavoidable. |
| `Assets/_ThirdParty/SurvivalAnimations` | Imported vendor pack | Treat as third-party source. Promote selected clips/controllers into project-owned folders before deep integration. |
| `Assets/TextMesh Pro` | Package support assets | Keep isolated. Do not mix project UI assets into this folder. |
| `Assets/TutorialInfo` | Template/tutorial content | Remove from production dependency path when no longer needed. |
| `Assets/UI Toolkit` | Unity UI Toolkit support assets | Keep isolated from project-owned UI. |
| `Assets/CodexTemp`, `Assets/Temp` | Temporary workspace | No production references. Clean when safe. |
| `Assets/Screenshots` | Debug captures | No production references. |
| `Assets/_Recovery` | Recovery artifacts | Audit before release; do not build gameplay dependencies on this path. |
| `exports` | Export output | Build/export staging outside runtime `Assets`. |

## Target Layout For New Work

Create missing folders only when they are needed. Keep project-owned work under `Assets/_Project` and vendor source under `Assets/_ThirdParty`.

```text
Assets/
  _Project/
    Art/
      Characters/
      Environment/
      Props/
      Materials/
      Shaders/
      VFX/
      UI/
    Audio/
    Data/
      Items/
      Recipes/
      Buildings/
      Balance/
    Input/
    Prefabs/
      Player/
      Networked/
      Building/
      Resources/
      UI/
    Scenes/
      Dev/
      Game/
      Test/
      Template/
    Scripts/
      Gameplay/
      Networking/
      Building/
      Inventory/
      Crafting/
      Persistence/
      UI/
      Shared/
    Settings/
    Tests/
    Tools/
  _ThirdParty/
    <Vendor>/
      <PackageName>/
```

## Import Workflow

1. Download or store source asset packages outside the Unity project, preferably on shared storage or NAS-backed asset storage.
2. Import vendor packs into `Assets/_ThirdParty/<Vendor>/<PackageName>`, not directly into production folders.
3. Keep vendor demo scenes, scripts, and samples isolated from Build Settings.
4. Review scale, pivots, collision, material compatibility, texture size, URP shader compatibility, and license constraints.
5. Promote only selected runtime-ready assets into project-owned folders.
6. Create project-owned prefabs and ScriptableObject data that reference promoted assets.
7. Leave vendor originals unchanged so updates remain possible.

## Multiplayer Asset Rules

- Player prefabs and networked world prefabs should live in `Assets/_Project/Prefabs`, not inside vendor folders.
- Networked prefabs must have intentional `NetworkObject` usage and clear authority ownership.
- Building pieces should be project-owned prefabs with explicit placement bounds, collision, cost data, health data, and save IDs.
- Resource nodes should separate visual mesh, gatherable state, loot table, respawn/persistence state, and network authority.
- Do not make vendor demo controllers authoritative gameplay code.

## Cleanup Notes

- The old root project folders have been migrated into `Assets/_Project` through Unity/AssetDatabase so `.meta` GUIDs and references are preserved.
- Existing vendor packs have been moved under `Assets/_ThirdParty`.
- The current build scene is `Assets/_Project/Scenes/Dev/SCN_Dev_Locomotion.unity`.
- Keep temporary/debug folders ignored and out of production scene references.
- Before release, audit template/tutorial/support folders and remove anything not used by production or tools.

## Quick Checklist Before Adding Assets

- Is this asset vendor source, raw source, processed output, or production runtime content?
- Does it belong in project-owned folders or third-party folders?
- Does the asset need a prefab wrapper instead of direct scene placement?
- Does it need `NetworkObject`, collision, LOD, Addressables grouping, or a save ID?
- Is it referenced by a production scene or only by a demo/test scene?
- Are `.meta` files preserved?
