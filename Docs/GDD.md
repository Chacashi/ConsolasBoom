# HOUSE ESCAPE: LIGHTS OUT

## Game Design Document

**Version:** 1.1  
**Updated:** 2026-09-13  
**Status:** Canonical design source

## 1. Game Summary

| Field | Definition |
|---|---|
| Name | House Escape: Lights Out |
| Genre | Survival Horror / Cooperative Multiplayer |
| Platform | Consoles only |
| Players | 1-4 players, online and local split-screen |
| Target match duration | 20 real minutes to reach 6 AM; a victory can end earlier |
| Perspective | First-person 3D |
| Networking | Netcode for GameObjects (NGO) |
| Input | Gamepad only in the shipped game |

### Concept

A group of friends is trapped in a haunted house. One to four players must activate all
generators and reach the front door before 6 AM. The monster is dangerous only while the
house lights are on. In darkness, it becomes completely blind and sleeps in place. Players
must balance generator progress, hiding and limited flashlight battery.

## 2. Main Objective

Activate the six generators distributed throughout the house and escape through the front
door before 6 AM.

- Victory: all six generators are active and at least one surviving player escapes.
- Defeat: 6 AM is reached before the escape condition, or all players are permanently
  eliminated.
- A solo player can complete and win a match.

## 3. Main Mechanics

### 3.1 Light And Darkness

| State | Behaviour |
|---|---|
| Lights off | Safe exploration period. The monster is completely blind and sleeps without changing position. Visibility is reduced. |
| Lights on | Danger period. The monster wakes and hunts. Generator minigames may be started and completed. |

Rules:

- The match begins with the house lights off.
- The server chooses each phase duration from the range for the current time bracket.
- A visible countdown shows the time remaining until the next light change.
- An audiovisual warning occurs three seconds before a change.
- Phases become shorter as 6 AM approaches.
- The authoritative match clock continues independently of light phases. At 6 AM, the
  match ends even if the current phase still had time remaining.

#### Match Clock Calculation

The interval from 12 AM to 6 AM contains six in-game hours and lasts exactly 20 real
minutes when the time limit is reached.

```text
20 real minutes = 1,200 real seconds
1,200 / 6 in-game hours = 200 real seconds per in-game hour
200 real seconds = 3 minutes 20 seconds per in-game hour
200 / 60 = 3.333333 real seconds per in-game minute
```

| In-game time | Real elapsed time | Real duration |
|---|---:|---:|
| 12 AM - 1 AM | 00:00 - 03:20 | 03:20 |
| 1 AM - 2 AM | 03:20 - 06:40 | 03:20 |
| 2 AM - 3 AM | 06:40 - 10:00 | 03:20 |
| 3 AM - 4 AM | 10:00 - 13:20 | 03:20 |
| 4 AM - 5 AM | 13:20 - 16:40 | 03:20 |
| 5 AM - 6 AM | 16:40 - 20:00 | 03:20 |

#### Light Cycle Table

The following random ranges preserve the accelerating pressure curve. They do not alter
the 20-minute authoritative match clock.

| In-game bracket | Real bracket | Lights on | Lights off |
|---|---:|---:|---:|
| 12 AM - 2 AM | 00:00 - 06:40 | 50-60 s | 80-90 s |
| 2 AM - 4 AM | 06:40 - 13:20 | 35-45 s | 50-60 s |
| 4 AM - 5 AM | 13:20 - 16:40 | 25-30 s | 35-45 s |
| 5 AM - 6 AM | 16:40 - 20:00 | 15-20 s | 25-30 s |

### 3.2 Flashlight

| Attribute | Value |
|---|---:|
| Maximum battery | 100% |
| Consumption | 5% per second |
| Continuous use from full | 20 seconds |
| Full recharge time | 45 seconds at a charging station |
| Recharge rate | Approximately 2.2% per second |

Rules:

- The flashlight illuminates a frontal cone of approximately three metres.
- It does not affect or stun the monster.
- It helps reveal generator puzzles, items and paths in darkness.
- Three or four charging stations are distributed throughout the house.
- A player using a charging station is vulnerable if the lights turn on.

### 3.3 Health And Energy

| Resource | Maximum | Purpose |
|---|---:|---|
| Health | 100 | At zero, the player becomes unconscious. |
| Energy | 100 | Controls movement speed and ability to act. |

| Energy level | Effect |
|---|---|
| 60-100% | Normal speed |
| 30-59% | Speed reduced by 20% |
| 10-29% | Speed reduced by 40% and reduced FOV |
| 0-9% | Speed reduced by 60% and player stagger |

Energy consumption:

- Running: 2% per second.
- While injured: 1% per second.
- Passive: 0.5% per minute.

### 3.4 Food And Items

| Item | Health | Energy | Map quantity |
|---|---:|---:|---:|
| Apple | +15 | +10 | 8-10 |
| Sandwich | +25 | +20 | 5-6 |
| Energy drink | 0 | +40 | 3-4 |
| First-aid kit | +40 | 0 | 2-3 |
| Flashlight battery | 0 | 0; +50% flashlight battery | 4-5 |

Spawn rules:

- Items use random valid positions at the beginning of each match.
- Some are exposed and others are in closed rooms.
- Stronger healing items appear near generators to reinforce risk and reward.

### 3.5 Generators

| Attribute | Value |
|---|---|
| Total | 6 |
| Activation | A 10-15 second minigame |
| Placement | One per main room, including basement and garage |

Activation rules:

1. Any player can approach and attempt a generator.
2. The activation minigame is available only while the house lights are on.
3. The player cannot move while actively solving the minigame.
4. Multiple players may attempt the same inactive generator simultaneously.
5. The first player to validly finish the minigame activates the generator.
6. Completion is resolved atomically by the authoritative NGO server. Later completion
   attempts have no effect.
7. UI displays global progress, for example `Generators: 3/6`.

Suggested locations:

1. Main room.
2. Kitchen.
3. Main bedroom.
4. Attic.
5. Basement.
6. Garage.

### 3.6 Hiding Places

| Type | Quantity | Maximum time | Restriction |
|---|---:|---:|---|
| Wardrobe | 4-5 | 30 s | None |
| Under a bed | 3-4 | 20 s | Works only if the monster did not see the player enter |
| Chest | 2-3 | 45 s | Requires two seconds to enter |
| Small closet | 2-3 | 15 s | Last resort |

Rules:

- A hidden player cannot move or use the flashlight.
- The monster may detect a player by sound at very short range while awake.
- Leaving a hiding place causes one second of vulnerability.
- Hiding has a ten-second cooldown.
- Only one player may occupy a hiding place.

## 4. Monster

### 4.1 Behaviour

| State | Behaviour |
|---|---|
| Lights off | Sleeps exactly where it is, remains completely blind, does not patrol, detect, chase or attack. |
| Lights on | Wakes, acquires a valid visible target and hunts at maximum speed. |

Turning the lights off immediately ends active hunting and makes the monster sleep in its
current position. It does not disappear or relocate.

### 4.2 Statistics

| Attribute | Value |
|---|---:|
| Chase speed | 1.2x normal player speed |
| Detection radius | 8 metres while lights are on |
| Attack damage | 30 health |
| Attack frequency | Every 2 seconds while in valid attack range |
| Maximum pursuit | 15 seconds, unless darkness interrupts it first |

With four active players, monster chase speed increases by 15%.

### 4.3 AI States

- Sleeping: mandatory state in darkness; movement and perception are disabled.
- Searching: selects a valid target after waking.
- Chasing: follows the selected target while the target remains valid.
- Attacking: attacks only while awake and in range.
- Losing interest: loses its target after five seconds without a valid detection, or
  immediately when the house enters darkness.

## 5. House Map

Main zones:

| Zone | Risk | Items | Hiding places |
|---|---|---|---|
| Main room | Medium | Food | One wardrobe |
| Kitchen | Low | Food, batteries | None |
| Main bedroom | High | First-aid kit | One bed |
| Bedroom 2 | Medium | Food | One wardrobe |
| Main hallway | High | None | One wardrobe |
| Attic | Very high | Flashlight battery | One chest |
| Basement | Very high | All types | One closet and one chest |
| Garage | Medium | Flashlight battery | One wardrobe |

## 6. Match Flow

```text
Character cosmetic selection
        |
Spawn in main room at 12 AM
        |
Lights off: explore, collect and plan
        |
Three-second light warning
        |
Lights on: generator minigames and monster hunt
        |
Lights off: monster sleeps in its current position
        |
Repeat until escape, total elimination or 6 AM
```

### Score

| Action | Points |
|---|---:|
| Activate a generator | +200 |
| Survive one lights-on phase | +50 |
| Successfully use a hiding place | +75 |
| Find an item | +25 |
| Revive a teammate | +150 |
| Escape | +500 |

## 7. Players

All players have the same capabilities. Starting supplies vary by assigned spawn slot:

| Slot | Starting items |
|---|---|
| Player 1 | Flashlight and two apples |
| Player 2 | Flashlight and one first-aid kit |
| Player 3 | Flashlight and one spare battery |
| Player 4 | Flashlight and one sandwich |

### Communication And Controls

- The shipped game is controlled entirely with a gamepad.
- Proximity voice chat has a ten-metre audible range.
- Players can place gamepad-controlled markers for generators, items, hiding places and
  the monster's last known position.
- There is no global keyboard text chat.
- Menus, minigames, interaction, markers and pause flows must all be navigable by gamepad.

### Local Player Join

- One console supports one to four local players within the global four-player limit.
- An unpaired gamepad joins by pressing its Start/Menu button.
- After at least one participant joins, Player 1 starts the match with the primary south
  face button (`A` on Xbox-style layouts or Cross on PlayStation-style layouts).
- Joining closes when the match starts; late local join is not available during a match.
- If a gamepad disconnects, its participant and local slot remain reserved while the local
  HUD requests reconnection. Control is not transferred automatically.
- Viewports prioritize equal play area: one player uses the full screen, two use equal
  vertical halves, and three or four use an equal 2x2 grid (one quadrant remains unused for
  three players).

### Incapacitation And Recovery

- At zero health, a player becomes unconscious rather than immediately eliminated.
- Another player can revive them in ten seconds by consuming a first-aid kit.
- If not revived within 30 seconds, the player is permanently eliminated.
- If every player is permanently eliminated, the match ends in defeat.
- In solo play, becoming unconscious leads to permanent elimination when the recovery
  window expires because no teammate can revive the player.

## 8. UI And Accessibility

The HUD contains:

- Health and energy for its assigned local player.
- Flashlight battery.
- Current in-game time and match time remaining.
- Countdown to the next light transition.
- Global generator progress.
- Contextual, gamepad-specific interaction prompts.
- Local warnings for monster proximity and player condition.

Each split-screen participant has an independent camera, HUD, prompts and menu focus.
The HUD must not discover a player through global scene searches.

## 9. Multiplayer And Local Co-op Requirements

- Netcode for GameObjects is mandatory for networked simulation.
- Online play and local split-screen must work together. A single console connection may
  represent multiple local participants.
- The first playable milestone targets up to four local players. Online connections remain
  a future requirement and must not be blocked by local-only shortcuts.
- A participant identity is distinct from an NGO client connection ID.
- The server is authoritative over match time, light phases, monster state, generator
  winners, item state, health, incapacitation, revival, score and victory/defeat.
- Each local player owns a paired gamepad, local camera, local audio perspective and local
  HUD.
- Local presentation objects are not replicated merely because their gameplay avatar is.
- Network latency must not allow two players to activate the same generator. The server
  accepts only the first valid completion.

The implementation rules and module boundaries are defined in
`Docs/TECHNICAL_ARCHITECTURE.md`.

## 10. Open Design Decisions

These details require an explicit design decision before implementation:

- What happens to an in-progress generator minigame when the lights turn off.
- Exact minigame rules and server-side validation data.
- Whether all surviving players must reach the exit or one escape ends the match.
- Online services for lobby, relay, authentication and proximity voice transport.
- Platform targets among PlayStation, Xbox and Nintendo hardware.

Agents must ask before choosing any unresolved option that changes player-facing behaviour.
