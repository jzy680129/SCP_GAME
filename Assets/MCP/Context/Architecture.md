# Project Architecture

## Current Shape

The project is a Unity 6 URP prototype for a 4-player cooperative
survival/building game. Multiplayer is built on Netcode for GameObjects with
Unity Transport.

## Core Runtime Areas

- `Assets/_Project/Scripts/Networking`: NetworkManager setup, dev host/client
  controls, spawned player ownership, player state sync, and Netcode helpers.
- `Assets/_Project/Scripts/Camera`: third-person camera and orbit target input.
- `Assets/_Project/Scripts/Animation`: locomotion and action state handling.
- `Assets/_Project/Scripts/Gameplay`: shared gameplay systems as they are added.
- `Assets/_Project/Scripts/UI`: HUD and runtime interface logic.

## Scene Contract

- Dev scene: `Assets/_Project/Scenes/Dev/SCN_Dev_Locomotion.unity`.
- Multiplayer bootstrap object: `NetworkBootstrap`.
- Network player prefab: `Assets/_Project/Prefabs/Player/PF_NetworkPlayer.prefab`.
- Netcode generated prefab list: `Assets/DefaultNetworkPrefabs.asset`.

## Design Direction

Use server-authoritative gameplay for shared world state. Clients submit intent;
the host/server validates and applies authoritative results.
