# House Escape Agent Rules

## Sources Of Truth

Read these before planning or editing gameplay:

1. `Docs/GDD.md` defines approved player-facing behaviour.
2. `Docs/TECHNICAL_ARCHITECTURE.md` defines technical boundaries and migration strategy.
3. Existing assets and code show the current implementation, not necessarily desired
   behaviour.

If code conflicts with the GDD, follow the GDD. If a requested change conflicts with either
canonical document, identify the conflict and ask the user before proceeding. Never invent
an unresolved game-design decision.

## Project Baseline

- Unity `6000.3.20f1`.
- Console-only game using gamepads.
- Input System `1.19.0`; legacy `UnityEngine.Input` is forbidden in new code.
- Netcode for GameObjects `2.13.0` is mandatory.
- Online co-op and local split-screen must work together for 1-4 total participants.
- The current `Assets/Scripts` implementation is prototype code and is mostly not wired to
  scenes or prefabs.

Do not update Unity, packages or generated project files unless explicitly requested.

## Engineering Rules

- Make the smallest complete vertical slice. Do not generate speculative frameworks.
- Keep gameplay rules in plain C# when they do not require Unity lifecycle or serialization.
- Keep `MonoBehaviour` classes as adapters, views or composition points where practical.
- Use explicit dependencies. Do not add service-locator access, mutable global state or new
  singleton dependencies.
- Do not use scene-wide object searches or `Camera.main` to resolve runtime dependencies.
- Introduce interfaces only at real boundaries or when needed for tests/multiple adapters.
- Use `ScriptableObject` for immutable tuning data, not as a global mutable state store.
- Keep one public `MonoBehaviour` or `ScriptableObject` in a same-named file.
- Add assembly definitions with actual modules, maintaining acyclic references.
- Unsubscribe events and dispose Input System resources symmetrically.
- Do not manually edit generated `Assets/PlayerControls.cs`. Edit
  `Assets/PlayerControls.inputactions` and regenerate it through Unity.
- Do not edit third-party code under `Assets/Plugins` or generated/cache folders such as
  `Library`, `Temp`, `Logs` and `obj`.

## Multiplayer And Split-screen

- Server authority is required for consequential gameplay state.
- A network connection is not a player. Use a participant identity distinct from NGO
  `ClientId` because one console may contain multiple local players.
- Validate RPC sender, participant ownership, match state, range, resources and timing.
- Use real NGO RPC attributes and explicit `NetworkVariable` permissions.
- Never dynamically add `NetworkManager`; configure transport, prefabs and scene management
  in the bootstrap composition.
- Only local participants enable input, cameras, HUD and local effects.
- Pair each local participant with a specific gamepad through `PlayerInput` infrastructure.
- Never read global devices such as `Gamepad.current`, `Keyboard.current` or `Mouse.current`
  in gameplay code.
- Do not pause an online simulation with global `Time.timeScale = 0`.
- Avoid per-frame network replication when state plus a network timestamp is sufficient.

## Verification

- Add EditMode tests for pure gameplay rules and PlayMode/multiplayer tests for Unity/NGO
  integration.
- Compile the affected assemblies and run relevant tests before reporting completion.
- If Unity cannot be run from the available environment, say exactly what remains to be
  validated in the Editor.
- Treat Console errors, missing serialized references and unregistered network prefabs as
  incomplete work.

## Current Risks

Do not silently build on these known prototype issues:

- There are two incompatible `PlayerController` classes.
- Some scripts use legacy or global keyboard/mouse input.
- NGO RPC-named methods are not actual RPCs and client writes violate server authority.
- There is no configured `NetworkManager`, transport bootstrap or network player prefab.
- There are no project assembly definitions or tests.
- `Game.unity` has no gameplay composition; `Menu.unity` is not in Build Settings.
- Current global HUD, camera, pause and audio assumptions are incompatible with split-screen.

Migrate these incrementally according to `Docs/TECHNICAL_ARCHITECTURE.md`; do not preserve a
shortcut solely because it already exists.
