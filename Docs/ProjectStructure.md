# Project Structure And Asset Pipeline

This document describes the current project layout and the target rules for future imports. It is intentionally conservative: existing assets are documented first, and physical moves should be done later through Unity so references stay intact.

## Current Baseline

- Unity: `6000.4.3f1`
- Render pipeline: URP
- Main packages added for multiplayer:
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
| `Assets/Scripts` | Project runtime code | Keep production gameplay, camera, animation, networking, UI, and rendering scripts here. |
| `Assets/Editor` | Project editor tools | Editor-only automation and import helpers. Do not reference from runtime code. |
| `Assets/Tests` | Project tests | Keep EditMode/PlayMode tests here. |
| `Assets/Scenes` | Project scenes | Only real project scenes should be added to Build Settings. |
| `Assets/UI` | Project UI | Runtime UI documents, styles, panel settings, and UI assets. |
| `Assets/Settings` | Project settings/assets | URP assets, lighting profiles, post-process profiles, and project configuration assets. |
| `Assets/Res` | Project runtime resources | Runtime shaders/materials and project-owned reusable visual resources. |
| `Assets/Characters` | Project-owned character content | Runtime-ready character materials/textures/models that have been promoted for use. |
| `Assets/Raw` | Raw import staging | Raw source-side content. Not final runtime content. |
| `Assets/Processed` | Pipeline outputs | Processed Mixamo/tool outputs. Promote selected outputs before production use. |
| `Assets/Tools` | Project pipeline tools | Scripts and helper files for art/import pipelines. |
| `Assets/Knife` | Imported vendor pack | Treat as third-party source. Do not edit directly unless unavoidable. |
| `Assets/Survival_Animations` | Imported vendor pack | Treat as third-party source. Copy/promote selected clips/controllers into project-owned folders before deep integration. |
| `Assets/TextMesh Pro` | Package support assets | Keep isolated. Do not mix project UI assets into this folder. |
| `Assets/TutorialInfo` | Template/tutorial content | Remove from production dependency path when no longer needed. |
| `Assets/UI Toolkit` | Unity UI Toolkit support assets | Keep isolated from project-owned UI. |
| `Assets/CodexTemp`, `Assets/Temp` | Temporary workspace | No production references. Clean when safe. |
| `Assets/Screenshots` | Debug captures | No production references. |
| `Assets/_Recovery` and `Assets/1` | Recovery/lightmap artifacts | Audit before release; do not build gameplay dependencies on these paths. |
| `exports` | Export output | Build/export staging outside runtime Assets. |

## Target Layout For New Work

Use the existing folders for now. If the project gets larger, introduce `Assets/_Project` and migrate through Unity Editor, not the filesystem.

Recommended future project-owned layout:

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
  _ThirdParty/
    <Vendor>/
      <PackageName>/
```

Until that migration happens, map these target areas to the current folders:

- `_Project/Scripts` -> `Assets/Scripts`
- `_Project/Scenes` -> `Assets/Scenes`
- `_Project/UI` -> `Assets/UI`
- `_Project/Settings` -> `Assets/Settings`
- `_Project/Art/Characters` -> `Assets/Characters`
- `_Project/Art/Materials` and `_Project/Art/Shaders` -> `Assets/Res`
- `_ThirdParty` -> existing vendor folders such as `Assets/Knife` and `Assets/Survival_Animations`

## Import Workflow

1. Download or store source asset packages outside the Unity project, preferably on shared storage or NAS-backed asset storage.
2. Import vendor packs into a vendor-owned folder, not directly into production folders.
3. Keep vendor demo scenes, scripts, and samples isolated from Build Settings.
4. Review scale, pivots, collision, material compatibility, texture size, and URP shader compatibility.
5. Promote only selected runtime-ready assets into project-owned folders.
6. Create project-owned prefabs and ScriptableObject data that reference promoted assets.
7. Leave vendor originals unchanged so updates remain possible.

## Multiplayer Asset Rules

- Player prefabs and networked world prefabs should live in project-owned prefab folders, not inside vendor folders.
- Networked prefabs must have intentional NetworkObject usage and clear authority ownership.
- Building pieces should be project-owned prefabs with explicit placement bounds, collision, cost data, health data, and save IDs.
- Resource nodes should separate visual mesh, gatherable state, loot table, respawn/persistence state, and network authority.
- Do not make vendor demo controllers authoritative gameplay code.

## Cleanup Plan

Do not perform these moves blindly. Use Unity Editor or AssetDatabase and verify scene/prefab references after each step.

1. Remove vendor demo scene `Assets/Knife/Real Blood/Scenes/RealBloodShowcase.unity` from Build Settings if it is not intentionally used.
2. Decide whether `Assets/1` and `Assets/_Recovery` are still needed. Archive or delete only after checking references.
3. Move future imported packs into `Assets/_ThirdParty`.
4. Promote the selected survival animation clips into project-owned animation folders.
5. Create project-owned network player prefab and keep vendor animation/model assets as dependencies only.
6. Add validation tests for Build Settings scene paths and forbidden production references to temp/debug folders.

## Quick Checklist Before Adding Assets

- Is this asset vendor source, raw source, processed output, or production runtime content?
- Does it belong in project-owned folders or third-party folders?
- Does the asset need a prefab wrapper instead of direct scene placement?
- Does it need NetworkObject, collision, LOD, addressable grouping, or save ID?
- Is it referenced by a production scene or only by a demo/test scene?
- Are `.meta` files preserved?
