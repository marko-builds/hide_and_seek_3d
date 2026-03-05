# Seeker AI

> **Status**: Draft
> **Author**: game-designer
> **Last Updated**: 2026-03-05
> **Implements Pillar**: The Room Has Rules (Pillar 1), Legible Jeopardy (Pillar 3)

---

## 1. Overview

The Seeker AI is the primary threat system in UNSEEN. Each seeker is a relentless, rule-governed patrol agent that moves through a dungeon chamber on a fixed route, escalating its response to player-generated stimuli through five discrete behavioral states: Unaware, Alert, Searching, Chase, and Caught. The seeker does not cheat — it reads only what its sensors physically detect, governed entirely by the Detection System's output. From the player's perspective, the seeker is a puzzle element with discoverable, consistent behavior: a tool to be understood, baited, and outmaneuvered rather than an opponent to be defeated. The Seeker AI exists because UNSEEN's tension is produced by the player's knowledge of the seeker's rules set against the seeker's relentless, mechanical execution of those rules — the seeker is always right, and the player must learn to be more right.

---

## 2. Player Fantasy

**What the player should feel:**

The seeker should feel like a clock with teeth. Not hostile in a personal sense — the seeker does not hate the player — but utterly indifferent to the player's survival in a way that makes it more frightening than malice. The player should feel the seeker is inevitable unless outsmarted, which means the player must feel they *can* outsmart it.

**Emotional arc over a session:**

1. **Discovery dread** (first encounter): The player sees the seeker's patrol and feels the space shrink. The seeker is large, purposeful, and unforgiving. Every sound feels dangerous. This is productive anxiety — the MDA aesthetic is *challenge* blended with *sensation*.

2. **Comprehension relief** (after 2-3 observations): The player begins to see the pattern. The patrol route resolves. The scan arc becomes predictable. This is the first competence signal — the player's knowledge is becoming power. SDT competence need activated.

3. **Masterful flow** (execution): The player threads the gap in the patrol, uses a thrown object to redirect the seeker mid-search, and escapes the chamber. This is the fantasy: not dominance over the seeker, but *understanding* deployed under pressure. The MDA aesthetic is *challenge* at its peak, tipping toward *expression*.

4. **Legible failure** (when caught): When caught, the player must immediately understand why — the seeker's sight cone was wider than expected, or a footstep noise was louder in the open corridor. Pillar 3 (Legible Jeopardy) demands this. Opaque death breaks the comprehension loop and replaces productive challenge with frustration.

**Connection to pillars:**

- Pillar 1 (The Room Has Rules): The seeker's behavior is a set of rules the player can discover. No randomness, no hidden exceptions. The player's growing model of the seeker is always accurate.
- Pillar 3 (Legible Jeopardy): The seeker's state is always readable from the world — posture, vocalization, movement speed, and visual indicators communicate every state transition without HUD text.

---

## 3. Detailed Design

### 3.1 State Machine Integration with DetectionOutput

The Seeker AI is a *responder*, not an *arbiter*. It does not independently decide when to chase or search — it receives a `DetectionOutput` struct from the Detection System each `FixedUpdate` and transitions its behavioral state to match the `RequestedState` field.

**State transition flow:**

```
DetectionOutput.RequestedState (from Detection System)
         |
         v
 SeekerStateMachine.EvaluateTransition()
         |
         +-- RequestedState matches CurrentState? --> No action
         |
         +-- RequestedState differs?              --> Exit current state
                                                       Enter new state
                                                       Cache LastKnownPlayerPosition
                                                           from DetectionOutput
```

The seeker caches `DetectionOutput.LastKnownPlayerPosition` on entry to Searching and Chase states. This position does not update during Searching — it is the position at the moment the seeker entered Searching. During Chase, the seeker navigates to the player's *current* position (polled from the Detection System's continuous update to `DetectionOutput.LastKnownPlayerPosition`).

**State machine owns:** NavMeshAgent velocity, animation state, audio cue triggers, and visual indicator state.

**Detection System owns:** All suspicion math, all LoS calculation, all noise radius math, and the `RequestedState` decision.

---

### 3.2 Patrol Behavior (Unaware State)

**Entry condition:** Default state on level load, or `RequestedState == Unaware`.

**Waypoint system:**

Each seeker has an ordered array of patrol waypoints (`PatrolRoute[]`) defined in the level scene. Waypoints are authored by the level designer as `Transform` references in the seeker's `EnemyController` component.

- On first entry to the Unaware state (level load), the seeker begins at waypoint index 0 and advances in ascending index order.
- On re-entry to Unaware from Alert (suspicion resolved), the seeker resumes from the *nearest waypoint* in its route, not from index 0. "Nearest" is determined by straight-line distance to each waypoint; the seeker selects the minimum.
- The route loops: when the seeker reaches the final waypoint, the next destination is index 0.
- There is no randomization of route direction or order. The route plays identically every time.

**Movement:**

The seeker moves toward its current target waypoint using NavMeshAgent. It considers a waypoint reached when its distance to the waypoint position is less than or equal to `waypointArrivalThreshold` (see Section 7). Upon arrival, it advances to the next waypoint without pausing unless a dwell time is authored at that waypoint (see Dwell Points below).

**Dwell points:**

A waypoint can be marked as a dwell point in the inspector via a boolean on the `PatrolWaypoint` data class. At a dwell point, the seeker stops for `patrolDwellDuration` seconds before advancing. This is how level designers author "the seeker stops and looks around" moments on the route. Dwell behavior is authored, not emergent.

**Sight and hearing during patrol:**

The seeker's Detection System remains active in Unaware. The seeker is always watching and listening at patrol speed. There is no "unaware means blind" state — the seeker can transition to Alert from Unaware the moment its Detection System crossing thresholds are met.

**Speed:** `patrolSpeed` (see Section 7).

**Exit condition:** `RequestedState` transitions to Alert.

---

### 3.3 Alert Behavior

**Entry condition:** `RequestedState == Alert`.

**Behavioral sequence (deterministic, linear):**

1. The seeker immediately stops NavMeshAgent movement. It does not continue toward its current patrol waypoint.
2. The seeker rotates to face the direction of the stimulus. The Detection System provides `LastKnownPlayerPosition`; the seeker rotates toward that position at `alertTurnSpeed` degrees per second. If the stimulus was audio (no line of sight), the seeker faces the estimated noise origin stored in `LastKnownPlayerPosition`.
3. The seeker holds its facing for `alertScanDuration` seconds, scanning with its active LoS system (Detection System continues running).
4. After `alertScanDuration` elapses:
   - If `RequestedState` has escalated to Searching or Chase during the scan, the seeker transitions immediately.
   - If `RequestedState` remains Alert or has dropped back to Unaware, the seeker resumes patrol by finding its nearest waypoint and resuming movement.

**What the player observes:** The seeker stops moving. Its head/torso turns toward the stimulus origin. It holds still for a moment, scanning. This is the "did it see me?" beat — a moment of legible tension where the player can observe the seeker's attention direction and assess exposure.

> **Art director pass needed**: Visual feedback for Alert state (punctuation mark, environmental cue, etc.) is TBD. Requirement: feedback must be diegetic or very peripheral — no HUD text. See Section 9.

**Alert does not make the seeker approach the stimulus.** That is Searching behavior. Alert is a brief, stationary investigation. This distinction is critical for legibility — the player learns that Alert is survivable if they are already hidden, but Searching means the seeker is coming to check.

**Speed:** 0 (stationary during scan phase).

**Exit conditions:**
- `RequestedState == Searching` → enter Searching
- `RequestedState == Chase` → enter Chase (skip Searching)
- `RequestedState == Unaware` → return to Unaware, resume nearest waypoint

---

### 3.4 Searching Behavior

**Entry condition:** `RequestedState == Searching`.

**Phase 1 — Move to last known position:**

The seeker navigates to `LastKnownPlayerPosition` (cached on entry to this state) using NavMeshAgent at `searchSpeedMultiplier * patrolSpeed`. It uses the same NavMesh pathfinding as patrol; no special pathfinding is required.

**Phase 2 — Search sweep (on arrival):**

Upon reaching the last known position (within `waypointArrivalThreshold`), the seeker performs a deterministic directional sweep:

1. The seeker samples `searchSweepDirectionCount` equidistant facing directions, beginning from its current forward vector and rotating clockwise in `(360 / searchSweepDirectionCount)` degree increments.
2. For each direction, the seeker rotates to face that direction at `searchTurnSpeed` degrees per second, holds the facing for `sweepHoldDuration` seconds (during which the Detection System actively checks LoS), then rotates to the next direction.
3. After completing all directions (one full 360-degree sweep), the seeker proceeds to Phase 3.

Total sweep duration (approximate): `searchSweepDirectionCount * (rotationTime_per_step + sweepHoldDuration)`. See Formula F-S2.

**Phase 3 — Waypoint sweep:**

After the directional sweep, the seeker visits the `searchWaypointCount` nearest patrol waypoints to the last known position (sorted by distance). It moves to each at `searchSpeedMultiplier * patrolSpeed` and performs a brief 2-direction scan (left 45 degrees, right 45 degrees) at each waypoint rather than the full directional sweep. This represents the seeker checking the obvious hiding spots near the last known position.

**Phase 4 — Return to patrol:**

After visiting all search waypoints, if `RequestedState` has not escalated to Chase, the seeker returns to Unaware by resuming patrol at the nearest waypoint to its current position.

**Distraction handling during Searching:**

During Searching (but NOT during Chase), a `NoiseEvent` from a thrown object redirects the seeker. If a distraction NoiseEvent fires within the seeker's hearing range while Searching, the Detection System updates `LastKnownPlayerPosition` to the noise origin, and the seeker's Searching phase restarts from Phase 1 with the new position. The previous sweep progress is abandoned. This creates the core distraction mechanic: lure the seeker away from the player's actual position by throwing an object to a different location.

**Speed:** `searchSpeedMultiplier * patrolSpeed`.

**Exit conditions:**
- `RequestedState == Chase` → enter Chase
- `RequestedState == Unaware` (suspicion < 50 after sweep completes) → return to Unaware

---

### 3.5 Chase Behavior

**Entry condition:** `RequestedState == Chase`.

**Movement:**

The seeker navigates directly to the player's current position, which the Detection System continuously updates in `DetectionOutput.LastKnownPlayerPosition` during Chase. The seeker polls this value and updates its NavMeshAgent destination every `chaseNavUpdateInterval` seconds — not every FixedUpdate — to manage per-frame NavMesh path recalculation cost.

The seeker moves at `chaseSpeedMultiplier * patrolSpeed`.

**Catch attempt:**

Each FixedUpdate during Chase, the seeker checks whether the player is within `catchRadius` of the seeker's position:

```
distance = Vector3.Distance(seekerPosition, playerPosition)
if distance <= catchRadius:
    catchDwellAccumulator += Time.fixedDeltaTime
else:
    catchDwellAccumulator = 0
if catchDwellAccumulator >= catchDwellTime:
    → Enter Caught state
```

The catch is not instantaneous — the seeker must maintain proximity for `catchDwellTime` seconds. This gives the player a small but legible window to break proximity and escape, preventing catches that feel instant and opaque. The `catchDwellAccumulator` resets to zero the moment the player exceeds `catchRadius`, so the player must actually escape, not just momentarily dodge.

**Distractions do not redirect during Chase:**

If a `NoiseEvent` fires during Chase, the Detection System does not change `RequestedState` in response. The seeker has locked onto the player's current position. This is a critical design rule: the player cannot throw their way out of a Chase. The distinction between Searching (distractable) and Chase (locked) is one of the most important rules the player must learn, and it must be legible.

**Vocalization:** The seeker triggers a chase audio cue on entry to Chase state (see Section 9).

**Speed:** `chaseSpeedMultiplier * patrolSpeed`.

**Exit conditions:**
- `catchDwellAccumulator >= catchDwellTime` → enter Caught
- `DetectionOutput.SuspicionLevel < 75` after `chaseLostPatienceSeconds` of no LoS → enter Searching (see F-S4)

---

### 3.6 Caught Behavior

**Entry condition:** `catchDwellAccumulator >= catchDwellTime` during Chase.

**Behavioral sequence:**

1. The seeker immediately halts NavMeshAgent movement. Velocity is set to zero.
2. The seeker triggers the catch animation (defined by art; the Seeker AI notifies the Animator via a trigger parameter — it does not own the animation logic).
3. The seeker invokes `GameManager.OnPlayerCaught()` — a single event call. The seeker then idles, waiting for the GameManager to either load the fail screen or trigger a level reset. The seeker takes no further autonomous action.

**Terminal state:** Caught is a terminal behavioral state. There is no exit condition — the GameManager owns what happens next.

---

### 3.7 MVP Seeker Variant: The Warden

**Concept:**

The Warden is the sole MVP seeker variant. It is a methodical, patrol-driven guardian that embodies the archetype the player learns the entire game against. All tutorial chambers use The Warden. All other variants (post-MVP) are defined by how they diverge from The Warden's behavioral baseline.

**Behavioral identity:**

The Warden is *patient and thorough*. It follows its patrol route at a measured pace, completes its full search sweep before returning to patrol, and does not accelerate dramatically during Chase. It is not fast — it is inevitable. The fantasy is outmaneuvering a juggernaut: the player can always be faster, but cannot be slower than the Warden.

**Default tuning values:** See Section 7. The Warden uses the default column for every tuning knob.

**Differentiation framework for future variants:**

Seeker variants must be differentiated along *behavioral axes*, not stat axes. A variant that is "The Warden but faster" is not a meaningful variant — it changes the difficulty without changing the puzzle logic. A variant that *behaves differently* creates new puzzle problems the player must solve differently.

The four behavioral axes available for variant design:

| Axis | Description | Example Variant Concept |
|------|-------------|------------------------|
| **Search Pattern** | How the seeker investigates a last known position | A variant that searches in a spiral outward from LKP rather than the directional sweep |
| **Alert Response** | What the seeker does when entering Alert | A variant that *approaches* the stimulus during Alert instead of holding position — compresses the player's warning window |
| **Distraction Resistance** | Whether and how distractions affect the seeker | A variant immune to distractions during Searching — forces direct-path thinking |
| **Patrol Logic** | How the patrol route is structured | A variant whose route timing synchronizes with a second seeker — the puzzle becomes coordination |

**Rules for variant design:**

1. Every variant must be fully describable in one behavioral sentence that does NOT use the word "faster" or "slower" alone. Speed changes are only meaningful when paired with a behavioral change.
2. Every variant must create a new puzzle type, not just a harder version of an existing puzzle.
3. Every variant's behavioral divergence must be legible to the player within 30 seconds of first encounter.
4. No variant may violate Pillar 1 (The Room Has Rules) — all behavior must be deterministic and discoverable.

---

## 4. Formulas

Formulas F1–F5 are defined in the Detection System GDD (`design/gdd/detection-system.md`) and are not duplicated here. The Seeker AI reads their outputs; it does not recompute them.

### F-S1: Nearest Waypoint Selection

Used on re-entry to Unaware (resume patrol) and in Phase 3 of Searching (nearest waypoints to visit).

```
NearestWaypoint = argmin over all i in PatrolRoute[] of:
    Vector3.Distance(seekerPosition, PatrolRoute[i].position)
```

| Variable | Type | Range | Source | Description |
|----------|------|-------|--------|-------------|
| `seekerPosition` | Vector3 | world space | NavMeshAgent.transform | Current seeker world position |
| `PatrolRoute[i].position` | Vector3 | world space | authored waypoints | World position of waypoint i |

**Output:** index of the nearest waypoint. Ties broken by lowest index.

---

### F-S2: Search Sweep Duration

Total time for Phase 2 of Searching at a given `searchTurnSpeed`:

```
angleBetweenDirections   = 360 / searchSweepDirectionCount  (degrees)
rotationTime_per_step    = angleBetweenDirections / searchTurnSpeed  (seconds)
sweepTotalDuration       = searchSweepDirectionCount * (rotationTime_per_step + sweepHoldDuration)
```

**Example (defaults: `searchSweepDirectionCount = 8`, `searchTurnSpeed = 120 deg/s`, `sweepHoldDuration = 0.5s`):**
```
angleBetweenDirections = 360 / 8 = 45 degrees
rotationTime_per_step  = 45 / 120 = 0.375s
sweepTotalDuration     = 8 * (0.375 + 0.5) = 7.0 seconds
```

| Variable | Type | Range | Source | Description |
|----------|------|-------|--------|-------------|
| `searchSweepDirectionCount` | int | 4–12 | `EnemyData` | Number of equidistant facing directions in the sweep |
| `searchTurnSpeed` | float | 60–240 deg/s | `EnemyData` | Rotation speed during sweep |
| `sweepHoldDuration` | float | 0.2–1.5s | `EnemyData` | How long the seeker holds each direction |

**Expected output range**: ~3.5s (min, at `searchSweepDirectionCount=4`, `searchTurnSpeed=240`, `sweepHoldDuration=0.2`) to ~80s (max, pathological values).

---

### F-S3: Catch Radius Check (per FixedUpdate)

```
catchDistance            = Vector3.Distance(seekerTransform.position, playerTransform.position)
isInCatchRange           = catchDistance <= catchRadius

if isInCatchRange:
    catchDwellAccumulator += Time.fixedDeltaTime
else:
    catchDwellAccumulator  = 0.0f

isCaught                 = catchDwellAccumulator >= catchDwellTime
```

| Variable | Type | Range | Source | Description |
|----------|------|-------|--------|-------------|
| `catchRadius` | float | 0.5–3.0m | `EnemyData` | Proximity radius within which a catch can be registered |
| `catchDwellTime` | float | 0.1–0.5s | `EnemyData` | Continuous time in catchRadius required to register Caught |
| `catchDwellAccumulator` | float | 0–∞ | runtime state | Accumulated time within catchRadius; resets on exit |

**Example (defaults):** `catchRadius = 1.5m`, `catchDwellTime = 0.2s` — player must remain within 1.5m of the seeker for 0.2 continuous seconds to be caught.

---

### F-S4: Chase Lost-Player Threshold

The seeker transitions from Chase back to Searching only when both conditions are simultaneously true:

```
condition_A = DetectionOutput.SuspicionLevel < 75
condition_B = timeSinceLastDirectLoS >= chaseLostPatienceSeconds
```

`timeSinceLastDirectLoS` is a timer maintained by the Seeker AI, reset whenever the Detection System reports a LoS hit during Chase. If the player breaks LoS and suspicion falls below 75 AND `chaseLostPatienceSeconds` elapses, the seeker enters Searching from the player's last cached position.

| Variable | Type | Range | Source | Description |
|----------|------|-------|--------|-------------|
| `chaseLostPatienceSeconds` | float | 1.0–6.0s | `EnemyData` | How long the seeker persists in Chase after losing LoS |
| `timeSinceLastDirectLoS` | float | 0–∞ | runtime state | Seconds since last confirmed LoS during Chase; resets on LoS hit |

---

### F-S5: Search Waypoint Selection (Phase 3)

```
sortedWaypoints = PatrolRoute[] sorted ascending by:
    Vector3.Distance(lastKnownPlayerPosition, waypoint.position)
searchTargets   = sortedWaypoints[0 .. searchWaypointCount - 1]
```

| Variable | Type | Range | Source | Description |
|----------|------|-------|--------|-------------|
| `lastKnownPlayerPosition` | Vector3 | world space | cached on Searching entry | LKP at the moment the seeker entered Searching |
| `searchWaypointCount` | int | 1–4 | `EnemyData` | How many nearby waypoints to check after the directional sweep |

**Edge case:** If `PatrolRoute.Length < searchWaypointCount`, all waypoints are visited.

---

## 5. Edge Cases

| Scenario | Expected Behavior | Rationale |
|----------|------------------|-----------|
| Seeker loses player mid-Chase (LoS breaks, suspicion drops below 75 after `chaseLostPatienceSeconds`) | Seeker transitions to Searching. Navigates to last cached `LastKnownPlayerPosition` (position at the moment LoS broke). Searching Phase 2 sweep begins from that position. | Player must be hidden before `chaseLostPatienceSeconds` elapses or the seeker arrives in Chase. |
| Seeker reaches last known position during Searching but player has moved | Seeker completes the full directional sweep regardless. If no LoS during the sweep, proceeds to Phase 3 (waypoint sweep). Sweep is not abbreviated. | The seeker does not "know" the player moved. Completing the sweep is consistent with deterministic behavior. |
| Two seekers simultaneously in Chase on the same player | Each seeker runs its own independent state machine and catch check. The first to accumulate `catchDwellTime` invokes `GameManager.OnPlayerCaught()`. GameManager must handle `OnPlayerCaught` as idempotent — calling it twice in one frame must not double-trigger the fail state. | Each seeker has no knowledge of other seekers' states. |
| Player hides in a HidingSpot at or very near the last known position | Seeker arrives at LKP and performs the full sweep. HidingSpot concealment is resolved by the Detection System (`IHideable.IsConcealed`). If the spot provides LoS cover, the sweep will not detect the player. | Hiding at LKP is an intended play pattern — "hide right where they think you are." |
| Patrol route has only one waypoint | Seeker arrives at the single waypoint, dwells (if marked), then immediately targets it again. Effectively stationary or oscillating. A `Debug.LogWarning` is logged at runtime. No exception or infinite loop. | Authored behavior — level designers must use ≥2 waypoints for any patrolling seeker. Warning surfaces the misconfiguration without breaking play. |
| Catch animation interrupted by level end or scene transition | GameManager's scene load takes precedence. `OnPlayerCaught()` may not execute before the transition. GameManager must check scene state before processing the caught event. The seeker takes no further action. | Race condition between catch and win/scene-load events; GameManager is the authoritative arbiter. |
| Alert stimulus source destroyed before scan ends (e.g., thrown object is recycled) | Seeker completes the full `alertScanDuration` facing the last recorded origin. If no new stimulus escalates suspicion, returns to Unaware. The object's destruction does not reset the timer. | The seeker "heard something" — whether the source still exists is irrelevant to its behavioral response. |
| NavMesh path unavailable during Chase (bake hole or edge) | NavMeshAgent attempts nearest reachable position. If no path exists, agent stops. Seeker AI logs a warning and holds position; does not exit Chase state. | Level designers are responsible for ensuring all seeker reachable areas are baked. The seeker not exiting Chase prevents false "lost the player" transitions. |

---

## 6. Dependencies

| System | Direction | What Seeker AI Provides | What Seeker AI Requires |
|--------|-----------|------------------------|------------------------|
| **Detection System** | Bidirectional | Seeker position, forward vector, FOV angle, visual range, catch radius (via `EnemyData`, read by Detection System) | `DetectionOutput` struct per FixedUpdate: `SuspicionLevel`, `RequestedState`, `LastKnownPlayerPosition` |
| **NavMesh / AI Navigation** (`com.unity.ai.navigation` 2.0.11) | Seeker AI → NavMesh | Destination waypoints, path requests | Computed paths; `NavMeshAgent` movement integration |
| **PlayerController** | Detection System → PlayerController (indirect) | None directly | None directly — player position flows through Detection System output |
| **HidingSpot System** | Detection System → HidingSpotSystem (indirect) | None directly | None directly — concealment resolved in Detection System via `IHideable.IsConcealed` |
| **GameManager** | Seeker AI → GameManager | `OnPlayerCaught()` event invocation on Caught state entry | Idempotent `OnPlayerCaught()` handler; scene management on level end |
| **AudioManager** | Seeker AI → AudioManager | State-change audio trigger calls (Alert cue, Chase cue, Search cue) | `SoundID` entries for each seeker state in `SoundLibrary`; seeker-specific audio variants via `EnemyData.soundProfile` |
| **EnemyData ScriptableObject** | Seeker AI reads | N/A | All tuning knob values at runtime; patrol waypoint array reference; seeker variant type identifier |
| **Animator (per seeker prefab)** | Seeker AI → Animator | Trigger/parameter writes for state transitions (patrol, alert, search, chase, caught) | Animator Controller with defined trigger parameters matching Seeker AI's expected parameter names |
| **Adaptive Music System** | Seeker AI → Music System | `OnSeekerStateChanged` C# event on state transitions | Music system subscribes to the event; seeker does not call the music system directly |
| **Level Exit System** | GameManager mediates | None directly | None directly — `OnPlayerCaught` prevents win condition via GameManager |

---

## 7. Tuning Knobs

All values are fields on `EnemyData` (`Assets/_Project/Scripts/Data/EnemyData.cs`). No value is hardcoded in `EnemyController` or any Seeker AI script.

| Knob | Category | Default (Warden) | Safe Range | Extreme Low Effect | Extreme High Effect |
|------|----------|-----------------|------------|-------------------|---------------------|
| `patrolSpeed` | Feel | 2.0 m/s | 1.0–4.0 m/s | Barely moves; trivially avoidable | Covers ground so fast patrol gaps disappear; forces constant hiding |
| `alertTurnSpeed` | Feel | 90 deg/s | 45–180 deg/s | Slow, telegraphed turn; very readable | Instant turn; snappy but less legible |
| `alertScanDuration` | Gate | 2.5s | 1.0–5.0s | Too short to register; feels like seeker ignores stimulus | Too long; pacing stalls |
| `searchSpeedMultiplier` | Feel | 1.3× | 1.0–2.0× | Indistinguishable from patrol speed | Searching feels rushed; less legible |
| `searchTurnSpeed` | Feel | 120 deg/s | 60–240 deg/s | Sweep is very slow; extends total duration significantly | Snap-between-directions; mechanical, less readable |
| `sweepHoldDuration` | Gate | 0.5s | 0.2–1.5s | Tiny window to move between directions | Sweep takes 15+ seconds; very punishing |
| `searchSweepDirectionCount` | Gate | 8 | 4–12 | Large angular blind spots between sweep checks | Extremely thorough; near-zero blind spots |
| `searchWaypointCount` | Gate | 2 | 1–4 | Checks only nearest waypoint; may miss obvious hides | Visits most of patrol route; very long searches |
| `chaseSpeedMultiplier` | Feel | 1.6× | 1.2–2.5× | Barely faster than patrol; player can walk away | Dramatically faster than sprint; near-impossible to outrun |
| `chaseNavUpdateInterval` | Perf | 0.2s | 0.1–0.5s | Very frequent; precise tracking; higher NavMesh cost | Seeker lags behind fast direction changes; may feel laggy |
| `catchRadius` | Feel | 1.5m | 0.5–3.0m | Must nearly collide to catch; very forgiving | Catches from arm's-length; feels unfair |
| `catchDwellTime` | Feel | 0.2s | 0.1–0.5s | Near-instant catch; no escape window | Player can be adjacent for 0.5s and escape; too lenient |
| `chaseLostPatienceSeconds` | Gate | 3.0s | 1.0–6.0s | Gives up Chase quickly; player can hide anywhere briefly | Chases for very long after LoS breaks; brutal tension |
| `waypointArrivalThreshold` | Feel | 0.3m | 0.1–0.8m | Must be very close to advance; may fight NavMesh rounding | Advances early; patrol appears to cut corners |
| `patrolDwellDuration` | Gate | 1.5s | 0.0–5.0s | Dwell feels like a brief pause; small safe window | Seeker stands still for long periods; stop-start pacing |

---

## 8. Acceptance Criteria

### Functional

- [ ] **AC-01 — Patrol loops correctly:** A seeker with a 4-waypoint patrol visits all 4 waypoints in order and returns to index 0 without skipping or duplicating.
- [ ] **AC-02 — Patrol resumes at nearest waypoint:** After returning to Unaware from Alert, the seeker resumes from the nearest waypoint to its current position, not index 0.
- [ ] **AC-03 — Alert holds position:** During Alert, `NavMeshAgent.velocity` is zero. The seeker does not advance toward the stimulus.
- [ ] **AC-04 — Alert scan duration is honored:** The seeker holds Alert for exactly `alertScanDuration` ± 1 frame (at 60fps) before resolving.
- [ ] **AC-05 — Search sweep covers all directions:** During Searching Phase 2, the seeker rotates through exactly `searchSweepDirectionCount` equidistant directions, holding each for `sweepHoldDuration`.
- [ ] **AC-06 — Distraction redirects during Searching:** A NoiseEvent within hearing range during Searching causes the seeker to abandon its current sweep and navigate to the noise origin.
- [ ] **AC-07 — Distraction does NOT redirect during Chase:** A NoiseEvent during Chase does not change the seeker's NavMeshAgent destination. It continues tracking the player's current position.
- [ ] **AC-08 — Catch requires dwell time:** A player who enters then immediately exits `catchRadius` is not caught. `catchDwellAccumulator` resets to 0 on exit. Only continuous presence for ≥ `catchDwellTime` triggers Caught.
- [ ] **AC-09 — Caught fires GameManager once:** `GameManager.OnPlayerCaught()` is called exactly once per catch event, even if two seekers enter Caught in the same frame.
- [ ] **AC-10 — Chase lost-player logic is correct:** A seeker in Chase that loses LoS does not immediately transition to Searching. It waits `chaseLostPatienceSeconds`. If LoS is reestablished within that window, the timer resets.
- [ ] **AC-11 — No hardcoded values:** A code review finds zero numeric literals in `EnemyController`, `SeekerStateMachine`, or any Seeker AI script. All values reference fields on `EnemyData`.
- [ ] **AC-12 — Single-waypoint route logs warning:** A seeker with a 1-waypoint patrol route logs `Debug.LogWarning` at runtime. No exception, no infinite loop.
- [ ] **AC-13 — NavMesh package API is correct:** Implementation uses `com.unity.ai.navigation` 2.0.11 APIs exclusively. No deprecated `UnityEngine.AI` patterns incompatible with 2.0.x.
- [ ] **AC-14 — Chase nav update interval is respected:** A profiler capture shows `NavMeshAgent.SetDestination` is called at most once per `chaseNavUpdateInterval` during Chase, not every FixedUpdate.

### Experiential (validated via observed play sessions)

- [ ] **AC-15 — Alert is legible:** Playtesters who trigger Alert can identify within 5 seconds, without UI prompts, that the seeker noticed something and is scanning.
- [ ] **AC-16 — Distraction is discoverable:** In a chamber with throwable objects, ≥70% of first-time playtesters discover that throwing redirects a Searching seeker within their first session, without being told.
- [ ] **AC-17 — Chase is distinct from Searching:** Playtesters verbally distinguish "it's looking for me" (Searching) from "it's coming right at me" (Chase) without prompting. No playtester reports confusion between the two states.
- [ ] **AC-18 — Death is legible:** After a Caught event, the playtester can identify the cause when asked. If >20% of Caught events produce an "I don't know why I got caught" response, visual/audio feedback for that state requires iteration.

---

## 9. Visual and Audio Requirements

The Seeker AI triggers state changes; it does not own feedback implementation. These requirements are for the Art and Audio teams.

| State Transition | Required Visual Feedback | Required Audio Feedback |
|-----------------|-------------------------|------------------------|
| Unaware → Alert | Seeker posture stiffens (animation blend); diegetic visual cue TBD — *art director pass needed* | Alert vocalization: short, clipped ("Hm?", "What was that?") — `SoundLibrary` variant |
| Alert → Searching | Seeker begins moving; posture heightens; scan animation ends | Searching vocalization: low, determined; music transitions to tension layer |
| Alert → Unaware | Seeker posture relaxes; resumes patrol animation | Short relaxation audio cue; music tension layer fades |
| Searching → Chase | Seeker snaps to locked-facing posture; moves distinctly faster | Chase exclamation ("There!"); music transitions to full chase layer immediately |
| Chase (ongoing) | Seeker maintains locked forward orientation toward player | Chase music layer sustains |
| Chase → Searching (lost player) | Seeker slows; search posture re-engages | "Lost it" vocalization; music partially releases but remains tense |
| Caught | Seeker halts; catch gesture animation | Catch SFX + musical sting; then silence for fail screen |

**Design principle:** Every state must be identifiable by audio alone (accessibility) AND by visual alone (noisy environments). These are parallel channels, not dependent.

---

## 10. UI Requirements

The HUD does not display seeker state directly (Pillar 3 — the world communicates state, not the HUD). HUD behaviors gated on seeker state are communicated via C# event `OnStateChanged(SeekState newState)`. `HUDManager` subscribes per active seeker; the seeker never calls HUD methods directly.

| Seeker State | HUD Behavior |
|-------------|-------------|
| Unaware | No seeker-specific HUD element active |
| Alert | Optional subtle peripheral cue — must be diegetic or very peripheral. Subject to UX review. |
| Searching | No additional HUD element. Noise indicator (`NoiseIndicatorUI`) remains active. |
| Chase | Screen vignette or pulse effect indicating extreme danger (implementation TBD with UX designer). Must not occlude gameplay-critical screen areas. |
| Caught | Full-screen transition to fail state; HUD replaced by `GameOverUI`. |

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|----------|-------|----------|-----------|
| Alert visual feedback: what specific diegetic cue communicates the state without HUD text? | art-director | Vertical Slice | Deferred to art director pass |
| Chase vignette design: what intensity and animation style is appropriate without occluding gameplay? | ux-designer | MVP | TBD with UX designer |
| Should Warden patrol speed be tuned per-level or fixed across all Level_1 rooms? | game-designer | Sprint 1 playtesting | Resolve after first Level_1 playtest |
