using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class HumanoidImportBatchWindow : EditorWindow
{
    private readonly List<ModelRow> rows = new List<ModelRow>();
    private Vector2 scroll;
    private bool onlyShowIssues;
    private bool setImportAnimation = true;
    private bool forceCreateFromThisModel = true;

    [MenuItem("Tools/Animation/Humanoid Import Checker")]
    public static void Open()
    {
        GetWindow<HumanoidImportBatchWindow>("Humanoid Imports");
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawOptions();
        DrawSummary();
        DrawRows();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Scan Selection", EditorStyles.toolbarButton, GUILayout.Width(110f)))
            {
                ScanSelection();
            }

            if (GUILayout.Button("Scan Project Art", EditorStyles.toolbarButton, GUILayout.Width(110f)))
            {
                ScanFolders(new[] { "Assets/_Project/Art" });
            }

            if (GUILayout.Button("Scan All Models", EditorStyles.toolbarButton, GUILayout.Width(110f)))
            {
                ScanFolders(new[] { "Assets" });
            }

            GUILayout.FlexibleSpace();

            onlyShowIssues = GUILayout.Toggle(onlyShowIssues, "Issues Only", EditorStyles.toolbarButton, GUILayout.Width(90f));
        }
    }

    private void DrawOptions()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Batch Fix", EditorStyles.boldLabel);
        setImportAnimation = EditorGUILayout.ToggleLeft("Enable Import Animation", setImportAnimation);
        forceCreateFromThisModel = EditorGUILayout.ToggleLeft("Use Create From This Model instead of Copy From Other Avatar", forceCreateFromThisModel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Select All Issues", GUILayout.Width(130f)))
            {
                foreach (ModelRow row in rows)
                {
                    row.Selected = row.HasIssue;
                }
            }

            if (GUILayout.Button("Select All", GUILayout.Width(90f)))
            {
                foreach (ModelRow row in rows)
                {
                    row.Selected = true;
                }
            }

            if (GUILayout.Button("Select None", GUILayout.Width(90f)))
            {
                foreach (ModelRow row in rows)
                {
                    row.Selected = false;
                }
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(!rows.Any(row => row.Selected)))
            {
                if (GUILayout.Button("Apply Humanoid Settings", GUILayout.Width(180f)))
                {
                    ApplySelected();
                }
            }
        }
    }

    private void DrawSummary()
    {
        int issueCount = rows.Count(row => row.HasIssue);
        int validHumanoidCount = rows.Count(row => row.AvatarValid && row.AvatarHuman);
        int selectedCount = rows.Count(row => row.Selected);

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            $"Models: {rows.Count}    Valid Humanoid Avatars: {validHumanoidCount}    Issues: {issueCount}    Selected: {selectedCount}",
            issueCount > 0 ? MessageType.Warning : MessageType.Info);
    }

    private void DrawRows()
    {
        EditorGUILayout.Space(4f);

        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            GUILayout.Label("", GUILayout.Width(24f));
            GUILayout.Label("Status", EditorStyles.boldLabel, GUILayout.Width(90f));
            GUILayout.Label("Rig", EditorStyles.boldLabel, GUILayout.Width(170f));
            GUILayout.Label("Clips", EditorStyles.boldLabel, GUILayout.Width(45f));
            GUILayout.Label("Asset", EditorStyles.boldLabel);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        foreach (ModelRow row in rows.Where(ShouldShowRow))
        {
            DrawRow(row);
        }

        EditorGUILayout.EndScrollView();
    }

    private bool ShouldShowRow(ModelRow row)
    {
        return !onlyShowIssues || row.HasIssue;
    }

    private void DrawRow(ModelRow row)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                row.Selected = EditorGUILayout.Toggle(row.Selected, GUILayout.Width(24f));
                GUILayout.Label(row.StatusLabel, StatusStyle(row), GUILayout.Width(90f));
                GUILayout.Label(row.RigLabel, GUILayout.Width(170f));
                GUILayout.Label(row.ClipCount.ToString(), GUILayout.Width(45f));
                GUILayout.Label(row.Path);

                if (GUILayout.Button("Ping", GUILayout.Width(48f)))
                {
                    Object asset = AssetDatabase.LoadMainAssetAtPath(row.Path);
                    EditorGUIUtility.PingObject(asset);
                    Selection.activeObject = asset;
                }
            }

            if (!string.IsNullOrEmpty(row.Details))
            {
                EditorGUILayout.LabelField(row.Details, EditorStyles.wordWrappedMiniLabel);
            }
        }
    }

    private static GUIStyle StatusStyle(ModelRow row)
    {
        if (!row.HasIssue)
        {
            return EditorStyles.label;
        }

        GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = new Color(0.85f, 0.45f, 0.1f);
        return style;
    }

    private void ScanSelection()
    {
        string[] paths = Selection.objects
            .Select(AssetDatabase.GetAssetPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .ToArray();

        if (paths.Length == 0)
        {
            EditorUtility.DisplayDialog("Humanoid Import Checker", "Select one or more FBX/model assets or folders first.", "OK");
            return;
        }

        ScanPaths(paths);
    }

    private void ScanFolders(string[] folders)
    {
        string[] existingFolders = folders.Where(AssetDatabase.IsValidFolder).ToArray();
        if (existingFolders.Length == 0)
        {
            rows.Clear();
            Repaint();
            return;
        }

        string[] paths = AssetDatabase.FindAssets("t:Model", existingFolders)
            .Select(AssetDatabase.GUIDToAssetPath)
            .ToArray();

        ScanPaths(paths);
    }

    private void ScanPaths(IEnumerable<string> paths)
    {
        rows.Clear();

        foreach (string path in ExpandModelPaths(paths).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                rows.Add(Analyze(path, importer));
            }
        }

        Repaint();
    }

    private static IEnumerable<string> ExpandModelPaths(IEnumerable<string> paths)
    {
        HashSet<string> results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string path in paths)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { path }))
                {
                    results.Add(AssetDatabase.GUIDToAssetPath(guid));
                }

                continue;
            }

            if (AssetImporter.GetAtPath(path) is ModelImporter)
            {
                results.Add(path);
            }
        }

        return results;
    }

    private static ModelRow Analyze(string path, ModelImporter importer)
    {
        List<string> issues = new List<string>();
        List<string> details = new List<string>();
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        Avatar avatar = subAssets.OfType<Avatar>().FirstOrDefault();
        AnimationClip[] clips = subAssets
            .OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
            .ToArray();

        bool isHumanRig = importer.animationType == ModelImporterAnimationType.Human;
        bool avatarHuman = avatar != null && avatar.isHuman;
        bool avatarValid = avatar != null && avatar.isValid;

        if (!isHumanRig)
        {
            issues.Add("Rig is not Humanoid");
        }

        if (isHumanRig && importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther)
        {
            issues.Add("Uses Copy From Other Avatar");
        }

        if (importer.importAnimation == false && clips.Length > 0)
        {
            issues.Add("Import Animation is disabled");
        }

        if (isHumanRig && avatar == null)
        {
            issues.Add("No Avatar sub-asset");
        }
        else if (isHumanRig && !avatarValid)
        {
            issues.Add("Avatar is invalid");
        }
        else if (isHumanRig && !avatarHuman)
        {
            issues.Add("Avatar is not human");
        }

        GameObject modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (modelRoot != null)
        {
            string hipsName = FindLikelyHipsName(modelRoot.transform);
            if (string.IsNullOrEmpty(hipsName))
            {
                details.Add("Common hips transform was not found by name. Humanoid auto-mapping may still work, but check Configure if Avatar stays invalid.");
            }
            else
            {
                details.Add("Likely hips: " + hipsName);
            }

            if (LooksAnimationOnly(path, modelRoot, clips.Length))
            {
                details.Add("Looks like an animation FBX. Use its AnimationClip in the Animator Controller, not the FBX prefab itself.");
            }
        }

        string avatarLabel = avatar == null ? "No Avatar" : avatarValid && avatarHuman ? "Valid Avatar" : "Invalid Avatar";
        string rigLabel = $"{importer.animationType} / {importer.avatarSetup}";

        return new ModelRow
        {
            Path = path,
            Selected = issues.Count > 0,
            AnimationType = importer.animationType,
            AvatarSetup = importer.avatarSetup,
            ImportAnimation = importer.importAnimation,
            ClipCount = clips.Length,
            AvatarHuman = avatarHuman,
            AvatarValid = avatarValid,
            HasIssue = issues.Count > 0,
            StatusLabel = issues.Count > 0 ? "Issue" : "OK",
            RigLabel = rigLabel,
            Details = string.Join("  |  ", new[] { avatarLabel }.Concat(issues).Concat(details))
        };
    }

    private static string FindLikelyHipsName(Transform root)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            string name = transform.name;
            if (name.Equals("Hips", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(":Hips", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Pelvis", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(":Pelvis", StringComparison.OrdinalIgnoreCase)
                || name.IndexOf("Bip001 Pelvis", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return name;
            }
        }

        return string.Empty;
    }

    private static bool LooksAnimationOnly(string path, GameObject modelRoot, int clipCount)
    {
        if (clipCount == 0)
        {
            return false;
        }

        if (Path.GetFileNameWithoutExtension(path).Contains("@"))
        {
            return true;
        }

        bool hasRenderer = modelRoot.GetComponentsInChildren<Renderer>(true).Length > 0;
        return !hasRenderer;
    }

    private void ApplySelected()
    {
        ModelRow[] selectedRows = rows.Where(row => row.Selected).ToArray();
        if (selectedRows.Length == 0)
        {
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Apply Humanoid Settings",
                $"This will reimport {selectedRows.Length} selected model asset(s). Continue?",
                "Apply",
                "Cancel"))
        {
            return;
        }

        try
        {
            for (int i = 0; i < selectedRows.Length; i++)
            {
                ModelRow row = selectedRows[i];
                EditorUtility.DisplayProgressBar("Applying Humanoid Settings", row.Path, (float)i / selectedRows.Length);

                ModelImporter importer = AssetImporter.GetAtPath(row.Path) as ModelImporter;
                if (importer == null)
                {
                    continue;
                }

                importer.animationType = ModelImporterAnimationType.Human;

                if (forceCreateFromThisModel)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.sourceAvatar = null;
                }

                if (setImportAnimation)
                {
                    importer.importAnimation = true;
                }

                importer.SaveAndReimport();
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        ScanPaths(selectedRows.Select(row => row.Path));
        AssetDatabase.SaveAssets();
    }

    private sealed class ModelRow
    {
        public string Path;
        public bool Selected;
        public ModelImporterAnimationType AnimationType;
        public ModelImporterAvatarSetup AvatarSetup;
        public bool ImportAnimation;
        public int ClipCount;
        public bool AvatarHuman;
        public bool AvatarValid;
        public bool HasIssue;
        public string StatusLabel;
        public string RigLabel;
        public string Details;
    }
}
