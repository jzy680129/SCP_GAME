using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(
    fileName = "STY_Lighting_Profile",
    menuName = "Rendering/Stylized Lighting Profile")]
public sealed class StylizedLightingProfile : ScriptableObject
{
    [Header("Main Light")]
    [SerializeField] private Color mainLightColor = new Color(1f, 0.82f, 0.55f, 1f);
    [SerializeField, Min(0f)] private float mainLightIntensity = 1.25f;
    [SerializeField] private Vector3 mainLightEulerAngles = new Vector3(48f, -35f, 0f);
    [SerializeField] private LightShadows mainLightShadows = LightShadows.Soft;
    [SerializeField, Range(0f, 1f)] private float mainLightShadowStrength = 0.82f;

    [Header("Ambient")]
    [SerializeField] private Color ambientSkyColor = new Color(0.52f, 0.68f, 0.86f, 1f);
    [SerializeField] private Color ambientEquatorColor = new Color(0.37f, 0.42f, 0.5f, 1f);
    [SerializeField] private Color ambientGroundColor = new Color(0.2f, 0.19f, 0.18f, 1f);

    [Header("Fog")]
    [SerializeField] private bool fogEnabled = true;
    [SerializeField] private FogMode fogMode = FogMode.Linear;
    [SerializeField] private Color fogColor = new Color(0.58f, 0.69f, 0.78f, 1f);
    [SerializeField, Min(0f)] private float fogStartDistance = 22f;
    [SerializeField, Min(0.01f)] private float fogEndDistance = 90f;
    [SerializeField, Min(0f)] private float fogDensity = 0.01f;

    [Header("Post Process")]
    [SerializeField] private VolumeProfile volumeProfile;
    [SerializeField, Range(0f, 1f)] private float volumeWeight = 1f;

    public Color MainLightColor => mainLightColor;
    public float MainLightIntensity => mainLightIntensity;
    public Vector3 MainLightEulerAngles => mainLightEulerAngles;
    public LightShadows MainLightShadows => mainLightShadows;
    public float MainLightShadowStrength => mainLightShadowStrength;
    public Color AmbientSkyColor => ambientSkyColor;
    public Color AmbientEquatorColor => ambientEquatorColor;
    public Color AmbientGroundColor => ambientGroundColor;
    public bool FogEnabled => fogEnabled;
    public FogMode FogMode => fogMode;
    public Color FogColor => fogColor;
    public float FogStartDistance => fogStartDistance;
    public float FogEndDistance => fogEndDistance;
    public float FogDensity => fogDensity;
    public VolumeProfile VolumeProfile => volumeProfile;
    public float VolumeWeight => volumeWeight;

    private void OnValidate()
    {
        mainLightIntensity = Mathf.Max(0f, mainLightIntensity);
        fogStartDistance = Mathf.Max(0f, fogStartDistance);
        fogEndDistance = Mathf.Max(fogStartDistance + 0.01f, fogEndDistance);
        fogDensity = Mathf.Max(0f, fogDensity);
    }
}
