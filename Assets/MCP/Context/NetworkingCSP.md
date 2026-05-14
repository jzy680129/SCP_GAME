# Networking Security Boundaries

## Allowed Services

- Unity Authentication.
- Unity Lobby.
- Unity Relay.
- Local direct connection through Unity Transport for development.

## Validation Rules

- Treat all client gameplay requests as intent, not truth.
- Server validates range, cost, ownership, cooldowns, placement rules, damage,
  inventory changes, and contamination changes.
- Do not trust client UI state for gameplay decisions.
- Do not send secrets or service credentials through gameplay RPCs.

## Rate And Abuse Controls

- Keep high-frequency simulation local or server-side.
- Throttle repeated client intent RPCs when introducing interaction, combat,
  building, or inventory systems.
- Log unexpected or invalid client requests during development.
