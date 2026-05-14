using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NetworkPlayerStateDebugPanel : MonoBehaviour
{
    [SerializeField] private bool showPanel = true;
    [SerializeField] private Rect panelRect = new Rect(12f, 124f, 520f, 180f);

    private readonly List<NetworkPlayerState> players = new();

    private void OnGUI()
    {
        if (!showPanel || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            return;
        }

        players.Clear();
        players.AddRange(FindObjectsByType<NetworkPlayerState>(
            FindObjectsInactive.Include));
        players.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));

        GUILayout.BeginArea(panelRect, GUI.skin.box);
        GUILayout.Label("Player State Sync  |  F5 Damage  F6 Contaminate  F7 Stamina  F8 Reset");

        foreach (NetworkPlayerState player in players)
        {
            GUILayout.Label(
                $"{player.DisplayName} id={player.PlayerId} " +
                $"hp={player.Health:0} st={player.Stamina:0} contam={player.Contamination:0} " +
                $"alive={player.IsAlive} interacting={player.IsInteracting} state={player.ActivityState}");
        }

        GUILayout.EndArea();
    }
}
