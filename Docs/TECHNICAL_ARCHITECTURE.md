# House Escape Technical Architecture

## Status And Scope

This document defines mandatory implementation boundaries for House Escape: Lights Out.
It complements `Docs/GDD.md`; it does not replace game-design decisions.

Current baseline:

- Unity `6000.3.10f1`.
- Universal Render Pipeline `17.3.0`.
- Input System `1.18.0`.
- Netcode for GameObjects `1.11.0`.
- AI Navigation `2.0.10`.
- Unity Test Framework `1.6.0`.

Do not update these dependencies as a side effect of feature work.

## Architectural Goals

1. Gameplay rules are testable without scenes whenever practical.
2. Online and local split-screen use the same gameplay model.
3. NGO authority is explicit and cannot be bypassed by presentation code.
4. A feature can evolve without direct knowledge of unrelated UI, audio or scene objects.
5. Modularity reduces coupling without adding interfaces or event buses that have no real
   boundary.

## Module Model

New production code should move incrementally under `Assets/HouseEscape/`. Do not create
empty modules in advance.

| Module | Responsibility | May depend on |
|---|---|---|
| `Core` | Pure rules, values, state machines and contracts | Standard C# only where feasible |
| `Gameplay` | Player, generators, items, hiding, monster and match use cases | Core |
| `Networking` | NGO replication, RPC validation, spawning and connection mapping | Core, Gameplay, NGO |
| `LocalMultiplayer` | Local participant joining, paired devices, cameras and viewports | Core, Gameplay, Input System |
| `Presentation` | HUD, menus, audio, effects and animation adapters | Core and read-only Gameplay state |
| `Infrastructure` | Unity scene composition, persistence and platform adapters | Other modules only at composition boundaries |
| `Tests` | EditMode and PlayMode verification | The module under test |

Use assembly definitions when a module receives production code. Keep dependency direction
acyclic. Runtime assemblies must not reference test or editor assemblies.

## Composition And Dependencies

- A scene composition root wires serialized Unity adapters to gameplay services.
- Prefer constructor injection for plain C# and explicit initialization or serialized
  references for `MonoBehaviour` adapters.
- Use an interface only at a genuine boundary, for multiple implementations, or to isolate
  nondeterministic Unity/network services in tests.
- `ScriptableObject` assets contain tunable immutable configuration. They are not global
  mutable state or service locators.
- C# events may report state changes to known subscribers. Do not introduce a global event
  bus by default.
- Avoid `FindObjectOfType`, `FindAnyObjectByType`, `FindObjectsOfType`, `Camera.main` and
  global singleton lookup as dependency resolution.
- Keep one public `MonoBehaviour` or `ScriptableObject` per matching file.

## NGO Authority

The server owns all consequential state:

- Match clock and in-game hour.
- Light-cycle state, random phase duration and transition timestamp.
- Monster simulation, target selection, movement and attacks.
- Generator availability, attempt lifecycle and activation winner.
- Items, health, energy, flashlight battery, incapacitation and revival.
- Score and match outcome.

Clients send intentions, not final outcomes. An RPC must validate sender ownership,
participant identity, current match state, range, cooldown, required item and any relevant
timing. Network variables use explicit read/write permissions.

Do not create `NetworkManager` dynamically. Configure `NetworkManager`, `UnityTransport`,
network prefabs and scene management in a bootstrap scene/prefab. Network simulation code
must guard execution with the correct `IsServer`, `IsClient` and `IsOwner` conditions.

## Participant Identity And Split-screen

NGO's `ClientId` identifies a network connection, not necessarily one human. Introduce a
stable `ParticipantId` for every player. A connection owns one or more participant records,
bounded by the global four-player match limit.

```text
Console / NGO connection
    +-- Local participant A -> gamepad A -> avatar A -> camera/HUD A
    +-- Local participant B -> gamepad B -> avatar B -> camera/HUD B
```

- The server maintains `ParticipantId -> OwnerClientId` registration.
- The server spawns one network avatar per participant.
- An input command identifies its participant; the server verifies that the RPC sender owns
  that participant.
- `PlayerInput`/`PlayerInputManager` pairs a specific gamepad with each local participant.
- Gameplay code consumes an input abstraction or commands. It never reads
  `Gamepad.current`, `Keyboard.current`, `Mouse.current` or legacy `UnityEngine.Input`.
- Each local participant receives an explicit camera, viewport, HUD, EventSystem/UI input
  context and effects target.
- Only local presentation listens to that participant's input. Remote avatars never enable
  local input or cameras.
- Multiple `AudioListener` components cannot remain active simultaneously. Use one listener
  strategy and a deliberate local audio mix.
- Pausing an online match is local UI state; it must not set global `Time.timeScale` to zero.

Approved local flow:

- Support up to four local participants on one console.
- Start/Menu joins an unpaired gamepad.
- Player 1 confirms match start with the south face button after at least one join.
- Joining closes when the match starts.
- A disconnected gamepad reserves its participant and slot rather than transferring control.
- Camera layouts use full screen for one, equal vertical halves for two, and an equal 2x2
  grid for three or four participants.

## Generator Race

1. The server authorizes an attempt while lights are on and the generator is inactive.
2. Each participant runs their own minigame presentation.
3. Completion sends the attempt ID and validation result to the server.
4. The server validates the attempt and performs a single atomic inactive-to-active
   transition.
5. The first valid transition wins. All later completions receive an already-completed
   result and cannot award duplicate score.

Do not use last-write-wins or trust a client-provided completion time without validation.

## Match Clock And Light Cycle

- The authoritative duration is 1,200 seconds.
- One in-game hour equals 200 real seconds.
- Derive displayed in-game time from normalized authoritative elapsed time.
- The server samples light duration ranges and replicates state plus a server-relative end
  timestamp. Clients derive countdown UI locally from synchronized network time.
- Do not replicate a countdown value every frame.
- Crossing 6 AM ends the match regardless of the current light phase.

## Testing Requirements

EditMode tests cover pure rules and state machines, including:

- 1,200-second clock conversion and hour boundaries.
- Light-phase range selection by time bracket.
- Atomic generator winner selection.
- Health, energy, inventory and revival rules.
- Participant registration and ownership validation.

PlayMode or multiplayer tests cover:

- Host and client spawning.
- Two local participants sharing one NGO connection.
- Paired gamepads routing input only to their assigned player.
- Per-player cameras and HUD binding.
- Server-only monster and match simulation.
- Disconnect, reconnect and scene transition behaviour.

Every bug fix should add the smallest regression test practical at the layer where the bug
occurred.

## Current Prototype Migration

The existing `Assets/Scripts` code is a prototype, not the target architecture. Migrate by
vertical slice rather than rewriting everything at once:

1. Establish assemblies, bootstrap composition and test infrastructure.
2. Implement the authoritative match clock and light state as the first tested slice.
3. Implement participant identity, local join, gamepad pairing and per-player presentation.
4. Configure NGO transport and spawn one avatar per participant.
5. Implement one complete networked generator race.
6. Add the sleeping/waking monster state machine.
7. Migrate remaining player, item, hiding, UI and scoring systems feature by feature.

Do not extend known prototype shortcuts merely to preserve them. Preserve user-created
assets and serialized data, and document any migration that can break Inspector references.
