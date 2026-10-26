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

- Victory: all six generators are active and every conscious player has escaped through the
  front door. Players who escape leave the house and spectate; the match ends in victory once
  no conscious player remains inside and at least one player escaped. Unconscious players
  still inside at that moment do not escape.
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
- The right shoulder button (`RB` on Xbox-style layouts or `R1` on PlayStation-style
  layouts) toggles the flashlight. It cannot be turned on with an empty battery and turns
  off automatically when the battery empties or the player becomes unconscious.
- Every player starts with a full flashlight battery.
- A charging station is used like a generator: within 2 metres, aim at it and hold the
  primary south face button. The battery recharges while the button is held and the player
  cannot walk, but may look. Releasing keeps the charge gained so far. Charging is
  available whenever the match is active, in light or darkness.
- A flashlight battery item adds 50% battery, capped at 100%.

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

Energy rules:

- Every player starts the match with 100 energy and 100 health.
- Holding the left stick button (L3) runs at 1.6x walking speed. Energy speed penalties
  apply on top of the running multiplier.
- A player counts as injured while their health is below 50.
- Energy drains only while the player is conscious and the match is active.
- From 10-29% energy the camera field of view is reduced from 70 to 60 degrees.
- Below 10% energy the player cannot run and their camera sways (local visual only).

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

Inventory and pickup rules:

- Each player carries an unlimited count of each item type.
- D-pad left/right selects the item type; the west face button (`X` on Xbox-style layouts or
  Square on PlayStation-style layouts) consumes one of the selected item instantly. The HUD
  shows the selected type and its count.
- Health and energy gains are capped at 100.
- A map item is picked up instantly by pressing the primary south face button while within
  2 metres of it and aiming the camera at it. The south face button acts on whatever the
  player is aiming at (generator, item or unconscious teammate).
- Finding a map item awards +25 points; starting items award nothing.

### 3.5 Generators

| Attribute | Value |
|---|---|
| Total | 6 |
| Activation | Hold the primary south face button continuously for exactly 10 seconds |
| Placement | One per main room, including basement and garage |

Activation rules:

1. Any player can approach and attempt a generator.
2. The activation minigame requires holding the primary south face button (`A` on
   Xbox-style layouts or Cross on PlayStation-style layouts) continuously for exactly ten
   seconds.
3. The activation minigame is available only while the house lights are on.
4. An attempt starts only when the player is within 2 metres of an inactive generator and
   is aiming their camera at it when pressing the primary south face button. Once started,
   the player may look away without canceling; moving beyond 2 metres is not possible
   because walking is blocked.
5. The player cannot walk while actively solving the minigame, but may continue to look.
6. Disconnecting the gamepad during an attempt counts as releasing the button: the attempt
   is canceled and its progress resets. The participant remains reserved as described in
   the local split-screen rules.
7. Releasing the button before ten seconds cancels the attempt and resets its progress.
8. Turning the lights off cancels every in-progress attempt and resets its progress.
9. Multiple players may attempt the same inactive generator simultaneously.
10. The first player to validly finish the minigame activates the generator.
11. Completion is resolved atomically by the authoritative NGO server. Later completion
    attempts have no effect.
12. The winning participant receives 200 points exactly once.
13. UI displays global progress, for example `Generators: 3/6`.

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
- A player enters by standing within 2 metres, aiming at the hiding place and pressing the
  primary south face button; a chest requires holding the button for two seconds, during
  which the player cannot walk and releasing cancels. Pressing the south face button again
  while hidden leaves.
- Entering turns the flashlight off. A hidden player may still look around, and their
  avatar is not visible to others.
- Under a bed: if, when the player enters, the monster is awake and detecting them (within
  8 metres with line of sight), the bed does not protect them and they remain a valid target.
- When the maximum time runs out the player is forced out, with the normal one second of
  vulnerability and the ten-second cooldown.
- During the second of vulnerability after leaving, the player cannot walk or act.
- The +75 points for successfully using a hiding place are awarded when the player leaves
  conscious after the hiding place made the monster lose them: the monster had them as its
  target when they became hidden and the hiding place protected them. At most once per use.

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

Detection and targeting rules:

- A player is detected while the lights are on when they are within the 8-metre detection
  radius in any direction and there is an unobstructed line of sight between the monster
  and the player. Hidden players are never valid targets.
- The monster targets the nearest detected player and switches to another detected player
  whenever that player becomes nearer than the current target.
- While Searching with no detected player, the monster wanders between random reachable
  points of the house until it detects a player.
- The 15-second maximum pursuit applies to the current target. When it expires the monster
  drops that target and cannot select the same player again for five seconds; it may select
  any other detected player immediately. The pursuit timer restarts whenever the target
  changes to a different player. Exclusions are cleared when the monster falls asleep.
- A target that becomes hidden stops being valid immediately and is dropped.
- While Losing interest, the monster moves at chase speed to the target's last detected
  position. If it does not detect the target again within five seconds, it resumes
  Searching.
- Attack range is 1.5 metres. The first attack lands as soon as a detected target is in
  range; afterwards the monster attacks at most once every two seconds, shared across all
  targets.
- While Searching, the monster wanders at normal player walking speed.
- The monster sleeps before the match starts and after it ends.

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
- Revival starts when a conscious player carrying a first-aid kit is within 2 metres of the
  unconscious teammate, aims at them and holds the primary south face button. The reviver
  cannot walk while holding. Releasing the button, the reviver taking damage or becoming
  unconscious, or the teammate's 30-second window expiring cancels the revival.
- The first-aid kit is consumed only when the revival completes. The revived player returns
  to consciousness with 40 health and the reviver earns +150 points.
- If not revived within 30 seconds, the player is permanently eliminated.
- If every player is permanently eliminated, the match ends in defeat.
- In solo play, becoming unconscious leads to permanent elimination when the recovery
  window expires because no teammate can revive the player.
- An unconscious player cannot walk or act; any in-progress generator attempt is canceled.
  They may still turn their camera. The monster never targets unconscious players.
- A permanently eliminated player's avatar is removed. Their view spectates a living
  teammate, and the primary south face button cycles to another living teammate.
- The monster's four-player speed bonus applies only while four conscious players remain.
- In online play, when a console's network connection is lost, every participant from that
  console becomes permanently eliminated. Mid-match reconnection is not available.
- Local audio is heard from the first living local participant on the console. If no local
  participant is alive, it is heard from the view that Player 1 is spectating.

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

- Online services for lobby, relay, authentication and proximity voice transport.
- Platform targets among PlayStation, Xbox and Nintendo hardware.

Agents must ask before choosing any unresolved option that changes player-facing behaviour.
