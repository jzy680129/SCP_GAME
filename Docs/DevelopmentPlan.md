# Development Plan

## Project Promise

A 4-player cooperative anomaly-zone survival/building game: prepare at base, enter a contaminated zone, gather resources, build temporary safety infrastructure, learn anomaly rules, finish an objective, extract, then research upgrades for deeper runs.

## Core Loop

1. Prepare gear, tools, and mission objective.
2. Enter a contaminated zone with up to 4 players.
3. Explore, gather, and identify the local anomaly rule.
4. Build functional structures for safety, light, purification, storage, and routing.
5. Complete containment, sampling, rescue, or extraction objectives.
6. Withdraw and convert findings into base research/upgrades.

## Design Pillars

- Cooperative survival: players should benefit from dividing scouting, gathering, building, defense, and rescue roles.
- Rule-based anomalies: flagship threats should change how the area works, not behave only like combat enemies.
- Functional building: structures must solve survival, containment, or extraction problems before they become decoration.

## MVP Scope

The first playable milestone is a 10-15 minute vertical slice:

- Local/Relay-ready Netcode foundation for up to 4 players.
- One small dev zone.
- Spawned network player prefab with local-only input, camera, and HUD ownership.
- Basic interaction ray/range.
- Two resource types.
- Three buildable pieces: floor/foundation, wall, purifier or safety light.
- Contamination value and one simple anomaly rule.
- One mission objective and extraction result.

## Milestones

### M1 - Multiplayer Foundation

Goal: all later gameplay can run in a shared session.

Exit criteria:
- Host and client can enter the same scene.
- Each client controls only its own spawned player.
- Remote players do not read local input or steal camera/HUD.
- Player transform and animation state synchronize well enough for prototype testing.

### M2 - Interaction And Resources

Goal: players can affect shared world objects.

Exit criteria:
- Owner-local interaction targeting.
- Server-authoritative gather action.
- Shared resource node state.
- Minimal inventory/resource count.

### M3 - Building Prototype

Goal: resources can become useful world structures.

Exit criteria:
- Placement preview.
- Valid/invalid placement checks.
- Server-spawned building objects.
- Resource cost validation.

### M4 - Contamination And First Anomaly

Goal: the zone has pressure beyond normal combat.

Exit criteria:
- Per-player contamination value.
- Area contamination sources.
- A purifier/safety-light countermeasure.
- One anomaly rule that changes player behavior.

### M5 - Mission And Extraction

Goal: a full run has a beginning, objective, and end.

Exit criteria:
- Mission start state.
- Objective completion state.
- Extraction zone.
- Success/failure result and reward placeholder.

## Explicitly Deferred

- Large open world.
- Dedicated server build.
- Procedural map generation.
- Full Lobby/Relay UI.
- Complex save system.
- Full base-management economy.
- Large enemy roster.
- Final art pass.
