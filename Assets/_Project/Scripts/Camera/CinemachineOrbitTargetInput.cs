using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class CinemachineOrbitTargetInput : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Transform followPoint;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.55f, 0f);
    [SerializeField] private float distance = 4.8f;
    [SerializeField] private float shoulderOffset = 0.25f;

    [Header("Orbit")]
    [SerializeField] private float minPitch = 5f;
    [SerializeField] private float maxPitch = 65f;
    [SerializeField] private float initialPitch = 18f;
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private bool lockCursorOnPlay = true;

    [Header("Collision Guard")]
    [SerializeField] private bool clampFollowPointHeight = true;
    [SerializeField] private float minFollowPointHeight = 0.75f;

    [Header("Follow Smoothing")]
    [SerializeField] private float followPositionSmoothTime = 0.12f;
    [SerializeField] private float followPointSmoothTime = 0.06f;

    [Header("Recentering")]
    [SerializeField] private bool recenterBehindMovingTarget = false;
    [SerializeField] private bool blockRecenteringOnBackInput = true;
    [SerializeField] private float recenterDelay = 0.75f;
    [SerializeField] private float recenterSharpness = 2.5f;
    [SerializeField] private float recenterMinSpeed = 0.35f;
    [SerializeField] private float backInputBlockThreshold = -0.2f;

    private float yaw;
    private float pitch;
    private bool initialized;
    private bool cursorReleasedByUser;
    private float timeSinceManualLook;
    private Vector3 previousTargetPosition;
    private bool hasPreviousTargetPosition;
    private ThirdPersonLocomotionController locomotionController;
    private Vector3 rigPositionVelocity;
    private Vector3 followPointVelocity;

    public Transform FollowTarget => followTarget;
    public Transform FollowPoint => followPoint;

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
        locomotionController = followTarget != null
            ? followTarget.GetComponent<ThirdPersonLocomotionController>()
            : null;
        RecenterBehindTarget();
        SnapToTarget();
    }

    public void SetFollowPoint(Transform point)
    {
        followPoint = point;
        SnapToTarget();
    }

    public void RecenterBehindTarget()
    {
        yaw = followTarget != null ? followTarget.eulerAngles.y : transform.eulerAngles.y;
        pitch = Mathf.Clamp(initialPitch, minPitch, maxPitch);
        initialized = true;
    }

    public void SnapToTarget()
    {
        if (followTarget == null)
        {
            return;
        }

        if (locomotionController == null)
        {
            locomotionController = followTarget.GetComponent<ThirdPersonLocomotionController>();
        }

        if (!initialized)
        {
            RecenterBehindTarget();
        }

        ApplyRigTransform(true);
        rigPositionVelocity = Vector3.zero;
        followPointVelocity = Vector3.zero;
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
        if (followTarget == null)
        {
            return;
        }

        if (!initialized)
        {
            RecenterBehindTarget();
        }

        UpdateCursorLock();
        UpdateOrbitFromInput();
        UpdateRecentering();
        ApplyRigTransform(false);
        previousTargetPosition = followTarget.position;
        hasPreviousTargetPosition = true;
    }

    private void ApplyRigTransform(bool instant)
    {
        Vector3 desiredPosition = followTarget.position + targetOffset;
        Quaternion desiredRotation = Quaternion.Euler(pitch, yaw, 0f);

        bool smoothFollow = Application.isPlaying && !instant && followPositionSmoothTime > 0f;
        if (smoothFollow)
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref rigPositionVelocity,
                followPositionSmoothTime);
            transform.rotation = desiredRotation;
        }
        else
        {
            transform.SetPositionAndRotation(desiredPosition, desiredRotation);
            rigPositionVelocity = Vector3.zero;
        }

        if (followPoint != null)
        {
            Vector3 desiredFollowLocalPosition = new Vector3(shoulderOffset, 0f, -distance);
            bool smoothFollowPoint = Application.isPlaying && !instant && followPointSmoothTime > 0f;
            followPoint.localPosition = smoothFollowPoint
                ? Vector3.SmoothDamp(
                    followPoint.localPosition,
                    desiredFollowLocalPosition,
                    ref followPointVelocity,
                    followPointSmoothTime)
                : desiredFollowLocalPosition;
            followPoint.localRotation = Quaternion.identity;

            if (clampFollowPointHeight)
            {
                Vector3 followPosition = followPoint.position;
                float minimumHeight = followTarget.position.y + minFollowPointHeight;
                if (followPosition.y < minimumHeight)
                {
                    followPosition.y = minimumHeight;
                    followPoint.position = followPosition;
                }
            }

            if (!smoothFollowPoint)
            {
                followPointVelocity = Vector3.zero;
            }
        }
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

        Cursor.lockState = cursorReleasedByUser ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = cursorReleasedByUser;
    }

    private void UpdateOrbitFromInput()
    {
        Vector2 lookInput = ReadLookInput();
        if (lookInput.sqrMagnitude < 0.0001f)
        {
            timeSinceManualLook += Time.deltaTime;
            return;
        }

        timeSinceManualLook = 0f;
        yaw += lookInput.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - lookInput.y * mouseSensitivity, minPitch, maxPitch);
    }

    private void UpdateRecentering()
    {
        if (!recenterBehindMovingTarget || followTarget == null || !hasPreviousTargetPosition)
        {
            return;
        }

        if (ShouldBlockRecenteringForInput())
        {
            return;
        }

        if (timeSinceManualLook < recenterDelay || Time.deltaTime <= 0f)
        {
            return;
        }

        Vector3 planarDelta = followTarget.position - previousTargetPosition;
        planarDelta.y = 0f;
        float targetSpeed = planarDelta.magnitude / Time.deltaTime;
        if (targetSpeed < recenterMinSpeed)
        {
            return;
        }

        float targetYaw = followTarget.eulerAngles.y;
        float recenterT = 1f - Mathf.Exp(-recenterSharpness * Time.deltaTime);
        yaw = Mathf.LerpAngle(yaw, targetYaw, recenterT);
    }

    private bool ShouldBlockRecenteringForInput()
    {
        return blockRecenteringOnBackInput
            && locomotionController != null
            && locomotionController.MoveInput.y <= backInputBlockThreshold;
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
}
