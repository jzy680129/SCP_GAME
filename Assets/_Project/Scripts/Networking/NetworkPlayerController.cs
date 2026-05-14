using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkPlayerState))]
public sealed class NetworkPlayerController : NetworkBehaviour
{
    [SerializeField] private ThirdPersonLocomotionController locomotionController;
    [SerializeField] private CharacterActionStateMachine legacyActionStateMachine;
    [SerializeField] private Animator animator;
    [SerializeField] private NetworkPlayerState playerState;

    private NetworkPlayerActivityState lastSentActivityState = NetworkPlayerActivityState.Idle;
    private bool hasSentActivityState;

    private void Update()
    {
        if (!IsSpawned || !IsOwner || playerState == null || !playerState.IsSpawned)
        {
            return;
        }

        SyncOwnedActivityState();
    }

    public override void OnNetworkSpawn()
    {
        CacheReferences();
        ApplyOwnershipState();
    }

    public override void OnGainedOwnership()
    {
        ApplyOwnershipState();
    }

    public override void OnLostOwnership()
    {
        ApplyOwnershipState();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            SetLocalPresentationEnabled(false);
        }
    }

    private void CacheReferences()
    {
        if (locomotionController == null)
        {
            locomotionController = GetComponent<ThirdPersonLocomotionController>();
        }

        if (legacyActionStateMachine == null)
        {
            legacyActionStateMachine = GetComponent<CharacterActionStateMachine>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (playerState == null)
        {
            playerState = GetComponent<NetworkPlayerState>();
        }
    }

    private void ApplyOwnershipState()
    {
        CacheReferences();

        bool isLocalPlayer = IsSpawned && IsOwner;
        gameObject.name = isLocalPlayer
            ? $"Player_Local_{OwnerClientId}"
            : $"Player_Remote_{OwnerClientId}";

        if (locomotionController != null)
        {
            locomotionController.SetLocalInputEnabled(isLocalPlayer);
            locomotionController.enabled = isLocalPlayer;
        }

        if (legacyActionStateMachine != null)
        {
            legacyActionStateMachine.enabled = isLocalPlayer;
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        if (isLocalPlayer)
        {
            BindLocalPresentation();
        }
    }

    private void SyncOwnedActivityState()
    {
        NetworkPlayerActivityState nextState = ResolveActivityState();
        if (hasSentActivityState && nextState == lastSentActivityState)
        {
            return;
        }

        playerState.RequestSetActivityStateServerRpc(nextState);
        lastSentActivityState = nextState;
        hasSentActivityState = true;
    }

    private NetworkPlayerActivityState ResolveActivityState()
    {
        if (playerState != null && !playerState.IsAlive)
        {
            return NetworkPlayerActivityState.Downed;
        }

        if (playerState != null && playerState.IsInteracting)
        {
            return NetworkPlayerActivityState.Interacting;
        }

        if (locomotionController != null && !locomotionController.IsGrounded)
        {
            return NetworkPlayerActivityState.Jumping;
        }

        return locomotionController != null && locomotionController.MoveInput.sqrMagnitude > 0.01f
            ? NetworkPlayerActivityState.Moving
            : NetworkPlayerActivityState.Idle;
    }

    private void BindLocalPresentation()
    {
        Camera mainCamera = Camera.main;
        Transform cameraTransform = mainCamera != null ? mainCamera.transform : null;

        if (mainCamera != null && mainCamera.TryGetComponent(out GtaStyleThirdPersonCamera gtaCamera))
        {
            gtaCamera.enabled = true;
            gtaCamera.SetTarget(transform);
        }

        CinemachineOrbitTargetInput orbitInput = FindAnyObjectByType<CinemachineOrbitTargetInput>();
        if (orbitInput != null)
        {
            orbitInput.enabled = true;
            orbitInput.SetFollowTarget(transform);
        }

        if (locomotionController != null)
        {
            locomotionController.SetCameraTransform(cameraTransform != null ? cameraTransform : transform);
        }

        ThirdPersonHudController hudController = FindAnyObjectByType<ThirdPersonHudController>();
        if (hudController != null)
        {
            hudController.enabled = true;
            hudController.SetAimCamera(mainCamera);
            hudController.SetReticleVisible(true);
        }
    }

    private void SetLocalPresentationEnabled(bool enabled)
    {
        ThirdPersonHudController hudController = FindAnyObjectByType<ThirdPersonHudController>();
        if (hudController != null)
        {
            hudController.SetReticleVisible(enabled);
        }
    }
}
