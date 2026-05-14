#!/usr/bin/env python3
"""
Batch-clean Mixamo FBX animation files in Blender and export Unity-ready FBX files.

Run with Blender, not normal Python:

blender --background --python batch_process_mixamo.py -- --input "Assets/Raw/Mixamo/Animations" --output "Assets/Processed/Mixamo/Animations"

Bone namespaces are stripped by default: mixamorig:Hips -> Hips.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import traceback
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Tuple

import bpy


ANIMATION_EXTENSIONS = {".fbx", ".FBX"}
ACTION_NAME_ALIASES = {
    "Jump": "Running_Jump",
}


def script_args() -> List[str]:
    if "--" not in sys.argv:
        return []

    return sys.argv[sys.argv.index("--") + 1 :]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Clean a folder of Mixamo FBX animation files and export processed FBX files."
    )
    parser.add_argument("--input", required=True, help="Folder containing raw Mixamo FBX files.")
    parser.add_argument("--output", required=True, help="Folder for processed FBX files.")
    parser.add_argument("--prefix", default="MX_", help="Prefix for exported action and file names.")
    parser.add_argument("--recursive", action="store_true", help="Process FBX files recursively.")
    parser.add_argument("--overwrite", action="store_true", help="Overwrite existing output files.")
    parser.add_argument("--keep-mesh", action="store_true", help="Keep mesh objects if the source FBX contains skin.")
    parser.set_defaults(strip_bone_namespace=True)
    parser.add_argument(
        "--strip-bone-namespace",
        action="store_true",
        help="Rename bones like mixamorig:Hips to Hips and update action curve paths. Enabled by default.",
    )
    parser.add_argument(
        "--keep-bone-namespace",
        dest="strip_bone_namespace",
        action="store_false",
        help="Keep source bone names exactly as downloaded.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Inspect files and print the planned output without exporting.",
    )
    parser.add_argument(
        "--report",
        default="",
        help="Optional JSON report path. Defaults to <output>/mixamo_batch_report.json.",
    )
    return parser.parse_args(script_args())


def iter_fbx_files(input_dir: Path, recursive: bool) -> Iterable[Path]:
    pattern = "**/*" if recursive else "*"
    for path in input_dir.glob(pattern):
        if path.is_file() and path.suffix in ANIMATION_EXTENSIONS:
            yield path


def sanitize_name(value: str) -> str:
    value = value.strip()
    value = value.replace("@", "_")
    value = re.sub(r"[^A-Za-z0-9_]+", "_", value)
    value = re.sub(r"_+", "_", value)
    return value.strip("_") or "Animation"


def clip_name_from_path(path: Path, prefix: str) -> str:
    stem = path.stem
    if "@" in stem:
        stem = stem.split("@", 1)[1]

    name = sanitize_name(stem)
    name = ACTION_NAME_ALIASES.get(name, name)
    if prefix and not name.startswith(prefix):
        name = prefix + name

    return name


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    purge_unused_data()


def purge_unused_data() -> None:
    for collection in (
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.textures,
        bpy.data.images,
        bpy.data.armatures,
        bpy.data.actions,
    ):
        for data_block in list(collection):
            if data_block.users == 0:
                collection.remove(data_block)


def import_fbx(path: Path) -> None:
    bpy.ops.import_scene.fbx(filepath=str(path))


def choose_armature() -> Optional[bpy.types.Object]:
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if not armatures:
        return None

    animated = [
        obj
        for obj in armatures
        if obj.animation_data is not None and obj.animation_data.action is not None
    ]
    return animated[0] if animated else armatures[0]


def remove_unneeded_objects(armature: bpy.types.Object, keep_mesh: bool) -> None:
    for obj in list(bpy.context.scene.objects):
        if obj == armature:
            continue

        if keep_mesh and obj.type == "MESH":
            continue

        bpy.data.objects.remove(obj, do_unlink=True)


def find_or_create_action(armature: bpy.types.Object) -> Optional[bpy.types.Action]:
    if armature.animation_data is not None and armature.animation_data.action is not None:
        return armature.animation_data.action

    actions = list(bpy.data.actions)
    if actions:
        if armature.animation_data is None:
            armature.animation_data_create()
        armature.animation_data.action = actions[0]
        return actions[0]

    return None


def normalize_action(action: bpy.types.Action, action_name: str) -> Tuple[int, int]:
    action.name = action_name
    bpy.context.scene.name = action_name
    start, end = action.frame_range
    frame_start = int(round(start))
    frame_end = int(round(end))
    bpy.context.scene.frame_start = frame_start
    bpy.context.scene.frame_end = frame_end
    return frame_start, frame_end


def strip_namespace(name: str) -> str:
    return name.split(":", 1)[1] if ":" in name else name


def rename_bones_without_namespace(armature: bpy.types.Object, action: Optional[bpy.types.Action]) -> Dict[str, str]:
    old_to_new: Dict[str, str] = {}
    used_names = {bone.name for bone in armature.data.bones if ":" not in bone.name}

    for bone in armature.data.bones:
        clean_name = strip_namespace(bone.name)
        if clean_name == bone.name or clean_name in used_names:
            continue

        old_name = bone.name
        bone.name = clean_name
        old_to_new[old_name] = clean_name
        used_names.add(clean_name)

    if action is not None and old_to_new:
        update_action_bone_paths(action, old_to_new)

    if old_to_new:
        update_mesh_vertex_group_names(old_to_new)

    return old_to_new


def update_mesh_vertex_group_names(old_to_new: Dict[str, str]) -> None:
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue

        for vertex_group in obj.vertex_groups:
            if vertex_group.name in old_to_new:
                vertex_group.name = old_to_new[vertex_group.name]


def iter_action_fcurves(action: bpy.types.Action):
    if hasattr(action, "fcurves"):
        yield from action.fcurves
        return

    for layer in getattr(action, "layers", []):
        for strip in getattr(layer, "strips", []):
            for channelbag in getattr(strip, "channelbags", []):
                yield from getattr(channelbag, "fcurves", [])


def update_action_bone_paths(action: bpy.types.Action, old_to_new: Dict[str, str]) -> None:
    for fcurve in iter_action_fcurves(action):
        data_path = fcurve.data_path
        for old_name, new_name in old_to_new.items():
            data_path = data_path.replace(f'pose.bones["{old_name}"]', f'pose.bones["{new_name}"]')
        fcurve.data_path = data_path


def select_export_objects(armature: bpy.types.Object, keep_mesh: bool) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    if not keep_mesh and armature.parent is not None:
        armature.parent.select_set(True)

    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature

    if keep_mesh:
        for obj in bpy.context.scene.objects:
            if obj.type == "MESH":
                obj.select_set(True)


def export_fbx(path: Path, keep_mesh: bool) -> None:
    object_types = {"ARMATURE", "MESH"} if keep_mesh else {"EMPTY", "ARMATURE"}
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types=object_types,
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        armature_nodetype="ROOT",
    )


def ensure_animation_export_root(armature: bpy.types.Object, action_name: str, keep_mesh: bool) -> Optional[bpy.types.Object]:
    if keep_mesh:
        return None

    if armature.parent is not None and armature.parent.type == "EMPTY":
        armature.parent.name = action_name
        return armature.parent

    root = bpy.data.objects.new(action_name, None)
    bpy.context.collection.objects.link(root)
    armature.parent = root
    armature.matrix_parent_inverse = root.matrix_world.inverted()
    return root


def process_file(path: Path, output_dir: Path, args: argparse.Namespace) -> Dict[str, object]:
    action_name = clip_name_from_path(path, args.prefix)
    output_path = output_dir / f"{action_name}.fbx"

    result: Dict[str, object] = {
        "source": str(path),
        "output": str(output_path),
        "action": action_name,
        "status": "pending",
    }

    if output_path.exists() and not args.overwrite:
        result["status"] = "skipped"
        result["reason"] = "output exists"
        return result

    if args.dry_run:
        result["status"] = "dry-run"
        return result

    reset_scene()
    import_fbx(path)

    armature = choose_armature()
    if armature is None:
        result["status"] = "failed"
        result["reason"] = "no armature found"
        return result

    remove_unneeded_objects(armature, args.keep_mesh)
    armature.name = "Armature"
    armature.data.name = "Armature"

    action = find_or_create_action(armature)
    if action is None:
        result["status"] = "failed"
        result["reason"] = "no animation action found"
        return result

    frame_start, frame_end = normalize_action(action, action_name)
    renamed_bones: Dict[str, str] = {}
    if args.strip_bone_namespace:
        renamed_bones = rename_bones_without_namespace(armature, action)

    export_root = ensure_animation_export_root(armature, action_name, args.keep_mesh)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    select_export_objects(armature, args.keep_mesh)
    export_fbx(output_path, args.keep_mesh)

    result.update(
        {
            "status": "exported",
            "frame_start": frame_start,
            "frame_end": frame_end,
            "renamed_bone_count": len(renamed_bones),
            "export_root": export_root.name if export_root else "",
        }
    )
    return result


def write_report(report_path: Path, results: List[Dict[str, object]]) -> None:
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(results, indent=2), encoding="utf-8")


def main() -> int:
    args = parse_args()
    input_dir = Path(args.input).resolve()
    output_dir = Path(args.output).resolve()
    report_path = Path(args.report).resolve() if args.report else output_dir / "mixamo_batch_report.json"

    if not input_dir.exists():
        print(f"Input folder does not exist: {input_dir}", file=sys.stderr)
        return 2

    files = sorted(iter_fbx_files(input_dir, args.recursive))
    if not files:
        print(f"No FBX files found in: {input_dir}")
        return 0

    results: List[Dict[str, object]] = []
    for index, path in enumerate(files, start=1):
        print(f"[{index}/{len(files)}] {path.name}")
        try:
            result = process_file(path, output_dir, args)
        except Exception as exc:  # noqa: BLE001 - batch tools should continue after one bad asset.
            result = {
                "source": str(path),
                "status": "failed",
                "reason": str(exc),
                "traceback": traceback.format_exc(),
            }

        results.append(result)
        print(f"  {result['status']}: {result.get('output', result.get('reason', ''))}")

    write_report(report_path, results)

    exported = sum(1 for result in results if result["status"] == "exported")
    failed = sum(1 for result in results if result["status"] == "failed")
    skipped = sum(1 for result in results if result["status"] == "skipped")

    print("")
    print(f"Exported: {exported}")
    print(f"Skipped: {skipped}")
    print(f"Failed: {failed}")
    print(f"Report: {report_path}")

    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
