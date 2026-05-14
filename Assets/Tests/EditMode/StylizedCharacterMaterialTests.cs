using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class StylizedCharacterMaterialTests
{
    private const string ScenePath = "Assets/1.unity";
    private const string ShaderPath = "Assets/Res/Shaders/StylizedLit.shader";
    private const string MaterialPath = "Assets/Res/Materials/PlayerStylizedLit.mat";
    private const string PlayerRendererPath = "Player/CHR_Ch36_nonPBR";

    [Test]
    public void StylizedLitShaderExposesArtistControls()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

        Assert.That(shader, Is.Not.Null);
        Assert.That(shader.name, Is.EqualTo("Project/StylizedLit"));
        Assert.That(shader.FindPropertyIndex("_BaseColor"), Is.GreaterThanOrEqualTo(0));
        Assert.That(shader.FindPropertyIndex("_ShadowColor"), Is.GreaterThanOrEqualTo(0));
        Assert.That(shader.FindPropertyIndex("_HighlightColor"), Is.GreaterThanOrEqualTo(0));
        Assert.That(shader.FindPropertyIndex("_RimColor"), Is.GreaterThanOrEqualTo(0));
        Assert.That(shader.FindPropertyIndex("_RampThreshold"), Is.GreaterThanOrEqualTo(0));
        Assert.That(shader.FindPropertyIndex("_RampSoftness"), Is.GreaterThanOrEqualTo(0));
        Assert.That(shader.FindPropertyIndex("_RimPower"), Is.GreaterThanOrEqualTo(0));
        Assert.That(shader.FindPropertyIndex("_SpecularStrength"), Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void PlayerStylizedMaterialUsesReadableWarmCoolPalette()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        Assert.That(material, Is.Not.Null);
        Assert.That(material.shader.name, Is.EqualTo("Project/StylizedLit"));
        Assert.That(material.GetColor("_ShadowColor").b, Is.GreaterThan(material.GetColor("_ShadowColor").r));
        Assert.That(material.GetColor("_HighlightColor").r, Is.GreaterThan(material.GetColor("_HighlightColor").b));
        Assert.That(material.GetFloat("_RampThreshold"), Is.InRange(0.35f, 0.65f));
        Assert.That(material.GetFloat("_RampSoftness"), Is.InRange(0.03f, 0.18f));
        Assert.That(material.GetFloat("_RimPower"), Is.GreaterThan(1.5f));
        Assert.That(material.GetFloat("_SpecularStrength"), Is.InRange(0.05f, 0.35f));
    }

    [Test]
    public void ScenePlayerRendererUsesPersistentStylizedMaterial()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        GameObject rendererObject = GameObject.Find(PlayerRendererPath);

        Assert.That(material, Is.Not.Null);
        Assert.That(rendererObject, Is.Not.Null);

        SkinnedMeshRenderer renderer = rendererObject.GetComponent<SkinnedMeshRenderer>();
        Assert.That(renderer, Is.Not.Null);
        Assert.That(renderer.sharedMaterials, Has.Length.GreaterThan(0));
        Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterials[0]), Is.EqualTo(MaterialPath));
    }

    [Test]
    public void UntexturedStylizedMaterialRendersVisibleOpaqueColor()
    {
        Material sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Assert.That(sourceMaterial, Is.Not.Null);

        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        GameObject cameraObject = new GameObject("Stylized Material Probe Camera");
        GameObject lightObject = new GameObject("Stylized Material Probe Light");
        Material material = new Material(sourceMaterial);
        RenderTexture renderTexture = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32);
        Texture2D readback = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        RenderTexture oldActive = RenderTexture.active;

        try
        {
            capsule.layer = 30;
            capsule.transform.position = Vector3.zero;
            capsule.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);
            capsule.GetComponent<Renderer>().sharedMaterial = material;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -5f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 30;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20f;
            camera.targetTexture = renderTexture;

            camera.Render();
            RenderTexture.active = renderTexture;
            readback.ReadPixels(new Rect(32, 32, 1, 1), 0, 0);
            readback.Apply();

            Color center = readback.GetPixel(0, 0);
            float visibleLuminance = center.r * 0.2126f + center.g * 0.7152f + center.b * 0.0722f;

            Assert.That(center.a, Is.GreaterThan(0.95f));
            Assert.That(visibleLuminance, Is.GreaterThan(0.15f));
        }
        finally
        {
            Camera camera = cameraObject.GetComponent<Camera>();
            if (camera != null)
            {
                camera.targetTexture = null;
            }

            RenderTexture.active = oldActive;
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(readback);
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(lightObject);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(capsule);
        }
    }

    [Test]
    public void StylizedMaterialSamplesBaseMapTexture()
    {
        Material sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Assert.That(sourceMaterial, Is.Not.Null);

        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        GameObject cameraObject = new GameObject("Stylized Texture Probe Camera");
        GameObject lightObject = new GameObject("Stylized Texture Probe Light");
        Material material = new Material(sourceMaterial);
        Texture2D redTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        RenderTexture renderTexture = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32);
        Texture2D readback = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        RenderTexture oldActive = RenderTexture.active;

        try
        {
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.red;
            }

            redTexture.SetPixels(pixels);
            redTexture.Apply();

            material.SetTexture("_BaseMap", redTexture);
            material.SetColor("_BaseColor", Color.white);
            capsule.layer = 30;
            capsule.transform.position = Vector3.zero;
            capsule.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);
            capsule.GetComponent<Renderer>().sharedMaterial = material;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -5f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 30;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20f;
            camera.targetTexture = renderTexture;

            camera.Render();
            RenderTexture.active = renderTexture;
            readback.ReadPixels(new Rect(32, 32, 1, 1), 0, 0);
            readback.Apply();

            Color center = readback.GetPixel(0, 0);

            Assert.That(center.a, Is.GreaterThan(0.95f));
            Assert.That(center.r, Is.GreaterThan(center.g + 0.15f));
            Assert.That(center.r, Is.GreaterThan(center.b + 0.15f));
        }
        finally
        {
            Camera camera = cameraObject.GetComponent<Camera>();
            if (camera != null)
            {
                camera.targetTexture = null;
            }

            RenderTexture.active = oldActive;
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(readback);
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(redTexture);
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(lightObject);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(capsule);
        }
    }
}
