# Project Guidelines

## Baseline

- Unity version: `6000.4.3f1`.
- Render pipeline: URP.
- Target game: third-person 4-player cooperative survival/building.
- Network stack: Netcode for GameObjects, Unity Transport, Authentication,
  Lobby, Relay, Multiplayer Play Mode, and Multiplayer Tools.

## Folder Rules

- Project-owned content belongs under `Assets/_Project`.
- Runtime scenes belong under `Assets/_Project/Scenes`.
- Runtime scripts belong under `Assets/_Project/Scripts`.
- Editor tools belong under `Assets/_Project/Editor`.
- Tests belong under `Assets/_Project/Tests`.
- Vendor assets belong under `Assets/_ThirdParty/<Vendor>/<PackageName>`.
- Keep generated or diagnostic folders out of production references.

## Asset Rules

- Keep vendor names unchanged unless promoting a copy into project-owned
  runtime folders.
- Do not edit vendor package files directly unless patching is unavoidable.
- Move Unity assets through the Unity Editor or AssetDatabase when possible so
  `.meta` GUIDs stay intact.

## Git Rules

- Use `main` as the default branch.
- Track `Assets`, `Packages`, `ProjectSettings`, `Docs`, root rules,
  `.gitignore`, and `.gitattributes`.
- Do not track `Library`, `Temp`, `Obj`, `Logs`, `Build`, `Builds`, or
  `UserSettings`.
- Keep `.meta` files with their matching Unity assets.
- Use Git LFS for binary art, audio, video, archives, and DCC source files.
