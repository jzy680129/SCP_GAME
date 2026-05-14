using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class StylizedLightingSystemTests
{
    private const string ScenePath = "Assets/1.unity";
    private const string ProfilePath = "Assets/Settings/StylizedLighting/STY_Lighting_Day_Courtyard.asset";

    [Test]
    public void RuntimeTypesExposeProfileDrivenLightingApi()
    {
        Type profileType = FindRuntimeType("StylizedLightingProfile");
        Type controllerType = FindRuntimeType("StylizedLightingController");

        Assert.That(profileType, Is.Not.Null);
        Assert.That(controllerType, Is.Not.Null);
        Assert.That(profileType.IsSubclassOf(typeof(ScriptableObject)), Is.True);
        Assert.That(controllerType.IsSubclassOf(typeof(MonoBehaviour)), Is.True);
        Assert.That(controllerType.GetMethod("ApplyProfile", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
    }

    [Test]
    public void ControllerAppliesProfileToMainLightAndRenderSettings()
    {
        Type profileType = FindRuntimeType("StylizedLightingProfile");
        Type controllerType = FindRuntimeType("StylizedLightingController");

        Assert.That(profileType, Is.Not.Null);
        Assert.That(controllerType, Is.Not.Null);

        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        GameObject lightObject = new GameObject("Stylized Test Sun");
        GameObject controllerObject = new GameObject("Stylized Lighting Test");
        Light light = lightObject.AddComponent<Light>();
        Component controller = controllerObject.AddComponent(controllerType);

        AmbientMode oldAmbientMode = RenderSettings.ambientMode;
        Color oldSky = RenderSettings.ambientSkyColor;
        Color oldEquator = RenderSettings.ambientEquatorColor;
        Color oldGround = RenderSettings.ambientGroundColor;
        bool oldFog = RenderSettings.fog;
        FogMode oldFogMode = RenderSettings.fogMode;
        Color oldFogColor = RenderSettings.fogColor;
        float oldFogStart = RenderSettings.fogStartDistance;
        float oldFogEnd = RenderSettings.fogEndDistance;
        float oldFogDensity = RenderSettings.fogDensity;

        try
        {
            light.type = LightType.Directional;
            SetSerialized(profile, "mainLightColor", new Color(1f, 0.78f, 0.45f, 1f));
            SetSerialized(profile, "mainLightIntensity", 1.35f);
            SetSerialized(profile, "mainLightEulerAngles", new Vector3(46f, -32f, 0f));
            SetSerialized(profile, "ambientSkyColor", new Color(0.58f, 0.72f, 0.88f, 1f));
            SetSerialized(profile, "ambientEquatorColor", new Color(0.36f, 0.42f, 0.48f, 1f));
            SetSerialized(profile, "ambientGroundColor", new Color(0.24f, 0.22f, 0.2f, 1f));
            SetSerialized(profile, "fogEnabled", true);
            SetSerialized(profile, "fogColor", new Color(0.62f, 0.74f, 0.82f, 1f));
            SetSerialized(profile, "fogStartDistance", 18f);
            SetSerialized(profile, "fogEndDistance", 75f);

            SetSerialized(controller, "profile", profile);
            SetSerialized(controller, "mainLight", light);

            controllerType.GetMethod("ApplyProfile", BindingFlags.Instance | BindingFlags.Public)?.Invoke(controller, null);

            Assert.That(light.color, Is.EqualTo(new Color(1f, 0.78f, 0.45f, 1f)));
            Assert.That(light.intensity, Is.EqualTo(1.35f).Within(0.001f));
            Assert.That(light.shadows, Is.EqualTo(LightShadows.Soft));
            Assert.That(RenderSettings.sun, Is.EqualTo(light));
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Trilight));
            Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(new Color(0.58f, 0.72f, 0.88f, 1f)));
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(RenderSettings.fogColor, Is.EqualTo(new Color(0.62f, 0.74f, 0.82f, 1f)));
            Assert.That(RenderSettings.fogStartDistance, Is.EqualTo(18f).Within(0.001f));
            Assert.That(RenderSettings.fogEndDistance, Is.EqualTo(75f).Within(0.001f));
        }
        finally
        {
            RenderSettings.ambientMode = oldAmbientMode;
            RenderSettings.ambientSkyColor = oldSky;
            RenderSettings.ambientEquatorColor = oldEquator;
            RenderSettings.ambientGroundColor = oldGround;
            RenderSettings.fog = oldFog;
            RenderSettings.fogMode = oldFogMode;
            RenderSettings.fogColor = oldFogColor;
            RenderSettings.fogStartDistance = oldFogStart;
            RenderSettings.fogEndDistance = oldFogEnd;
            RenderSettings.fogDensity = oldFogDensity;
            UnityEngine.Object.DestroyImmediate(controllerObject);
            UnityEngine.Object.DestroyImmediate(lightObject);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void SceneHasStylizedLightingControllerAndProfile()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Type controllerType = FindRuntimeType("StylizedLightingController");
        UnityEngine.Object profileAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ProfilePath);
        GameObject lightingObject = GameObject.Find("StylizedLighting");

        Assert.That(controllerType, Is.Not.Null);
        Assert.That(profileAsset, Is.Not.Null);
        Assert.That(lightingObject, Is.Not.Null);

        Component controller = lightingObject.GetComponent(controllerType);
        Assert.That(controller, Is.Not.Null);

        SerializedObject serializedController = new SerializedObject(controller);
        SerializedProperty profileProperty = serializedController.FindProperty("profile");
        SerializedProperty lightProperty = serializedController.FindProperty("mainLight");

        Assert.That(profileProperty, Is.Not.Null);
        Assert.That(lightProperty, Is.Not.Null);
        Assert.That(profileProperty.objectReferenceValue, Is.EqualTo(profileAsset));
        Assert.That(lightProperty.objectReferenceValue, Is.Not.Null);
    }

    private static Type FindRuntimeType(string typeName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName))
            .FirstOrDefault(type => type != null);
    }

    private static void SetSerialized(UnityEngine.Object target, string propertyName, object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Assert.That(property, Is.Not.Null, propertyName);

        switch (property.propertyType)
        {
            case SerializedPropertyType.Boolean:
                property.boolValue = (bool)value;
                break;
            case SerializedPropertyType.Float:
                property.floatValue = (float)value;
                break;
            case SerializedPropertyType.Color:
                property.colorValue = (Color)value;
                break;
            case SerializedPropertyType.Vector3:
                property.vector3Value = (Vector3)value;
                break;
            case SerializedPropertyType.ObjectReference:
                property.objectReferenceValue = (UnityEngine.Object)value;
                break;
            default:
                Assert.Fail($"Unsupported serialized property type for {propertyName}: {property.propertyType}");
                break;
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
