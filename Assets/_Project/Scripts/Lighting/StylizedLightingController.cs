using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class StylizedLightingController : MonoBehaviour
{
    [SerializeField] private StylizedLightingProfile profile;
    [SerializeField] private Light mainLight;
    [SerializeField] private Volume targetVolume;
    [SerializeField] private bool applyOnEnable = true;

    public StylizedLightingProfile Profile => profile;
    public Light MainLight => mainLight;
    public Volume TargetVolume => targetVolume;

    public void ApplyProfile()
    {
        if (profile == null)
        {
            return;
        }

        EnsureMainLight();
        ApplyMainLight();
        ApplyAmbient();
        ApplyFog();
        ApplyVolume();
    }

    private void OnEnable()
    {
        if (applyOnEnable)
        {
            ApplyProfile();
        }
    }

    private void OnValidate()
    {
        if (applyOnEnable && isActiveAndEnabled)
        {
            ApplyProfile();
        }
    }

    private void EnsureMainLight()
    {
        if (mainLight != null)
        {
            return;
        }

        if (RenderSettings.sun != null)
        {
            mainLight = RenderSettings.sun;
            return;
        }

        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        foreach (Light light in lights)
        {
            if (light != null && light.type == LightType.Directional)
            {
                mainLight = light;
                return;
            }
        }
    }

    private void ApplyMainLight()
    {
        if (mainLight == null)
        {
            return;
        }

        mainLight.type = LightType.Directional;
        mainLight.color = profile.MainLightColor;
        mainLight.intensity = profile.MainLightIntensity;
        mainLight.shadows = profile.MainLightShadows;
        mainLight.shadowStrength = profile.MainLightShadowStrength;
        mainLight.transform.rotation = Quaternion.Euler(profile.MainLightEulerAngles);
        RenderSettings.sun = mainLight;
    }

    private void ApplyAmbient()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = profile.AmbientSkyColor;
        RenderSettings.ambientEquatorColor = profile.AmbientEquatorColor;
        RenderSettings.ambientGroundColor = profile.AmbientGroundColor;
    }

    private void ApplyFog()
    {
        RenderSettings.fog = profile.FogEnabled;
        RenderSettings.fogMode = profile.FogMode;
        RenderSettings.fogColor = profile.FogColor;
        RenderSettings.fogStartDistance = profile.FogStartDistance;
        RenderSettings.fogEndDistance = profile.FogEndDistance;
        RenderSettings.fogDensity = profile.FogDensity;
    }

    private void ApplyVolume()
    {
        if (targetVolume == null || profile.VolumeProfile == null)
        {
            return;
        }

        targetVolume.isGlobal = true;
        targetVolume.sharedProfile = profile.VolumeProfile;
        targetVolume.weight = profile.VolumeWeight;
    }
}
