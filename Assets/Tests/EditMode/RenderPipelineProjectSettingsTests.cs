using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;

public sealed class RenderPipelineProjectSettingsTests
{
    private const string UrpAssetPath = "Assets/Settings/Settings/PC_RPAsset.asset";

    [Test]
    public void ProjectUsesPcUniversalRenderPipelineAsset()
    {
        RenderPipelineAsset urpAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(UrpAssetPath);

        Assert.That(urpAsset, Is.Not.Null);
        Assert.That(urpAsset.GetType().FullName, Does.Contain("UniversalRenderPipelineAsset"));
        Assert.That(GraphicsSettings.defaultRenderPipeline, Is.EqualTo(urpAsset));
        Assert.That(GraphicsSettings.currentRenderPipeline, Is.Not.Null);
        Assert.That(GraphicsSettings.currentRenderPipeline.GetType().FullName, Does.Contain("UniversalRenderPipelineAsset"));
    }
}
