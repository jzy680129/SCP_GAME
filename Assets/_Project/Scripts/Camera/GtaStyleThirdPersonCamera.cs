using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class GtaStyleThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.45f, 0f);

    [Header("Orbit")]
    [SerializeField] private float distance = 4.8f;
    [SerializeField] private float minPitch = -25f;
    [SerializeField] private float maxPitch = 65f;
    [SerializeField] private float initialPitch = 18f;
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float positionSmoothTime = 0.06f;
    [SerializeField] private float rotationSharpness = 18f;
    [SerializeField] private bool lockCursorOnPlay = true;

    [Header("Collision")]
    [SerializeField] private bool avoidObstacles = true;
    [SerializeField] private float collisionPadding = 0.25f;
    [SerializeField] private LayerMask obstacleLayers = ~0;

    private Vector3 smoothVelocity;
    private float yaw;
    private float pitch;
    private bool initialized;
    private bool cursorReleasedByUser;

    public Transform Target => target;

    public void SetTarget(Transform followTarget)
    {
        target = followTarget;
        RecenterBehindTarget();
        SnapToTarget();
    }

    public void RecenterBehindTarget()
    {
        if (target == null)
        {
            return;
        }

        yaw = target.eulerAngles.y;
        pitch = Mathf.Clamp(initialPitch, minPitch, maxPitch);
        initialized = true;
    }

    public void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        if (!initialized)
        {
            RecenterBehindTarget();
        }

        Vector3 focusPoint = GetFocusPoint();
        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = ResolveCameraPosition(focusPoint, orbitRotation);

        transform.SetPositionAndRotation(
            desiredPosition,
            Quaternion.LookRotation(focusPoint - desiredPosition, Vector3.up));
        smoothVelocity = Vector3.zero;
    }

    private void OnEnable()
    {
        if (!initialized)
        {
            RecenterBehindTarget();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        if (!initialized)
        {
            RecenterBehindTarget();
        }

        UpdateCursorLock();
        UpdateOrbitFromInput();

        Vector3 focusPoint = GetFocusPoint();
        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = ResolveCameraPosition(focusPoint, orbitRotation);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref smoothVelocity,
            positionSmoothTime);

        Quaternion desiredRotation = Quaternion.LookRotation(focusPoint - transform.position, Vector3.up);
        float rotationT = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationT);
    }

    private void UpdateCursorLock()
    {
        if (!Application.isPlaying || !lockCursorOnPlay)
        {
            return;
        }

        if (WasUnlockCursorPressed())
        {
            cursorReleasedByUser = true;
        }
        else if (WasLockCursorPressed())
        {
            cursorReleasedByUser = false;
        }

        if (cursorReleasedByUser)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UpdateOrbitFromInput()
    {
        Vector2 lookInput = ReadLookInput();
        if (lookInput.sqrMagnitude < 0.0001f)
        {
            return;
        }

        yaw += lookInput.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - lookInput.y * mouseSensitivity, minPitch, maxPitch);
    }

    private Vector2 ReadLookInput()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            return mouse.delta.ReadValue();
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
#else
        return Vector2.zero;
#endif
    }

    private bool WasUnlockCursorPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            return keyboard.escapeKey.wasPressedThisFrame;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    private bool WasLockCursorPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            return mouse.leftButton.wasPressedThisFrame;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    private Vector3 GetFocusPoint()
    {
        return target.position + targetOffset;
    }

    private Vector3 ResolveCameraPosition(Vector3 focusPoint, Quaternion orbitRotation)
    {
        Vector3 desiredPosition = focusPoint - orbitRotation * Vector3.forward * distance;
        if (!avoidObstacles)
        {
            return desiredPosition;
        }

        Vector3 ray = desiredPosition - focusPoint;
        float rayDistance = ray.magnitude;
        if (rayDistance <= 0.01f)
        {
            return desiredPosition;
        }

        Vector3 direction = ray / rayDistance;
        if (Physics.SphereCast(
                focusPoint,
                collisionPadding,
                direction,
                out RaycastHit hit,
                rayDistance,
                obstacleLayers,
                QueryTriggerInteraction.Ignore))
        {
            return focusPoint + direction * Mathf.Max(0.1f, hit.distance - collisionPadding);
        }

        return desiredPosition;
    }
}
