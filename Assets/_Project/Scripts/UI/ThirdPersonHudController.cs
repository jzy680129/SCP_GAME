using UnityEngine;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class ThirdPersonHudController : MonoBehaviour
{
    private const string ReticleElementName = "ReticleRoot";
    private const string TargetClassName = "reticle--target";

    [Header("Reticle")]
    [SerializeField] private bool showReticle = true;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private float aimDistance = 120f;
    [SerializeField] private LayerMask aimLayers = ~0;

    private UIDocument document;
    private VisualElement reticleRoot;
    private Vector3 aimPoint;
    private bool hasAimHit;

    public Vector3 AimPoint => aimPoint;
    public bool HasAimHit => hasAimHit;

    public void SetAimCamera(Camera camera)
    {
        aimCamera = camera;
    }

    public void SetReticleVisible(bool visible)
    {
        showReticle = visible;
        ApplyReticleVisibility();
    }

    private void Awake()
    {
        document = GetComponent<UIDocument>();
        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }
    }

    private void OnEnable()
    {
        CacheElements();
        ApplyReticleVisibility();
    }

    private void LateUpdate()
    {
        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }

        UpdateAimPoint();
        UpdateReticleState();
    }

    private void CacheElements()
    {
        if (document == null)
        {
            document = GetComponent<UIDocument>();
        }

        reticleRoot = document != null && document.rootVisualElement != null
            ? document.rootVisualElement.Q<VisualElement>(ReticleElementName)
            : null;
    }

    private void ApplyReticleVisibility()
    {
        if (reticleRoot == null)
        {
            CacheElements();
        }

        if (reticleRoot != null)
        {
            reticleRoot.style.display = showReticle ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void UpdateAimPoint()
    {
        if (aimCamera == null)
        {
            hasAimHit = false;
            aimPoint = transform.position + transform.forward * aimDistance;
            return;
        }

        Ray aimRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(aimRay, out RaycastHit hit, aimDistance, aimLayers, QueryTriggerInteraction.Ignore))
        {
            hasAimHit = true;
            aimPoint = hit.point;
            return;
        }

        hasAimHit = false;
        aimPoint = aimRay.origin + aimRay.direction * aimDistance;
    }

    private void UpdateReticleState()
    {
        if (reticleRoot == null)
        {
            CacheElements();
        }

        if (reticleRoot != null)
        {
            reticleRoot.EnableInClassList(TargetClassName, hasAimHit);
        }
    }
}
