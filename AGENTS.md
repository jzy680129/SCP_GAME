# Project Rules

Scope: these rules apply to this Unity project root.

## Project Baseline

- Unity version: `6000.4.3f1`.
- Render pipeline: URP.
- Target multiplayer shape: third-person cooperative survival/building game, up to 4 players.
- Network stack: Netcode for GameObjects, Unity Transport, Unity Authentication, Lobby, Relay, Multiplayer Play Mode, and Multiplayer Tools.
- Keep package versions pinned in `Packages/manifest.json`; do not replace installed UPM packages with copied package folders unless there is a specific reason.

## Multiplayer Rules

- Default to server-authoritative gameplay. Clients submit input or intent; the host/server validates movement-sensitive interactions, gathering, inventory changes, crafting, building placement, damage, and world persistence.
- Do not put final gameplay authority in UI, animation controllers, local-only input scripts, or client-owned visual scripts.
- Local player input may read keyboard/controller state, but networked simulation code must expose explicit input/intent APIs so host and clients can run the same rules.
- Use NetworkObjects only for objects that need network identity. Do not make every visual prop networked.
- Prefer low-frequency state sync and event RPCs over per-frame RPCs. Per-frame visual animation should stay local unless it changes authoritative gameplay state.
- Test multiplayer changes with Multiplayer Play Mode and Multiplayer Tools before calling them complete.

## Asset And Folder Rules

- Runtime game scenes belong under `Assets/Scenes`. Build Settings should only include real project scenes from `Assets/Scenes`, not vendor demo scenes.
- Runtime scripts belong under `Assets/Scripts`, grouped by responsibility such as `Animation`, `Camera`, `Gameplay`, `Inventory`, `Building`, `Networking`, `Persistence`, `UI`, and `Rendering`.
- Project-owned runtime art belongs under stable project folders such as `Assets/Characters`, `Assets/Res`, `Assets/UI`, `Assets/Settings`, and future domain folders.
- External asset packs must be treated as vendor source. New imports should go under `Assets/_ThirdParty/<Vendor>/<PackageName>` or another clearly vendor-owned folder. Existing vendor imports include `Assets/Knife`, `Assets/Survival_Animations`, `Assets/TextMesh Pro`, and `Assets/TutorialInfo`.
- Do not edit vendor package files directly unless patching is unavoidable. Put wrappers, adapters, prefabs, material overrides, and gameplay integration scripts in project-owned folders.
- Raw source assets and conversion outputs should stay separated:
  - `Assets/Raw`: raw imported or source-side content that is not final runtime content.
  - `Assets/Processed`: processed outputs from tools or pipelines.
  - Runtime-ready prefabs/materials/controllers should be promoted into project-owned runtime folders.
- Temporary or diagnostic folders such as `Assets/CodexTemp`, `Assets/Temp`, `Assets/Screenshots`, `Assets/_Recovery`, and root `exports` must not be referenced by production scenes or prefabs.
- Move Unity assets through the Unity Editor or AssetDatabase whenever possible so `.meta` GUIDs and references remain intact.

## Naming Rules

- Prefer clear English asset names with type prefixes for new production assets:
  - `SCN_` scene
  - `PF_` prefab
  - `MAT_` material
  - `TEX_` texture
  - `AN_` animation clip
  - `AC_` animator controller
  - `SO_` ScriptableObject data
  - `UI_` UI document/style/resource
  - `SFX_` sound effect
- Keep vendor asset names unchanged unless promoting a copy into project-owned runtime folders.
- Do not create long-lived assets in folders named `Temp`, `CodexTemp`, `Screenshots`, or `_Recovery`.

## NAS And Shared Storage

- When the user asks about NAS, network drive, SMB share, art asset storage on NAS, or temporary game development server, first read `C:\Users\10304\.codex\NAS_CONFIG.md`.
- Do not persist NAS passwords in files or responses; ask the user to enter credentials interactively when needed.
- Keep the Unity project and cache folders on local storage. Use NAS/shared storage for source art, references, exported builds, backups, and delivery packages.

## Development Hygiene

- Before adding a plugin or asset pack, decide whether it is runtime dependency, editor-only tool, or vendor source art.
- Keep demo scenes and sample scripts isolated from production build scenes.
- Add focused EditMode or PlayMode tests for gameplay rules, state transitions, save/load boundaries, and network validation logic when those systems are introduced.
- Do not perform broad asset moves or cleanup without first checking references and Build Settings.

## Git Rules

- Track Unity source folders and project configuration: `Assets`, `Packages`, `ProjectSettings`, `Docs`, root project rules, `.gitignore`, and `.gitattributes`.
- Do not track generated Unity cache folders: `Library`, `Temp`, `Obj`, `Logs`, `Build`, `Builds`, or `UserSettings`.
- Do not track local Plastic metadata, local exports, screenshots, recovery folders, IDE solution files, or Python cache files.
- Keep `.meta` files with their matching Unity assets.
- Use Git LFS for binary art, audio, video, archives, and large DCC files as defined in `.gitattributes`.
- Use `main` as the default branch name.
