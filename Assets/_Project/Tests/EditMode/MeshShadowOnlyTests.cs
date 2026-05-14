using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class MeshShadowOnlyTests
{
    private const string ScenePath = "Assets/_Project/Scenes/Dev/SCN_Dev_Locomotion.unity";

    [Test]
    public void SceneDoesNotUseFakePlayerShadowObjects()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Assert.That(GameObject.Find("Player/Player_GroundShadow"), Is.Null);
        Assert.That(GameObject.Find("Player_GroundShadow"), Is.Null);
        Assert.That(GameObject.Find("Player")?.GetComponent("PlayerGroundShadow"), Is.Null);
    }

    [Test]
    public void PlayerMeshCastsAndReceivesRealShadows()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject playerMeshObject = GameObject.Find("Player/CHR_Ch36_nonPBR");
        Assert.That(playerMeshObject, Is.Not.Null);

        SkinnedMeshRenderer renderer = playerMeshObject.GetComponent<SkinnedMeshRenderer>();
        Assert.That(renderer, Is.Not.Null);
        Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
        Assert.That(renderer.receiveShadows, Is.True);
    }

    [Test]
    public void MainDirectionalLightCastsRealShadows()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Light mainLight = GameObject.Find("Directional Light")?.GetComponent<Light>();
        Assert.That(mainLight, Is.Not.Null);
        Assert.That(mainLight.type, Is.EqualTo(LightType.Directional));
        Assert.That(mainLight.shadows, Is.Not.EqualTo(LightShadows.None));
        Assert.That(mainLight.shadowStrength, Is.GreaterThanOrEqualTo(0.6f));
    }

    [Test]
    public void FakeShadowAssetsAreRemovedFromProject()
    {
        Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/_Project/Scripts/Rendering/PlayerGroundShadow.cs"), Is.Null);
        Assert.That(AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Art/Common/Shaders/BlobShadow.shader"), Is.Null);
        Assert.That(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Common/Materials/PlayerBlobShadow.mat"), Is.Null);
    }
}
