using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkManager))]
public sealed class NetworkDevBootstrap : MonoBehaviour
{
    [SerializeField] private string address = "127.0.0.1";
    [SerializeField] private ushort port = 7777;
    [SerializeField] private string serverListenAddress = "0.0.0.0";
    [SerializeField] private bool showRuntimeControls = true;

    private NetworkManager networkManager;
    private UnityTransport unityTransport;

    private void Awake()
    {
        CacheReferences();
        ConfigureDirectTransport();
    }

    private void Update()
    {
        if (networkManager == null)
        {
            return;
        }

        if (WasPressedThisFrame(DevNetworkCommand.StartHost))
        {
            StartHost();
        }
        else if (WasPressedThisFrame(DevNetworkCommand.StartClient))
        {
            StartClient();
        }
        else if (WasPressedThisFrame(DevNetworkCommand.StartServer))
        {
            StartServer();
        }
        else if (WasPressedThisFrame(DevNetworkCommand.Shutdown))
        {
            Shutdown();
        }
    }

    private void OnGUI()
    {
        if (!showRuntimeControls || networkManager == null)
        {
            return;
        }

        string state = networkManager.IsListening
            ? $"Listening Host={networkManager.IsHost} Server={networkManager.IsServer} Client={networkManager.IsClient} LocalId={networkManager.LocalClientId}"
            : "Not listening";

        GUI.Label(
            new Rect(12f, 12f, 640f, 72f),
            $"Dev Multiplayer\nF1 Host  F2 Client  F3 Server  F4 Shutdown\n{state}");
    }

    public void StartHost()
    {
        if (!CanStart())
        {
            return;
        }

        ConfigureDirectTransport();
        networkManager.StartHost();
    }

    public void StartClient()
    {
        if (!CanStart())
        {
            return;
        }

        ConfigureDirectTransport();
        networkManager.StartClient();
    }

    public void StartServer()
    {
        if (!CanStart())
        {
            return;
        }

        ConfigureDirectTransport();
        networkManager.StartServer();
    }

    public void Shutdown()
    {
        CacheReferences();
        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
        }
    }

    private bool CanStart()
    {
        CacheReferences();
        return networkManager != null && unityTransport != null && !networkManager.IsListening;
    }

    private void CacheReferences()
    {
        if (networkManager == null)
        {
            networkManager = GetComponent<NetworkManager>();
        }

        if (unityTransport == null)
        {
            unityTransport = GetComponent<UnityTransport>();
        }
    }

    private void ConfigureDirectTransport()
    {
        CacheReferences();
        if (networkManager == null || unityTransport == null)
        {
            return;
        }

        unityTransport.SetConnectionData(address, port, serverListenAddress);
        networkManager.NetworkConfig.NetworkTransport = unityTransport;
    }

    private bool WasPressedThisFrame(DevNetworkCommand command)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            return command switch
            {
                DevNetworkCommand.StartHost => keyboard.f1Key.wasPressedThisFrame,
                DevNetworkCommand.StartClient => keyboard.f2Key.wasPressedThisFrame,
                DevNetworkCommand.StartServer => keyboard.f3Key.wasPressedThisFrame,
                DevNetworkCommand.Shutdown => keyboard.f4Key.wasPressedThisFrame,
                _ => false
            };
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return command switch
        {
            DevNetworkCommand.StartHost => Input.GetKeyDown(KeyCode.F1),
            DevNetworkCommand.StartClient => Input.GetKeyDown(KeyCode.F2),
            DevNetworkCommand.StartServer => Input.GetKeyDown(KeyCode.F3),
            DevNetworkCommand.Shutdown => Input.GetKeyDown(KeyCode.F4),
            _ => false
        };
#else
        return false;
#endif
    }

    private enum DevNetworkCommand
    {
        StartHost,
        StartClient,
        StartServer,
        Shutdown
    }
}
