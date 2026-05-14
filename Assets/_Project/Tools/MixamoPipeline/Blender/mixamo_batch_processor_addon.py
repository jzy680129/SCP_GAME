bl_info = {
    "name": "Mixamo Batch Processor",
    "author": "Codex",
    "version": (1, 0, 0),
    "blender": (3, 6, 0),
    "location": "View3D > Sidebar > Mixamo",
    "description": "Batch-clean Mixamo FBX animations and export Unity-ready FBX files.",
    "category": "Import-Export",
}

import json
import re
import traceback
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Tuple

import bpy
from bpy.props import BoolProperty, CollectionProperty, IntProperty, StringProperty
from bpy.types import Operator, Panel, PropertyGroup, UIList


ANIMATION_EXTENSIONS = {".fbx", ".FBX"}
DEFAULT_ACTION_INPUT_DIR = "Z:/游戏资源/美术资源/Action"


def normalize_folder(value: str) -> Path:
    return Path(bpy.path.abspath(value))


def iter_fbx_files(input_dir: Path, recursive: bool) -> Iterable[Path]:
    pattern = "**/*" if recursive else "*"
    for path in input_dir.glob(pattern):
        if path.is_file() and path.suffix in ANIMATION_EXTENSIONS:
            yield path


def sanitize_name(value: str) -> str:
    value = value.strip().replace("@", "_")
    value = re.sub(r"[^A-Za-z0-9_]+", "_", value)
    value = re.sub(r"_+", "_", value)
    return value.strip("_") or "Animation"


def clip_name_from_path(path: Path, prefix: str) -> str:
    stem = path.stem
    if "@" in stem:
        stem = stem.split("@", 1)[1]

    name = sanitize_name(stem)
    if prefix and not name.startswith(prefix):
        name = prefix + name

    return name


def ensure_object_mode() -> None:
    if bpy.ops.object.mode_set.poll():
        bpy.ops.object.mode_set(mode="OBJECT")


def reset_scene() -> None:
    ensure_object_mode()
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


def rename_bones_without_namespace(
    armature: bpy.types.Object,
    action: Optional[bpy.types.Action],
) -> Dict[str, str]:
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


def ensure_animation_export_root(
    armature: bpy.types.Object,
    action_name: str,
    keep_mesh: bool,
) -> Optional[bpy.types.Object]:
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


def process_file(path: Path, settings: "MixamoBatchSettings") -> Dict[str, object]:
    output_dir = normalize_folder(settings.output_dir)
    action_name = clip_name_from_path(path, settings.prefix)
    output_path = output_dir / f"{action_name}.fbx"

    result: Dict[str, object] = {
        "source": str(path),
        "output": str(output_path),
        "action": action_name,
        "status": "pending",
    }

    if output_path.exists() and not settings.overwrite:
        result["status"] = "skipped"
        result["reason"] = "output exists"
        return result

    if settings.dry_run:
        result["status"] = "dry-run"
        return result

    reset_scene()
    bpy.ops.import_scene.fbx(filepath=str(path))

    armature = choose_armature()
    if armature is None:
        result["status"] = "failed"
        result["reason"] = "no armature found"
        return result

    remove_unneeded_objects(armature, settings.keep_mesh)
    armature.name = "Armature"
    armature.data.name = "Armature"

    action = find_or_create_action(armature)
    if action is None:
        result["status"] = "failed"
        result["reason"] = "no animation action found"
        return result

    frame_start, frame_end = normalize_action(action, action_name)
    renamed_bones: Dict[str, str] = {}
    if settings.strip_bone_namespace:
        renamed_bones = rename_bones_without_namespace(armature, action)

    export_root = ensure_animation_export_root(armature, action_name, settings.keep_mesh)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    select_export_objects(armature, settings.keep_mesh)
    export_fbx(output_path, settings.keep_mesh)

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


def write_report(settings: "MixamoBatchSettings", results: List[Dict[str, object]]) -> Path:
    output_dir = normalize_folder(settings.output_dir)
    report_path = Path(settings.report_path) if settings.report_path else output_dir / "mixamo_batch_report.json"
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(results, indent=2), encoding="utf-8")
    return report_path


class MixamoBatchItem(PropertyGroup):
    selected: BoolProperty(name="Selected", default=True)
    source_path: StringProperty(name="Source Path", default="")
    output_name: StringProperty(name="Output Name", default="")
    status: StringProperty(name="Status", default="Pending")


class MixamoBatchSettings(PropertyGroup):
    input_dir: StringProperty(name="Input Folder", subtype="DIR_PATH", default=DEFAULT_ACTION_INPUT_DIR)
    output_dir: StringProperty(name="Output Folder", subtype="DIR_PATH", default="")
    report_path: StringProperty(name="Report Path", subtype="FILE_PATH", default="")
    prefix: StringProperty(name="Prefix", default="MX_")
    recursive: BoolProperty(name="Recursive", default=False)
    overwrite: BoolProperty(name="Overwrite", default=False)
    dry_run: BoolProperty(name="Dry Run", default=True)
    keep_mesh: BoolProperty(name="Keep Mesh", default=False)
    strip_bone_namespace: BoolProperty(
        name="Standardize Bone Names",
        description="Rename bones like mixamorig:Hips to Hips and update action curve paths",
        default=True,
    )
    last_summary: StringProperty(name="Last Summary", default="")
    active_index: IntProperty(name="Active Index", default=0)
    items: CollectionProperty(type=MixamoBatchItem)


class MIXAMO_UL_batch_items(UIList):
    def draw_item(
        self,
        context,
        layout,
        data,
        item,
        icon,
        active_data,
        active_propname,
        index,
    ) -> None:
        row = layout.row(align=True)
        row.prop(item, "selected", text="")
        row.label(text=item.output_name or Path(item.source_path).name, icon="ACTION")
        row.label(text=item.status)


class MIXAMO_OT_scan(Operator):
    bl_idname = "mixamo_batch.scan"
    bl_label = "Scan"
    bl_description = "Scan the input folder for FBX files"

    def execute(self, context):
        settings = context.scene.mixamo_batch_settings
        input_dir = normalize_folder(settings.input_dir)

        settings.items.clear()
        if not input_dir.exists():
            settings.last_summary = f"Input folder does not exist: {input_dir}"
            self.report({"ERROR"}, settings.last_summary)
            return {"CANCELLED"}

        files = sorted(iter_fbx_files(input_dir, settings.recursive))
        for path in files:
            item = settings.items.add()
            item.selected = True
            item.source_path = str(path)
            item.output_name = f"{clip_name_from_path(path, settings.prefix)}.fbx"
            item.status = "Ready"

        settings.last_summary = f"Found {len(files)} FBX file(s)."
        self.report({"INFO"}, settings.last_summary)
        return {"FINISHED"}


class MIXAMO_OT_process_selected(Operator):
    bl_idname = "mixamo_batch.process_selected"
    bl_label = "Process Selected"
    bl_description = "Process selected FBX files. This resets the current Blender scene during processing."

    def invoke(self, context, event):
        return context.window_manager.invoke_confirm(self, event)

    def execute(self, context):
        settings = context.scene.mixamo_batch_settings
        if not settings.output_dir:
            self.report({"ERROR"}, "Output folder is required.")
            return {"CANCELLED"}

        selected_paths = [
            Path(item.source_path)
            for item in settings.items
            if item.selected and item.source_path
        ]
        if not selected_paths:
            self.report({"ERROR"}, "No selected FBX files.")
            return {"CANCELLED"}

        results: List[Dict[str, object]] = []
        context.window_manager.progress_begin(0, len(selected_paths))

        try:
            for index, path in enumerate(selected_paths, start=1):
                context.window_manager.progress_update(index)
                item = next((entry for entry in settings.items if entry.source_path == str(path)), None)
                if item is not None:
                    item.status = "Processing"

                try:
                    result = process_file(path, settings)
                except Exception as exc:  # noqa: BLE001 - batch tools should continue after one bad asset.
                    result = {
                        "source": str(path),
                        "status": "failed",
                        "reason": str(exc),
                        "traceback": traceback.format_exc(),
                    }

                results.append(result)
                if item is not None:
                    item.status = str(result["status"])
        finally:
            context.window_manager.progress_end()

        report_path = write_report(settings, results)
        exported = sum(1 for result in results if result["status"] == "exported")
        failed = sum(1 for result in results if result["status"] == "failed")
        skipped = sum(1 for result in results if result["status"] == "skipped")
        dry_run = sum(1 for result in results if result["status"] == "dry-run")

        settings.last_summary = (
            f"Exported: {exported}  Dry-run: {dry_run}  Skipped: {skipped}  "
            f"Failed: {failed}  Report: {report_path}"
        )
        self.report({"INFO"}, settings.last_summary)
        return {"FINISHED"}


class MIXAMO_OT_select_all(Operator):
    bl_idname = "mixamo_batch.select_all"
    bl_label = "Select All"

    selected: BoolProperty(default=True)

    def execute(self, context):
        settings = context.scene.mixamo_batch_settings
        for item in settings.items:
            item.selected = self.selected
        return {"FINISHED"}


class MIXAMO_PT_batch_panel(Panel):
    bl_label = "Mixamo Batch Processor"
    bl_idname = "MIXAMO_PT_batch_panel"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Mixamo"

    def draw(self, context):
        layout = self.layout
        settings = context.scene.mixamo_batch_settings

        layout.prop(settings, "input_dir")
        layout.prop(settings, "output_dir")
        layout.prop(settings, "report_path")

        box = layout.box()
        box.label(text="Options")
        box.prop(settings, "prefix")
        box.prop(settings, "recursive")
        box.prop(settings, "overwrite")
        box.prop(settings, "dry_run")
        box.prop(settings, "strip_bone_namespace")
        box.prop(settings, "keep_mesh")

        row = layout.row(align=True)
        row.operator("mixamo_batch.scan", icon="VIEWZOOM")
        row.operator("mixamo_batch.process_selected", icon="EXPORT")

        row = layout.row(align=True)
        op = row.operator("mixamo_batch.select_all", text="Select All")
        op.selected = True
        op = row.operator("mixamo_batch.select_all", text="Select None")
        op.selected = False

        layout.template_list(
            "MIXAMO_UL_batch_items",
            "",
            settings,
            "items",
            settings,
            "active_index",
            rows=8,
        )

        if settings.last_summary:
            layout.label(text=settings.last_summary)


classes = (
    MixamoBatchItem,
    MixamoBatchSettings,
    MIXAMO_UL_batch_items,
    MIXAMO_OT_scan,
    MIXAMO_OT_process_selected,
    MIXAMO_OT_select_all,
    MIXAMO_PT_batch_panel,
)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.Scene.mixamo_batch_settings = bpy.props.PointerProperty(type=MixamoBatchSettings)


def unregister():
    del bpy.types.Scene.mixamo_batch_settings
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)


if __name__ == "__main__":
    register()
