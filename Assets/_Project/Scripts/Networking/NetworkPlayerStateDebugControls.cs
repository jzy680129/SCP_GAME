using Unity.Netcode;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkPlayerState))]
public sealed class NetworkPlayerStateDebugControls : NetworkBehaviour
{
    [SerializeField] private bool enableDevHotkeys = true;
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] private float contaminationAmount = 10f;
    [SerializeField] private float staminaCost = 20f;

    private NetworkPlayerState playerState;

    private void Awake()
    {
        CacheState();
    }

    private void Update()
    {
        if (!enableDevHotkeys || !IsSpawned || !IsOwner)
        {
            return;
        }

        if (WasPressedThisFrame(DebugCommand.Damage))
        {
            ApplyDebugDamageServerRpc(damageAmount);
        }
        else if (WasPressedThisFrame(DebugCommand.Contaminate))
        {
            AddDebugContaminationServerRpc(contaminationAmount);
        }
        else if (WasPressedThisFrame(DebugCommand.ConsumeStamina))
        {
            ConsumeDebugStaminaServerRpc(staminaCost);
        }
        else if (WasPressedThisFrame(DebugCommand.Reset))
        {
            ResetDebugStateServerRpc();
        }
    }

    [ServerRpc]
    private void ApplyDebugDamageServerRpc(float amount)
    {
        if (CacheState())
        {
            playerState.ApplyDamageServer(amount);
        }
    }

    [ServerRpc]
    private void AddDebugContaminationServerRpc(float amount)
    {
        if (CacheState())
        {
            playerState.AddContaminationServer(amount);
        }
    }

    [ServerRpc]
    private void ConsumeDebugStaminaServerRpc(float amount)
    {
        if (CacheState())
        {
            playerState.ConsumeStaminaServer(amount);
        }
    }

    [ServerRpc]
    private void ResetDebugStateServerRpc()
    {
        if (CacheState())
        {
            playerState.ResetStateServer();
        }
    }

    private bool CacheState()
    {
        if (playerState == null)
        {
            playerState = GetComponent<NetworkPlayerState>();
        }

        return playerState != null;
    }

    private bool WasPressedThisFrame(DebugCommand command)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            return command switch
            {
                DebugCommand.Damage => keyboard.f5Key.wasPressedThisFrame,
                DebugCommand.Contaminate => keyboard.f6Key.wasPressedThisFrame,
                DebugCommand.ConsumeStamina => keyboard.f7Key.wasPressedThisFrame,
                DebugCommand.Reset => keyboard.f8Key.wasPressedThisFrame,
                _ => false
            };
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return command switch
        {
            DebugCommand.Damage => Input.GetKeyDown(KeyCode.F5),
            DebugCommand.Contaminate => Input.GetKeyDown(KeyCode.F6),
            DebugCommand.ConsumeStamina => Input.GetKeyDown(KeyCode.F7),
            DebugCommand.Reset => Input.GetKeyDown(KeyCode.F8),
            _ => false
        };
#else
        return false;
#endif
    }

    private enum DebugCommand
    {
        Damage,
        Contaminate,
        ConsumeStamina,
        Reset
    }
}
