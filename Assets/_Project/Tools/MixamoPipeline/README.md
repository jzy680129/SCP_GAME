# Mixamo Pipeline

This folder contains offline tools for preparing Mixamo FBX animations before importing them into Unity.

Recommended Mixamo download settings:

- Format: `FBX for Unity`
- Skin: `Without Skin`

## Blender Batch Processing

Install the visual Blender add-on when you do not want to run command-line tools:

1. Open Blender.
2. Go to `Edit > Preferences > Add-ons`.
3. Click `Install...`.
4. Select `mixamo_batch_processor_addon.py`.
5. Enable `Mixamo Batch Processor`.
6. Open the 3D View sidebar and use the `Mixamo` tab.

The add-on lets you select a NAS input folder and any output folder from Blender's UI.
The default action input folder is:

```text
Z:/游戏资源/美术资源/Action
```

## Blender Command-Line Processing

Run Blender in background mode and pass the source/output folders after `--`:

```powershell
blender --background --python "Assets/_Project/Tools/MixamoPipeline/Blender/batch_process_mixamo.py" -- --input "Z:/游戏资源/美术资源/Action" --output "Assets/_Project/Pipeline/Processed/Mixamo/Animations" --overwrite
```

Useful options:

- `--recursive`: process nested folders.
- `--overwrite`: replace existing processed FBX files.
- `--dry-run`: print planned outputs without exporting.
- Bone names are standardized by default: `mixamorig:Hips` becomes `Hips`, and action curve paths are updated.
- `--keep-bone-namespace`: opt out and keep downloaded bone names.
- `--keep-mesh`: keep mesh objects if a source file was downloaded with skin.

The tool writes `mixamo_batch_report.json` in the output folder.
