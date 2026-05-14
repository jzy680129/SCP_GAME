using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

public static class MixamoThirdPersonSetup
{
    private const string ControllerPath = "Assets/Processed/Mixamo/ThirdPerson/MX_ThirdPerson.controller";
    private const string CharacterPrefabPath = "Assets/Processed/Mixamo/Characters_MCP/CHR_Ch36_nonPBR.fbx";
    private const string CameraRigName = "PlayerCameraRig";
    private const string CameraFollowPointName = "FollowPoint";
    private const string CinemachineCameraName = "CM_PlayerThirdPerson";
    private const string HudObjectName = "GameHUD";
    private const string HudUxmlPath = "Assets/UI/HUD/ThirdPersonHud.uxml";
    private const string HudPanelSettingsPath = "Assets/UI/HUD/GameHudPanelSettings.asset";
    private const string CinematicVolumeName = "CinematicPostProcessVolume";
    private const string CinematicVolumeProfilePath = "Assets/Settings/CinematicPostProcessProfile.asset";

    private static readonly string[] IdleClipPaths =
    {
        "Assets/Processed/Mixamo/Animations_MCP/MX_Breathing_Idle.fbx",
        "Assets/Processed/Mixamo/Animations_MCP/MX_Breathing_Idle_1.fbx"
    };

    private static readonly string[] JumpLandClipPaths =
    {
        "Assets/Processed/Mixamo/Animations_MCP/MX_Falling_To_Landing.fbx",
        "Assets/Processed/Mixamo/Animations_MCP/MX_Jumping_Down.fbx"
    };

    [MenuItem("Tools/Mixamo Pipeline/Configure Third Person Controller")]
    public static void Configure()
    {
        EnsureFolder("Assets/Processed/Mixamo", "ThirdPerson");

        AnimatorController controller = EnsureAnimatorController();
        GameObject character = EnsurePlayerCharacter();
        if (character == null)
        {
            Debug.LogError("Mixamo third-person setup failed: no humanoid player character was found or instantiated.");
            return;
        }

        bool sceneChanged = ConfigurePlayer(character, controller);
        sceneChanged |= ConfigureCamera(character);
        sceneChanged |= ConfigureHud();
        sceneChanged |= ConfigureCinematicPostProcessing();

        AssetDatabase.SaveAssets();
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            AssetDatabase.ForceReserializeAssets(new[] { ControllerPath });
        }

        AssetDatabase.SaveAssets();

        if (sceneChanged && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        Selection.activeGameObject = character;
        Debug.Log("Mixamo third-person controller configured for " + GetPath(character.transform));
    }

    private static AnimatorController EnsureAnimatorController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        RebuildController(controller);
        return controller;
    }

    private static void RebuildController(AnimatorController controller)
    {
        RemoveGeneratedSubAssets(controller);
        AnimatorStateMachine stateMachine = ResetBaseLayer(controller);

        foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
        {
            controller.RemoveParameter(parameter);
        }

        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Turn180", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
        controller.AddParameter("LandingSoon", AnimatorControllerParameterType.Bool);
        controller.AddParameter("GroundDistance", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);

        AnimatorState locomotion = stateMachine.AddState("Locomotion", new Vector3(260f, 80f, 0f));
        locomotion.writeDefaultValues = false;
        locomotion.motion = CreateLocomotionBlendTree(controller);
        stateMachine.defaultState = locomotion;

        AnimatorState sprintTurn = stateMachine.AddState("SprintTurn180", new Vector3(570f, 80f, 0f));
        sprintTurn.writeDefaultValues = false;
        sprintTurn.motion = LoadAnimationClip("Assets/Processed/Mixamo/Animations_MCP/MX_Running_Turn_180.fbx");

        AnimatorState runningJump = stateMachine.AddState("RunningJump", new Vector3(570f, -110f, 0f));
        runningJump.writeDefaultValues = false;
        runningJump.motion = LoadAnimationClip("Assets/Processed/Mixamo/Animations_MCP/MX_Running_Jump.fbx");

        AnimatorState jumpAir = stateMachine.AddState("JumpAirLoop", new Vector3(880f, -110f, 0f));
        jumpAir.writeDefaultValues = false;
        jumpAir.motion = LoadAnimationClip("Assets/Processed/Mixamo/Animations_MCP/MX_Falling_Idle.fbx");

        AnimatorState jumpLand = stateMachine.AddState("JumpLand", new Vector3(1190f, -110f, 0f));
        jumpLand.writeDefaultValues = false;
        jumpLand.motion = LoadFirstAvailableClip(JumpLandClipPaths);

        AnimatorStateTransition turnTransition = locomotion.AddTransition(sprintTurn);
        turnTransition.hasExitTime = false;
        turnTransition.duration = 0.06f;
        turnTransition.AddCondition(AnimatorConditionMode.If, 0f, "Turn180");

        AnimatorStateTransition exitTransition = sprintTurn.AddTransition(locomotion);
        exitTransition.hasExitTime = true;
        exitTransition.exitTime = 0.88f;
        exitTransition.duration = 0.12f;

        AnimatorStateTransition movingJumpTransition = locomotion.AddTransition(runningJump);
        movingJumpTransition.hasExitTime = false;
        movingJumpTransition.duration = 0.04f;
        movingJumpTransition.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
        movingJumpTransition.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");

        AnimatorStateTransition stationaryJumpTransition = locomotion.AddTransition(jumpAir);
        stationaryJumpTransition.hasExitTime = false;
        stationaryJumpTransition.duration = 0.04f;
        stationaryJumpTransition.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
        stationaryJumpTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

        AnimatorStateTransition fallTransition = locomotion.AddTransition(jumpAir);
        fallTransition.hasExitTime = false;
        fallTransition.duration = 0.12f;
        fallTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
        fallTransition.AddCondition(AnimatorConditionMode.Less, -2f, "VerticalVelocity");

        AnimatorStateTransition runningJumpToAirTransition = runningJump.AddTransition(jumpAir);
        runningJumpToAirTransition.hasExitTime = true;
        runningJumpToAirTransition.exitTime = 0.55f;
        runningJumpToAirTransition.duration = 0.08f;
        runningJumpToAirTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");

        AnimatorStateTransition runningJumpToLandTransition = runningJump.AddTransition(jumpLand);
        runningJumpToLandTransition.hasExitTime = false;
        runningJumpToLandTransition.duration = 0.04f;
        runningJumpToLandTransition.AddCondition(AnimatorConditionMode.If, 0f, "LandingSoon");
        runningJumpToLandTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

        AnimatorStateTransition groundedRunningJumpToLandTransition = runningJump.AddTransition(jumpLand);
        groundedRunningJumpToLandTransition.hasExitTime = false;
        groundedRunningJumpToLandTransition.duration = 0.04f;
        groundedRunningJumpToLandTransition.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
        groundedRunningJumpToLandTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

        AnimatorStateTransition movingRunningJumpLandingTransition = runningJump.AddTransition(locomotion);
        movingRunningJumpLandingTransition.hasExitTime = false;
        movingRunningJumpLandingTransition.duration = 0.08f;
        movingRunningJumpLandingTransition.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
        movingRunningJumpLandingTransition.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");

        AnimatorStateTransition landTransition = jumpAir.AddTransition(jumpLand);
        landTransition.hasExitTime = false;
        landTransition.duration = 0.04f;
        landTransition.AddCondition(AnimatorConditionMode.If, 0f, "LandingSoon");
        landTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

        AnimatorStateTransition groundedLandFallbackTransition = jumpAir.AddTransition(jumpLand);
        groundedLandFallbackTransition.hasExitTime = false;
        groundedLandFallbackTransition.duration = 0.04f;
        groundedLandFallbackTransition.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
        groundedLandFallbackTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

        AnimatorStateTransition movingLandingTransition = jumpAir.AddTransition(locomotion);
        movingLandingTransition.hasExitTime = false;
        movingLandingTransition.duration = 0.08f;
        movingLandingTransition.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
        movingLandingTransition.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");

        AnimatorStateTransition movingLandingInterruptTransition = jumpLand.AddTransition(locomotion);
        movingLandingInterruptTransition.hasExitTime = false;
        movingLandingInterruptTransition.duration = 0.08f;
        movingLandingInterruptTransition.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
        movingLandingInterruptTransition.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");

        AnimatorStateTransition landingExitTransition = jumpLand.AddTransition(locomotion);
        landingExitTransition.hasExitTime = true;
        landingExitTransition.exitTime = 0.62f;
        landingExitTransition.duration = 0.12f;
        landingExitTransition.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

        EditorUtility.SetDirty(controller);
    }

    private static void RemoveGeneratedSubAssets(AnimatorController controller)
    {
        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(ControllerPath)
            .Where(asset => asset != controller)
            .ToArray();

        foreach (UnityEngine.Object subAsset in subAssets)
        {
            UnityEngine.Object.DestroyImmediate(subAsset, true);
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
            name = "Locomotion Speed Blend",
            hideFlags = HideFlags.HideInHierarchy,
            blendType = BlendTreeType.Simple1D,
            blendParameter = "Speed",
            useAutomaticThresholds = false
        };

        AssetDatabase.AddObjectToAsset(blendTree, controller);

        AddBlendChild(blendTree, LoadFirstAvailableClip(IdleClipPaths), 0f, "idle");
        AddBlendChild(blendTree, LoadAnimationClip("Assets/Processed/Mixamo/Animations_MCP/MX_Walking.fbx"), 1f, "walk");
        AddBlendChild(blendTree, LoadAnimationClip("Assets/Processed/Mixamo/Animations_MCP/MX_Standard_Run.fbx"), 2f, "run");

        return blendTree;
    }

    private static void AddBlendChild(BlendTree blendTree, Motion motion, float threshold, string label)
    {
        if (motion == null)
        {
            Debug.LogWarning("Missing " + label + " clip for " + ControllerPath);
            return;
        }

        blendTree.AddChild(motion, threshold);
    }

    private static AnimationClip LoadFirstAvailableClip(string[] paths)
    {
        foreach (string path in paths)
        {
            AnimationClip clip = LoadAnimationClip(path);
            if (clip != null)
            {
                return clip;
            }
        }

        return null;
    }

    private static AnimationClip LoadAnimationClip(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
    }

    private static GameObject EnsurePlayerCharacter()
    {
        Animator existingAnimator = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None)
            .FirstOrDefault(animator => animator != null && animator.avatar != null && animator.avatar.isHuman);
        if (existingAnimator != null)
        {
            return existingAnimator.gameObject;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "Player";
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Animator animator = instance.GetComponentInChildren<Animator>();
        return animator != null ? animator.gameObject : instance;
    }

    private static bool ConfigurePlayer(GameObject character, RuntimeAnimatorController controller)
    {
        bool changed = false;

        try
        {
            if (character.tag != "Player")
            {
                character.tag = "Player";
                changed = true;
            }
        }
        catch (UnityException)
        {
            Debug.LogWarning("Could not assign Player tag. The project may not have a Player tag configured.");
        }

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

        if (animator.applyRootMotion)
        {
            animator.applyRootMotion = false;
            changed = true;
        }

        CharacterController characterController = character.GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = character.AddComponent<CharacterController>();
            changed = true;
        }

        changed |= ConfigureCharacterController(character, characterController);

        ThirdPersonLocomotionController locomotion = character.GetComponent<ThirdPersonLocomotionController>();
        if (locomotion == null)
        {
            locomotion = character.AddComponent<ThirdPersonLocomotionController>();
            changed = true;
        }

        foreach (CharacterActionStateMachine oldController in character.GetComponents<CharacterActionStateMachine>())
        {
            if (oldController.enabled)
            {
                oldController.enabled = false;
                changed = true;
            }
        }

        SerializedObject serializedLocomotion = new SerializedObject(locomotion);
        changed |= SetSerializedBool(serializedLocomotion, "readKeyboardInput", true);
        changed |= SetSerializedBool(serializedLocomotion, "autoTurn180OnBackInput", true);
        changed |= SetSerializedBool(serializedLocomotion, "allowBackpedal", false);
        changed |= SetSerializedBool(serializedLocomotion, "lockMovementDuringTurn", true);
        changed |= SetSerializedBool(serializedLocomotion, "disableRootMotion", true);
        changed |= SetSerializedFloat(serializedLocomotion, "walkSpeed", 1.8f);
        changed |= SetSerializedFloat(serializedLocomotion, "runSpeed", 4.8f);
        changed |= SetSerializedFloat(serializedLocomotion, "acceleration", 16f);
        changed |= SetSerializedFloat(serializedLocomotion, "deceleration", 42f);
        changed |= SetSerializedFloat(serializedLocomotion, "stopSnapSpeed", 0.08f);
        changed |= SetSerializedFloat(serializedLocomotion, "rotationSpeed", 720f);
        changed |= SetSerializedFloat(serializedLocomotion, "rotationSmoothTime", 0.08f);
        changed |= SetSerializedFloat(serializedLocomotion, "backpedalSpeedMultiplier", 0.6f);
        changed |= SetSerializedFloat(serializedLocomotion, "backpedalInputThreshold", -0.2f);
        changed |= SetSerializedFloat(serializedLocomotion, "gravity", -26f);
        changed |= SetSerializedFloat(serializedLocomotion, "groundedStickForce", 2f);
        changed |= SetSerializedBool(serializedLocomotion, "allowJump", true);
        changed |= SetSerializedFloat(serializedLocomotion, "jumpHeight", 1.15f);
        changed |= SetSerializedFloat(serializedLocomotion, "jumpBufferTime", 0.12f);
        changed |= SetSerializedFloat(serializedLocomotion, "coyoteTime", 0.08f);
        changed |= SetSerializedFloat(serializedLocomotion, "jumpCooldown", 0.18f);
        changed |= SetSerializedFloat(serializedLocomotion, "airControlMultiplier", 0.4f);
        changed |= SetSerializedFloat(serializedLocomotion, "fallGravityMultiplier", 1.35f);
        changed |= SetSerializedFloat(serializedLocomotion, "movingFallGravityMultiplier", 1.05f);
        changed |= SetSerializedFloat(serializedLocomotion, "jumpCutGravityMultiplier", 1.75f);
        changed |= SetSerializedFloat(serializedLocomotion, "maxFallSpeed", 18f);
        changed |= SetSerializedInt(serializedLocomotion, "groundMask", -1);
        changed |= SetSerializedFloat(serializedLocomotion, "landingProbeDistance", 2f);
        changed |= SetSerializedFloat(serializedLocomotion, "landingEnterDistance", 0.38f);
        changed |= SetSerializedFloat(serializedLocomotion, "landingLeadTime", 0.12f);
        changed |= SetSerializedFloat(serializedLocomotion, "landingMinFallSpeed", 2.6f);
        changed |= SetSerializedFloat(serializedLocomotion, "landingProbeRadiusMultiplier", 0.75f);
        changed |= SetSerializedFloat(serializedLocomotion, "minLandingNormalY", 0.55f);
        changed |= SetSerializedFloat(serializedLocomotion, "stairGroundedDistance", 0.5f);
        changed |= SetSerializedFloat(serializedLocomotion, "fallAnimationDelay", 0.16f);
        changed |= SetSerializedFloat(serializedLocomotion, "fallAnimationMinDistance", 0.65f);
        changed |= SetSerializedFloat(serializedLocomotion, "landingMoveInputThreshold", 0.1f);
        changed |= SetSerializedFloat(serializedLocomotion, "landingMoveSpeedThreshold", 0.25f);
        changed |= SetSerializedFloat(serializedLocomotion, "landingImpactMinSpeed", 5.5f);
        changed |= SetSerializedFloat(serializedLocomotion, "landingPlanarSpeedMultiplier", 0.92f);
        serializedLocomotion.ApplyModifiedProperties();

        if (changed)
        {
            EditorUtility.SetDirty(character);
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(characterController);
            EditorUtility.SetDirty(locomotion);
        }

        return changed;
    }

    private static bool ConfigureCamera(GameObject character)
    {
        Camera mainCamera = EnsureMainCamera();
        if (mainCamera == null)
        {
            return false;
        }

        GameObject cameraRig = FindOrCreateRoot(CameraRigName);
        GameObject followPoint = FindOrCreateChild(cameraRig.transform, CameraFollowPointName);

        CinemachineOrbitTargetInput orbitInput = cameraRig.GetComponent<CinemachineOrbitTargetInput>();
        if (orbitInput == null)
        {
            orbitInput = cameraRig.AddComponent<CinemachineOrbitTargetInput>();
        }

        SerializedObject serializedOrbit = new SerializedObject(orbitInput);
        bool changed = false;
        changed |= SetSerializedObject(serializedOrbit, "followTarget", character.transform);
        changed |= SetSerializedObject(serializedOrbit, "followPoint", followPoint.transform);
        changed |= SetSerializedFloat(serializedOrbit, "distance", 4.8f);
        changed |= SetSerializedFloat(serializedOrbit, "shoulderOffset", 0.25f);
        changed |= SetSerializedFloat(serializedOrbit, "minPitch", 5f);
        changed |= SetSerializedBool(serializedOrbit, "clampFollowPointHeight", true);
        changed |= SetSerializedFloat(serializedOrbit, "minFollowPointHeight", 0.75f);
        changed |= SetSerializedFloat(serializedOrbit, "followPositionSmoothTime", 0.12f);
        changed |= SetSerializedFloat(serializedOrbit, "followPointSmoothTime", 0.06f);
        changed |= SetSerializedBool(serializedOrbit, "recenterBehindMovingTarget", false);
        changed |= SetSerializedBool(serializedOrbit, "blockRecenteringOnBackInput", true);
        changed |= SetSerializedFloat(serializedOrbit, "recenterDelay", 0.75f);
        changed |= SetSerializedFloat(serializedOrbit, "recenterSharpness", 2.5f);
        changed |= SetSerializedFloat(serializedOrbit, "backInputBlockThreshold", -0.2f);
        serializedOrbit.ApplyModifiedProperties();
        orbitInput.RecenterBehindTarget();
        orbitInput.SnapToTarget();

        ThirdPersonLocomotionController locomotion = character.GetComponent<ThirdPersonLocomotionController>();
        if (locomotion != null)
        {
            SerializedObject serializedLocomotion = new SerializedObject(locomotion);
            changed |= SetSerializedObject(serializedLocomotion, "cameraTransform", cameraRig.transform);
            serializedLocomotion.ApplyModifiedProperties();
            locomotion.SetCameraTransform(cameraRig.transform);
        }

        bool cinemachineConfigured = ConfigureCinemachine(mainCamera, cameraRig.transform, followPoint.transform);
        if (cinemachineConfigured)
        {
            GtaStyleThirdPersonCamera oldCamera = mainCamera.GetComponent<GtaStyleThirdPersonCamera>();
            if (oldCamera != null && oldCamera.enabled)
            {
                oldCamera.enabled = false;
                changed = true;
            }
        }
        else
        {
            changed |= ConfigureFallbackCamera(mainCamera, character.transform);
        }

        if (!Mathf.Approximately(mainCamera.fieldOfView, 60f))
        {
            mainCamera.fieldOfView = 60f;
            changed = true;
        }

        EditorUtility.SetDirty(cameraRig);
        EditorUtility.SetDirty(followPoint);
        EditorUtility.SetDirty(orbitInput);
        EditorUtility.SetDirty(mainCamera);
        EditorUtility.SetDirty(mainCamera.gameObject);

        return changed;
    }

    private static bool ConfigureHud()
    {
        EnsureFolder("Assets", "UI");
        EnsureFolder("Assets/UI", "HUD");

        VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudUxmlPath);
        if (visualTree == null)
        {
            Debug.LogWarning("HUD UXML was not found: " + HudUxmlPath);
            return false;
        }

        PanelSettings panelSettings = EnsureHudPanelSettings();
        GameObject hudObject = FindOrCreateRoot(HudObjectName);
        bool changed = false;

        UIDocument document = hudObject.GetComponent<UIDocument>();
        if (document == null)
        {
            document = hudObject.AddComponent<UIDocument>();
            changed = true;
        }

        if (document.panelSettings != panelSettings)
        {
            document.panelSettings = panelSettings;
            changed = true;
        }

        if (document.visualTreeAsset != visualTree)
        {
            document.visualTreeAsset = visualTree;
            changed = true;
        }

        if (document.sortingOrder != 100)
        {
            document.sortingOrder = 100;
            changed = true;
        }

        ThirdPersonHudController hudController = hudObject.GetComponent<ThirdPersonHudController>();
        if (hudController == null)
        {
            hudController = hudObject.AddComponent<ThirdPersonHudController>();
            changed = true;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            SerializedObject serializedHud = new SerializedObject(hudController);
            changed |= SetSerializedObject(serializedHud, "aimCamera", mainCamera);
            changed |= SetSerializedBool(serializedHud, "showReticle", true);
            changed |= SetSerializedFloat(serializedHud, "aimDistance", 120f);
            serializedHud.ApplyModifiedProperties();
            hudController.SetAimCamera(mainCamera);
        }

        EditorUtility.SetDirty(hudObject);
        EditorUtility.SetDirty(document);
        EditorUtility.SetDirty(hudController);
        return changed;
    }

    private static PanelSettings EnsureHudPanelSettings()
    {
        PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(HudPanelSettingsPath);
        if (panelSettings == null)
        {
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            AssetDatabase.CreateAsset(panelSettings, HudPanelSettingsPath);
        }

        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(1920, 1080);
        panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panelSettings.match = 0.5f;
        EditorUtility.SetDirty(panelSettings);
        return panelSettings;
    }

    private static bool ConfigureCinemachine(Camera mainCamera, Transform lookAtTarget, Transform followPoint)
    {
        Type brainType = FindType(
            "Unity.Cinemachine.CinemachineBrain",
            "Cinemachine.CinemachineBrain");
        Type cameraType = FindType(
            "Unity.Cinemachine.CinemachineCamera",
            "Cinemachine.CinemachineVirtualCamera");

        if (brainType == null || cameraType == null)
        {
            Debug.LogWarning("Cinemachine package is not available yet. Falling back to the existing camera script.");
            return false;
        }

        bool changed = false;
        if (mainCamera.GetComponent(brainType) == null)
        {
            mainCamera.gameObject.AddComponent(brainType);
            changed = true;
        }

        GameObject virtualCamera = FindOrCreateRoot(CinemachineCameraName);
        Component cameraComponent = virtualCamera.GetComponent(cameraType);
        if (cameraComponent == null)
        {
            cameraComponent = virtualCamera.AddComponent(cameraType);
            changed = true;
        }

        virtualCamera.transform.SetPositionAndRotation(
            followPoint.position,
            Quaternion.LookRotation(lookAtTarget.position - followPoint.position, Vector3.up));

        changed |= SetMember(cameraComponent, "Follow", followPoint);
        changed |= SetMember(cameraComponent, "LookAt", lookAtTarget);
        changed |= SetMember(cameraComponent, "Priority", 20);
        changed |= ConfigureCinemachineLens(cameraComponent, 55f, 0.05f, 500f);

        changed |= ConfigureCinemachineComponent(
            virtualCamera,
            "Unity.Cinemachine.CinemachineFollow",
            component => SetMember(component, "FollowOffset", Vector3.zero));
        changed |= ConfigureCinemachineComponent(
            virtualCamera,
            "Unity.Cinemachine.CinemachineHardLookAt",
            component => SetMember(component, "LookAtOffset", Vector3.zero));
        changed |= ConfigureCinemachineComponent(
            virtualCamera,
            "Unity.Cinemachine.CinemachineDeoccluder",
            ConfigureExplorationDeoccluder);

        EditorUtility.SetDirty(virtualCamera);
        EditorUtility.SetDirty(cameraComponent);
        return changed;
    }

    private static bool ConfigureCinematicPostProcessing()
    {
        EnsureFolder("Assets", "Settings");

        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(CinematicVolumeProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, CinematicVolumeProfilePath);
        }

        ConfigureCinematicVolumeProfile(profile);

        GameObject volumeObject = FindOrCreateRoot(CinematicVolumeName);
        bool changed = false;
        Volume volume = volumeObject.GetComponent<Volume>();
        if (volume == null)
        {
            volume = volumeObject.AddComponent<Volume>();
            changed = true;
        }

        if (!volume.isGlobal)
        {
            volume.isGlobal = true;
            changed = true;
        }

        if (!Mathf.Approximately(volume.priority, 10f))
        {
            volume.priority = 10f;
            changed = true;
        }

        if (!Mathf.Approximately(volume.weight, 1f))
        {
            volume.weight = 1f;
            changed = true;
        }

        if (volume.sharedProfile != profile)
        {
            volume.sharedProfile = profile;
            changed = true;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            UniversalAdditionalCameraData cameraData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                cameraData = mainCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                changed = true;
            }

            if (!cameraData.renderPostProcessing)
            {
                cameraData.renderPostProcessing = true;
                changed = true;
            }

            if (!mainCamera.allowHDR)
            {
                mainCamera.allowHDR = true;
                changed = true;
            }

            if (!Mathf.Approximately(mainCamera.fieldOfView, 55f))
            {
                mainCamera.fieldOfView = 55f;
                changed = true;
            }

            EditorUtility.SetDirty(mainCamera);
            EditorUtility.SetDirty(cameraData);
        }

        EditorUtility.SetDirty(profile);
        EditorUtility.SetDirty(volumeObject);
        EditorUtility.SetDirty(volume);
        return changed;
    }

    private static void ConfigureCinematicVolumeProfile(VolumeProfile profile)
    {
        Bloom bloom = GetOrAddVolumeComponent<Bloom>(profile);
        SetVolumeParameter(bloom.threshold, 1.1f);
        SetVolumeParameter(bloom.intensity, 0.18f);
        SetVolumeParameter(bloom.scatter, 0.55f);
        SetVolumeParameter(bloom.highQualityFiltering, true);

        Tonemapping tonemapping = GetOrAddVolumeComponent<Tonemapping>(profile);
        SetVolumeParameter(tonemapping.mode, TonemappingMode.ACES);

        ColorAdjustments colorAdjustments = GetOrAddVolumeComponent<ColorAdjustments>(profile);
        SetVolumeParameter(colorAdjustments.postExposure, -0.05f);
        SetVolumeParameter(colorAdjustments.contrast, 10f);
        SetVolumeParameter(colorAdjustments.saturation, -4f);
        SetVolumeParameter(colorAdjustments.colorFilter, new Color(1f, 0.965f, 0.92f, 1f));

        Vignette vignette = GetOrAddVolumeComponent<Vignette>(profile);
        SetVolumeParameter(vignette.intensity, 0.18f);
        SetVolumeParameter(vignette.smoothness, 0.42f);
        SetVolumeParameter(vignette.rounded, false);
        SetVolumeParameter(vignette.color, Color.black);

        DepthOfField depthOfField = GetOrAddVolumeComponent<DepthOfField>(profile);
        SetVolumeParameter(depthOfField.mode, DepthOfFieldMode.Gaussian);
        SetVolumeParameter(depthOfField.gaussianStart, 18f);
        SetVolumeParameter(depthOfField.gaussianEnd, 45f);
        SetVolumeParameter(depthOfField.gaussianMaxRadius, 0.25f);
        SetVolumeParameter(depthOfField.highQualitySampling, true);

        FilmGrain filmGrain = GetOrAddVolumeComponent<FilmGrain>(profile);
        SetVolumeParameter(filmGrain.intensity, 0.08f);
        SetVolumeParameter(filmGrain.response, 0.75f);
    }

    private static T GetOrAddVolumeComponent<T>(VolumeProfile profile)
        where T : VolumeComponent
    {
        if (!profile.TryGet(out T component))
        {
            component = profile.Add<T>(true);
        }

        component.active = true;
        return component;
    }

    private static void SetVolumeParameter<T>(VolumeParameter<T> parameter, T value)
    {
        parameter.overrideState = true;
        parameter.value = value;
    }

    private static bool ConfigureCinemachineLens(Component cameraComponent, float fieldOfView, float nearClip, float farClip)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo lensField = cameraComponent.GetType().GetField("Lens", Flags);
        if (lensField == null)
        {
            return false;
        }

        object lens = lensField.GetValue(cameraComponent);
        bool changed = false;
        changed |= SetStructField(ref lens, lensField.FieldType, "FieldOfView", fieldOfView);
        changed |= SetStructField(ref lens, lensField.FieldType, "NearClipPlane", nearClip);
        changed |= SetStructField(ref lens, lensField.FieldType, "FarClipPlane", farClip);

        if (changed)
        {
            lensField.SetValue(cameraComponent, lens);
        }

        return changed;
    }

    private static bool SetStructField(ref object structValue, Type structType, string fieldName, float value)
    {
        FieldInfo field = structType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null)
        {
            return false;
        }

        float current = (float)field.GetValue(structValue);
        if (Mathf.Approximately(current, value))
        {
            return false;
        }

        field.SetValue(structValue, value);
        return true;
    }

    private static bool ConfigureExplorationDeoccluder(Component component)
    {
        bool changed = false;
        changed |= SetMember(component, "IgnoreTag", "Player");
        changed |= SetMember(component, "MinimumDistanceFromTarget", 0.2f);

        if (component is Behaviour behaviour && behaviour.enabled)
        {
            behaviour.enabled = false;
            changed = true;
        }

        return changed;
    }

    private static bool ConfigureCinemachineComponent(
        GameObject virtualCamera,
        string typeName,
        Func<Component, bool> configure)
    {
        Type componentType = FindType(typeName);
        if (componentType == null)
        {
            return false;
        }

        bool changed = false;
        Component component = virtualCamera.GetComponent(componentType);
        if (component == null)
        {
            component = virtualCamera.AddComponent(componentType);
            changed = true;
        }

        changed |= configure(component);
        EditorUtility.SetDirty(component);
        return changed;
    }

    private static bool ConfigureFallbackCamera(Camera mainCamera, Transform target)
    {
        bool changed = false;
        GtaStyleThirdPersonCamera fallback = mainCamera.GetComponent<GtaStyleThirdPersonCamera>();
        if (fallback == null)
        {
            fallback = mainCamera.gameObject.AddComponent<GtaStyleThirdPersonCamera>();
            changed = true;
        }

        if (!fallback.enabled)
        {
            fallback.enabled = true;
            changed = true;
        }

        if (fallback.Target != target)
        {
            fallback.SetTarget(target);
            changed = true;
        }
        else
        {
            fallback.SnapToTarget();
        }

        return changed;
    }

    private static Camera EnsureMainCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            return mainCamera;
        }

        GameObject cameraObject = GameObject.Find("Main Camera") ?? new GameObject("Main Camera");
        if (!cameraObject.CompareTag("MainCamera"))
        {
            cameraObject.tag = "MainCamera";
        }

        mainCamera = cameraObject.GetComponent<Camera>();
        if (mainCamera == null)
        {
            mainCamera = cameraObject.AddComponent<Camera>();
        }

        if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null)
        {
            cameraObject.AddComponent<AudioListener>();
        }

        return mainCamera;
    }

    private static bool ConfigureCharacterController(GameObject character, CharacterController characterController)
    {
        Bounds bounds = CalculateRenderableBounds(character);
        float height = Mathf.Max(1f, bounds.size.y);
        float horizontalSize = Mathf.Max(bounds.size.x, bounds.size.z);
        float radius = Mathf.Clamp(horizontalSize * 0.22f, 0.22f, height * 0.45f);
        Vector3 localCenter = character.transform.InverseTransformPoint(bounds.center);
        localCenter.x = 0f;
        localCenter.z = 0f;

        bool changed = false;
        changed |= SetCharacterControllerFloat(characterController, "height", height);
        changed |= SetCharacterControllerFloat(characterController, "radius", radius);
        changed |= SetCharacterControllerFloat(characterController, "stepOffset", Mathf.Min(0.4f, height * 0.22f));
        changed |= SetCharacterControllerFloat(characterController, "slopeLimit", 50f);
        changed |= SetCharacterControllerFloat(characterController, "skinWidth", 0.04f);

        if ((characterController.center - localCenter).sqrMagnitude > 0.0001f)
        {
            characterController.center = localCenter;
            changed = true;
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

    private static bool SetCharacterControllerFloat(CharacterController characterController, string propertyName, float value)
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

    private static bool SetSerializedInt(SerializedObject serializedObject, string propertyName, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.intValue == value)
        {
            return false;
        }

        property.intValue = value;
        return true;
    }

    private static bool SetSerializedObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == value)
        {
            return false;
        }

        property.objectReferenceValue = value;
        return true;
    }

    private static bool SetMember(object target, string memberName, object value)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type targetType = target.GetType();

        PropertyInfo property = targetType.GetProperty(memberName, Flags);
        if (property != null && property.CanWrite && CanAssign(property.PropertyType, value))
        {
            object current = property.GetValue(target);
            if (Equals(current, value))
            {
                return false;
            }

            property.SetValue(target, value);
            return true;
        }

        FieldInfo field = targetType.GetField(memberName, Flags);
        if (field != null && CanAssign(field.FieldType, value))
        {
            object current = field.GetValue(target);
            if (Equals(current, value))
            {
                return false;
            }

            field.SetValue(target, value);
            return true;
        }

        return false;
    }

    private static bool CanAssign(Type memberType, object value)
    {
        if (value == null)
        {
            return !memberType.IsValueType || Nullable.GetUnderlyingType(memberType) != null;
        }

        Type valueType = value.GetType();
        return memberType.IsAssignableFrom(valueType)
            || (memberType == typeof(int) && valueType == typeof(int));
    }

    private static Type FindType(params string[] typeNames)
    {
        foreach (string typeName in typeNames)
        {
            Type type = Type.GetType(typeName);
            if (type != null)
            {
                return type;
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }
        }

        return null;
    }

    private static GameObject FindOrCreateRoot(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            return existing;
        }

        return new GameObject(name);
    }

    private static GameObject FindOrCreateChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            return child.gameObject;
        }

        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(parent, false);
        return childObject;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string folderPath = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static string GetPath(Transform transform)
    {
        if (transform.parent == null)
        {
            return transform.name;
        }

        return GetPath(transform.parent) + "/" + transform.name;
    }
}
