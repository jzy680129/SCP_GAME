from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


def read_tool(name: str) -> str:
    return (ROOT / name).read_text(encoding="utf-8")


class MixamoPipelineDefaultTests(unittest.TestCase):
    def test_command_line_processor_strips_bone_namespace_by_default(self):
        source = read_tool("batch_process_mixamo.py")

        self.assertIn("parser.set_defaults(strip_bone_namespace=True)", source)
        self.assertIn('"--keep-bone-namespace"', source)

    def test_blender_addon_strips_bone_namespace_by_default(self):
        source = read_tool("mixamo_batch_processor_addon.py")

        self.assertIn(
            'name="Standardize Bone Names"',
            source,
        )
        self.assertIn(
            "default=True",
            source,
        )

    def test_blender_addon_defaults_to_nas_action_folder(self):
        source = read_tool("mixamo_batch_processor_addon.py")

        self.assertIn('DEFAULT_ACTION_INPUT_DIR = "Z:/游戏资源/美术资源/Action"', source)

    def test_processors_update_mesh_vertex_groups_when_bones_are_renamed(self):
        for tool_name in ("batch_process_mixamo.py", "mixamo_batch_processor_addon.py"):
            source = read_tool(tool_name)

            self.assertIn("def update_mesh_vertex_group_names", source)
            self.assertIn("vertex_group.name = old_to_new[vertex_group.name]", source)

    def test_animation_exports_preserve_armature_parent_node(self):
        for tool_name in ("batch_process_mixamo.py", "mixamo_batch_processor_addon.py"):
            source = read_tool(tool_name)

            self.assertIn("def ensure_animation_export_root", source)
            self.assertIn('object_types = {"ARMATURE", "MESH"} if keep_mesh else {"EMPTY", "ARMATURE"}', source)
            self.assertIn('armature_nodetype="ROOT"', source)


if __name__ == "__main__":
    unittest.main()
