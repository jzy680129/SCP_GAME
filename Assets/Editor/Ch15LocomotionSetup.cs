using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Ch15LocomotionSetup
{
    private const string ControllerPath = "Assets/Res/Ch15_Locomotion.controller";
    private const string CharacterName = "Ch15_nonPBR";
    private const string IdleClipPath = "Assets/Res/Ch12_nonPBR@Breathing Idle.fbx";
    private const string WalkClipPath = "Assets/Res/Ch12_nonPBR@Walking.fbx";
    private const string RunClipPath = "Assets/Res/Ch12_nonPBR@Standard Run.fbx";
    private const string JumpClipPath = "Assets/Res/Ch12_nonPBR@Jump.fbx";
    private const string LeftStrafeClipPath = "Assets/Res/Ch12_nonPBR@Left Strafe.fbx";
    private const string RightStrafeClipPath = "Assets/Res/Ch12_nonPBR@Right Strafe.fbx";

    [MenuItem("Tools/Animation/Configure Ch15 Locomotion")]
    public static void Configure()
    {
        Configure(true);
    }

    private static void Configure(bool forceRebuild)
    {
        AnimatorController controller = EnsureController(forceRebuild);
        bool sceneChanged = AssignControllerAndRuntime(controller);
        sceneChanged |= AssignThirdPersonCamera();

        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(new[] { ControllerPath });
            AssetDatabase.SaveAssets();
        }

        if (sceneChanged)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }
    }

    private static AnimatorController EnsureController(bool forceRebuild)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        if (forceRebuild || !HasRequiredSetup(controller))
        {
            RebuildController(controller);
        }

        return controller;
    }

    private static bool HasRequiredSetup(AnimatorController controller)
    {
        if (controller.layers.Length == 0 || controller.layers[0].stateMachine == null)
        {
            return false;
        }

        bool hasParameters = new[]
        {
            "MoveX",
            "MoveY",
            "Speed",
            "IsRunning",
            "Jump",
            "TurnLeft",
            "TurnRight",
            "TurnLeft90",
            "TurnRight90"
        }.All(name => controller.parameters.Any(parameter => parameter.name == name));

        ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
        bool hasStates = states.Any(state => state.state.name == "Locomotion")
            && states.Any(state => state.state.name == "Jump")
            && states.Any(state => state.state.name == "LeftTurn")
            && states.Any(state => state.state.name == "RightTurn")
            && states.Any(state => state.state.name == "LeftTurn90")
            && states.Any(state => state.state.name == "RightTurn90");

        return hasParameters && hasStates && HasRequiredMotions(states);
    }

    private static bool HasRequiredMotions(ChildAnimatorState[] states)
    {
        AnimatorState locomotion = states
            .Select(state => state.state)
            .FirstOrDefault(state => state != null && state.name == "Locomotion");

        if (!(locomotion != null && locomotion.motion is BlendTree blendTree && blendTree.children.Length > 0))
        {
            return false;
        }

        return states
            .Where(state => state.state != null && state.state.name != "Locomotion")
            .All(state => state.state.motion != null);
    }

    private static void RebuildController(AnimatorController controller)
    {
        RemoveGeneratedSubAssets(controller);
        AnimatorStateMachine stateMachine = ResetBaseLayer(controller);

        foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
        {
            controller.RemoveParameter(parameter);
        }

        AddParameter(controller, "MoveX", AnimatorControllerParameterType.Float);
        AddParameter(controller, "MoveY", AnimatorControllerParameterType.Float);
        AddParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        AddParameter(controller, "IsRunning", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "Jump", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "TurnLeft", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "TurnRight", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "TurnLeft90", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "TurnRight90", AnimatorControllerParameterType.Trigger);

        AnimatorState locomotion = stateMachine.AddState("Locomotion", new Vector3(260f, 80f, 0f));
        locomotion.motion = CreateLocomotionBlendTree(controller);
        stateMachine.defaultState = locomotion;

        AnimatorState jump = AddClipState(stateMachine, "Jump", JumpClipPath, new Vector3(540f, -80f, 0f));
        AnimatorState leftTurn = AddClipState(stateMachine, "LeftTurn", LeftStrafeClipPath, new Vector3(540f, 80f, 0f));
        AnimatorState rightTurn = AddClipState(stateMachine, "RightTurn", RightStrafeClipPath, new Vector3(540f, 200f, 0f));
        AnimatorState leftTurn90 = AddClipState(stateMachine, "LeftTurn90", LeftStrafeClipPath, new Vector3(820f, 80f, 0f));
        AnimatorState rightTurn90 = AddClipState(stateMachine, "RightTurn90", RightStrafeClipPath, new Vector3(820f, 200f, 0f));

        AddTriggerTransition(stateMachine, locomotion, jump, "Jump", 0.05f);
        AddTriggerTransition(stateMachine, locomotion, leftTurn, "TurnLeft", 0.05f);
        AddTriggerTransition(stateMachine, locomotion, rightTurn, "TurnRight", 0.05f);
        AddTriggerTransition(stateMachine, locomotion, leftTurn90, "TurnLeft90", 0.05f);
        AddTriggerTransition(stateMachine, locomotion, rightTurn90, "TurnRight90", 0.05f);

        AddExitTransition(jump, locomotion, 0.12f);
        AddExitTransition(leftTurn, locomotion, 0.12f);
        AddExitTransition(rightTurn, locomotion, 0.12f);
        AddExitTransition(leftTurn90, locomotion, 0.12f);
        AddExitTransition(rightTurn90, locomotion, 0.12f);

        EditorUtility.SetDirty(controller);
    }

    private static void RemoveGeneratedSubAssets(AnimatorController controller)
    {
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(ControllerPath)
            .Where(asset => asset != controller)
            .ToArray();

        foreach (Object subAsset in subAssets)
        {
            Object.DestroyImmediate(subAsset, true);
        }
    }

    private static AnimatorStateMachine ResetBaseLayer(AnimatorController controller)
    {
        AnimatorStateMachine stateMachine = new AnimatorStateMachine
        {
            name = "Base Layer",
            hideFlags = HideFlags.HideInHierarchy
        };

        AssetDatabase.AddObjectToAsset(stateMachine, controller);

        controller.layers = new[]
        {
            new AnimatorControllerLayer
            {
                name = "Base Layer",
                defaultWeight = 1f,
                stateMachine = stateMachine
            }
        };

        return stateMachine;
    }

    private static BlendTree CreateLocomotionBlendTree(AnimatorController controller)
    {
        BlendTree blendTree = new BlendTree
        {
            name = "Locomotion Blend Tree",
            blendType = BlendTreeType.FreeformCartesian2D,
            blendParameter = "MoveX",
            blendParameterY = "MoveY",
            useAutomaticThresholds = false
        };

        AssetDatabase.AddObjectToAsset(blendTree, controller);

        AddBlendChild(blendTree, IdleClipPath, Vector2.zero);
        AddBlendChild(blendTree, WalkClipPath, new Vector2(0f, 1f));
        AddBlendChild(blendTree, RunClipPath, new Vector2(0f, 2f));
        AddBlendChild(blendTree, LeftStrafeClipPath, new Vector2(-1f, 0f));
        AddBlendChild(blendTree, RightStrafeClipPath, new Vector2(1f, 0f));

        return blendTree;
    }

    private static void AddBlendChild(BlendTree blendTree, string clipPath, Vector2 position)
    {
        AnimationClip clip = LoadAnimationClip(clipPath);
        if (clip != null)
        {
            blendTree.AddChild(clip, position);
            return;
        }

        Debug.LogWarning($"Animation clip not found for blend tree: {clipPath}");
    }

    private static AnimatorState AddClipState(
        AnimatorStateMachine stateMachine,
        string stateName,
        string clipPath,
        Vector3 position)
    {
        AnimatorState state = stateMachine.AddState(stateName, position);
        state.motion = LoadAnimationClip(clipPath);
        if (state.motion == null)
        {
            Debug.LogWarning($"Animation clip not found for state '{stateName}': {clipPath}");
        }

        return state;
    }

    private static AnimationClip LoadAnimationClip(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal));
    }

    private static void AddParameter(
        AnimatorController controller,
        string name,
        AnimatorControllerParameterType type)
    {
        controller.AddParameter(name, type);
    }

    private static void AddTriggerTransition(
        AnimatorStateMachine stateMachine,
        AnimatorState from,
        AnimatorState to,
        string triggerName,
        float duration)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);

        AnimatorStateTransition anyStateTransition = stateMachine.AddAnyStateTransition(to);
        anyStateTransition.hasExitTime = false;
        anyStateTransition.duration = duration;
        anyStateTransition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
    }

    private static void AddExitTransition(AnimatorState from, AnimatorState to, float duration)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = 0.9f;
        transition.duration = duration;
    }

    private static bool AssignControllerAndRuntime(RuntimeAnimatorController controller)
    {
        GameObject character = GameObject.Find(CharacterName);
        if (character == null)
        {
            return false;
        }

        bool changed = false;
        Animator animator = character.GetComponent<Animator>();
        if (animator == null)
        {
            animator = character.AddComponent<Animator>();
            changed = true;
        }

        if (animator.runtimeAnimatorController != controller)
        {
            animator.runtimeAnimatorController = controller;
            changed = true;
        }

        CharacterController characterController = character.GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = character.AddComponent<CharacterController>();
            changed = true;
        }

        changed |= ConfigureCharacterController(character, characterController);

        if (character.GetComponent<CharacterActionStateMachine>() == null)
        {
            character.AddComponent<CharacterActionStateMachine>();
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(character);
        }

        return changed;
    }

    private static bool AssignThirdPersonCamera()
    {
        GameObject character = GameObject.Find(CharacterName);
        if (character == null)
        {
            return false;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject existingCamera = GameObject.Find("Main Camera");
            if (existingCamera == null)
            {
                existingCamera = new GameObject("Main Camera");
            }

            if (!existingCamera.CompareTag("MainCamera"))
            {
                existingCamera.tag = "MainCamera";
            }

            mainCamera = existingCamera.GetComponent<Camera>();
            if (mainCamera == null)
            {
                mainCamera = existingCamera.AddComponent<Camera>();
            }

            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                existingCamera.AddComponent<AudioListener>();
            }
        }

        bool changed = false;
        GtaStyleThirdPersonCamera followCamera = mainCamera.GetComponent<GtaStyleThirdPersonCamera>();
        if (followCamera == null)
        {
            followCamera = mainCamera.gameObject.AddComponent<GtaStyleThirdPersonCamera>();
            changed = true;
        }

        if (followCamera.Target != character.transform)
        {
            followCamera.SetTarget(character.transform);
            changed = true;
        }
        else
        {
            followCamera.SnapToTarget();
        }

        if (!Mathf.Approximately(mainCamera.fieldOfView, 60f))
        {
            mainCamera.fieldOfView = 60f;
            changed = true;
        }

        CharacterActionStateMachine actionStateMachine = character.GetComponent<CharacterActionStateMachine>();
        if (actionStateMachine != null)
        {
            SerializedObject serializedStateMachine = new SerializedObject(actionStateMachine);
            changed |= SetSerializedBool(serializedStateMachine, "moveCharacterController", true);
            changed |= SetSerializedBool(serializedStateMachine, "useCameraRelativeMovement", true);
            changed |= SetSerializedBool(serializedStateMachine, "useLocalLocomotionBlend", true);
            changed |= SetSerializedBool(serializedStateMachine, "rotateTowardMoveDirection", true);
            changed |= SetSerializedFloat(serializedStateMachine, "turnSpeed", 540f);
            changed |= SetSerializedObject(serializedStateMachine, "cameraTransform", mainCamera.transform);
            serializedStateMachine.ApplyModifiedProperties();
            actionStateMachine.SetCameraTransform(mainCamera.transform);
        }

        if (changed)
        {
            EditorUtility.SetDirty(mainCamera);
            EditorUtility.SetDirty(mainCamera.gameObject);
            EditorUtility.SetDirty(character);
        }

        return changed;
    }

    private static bool ConfigureCharacterController(
        GameObject character,
        CharacterController characterController)
    {
        Bounds bounds = CalculateRenderableBounds(character);
        float height = Mathf.Max(1f, bounds.size.y);
        float horizontalSize = Mathf.Min(bounds.size.x, bounds.size.z);
        float radius = Mathf.Clamp(horizontalSize * 0.35f, 0.2f, height * 0.45f);
        Vector3 localCenter = character.transform.InverseTransformPoint(bounds.center);
        localCenter.x = 0f;
        localCenter.z = 0f;

        bool changed = false;
        changed |= SetCharacterControllerFloat(characterController, "height", height);
        changed |= SetCharacterControllerFloat(characterController, "radius", radius);
        changed |= SetCharacterControllerFloat(characterController, "stepOffset", Mathf.Min(0.35f, height * 0.2f));
        changed |= SetCharacterControllerFloat(characterController, "slopeLimit", 50f);
        changed |= SetCharacterControllerFloat(characterController, "skinWidth", 0.04f);

        if ((characterController.center - localCenter).sqrMagnitude > 0.0001f)
        {
            characterController.center = localCenter;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(characterController);
        }

        return changed;
    }

    private static Bounds CalculateRenderableBounds(GameObject character)
    {
        Renderer[] renderers = character.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(character.transform.position + Vector3.up, new Vector3(0.7f, 2f, 0.7f));
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static bool SetCharacterControllerFloat(
        CharacterController characterController,
        string propertyName,
        float value)
    {
        float currentValue = propertyName switch
        {
            "height" => characterController.height,
            "radius" => characterController.radius,
            "stepOffset" => characterController.stepOffset,
            "slopeLimit" => characterController.slopeLimit,
            "skinWidth" => characterController.skinWidth,
            _ => value
        };

        if (Mathf.Approximately(currentValue, value))
        {
            return false;
        }

        switch (propertyName)
        {
            case "height":
                characterController.height = value;
                break;
            case "radius":
                characterController.radius = value;
                break;
            case "stepOffset":
                characterController.stepOffset = value;
                break;
            case "slopeLimit":
                characterController.slopeLimit = value;
                break;
            case "skinWidth":
                characterController.skinWidth = value;
                break;
        }

        return true;
    }

    private static bool SetSerializedBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.boolValue == value)
        {
            return false;
        }

        property.boolValue = value;
        return true;
    }

    private static bool SetSerializedFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || Mathf.Approximately(property.floatValue, value))
        {
            return false;
        }

        property.floatValue = value;
        return true;
    }

    private static bool SetSerializedObject(
        SerializedObject serializedObject,
        string propertyName,
        Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == value)
        {
            return false;
        }

        property.objectReferenceValue = value;
        return true;
    }
}
