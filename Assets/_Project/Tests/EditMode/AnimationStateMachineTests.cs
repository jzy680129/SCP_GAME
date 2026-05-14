using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class AnimationStateMachineTests
{
    private const string ControllerPath = "Assets/_Project/Pipeline/Processed/Mixamo/ThirdPerson/MX_ThirdPerson.controller";

    [Test]
    public void ResControllerContainsControllableLocomotionParameters()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        Assert.That(controller, Is.Not.Null, "Animator Controller should be generated under the processed Mixamo pipeline folder.");
        Assert.That(controller.parameters.Select(parameter => parameter.name), Is.SupersetOf(new[]
        {
            "MoveX",
            "MoveY",
            "Speed",
            "IsRunning",
            "Jump"
        }));
    }

    [Test]
    public void ResControllerHasLocomotionAndJumpStates()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        Assert.That(controller, Is.Not.Null);

        ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
        Assert.That(states.Select(state => state.state.name), Is.SupersetOf(new[]
        {
            "Locomotion",
            "RunningJump",
            "JumpAirLoop",
            "JumpLand"
        }));

        AnimatorState locomotion = states.First(state => state.state.name == "Locomotion").state;
        Assert.That(locomotion.motion, Is.TypeOf<BlendTree>());
    }

    [Test]
    public void RuntimeStateMachineExposesControlApi()
    {
        Type controllerType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("CharacterActionStateMachine"))
            .FirstOrDefault(type => type != null);

        Assert.That(controllerType, Is.Not.Null);
        Assert.That(controllerType.GetMethod("SetMoveInput", new[] { typeof(Vector2) }), Is.Not.Null);
        Assert.That(controllerType.GetMethod("SetRunning", new[] { typeof(bool) }), Is.Not.Null);
        Assert.That(controllerType.GetMethod("TriggerJump", Type.EmptyTypes), Is.Not.Null);
        Assert.That(controllerType.GetMethod("ForceState", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
    }

    [Test]
    public void RuntimeStateMachineRequiresUnityCharacterController()
    {
        Type controllerType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("CharacterActionStateMachine"))
            .FirstOrDefault(type => type != null);

        Assert.That(controllerType, Is.Not.Null);

        Type[] requiredTypes = controllerType
            .GetCustomAttributes(typeof(RequireComponent), true)
            .Cast<RequireComponent>()
            .SelectMany(attribute => new[] { attribute.m_Type0, attribute.m_Type1, attribute.m_Type2 })
            .Where(type => type != null)
            .ToArray();

        Assert.That(requiredTypes, Does.Contain(typeof(Animator)));
        Assert.That(requiredTypes, Does.Contain(typeof(CharacterController)));
    }

    [Test]
    public void ResControllerUsesAvailableMixamoMotionClips()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        Assert.That(controller, Is.Not.Null);

        BlendTree blendTree = controller.layers[0].stateMachine.states
            .Select(state => state.state)
            .Where(state => state != null)
            .Select(state => state.motion)
            .OfType<BlendTree>()
            .FirstOrDefault();

        Assert.That(blendTree, Is.Not.Null);
        Assert.That(
            blendTree.children.Select(child => child.motion != null ? child.motion.name : null),
            Is.SupersetOf(new[]
            {
                "MX_Breathing_Idle",
                "MX_Walking",
                "MX_Standard_Run"
            }));

        AnimatorState jump = controller.layers[0].stateMachine.states
            .Select(state => state.state)
            .FirstOrDefault(state => state != null && state.name == "RunningJump");

        Assert.That(jump, Is.Not.Null);
        Assert.That(jump.motion, Is.Not.Null);
        Assert.That(jump.motion.name, Is.EqualTo("MX_Running_Jump"));
    }

    [Test]
    public void RuntimeStateMachineExposesCameraRelativeControlApi()
    {
        Type controllerType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("CharacterActionStateMachine"))
            .FirstOrDefault(type => type != null);

        Assert.That(controllerType, Is.Not.Null);
        Assert.That(controllerType.GetMethod("SetCameraTransform", new[] { typeof(Transform) }), Is.Not.Null);
        Assert.That(controllerType.GetMethod("SetCameraRelativeMoveInput", new[] { typeof(Vector2) }), Is.Not.Null);
    }

    [Test]
    public void RuntimeStateMachineExposesLocalLocomotionBlendApi()
    {
        Type controllerType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("CharacterActionStateMachine"))
            .FirstOrDefault(type => type != null);

        Assert.That(controllerType, Is.Not.Null);
        Assert.That(controllerType.GetMethod("SetUseLocalLocomotionBlend", new[] { typeof(bool) }), Is.Not.Null);
    }

    [Test]
    public void ThirdPersonCameraExposesFollowApi()
    {
        Type cameraType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("GtaStyleThirdPersonCamera"))
            .FirstOrDefault(type => type != null);

        Assert.That(cameraType, Is.Not.Null);
        Assert.That(cameraType.GetMethod("SetTarget", new[] { typeof(Transform) }), Is.Not.Null);
        Assert.That(cameraType.GetMethod("RecenterBehindTarget", Type.EmptyTypes), Is.Not.Null);
    }

    [Test]
    public void SceneMainCameraUsesThirdPersonFollowController()
    {
        OpenConfiguredScene();

        Type cameraType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("GtaStyleThirdPersonCamera"))
            .FirstOrDefault(type => type != null);
        GameObject mainCamera = GameObject.Find("Main Camera");

        Assert.That(cameraType, Is.Not.Null);
        Assert.That(mainCamera, Is.Not.Null);
        Assert.That(mainCamera.GetComponent(cameraType), Is.Not.Null);
    }

    [Test]
    public void SceneStateMachineUsesLocalLocomotionBlend()
    {
        OpenConfiguredScene();

        Type controllerType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("CharacterActionStateMachine"))
            .FirstOrDefault(type => type != null);
        GameObject character = GameObject.Find("Ch15_nonPBR");

        Assert.That(controllerType, Is.Not.Null);
        Assert.That(character, Is.Not.Null);

        Component stateMachine = character.GetComponent(controllerType);
        Assert.That(stateMachine, Is.Not.Null);

        SerializedObject serializedStateMachine = new SerializedObject(stateMachine);
        SerializedProperty useLocalLocomotionBlend = serializedStateMachine.FindProperty("useLocalLocomotionBlend");

        Assert.That(useLocalLocomotionBlend, Is.Not.Null);
        Assert.That(useLocalLocomotionBlend.boolValue, Is.True);
    }

    [Test]
    public void SceneCharacterUsesUnityCharacterController()
    {
        OpenConfiguredScene();

        GameObject character = GameObject.Find("Ch15_nonPBR");

        Assert.That(character, Is.Not.Null);
        Assert.That(character.GetComponent<CharacterController>(), Is.Not.Null);
    }

    private static void OpenConfiguredScene()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Dev/SCN_Dev_Locomotion.unity", OpenSceneMode.Single);
    }
}
