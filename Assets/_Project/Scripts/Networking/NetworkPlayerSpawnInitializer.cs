using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkPlayerSpawnInitializer : NetworkBehaviour
{
    [SerializeField] private Vector3 origin = new Vector3(-1.5f, 0f, 0f);
    [SerializeField] private float spacing = 2.25f;
    [SerializeField] private int columns = 2;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            return;
        }

        int slot = (int)(OwnerClientId % 4);
        int column = columns <= 0 ? 0 : slot % columns;
        int row = columns <= 0 ? slot : slot / columns;
        Vector3 spawnPosition = origin + new Vector3(column * spacing, 0f, row * spacing);
        transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);
    }
}
