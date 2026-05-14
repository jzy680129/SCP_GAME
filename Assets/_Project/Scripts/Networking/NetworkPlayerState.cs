using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public enum NetworkPlayerActivityState : byte
{
    Idle,
    Moving,
    Jumping,
    Interacting,
    Downed
}

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkPlayerState : NetworkBehaviour
{
    public const float MaxHealth = 100f;
    public const float MaxStamina = 100f;
    public const float MaxContamination = 100f;

    private readonly NetworkVariable<FixedString32Bytes> displayName = new(
        new FixedString32Bytes("Player"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> health = new(
        MaxHealth,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> stamina = new(
        MaxStamina,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> contamination = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isAlive = new(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isInteracting = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<NetworkPlayerActivityState> activityState = new(
        NetworkPlayerActivityState.Idle,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public ulong PlayerId => OwnerClientId;
    public string DisplayName => displayName.Value.ToString();
    public float Health => health.Value;
    public float Stamina => stamina.Value;
    public float Contamination => contamination.Value;
    public bool IsAlive => isAlive.Value;
    public bool IsInteracting => isInteracting.Value;
    public NetworkPlayerActivityState ActivityState => activityState.Value;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            ResetStateServer();
        }
    }

    [ServerRpc]
    public void RequestSetInteractingServerRpc(bool interacting)
    {
        SetInteractingServer(interacting);
    }

    [ServerRpc]
    public void RequestSetActivityStateServerRpc(NetworkPlayerActivityState requestedState)
    {
        SetActivityStateServer(requestedState);
    }

    [ServerRpc]
    public void RequestConsumeStaminaServerRpc(float amount)
    {
        ConsumeStaminaServer(amount);
    }

    public void ResetStateServer()
    {
        if (!IsServer)
        {
            return;
        }

        displayName.Value = new FixedString32Bytes($"Player {OwnerClientId}");
        health.Value = MaxHealth;
        stamina.Value = MaxStamina;
        contamination.Value = 0f;
        isAlive.Value = true;
        isInteracting.Value = false;
        activityState.Value = NetworkPlayerActivityState.Idle;
    }

    public void SetInteractingServer(bool interacting)
    {
        if (!IsServer || !isAlive.Value)
        {
            return;
        }

        isInteracting.Value = interacting;
        activityState.Value = interacting
            ? NetworkPlayerActivityState.Interacting
            : NetworkPlayerActivityState.Idle;
    }

    public void SetActivityStateServer(NetworkPlayerActivityState requestedState)
    {
        if (!IsServer)
        {
            return;
        }

        if (!isAlive.Value)
        {
            activityState.Value = NetworkPlayerActivityState.Downed;
            return;
        }

        if (isInteracting.Value)
        {
            activityState.Value = NetworkPlayerActivityState.Interacting;
            return;
        }

        activityState.Value = requestedState == NetworkPlayerActivityState.Downed ||
            requestedState == NetworkPlayerActivityState.Interacting
            ? NetworkPlayerActivityState.Idle
            : requestedState;
    }

    public bool ConsumeStaminaServer(float amount)
    {
        if (!IsServer || !isAlive.Value)
        {
            return false;
        }

        float cost = Mathf.Max(0f, amount);
        if (stamina.Value < cost)
        {
            return false;
        }

        stamina.Value = Mathf.Clamp(stamina.Value - cost, 0f, MaxStamina);
        return true;
    }

    public void RestoreStaminaServer(float amount)
    {
        if (!IsServer || !isAlive.Value)
        {
            return;
        }

        stamina.Value = Mathf.Clamp(stamina.Value + Mathf.Max(0f, amount), 0f, MaxStamina);
    }

    public void ApplyDamageServer(float amount)
    {
        if (!IsServer || !isAlive.Value)
        {
            return;
        }

        health.Value = Mathf.Clamp(health.Value - Mathf.Max(0f, amount), 0f, MaxHealth);
        if (health.Value > 0f)
        {
            return;
        }

        isAlive.Value = false;
        isInteracting.Value = false;
        activityState.Value = NetworkPlayerActivityState.Downed;
    }

    public void HealServer(float amount)
    {
        if (!IsServer || !isAlive.Value)
        {
            return;
        }

        health.Value = Mathf.Clamp(health.Value + Mathf.Max(0f, amount), 0f, MaxHealth);
    }

    public void AddContaminationServer(float amount)
    {
        if (!IsServer || !isAlive.Value)
        {
            return;
        }

        contamination.Value = Mathf.Clamp(
            contamination.Value + Mathf.Max(0f, amount),
            0f,
            MaxContamination);
    }

    public void ClearContaminationServer(float amount)
    {
        if (!IsServer || !isAlive.Value)
        {
            return;
        }

        contamination.Value = Mathf.Clamp(
            contamination.Value - Mathf.Max(0f, amount),
            0f,
            MaxContamination);
    }

    public void ReviveServer(float restoredHealth = 50f)
    {
        if (!IsServer)
        {
            return;
        }

        isAlive.Value = true;
        health.Value = Mathf.Clamp(restoredHealth, 1f, MaxHealth);
        stamina.Value = Mathf.Max(stamina.Value, MaxStamina * 0.35f);
        isInteracting.Value = false;
        activityState.Value = NetworkPlayerActivityState.Idle;
    }
}
