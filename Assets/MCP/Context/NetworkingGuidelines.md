# Networking Guidelines

## Stack

- Framework: Netcode for GameObjects.
- Transport: Unity Transport.
- Session services: Authentication, Lobby, Relay.
- Maximum target: 4 cooperative players.

## Authority Model

- Default to server-authoritative gameplay.
- Clients may read local input, but networked gameplay must expose explicit
  intent APIs.
- Server validates movement-sensitive interactions, gathering, inventory,
  crafting, building placement, damage, contamination, and persistence.
- Do not put final authority in UI, animation controllers, or local-only visual
  scripts.

## Synchronization Rules

- Use NetworkObjects only for objects requiring network identity.
- Prefer NetworkVariables for low-frequency shared state.
- Prefer RPCs for validated intent and discrete events.
- Avoid per-frame RPCs; keep per-frame visuals local unless they affect
  authoritative gameplay.

## Current Player State

`NetworkPlayerState` synchronizes display name, health, stamina, contamination,
alive/interacting flags, and activity state. Owner input can request activity
state changes; final state remains server-written.
