# Detection System

> **Status**: In Review
> **Author**: game-designer + systems-designer
> **Last Updated**: 2026-03-04 (revised)
> **Implements Pillar**: Legible Jeopardy (Pillar 3), The Room Has Rules (Pillar 1)

---

## Overview

The Detection System is UNSEEN's central threat engine. It determines, every
fixed-update frame, whether each seeker can perceive the player — through sight
or sound — and by how much. Perception accumulates into a per-seeker suspicion
value (0–100) that drives the seeker's AI state machine through five escalating
states: Unaware, Alert, Searching, Chase, and Caught. The system is intentionally
deterministic: every outcome is the product of readable, discoverable rules. A
player who understands which surfaces muffle sound, how darkness degrades a
seeker's sight, and what suspicion level triggers a state change can, in
principle, predict exactly what each seeker will do at every moment. The
Detection System does not roll dice. It does not cheat. It reports what the
simulation says.

---

## Player Fantasy

The player should feel like a code-breaker, not a gambler.

When the Detection System is working correctly, the player experiences this
emotional arc in each encounter:

**Tension on approach.** They can see the seeker's attention state (eye icon)
and hear its footsteps. They know the rules are running. They are not afraid of
the system — they are *reading* it.

**The near-miss rush.** A seeker's suspicion meter ticks up, then starts to
decay. The player holds still. The meter retreats. That flicker — at the edge
of Alert — is the game's primary emotional peak. It is satisfying precisely
because the player predicted it and chose correctly.

**Earned safety.** When the player reaches cover, they feel the room's logic
confirmed: *"I knew the shadow would cover me there. I was right."*

**Fair failure.** When caught, the player should be able to replay the last
10 seconds and identify the mistake. Never "the game cheated." Always "I moved
too fast" or "I forgot that surface is loud."

This requires **Pillar 3 (Legible Jeopardy)** — danger level must be readable
from world-state cues at all times, never from hidden math. And **Pillar 1
(The Room Has Rules)** — rules must be consistent enough that the player builds
an accurate mental model after 1–2 encounters.

*Self-Determination Theory anchor: **Competence**. The player must feel their
skill at reading the detection system grows across sessions.*

---

## Detailed Design

### Core Rules

#### 3a. Visual Detection (Line of Sight)

**Rule V-1.** A seeker performs a visual detection check against the player each
`FixedUpdate` tick, if and only if the player is within the seeker's maximum
visual range. Maximum visual range is defined in the seeker's data asset.

**Rule V-2.** For a visual check to be possible, the player must fall within the
seeker's horizontal FOV cone. FOV angle is measured from the seeker's forward
vector to the vector toward the player, in the horizontal plane only. Vertical
angle is not checked independently — the same cone applies for all player heights
within ±2.0 meters of the seeker's eye-level origin.

**Rule V-3.** If the player is within range and within the FOV cone, a LoS
raycast is performed from the seeker's eye-position to the player's
center-of-mass. The ray uses the `EnemyDetection` physics layer mask. If the
ray hits any geometry before reaching the player, LoS is blocked and visual
suspicion delta is zero for this tick.

**Rule V-4.** If LoS is confirmed, a base visual suspicion delta is calculated
(see Formulas — F2). This base value is modified by two factors:
- **Light modifier**: scales delta by a multiplier derived from ambient light
  level at the player's position (0.0 = darkness → 0.0 modifier; 1.0 = full
  light → 1.0 modifier). At light level 0.0, visual suspicion delta is zero
  regardless of LoS.
- **Distance modifier**: scales delta by a falloff curve based on normalized
  distance (current distance ÷ max visual range). Detection rate falls off at
  distance.

**Rule V-5.** Player crouch state reduces visual suspicion delta by a
`crouch_stealth_multiplier` (tuning knob). Crouch does not affect LoS geometry
or FOV — the seeker's cone still intersects the crouched player.

**Rule V-6.** Visual detection is blocked entirely when the player is inside a
hiding spot (`PlayerHiding.IsHidden == true`). No visual LoS check is performed.

**Rule V-7.** The seeker has no persistent visual memory buffer. All memory of
the player's position is encoded in the suspicion meter and Searching state
behavior.

---

#### 3b. Audio Detection

**Rule A-1.** Audio detection is event-driven, not continuous. A `NoiseEvent` is
a discrete struct emitted by the Player Noise Emitter each time the player makes
sound. It carries: world position, base intensity (0.0–1.0), and surface type.

**Rule A-2.** When a `NoiseEvent` is emitted, the Sound Propagation Model
determines per seeker: is it audible? and at what attenuated intensity? The
Detection System consumes these results as a bool and float per seeker.

**Rule A-3.** If a `NoiseEvent` is audible to a seeker, the Detection System
applies an audio suspicion spike to that seeker (one-time addition on the
receipt frame, not a continuous per-frame delta). See Formulas — F3.

**Rule A-4.** Player actions that emit `NoiseEvent`s and their base intensities
(exact values are tuning knobs in the sound data asset):

| Action | Base Intensity | Notes |
|--------|---------------|-------|
| Walking on stone | Medium | Default dungeon surface |
| Walking on wood | Medium-High | Floorboards creak |
| Walking on carpet / soft | Low | Muffled footstep |
| Sprinting (any surface) | High | Always louder than walk |
| Crouching walk (any surface) | Low | Slower cadence, reduced per-step |
| Landing from a jump | High | Single spike on landing frame |
| Throwing an object | Medium | Emitted at throw origin |
| Thrown object impact | High | Emitted at impact position — may be far from player |
| Interacting with a prop | Low–Medium | Door = Medium, drawer = Low |
| Entering/exiting hiding spot | Low | Deliberate and careful |

**Rule A-5.** The player cannot hear their own `NoiseEvent`s through the
Detection System. The noise meter on the HUD is driven by the Player Noise
Emitter directly.

**Rule A-6.** Seekers emit their own `NoiseEvent`s (footsteps, vocalizations)
for player-facing audio feedback only. Seekers do not hear each other.

**Rule A-7.** A thrown object's impact `NoiseEvent` is emitted at the impact
world position, not the player's position. This is the core distraction mechanic:
the seeker investigates the noise origin, not the player.

---

#### 3c. Suspicion Meter

**Rule S-1.** Each seeker has its own independent suspicion meter. There is no
global suspicion state shared across seekers.

**Rule S-2.** Suspicion is a float value clamped to `[0, 100]`. It is stored on
the seeker's `SuspicionMeter` component, not on the player.

**Rule S-3.** Each `FixedUpdate` tick, the system:
1. Evaluates visual suspicion delta for this seeker (Section 3a)
2. Applies audio suspicion spikes received since last tick (Section 3b)
3. Applies suspicion decay if no detection input is active (see F4)
4. Clamps result to `[0, 100]`
5. Evaluates state transition thresholds (see F5)

**Rule S-4.** Suspicion decay applies when both: no LoS this tick, and no audio
event received since last tick. Decay rate is state-dependent (see F4). Decay
does not apply during Caught state.

**Rule S-5.** State transition thresholds:

| From | To | Rising Condition | Falling Condition |
|------|-----|-----------------|------------------|
| Unaware | Alert | suspicion > 25 | — |
| Alert | Searching | suspicion > 60 | — |
| Searching | Chase | suspicion > 85 | — |
| Chase | Caught | player within `catch_radius` for ≥ `catch_dwell_time` | — |
| Chase | Searching | — | suspicion < 75 AND no LoS AND no audio events for `chase_lost_patience_seconds` continuously (see Rule S-7) |
| Searching | Alert | — | suspicion < 50 |
| Alert | Unaware | — | suspicion < 15 |

Threshold values are tuning knobs stored in the seeker data asset — never hardcoded.

**Rule S-6.** Upward transitions are immediate. Downward transitions apply
hysteresis (falling threshold is lower than rising threshold) to prevent
oscillation at boundaries (see F5).

**Rule S-7.** Suspicion cannot decrease while in Chase state unless the seeker
loses LoS AND receives no audio events for a continuous duration of
`chase_lost_patience_seconds` (tuning knob, default: 3.0s).

---

#### 3d. Hiding Spot Awareness Penalty

**Rule H-1.** When `PlayerHiding.IsHidden == true`, visual detection is bypassed
entirely (Rule V-6). However, hiding does not make the player silent or
invisible at all ranges.

**Rule H-2.** Audio detection functions normally while hidden. The hiding spot
provides no audio dampening. Sound events emitted by the player are propagated
to seekers at normal intensity.

**Rule H-3.** Proximity penalty: if a seeker is within `hiding_spot_awareness_radius`
of the occupied hiding spot's center, that seeker gains continuous suspicion
accumulation at `creeping_dread_rate` per second (scaled by proximity factor).
This represents the seeker sensing something may be inside — displaced air,
subtle sounds, behavioral tells.

**Rule H-4.** The proximity penalty activates only when the seeker is in Alert,
Searching, or Chase state. An Unaware seeker does not apply the penalty — it is
not actively scrutinizing.

**Rule H-5.** The proximity penalty is not separately telegraphed to the player.
The suspicion meter rises from proximity exactly as it would from any detection
source. No distinct UI cue identifies proximity as the cause. The player
experiences "the meter is rising while I am hidden" and must infer why. This
ambiguity is intentional — it creates the "hold your breath" tension that makes
hiding a live decision, not a safe room.

**Rule H-6.** If proximity penalty drives suspicion above the Chase threshold,
the seeker transitions to Chase and navigates to the hiding spot's position.
On arrival, it does not immediately catch the player — it performs a brief
"search the spot" behavior (deterministic pause + look-around). The Caught check
fires if the player is within `catch_radius` during this behavior.

---

### States and Transitions

| State | Entry Condition | Exit Condition | Seeker Behavior | Suspicion in State |
|-------|----------------|----------------|-----------------|-------------------|
| **Unaware** | Default / suspicion < 15 from Alert | suspicion > 25 → Alert | Follows patrol route at base speed; FOV active | Standard accumulation; fastest decay; no proximity penalty |
| **Alert** | suspicion > 25 from Unaware | suspicion > 60 → Searching; suspicion < 15 → Unaware | Stops patrol; turns toward stimulus; scans for `alert_scan_duration`; resumes nearest waypoint if suspicion decays | Standard visual; audio normal; reduced decay; proximity penalty active |
| **Searching** | suspicion > 60 from Alert | suspicion > 85 → Chase; suspicion < 50 → Alert | Moves to last known position; deterministic search sweep; searches nearby waypoints at `search_speed_multiplier` | Visual delta increased; audio spikes multiplied; very slow decay; proximity penalty active |
| **Chase** | suspicion > 85 from Searching | Player in `catch_radius` ≥ `catch_dwell_time` → Caught; suspicion < 75 after `chase_lost_patience_seconds` → Searching | Navigates to player's current position; updates nav target every `chase_nav_update_interval`; moves at `chase_speed_multiplier`; vocalizes chase cue | Auto-rises at `chase_auto_suspicion_rate` regardless of LoS; decay suppressed for `chase_lost_patience_seconds`; distractions DO NOT redirect nav (see Edge Cases — "Seeker hears a distraction during Chase") |
| **Caught** | Player in `catch_radius` ≥ `catch_dwell_time` | Terminal — no exit | Halts; plays catch animation and audio; waits for Game Manager response | Suspended — no suspicion updates after Caught fires |

---

### Interactions with Other Systems

| System | Direction | Data | Owner |
|--------|-----------|------|-------|
| Light Source System | Reads from | `GetLightLevelAtPosition(Vector3) → float` each FixedUpdate | Light Source System owns the calculation; Detection System applies the modifier |
| Sound Propagation Model | Reads from | `NoiseEvent.AttenuatedIntensity` (float), `NoiseEvent.IsAudible` (bool) per seeker | Sound Propagation Model owns attenuation; Detection System owns suspicion response |
| Seeker AI State Machine | Writes to | `DetectionOutput { float SuspicionLevel, SeekState RequestedState, Vector3 LastKnownPlayerPosition }` each FixedUpdate | Detection System owns suspicion and state transitions; Seeker AI owns navigation and animation responses |
| Seeker AI State Machine | Reads from | Seeker position, forward vector, FOV angle, visual range, catch radius (read-only from seeker data) | Detection System reads; never modifies seeker parameters |
| Hiding Spot System | Reads from | `PlayerHiding.IsHidden` (bool), `PlayerHiding.IsPeeking` (bool), `PlayerHiding.CurrentHidingSpot.transform.position` | Hiding Spot System owns enter/exit, `IsHidden`, and `IsPeeking`; Detection System owns penalty calculation and hide_modifier application |
| HUD | Publishes | `OnSuspicionChanged(float newValue, SeekState currentState)` when highest suspicion changes by > 0.01; detection event notifications | HUD owns display; Detection System publishes normalized data only |
| Adaptive Music | Publishes | `OnGlobalThreatLevelChanged(float threatLevel)` — highest suspicion across all seekers, normalized 0.0–1.0 | Adaptive Music owns layer decisions |
| Level Timer + Stats | Publishes | `OnDetectionEvent(DetectionEventType)` for: `SoundDetected`, `AlertRaised`, `ChaseStarted`, `PlayerCaught` | Level Timer + Stats owns counting |

---

## Formulas

### F1. Visual Field-of-View Check

Boolean gate executed before any suspicion calculation. If false, `visual_suspicion_delta = 0`.

```
is_in_fov = (distance <= seeker_max_range)
         AND (angle_to_player <= seeker_fov / 2)
         AND (line_of_sight_clear == true)
```

| Variable | Type | Range | Source | Description |
|---|---|---|---|---|
| `distance` | float | 0–∞ m | computed | Euclidean distance seeker eye → player center |
| `seeker_max_range` | float | 5–20 m | seeker data | Maximum visual range of this seeker type |
| `angle_to_player` | float | 0–180 deg | computed | Horizontal angle between seeker forward and direction to player |
| `seeker_fov` | float | 60–180 deg | seeker data | Total horizontal field of view; half-angle is the cone boundary |
| `line_of_sight_clear` | bool | — | raycast | Single raycast seeker eye → player center; false if occluded |

**Expected output**: true / false

**Edge case — distance = 0**: `is_in_fov` forced true regardless of angle. Prevents degenerate invisibility if seeker clips into player.

**Edge case — seeker_fov = 180**: Angle check always passes; system degrades to range + LoS only. Valid for "all-seeing" seeker variants.

**Simplification note**: FOV check is horizontal only. Vertical angle is not independently checked. This is a deliberate legibility simplification — 3D vertical FOV creates unintuitive edge cases (prone player not seen by adjacent seeker). Documented as a known simplification.

---

### F2. Visual Suspicion Delta

Executed only when `is_in_fov == true`.

```
distance_factor          = 1 - (distance / seeker_max_range) ^ distance_falloff_exponent
angle_deviation          = angle_to_player / (seeker_fov / 2)
angle_factor             = 1 - (angle_deviation ^ angle_falloff_exponent)
light_factor             = light_level ^ light_sensitivity_exponent
// Note: Rule V-6 short-circuits this formula entirely when IsHidden == true and not peeking.
// F2 is only evaluated when the player is exposed or peeking.
// The is_in_hiding_spot branch below is defensive fallback only — it should never be reached.
hide_modifier            = peeking ? peek_visibility_modifier
                         : (is_in_hiding_spot ? 0.0 : 1.0)
crouch_modifier          = player_is_crouching ? crouch_stealth_multiplier : 1.0
state_multiplier         = (seeker_state == Searching) ? searching_visual_detection_multiplier : 1.0

visual_suspicion_delta   = base_detection_rate
                         * distance_factor
                         * angle_factor
                         * light_factor
                         * hide_modifier
                         * crouch_modifier
                         * state_multiplier

// Integration per FixedUpdate tick:
suspicion               += visual_suspicion_delta * Time.fixedDeltaTime
```

| Variable | Type | Range | Source | Description |
|---|---|---|---|---|
| `base_detection_rate` | float | tuning | tuning | Suspicion/sec at ideal conditions (point-blank, center FOV, full light, exposed) |
| `distance_factor` | float | 0.0–1.0 | computed | 1.0 at distance=0; approaches 0 at max range |
| `distance_falloff_exponent` | float | tuning | tuning | Curve shape: 1.0 = linear, 2.0 = quadratic (fast drop near max range) |
| `angle_deviation` | float | 0.0–1.0 | computed | 0 = dead center of FOV, 1 = edge of FOV |
| `angle_factor` | float | 0.0–1.0 | computed | 1.0 at center; 0.0 at edge |
| `angle_falloff_exponent` | float | tuning | tuning | Edge-of-FOV softness |
| `light_level` | float | 0.0–1.0 | Light Source System | 0 = full dark; 1 = fully lit |
| `light_factor` | float | 0.0–1.0 | computed | Nonlinear curve via exponent |
| `light_sensitivity_exponent` | float | tuning | tuning | < 1.0 = darkness highly protective; > 1.0 = dim light is dangerous |
| `hide_modifier` | float | 0.0, peek_modifier, or 1.0 | Hiding Spot System | 0.0 if fully hidden; reduced if peeking; 1.0 if exposed |
| `peek_visibility_modifier` | float | tuning | tuning | Fraction of detection rate while peeking |
| `crouch_modifier` | float | 0.0–1.0 | computed | `crouch_stealth_multiplier` when crouching; 1.0 when standing |
| `crouch_stealth_multiplier` | float | tuning | tuning | Fraction of visual detection rate while player is crouching (Rule V-5) |
| `state_multiplier` | float | 1.0–3.0 | computed | 1.0 in Unaware/Alert/Chase; `searching_visual_detection_multiplier` in Searching. During Chase, visual delta is typically dominated by F7 auto-rise, but FixedUpdate still evaluates F2 when `is_in_fov == true`. |
| `searching_visual_detection_multiplier` | float | tuning | tuning | Extra visual detection rate multiplier when seeker is actively searching |

**Expected output range**: 0.0 (full dark / max range / edge of FOV) to `base_detection_rate` × `searching_visual_detection_multiplier` /sec.

**Example calculation** — player partially in shadow, medium range, off-center (seeker Unaware):
- distance=5m, max_range=12m, falloff_exp=2.0 → distance_factor = 1-(5/12)² = 0.826
- angle=20°, fov=80°, angle_exp=2.0 → angle_factor = 1-(20/40)² = 0.75
- light_level=0.4, light_exp=0.8 → light_factor = 0.4^0.8 = 0.481
- base_detection_rate=15.0, hide_modifier=1.0, state_multiplier=1.0
- **visual_suspicion_delta = 15.0 × 0.826 × 0.75 × 0.481 × 1.0 = 4.47 suspicion/sec**
- Per-tick integration (`Time.fixedDeltaTime = 0.02s`): **4.47 × 0.02 = 0.089 suspicion points per FixedUpdate**

At this rate, the seeker reaches Alert (25) after ~5.6 seconds of continuous exposure — a meaningful but not instant danger window.

**Edge case — light_level = 0.0**: `light_factor = 0`, therefore `visual_suspicion_delta = 0`. Full darkness provides complete visual immunity. The player's light exposure level must be communicated via HUD (see UI Requirements).

---

### F3. Audio Detection Check and Spike

Audio suspicion is event-driven. Evaluated once per `NoiseEvent`, not per frame.

**Step 1 — Threshold check (per seeker, per event):**
```
// Guard: seeker_hearing_threshold is clamped to [0.0, 0.95] in data validation
// to prevent division by zero in Step 2. Values >= 1.0 are rejected at asset import.
is_heard = attenuated_intensity >= seeker_hearing_threshold
```

**Step 2 — Suspicion spike (if heard):**
```
intensity_excess            = attenuated_intensity - seeker_hearing_threshold
normalized_excess           = intensity_excess / (1.0 - seeker_hearing_threshold)
audio_state_multiplier      = (seeker_state == Searching) ? searching_audio_spike_multiplier : 1.0
audio_suspicion_spike       = audio_base_spike
                            * (normalized_excess ^ audio_spike_exponent)
                            * audio_state_multiplier
```

| Variable | Type | Range | Source | Description |
|---|---|---|---|---|
| `attenuated_intensity` | float | 0.0–1.0 | Sound Propagation Model | Sound intensity after distance + surface attenuation |
| `seeker_hearing_threshold` | float | 0.0–1.0 | seeker data | Minimum intensity to trigger detection. 0.0 = hears everything; 0.8 = loud sounds only |
| `intensity_excess` | float | 0.0–1.0 | computed | How far the sound exceeds the threshold |
| `normalized_excess` | float | 0.0–1.0 | computed | Excess normalized to available range above threshold |
| `audio_base_spike` | float | tuning | tuning | Maximum suspicion added by a single audio event at full excess (suspicion points) |
| `audio_spike_exponent` | float | tuning | tuning | 1.0 = linear; < 1.0 = even faint sounds cause big spikes; > 1.0 = only loud sounds cause big spikes |
| `audio_state_multiplier` | float | 1.0–3.0 | computed | 1.0 in Unaware/Alert; `searching_audio_spike_multiplier` in Searching |
| `searching_audio_spike_multiplier` | float | tuning | tuning | Extra audio spike multiplier when seeker is actively searching |
| `audio_suspicion_spike` | float | 0.0–`audio_base_spike` × multiplier | output | Added to seeker's suspicion meter as a flat point addition on receipt frame |

**Expected output range**: 0 (below threshold) to `audio_base_spike` suspicion points.

**Example** — player sprints past a seeker:
- attenuated_intensity=0.65, threshold=0.4 → excess=0.25, normalized=0.25/0.6=0.417
- audio_base_spike=20, audio_spike_exponent=1.5
- **audio_suspicion_spike = 20 × 0.417^1.5 = 5.4 suspicion points**

A 5.4-point spike is legible — the meter jumps but one footstep does not reach Alert (25).

**Design note**: Audio is a one-time spike; visual is a continuous rate. The player sees audio as a "needle jump," which is more immediately readable than a continuous rise. This is intentional.

**Edge case — attenuated_intensity exactly equals seeker_hearing_threshold**: `intensity_excess = 0`, `audio_suspicion_spike = 0`. The seeker "hears" the sound (is_heard = true) and may turn its head toward the origin (behavioral signal), but no suspicion is added. This enables the satisfying near-miss of "they heard something but couldn't confirm."

**Edge case — multiple simultaneous sounds**: Each event evaluated independently; spikes stack additively with no per-frame cap. If stacking becomes exploitable, add `audio_spike_frame_cap` as a future tuning knob.

---

### F4. Suspicion Decay

Applied each frame when neither visual delta > 0 nor audio spike fired this frame. Includes a cooldown before decay begins.

**Cooldown before decay:**
```
// detection_cooldown_timer is a per-seeker runtime float stored on the SuspicionMeter component.
// delta_time = Time.fixedDeltaTime (system runs in FixedUpdate)
if (visual_suspicion_delta > 0 OR audio_spike_this_frame):
    detection_cooldown_timer = detection_cooldown_duration
else:
    detection_cooldown_timer = max(0, detection_cooldown_timer - Time.fixedDeltaTime)
    if (detection_cooldown_timer == 0):
        apply_decay()
```

**Decay per state:**
```
// delta_time = Time.fixedDeltaTime
decay_delta = -decay_rate_for_current_state * Time.fixedDeltaTime
suspicion   = max(0, suspicion + decay_delta)
```

Decay rate is indexed by **behavioral state** (not suspicion value). A seeker in Chase state with
suspicion at 78 (decaying from a 85+ entry, hysteresis holding it in Chase until < 75) still
uses `decay_rate_chase`. The suspicion ranges shown below are the *typical* range for each state;
the state boundary is the authority, not the suspicion number.

| Behavioral State | Indicative Range (state is authority) | Decay Rate | Design Rationale |
|-----------------|--------------------------------------|------------|------------------|
| Unaware | 0–24 | `decay_rate_unaware` (8.0/sec) | Seeker forgot quickly; early near-misses are forgiving |
| Alert | 25–59 | `decay_rate_alert` (3.0/sec) | Seeker still on guard; slower recovery reflects real cost |
| Searching | 60–84 | `decay_rate_searching` (1.5/sec) | Seeker actively investigates; recovery is very slow |
| Chase | 85–99 | `decay_rate_chase` (0.5/sec) | Near-confirmation; decay is nearly cosmetic during active chase |

**Time to fully decay from Chase entry (85) to Unaware (0) after breaking detection:**
- Through Chase (85→75 at 0.5/sec): 20.0 sec
- Through Searching (75→50 at 1.5/sec): 16.7 sec
- Through Alert (50→15 at 3.0/sec): 11.7 sec
- Through Unaware (15→0 at 8.0/sec): 1.9 sec
- **Total: ~50 seconds of hiding and silence to fully reset from Chase**

This is intentional. Getting chased is a serious event with lasting consequences.

**Edge case — state boundary during decay**: Decay rate switches immediately when suspicion crosses a state boundary downward. No hysteresis on decay-direction rate switching (hysteresis applies only to behavioral state transitions, not decay rates).

---

### F5. State Transition Thresholds and Hysteresis

**Upward transitions (immediate):**

| Rising Threshold | Transition |
|-----------------|------------|
| suspicion > 25 | Unaware → Alert |
| suspicion > 60 | Alert → Searching |
| suspicion > 85 | Searching → Chase |
| player in catch_radius ≥ catch_dwell_time | Chase → Caught |

**Downward transitions (hysteresis applied):**

| Revert Transition | Downward Threshold | Hysteresis Gap |
|-------------------|-------------------|----------------|
| Chase → Searching | suspicion < 75 | 10 points |
| Searching → Alert | suspicion < 50 | 10 points |
| Alert → Unaware | suspicion < 15 | 10 points |

| Variable | Type | Range | Source | Description |
|---|---|---|---|---|
| `hysteresis_gap` | float | tuning | tuning | How far below each upward threshold suspicion must fall before state reverts |

**Edge case — suspicion skips a state in a single frame**: Valid and intentional. A large audio spike from Unaware (20) adding 45 points lands at 65 — directly in Searching, skipping Alert. All state `Enter()` methods must initialize from scratch without assuming prior state.

**Edge case — simultaneous Caught triggers from multiple seekers**: The first `OnPlayerCaught` event resolved in the FixedUpdate iteration fires the game-over/checkpoint flow. The Game Manager disables subsequent Caught events on the same frame. No double-trigger.

---

### F6. Hiding Spot Proximity Penalty (Creeping Dread)

Applied when `is_in_hiding_spot == true`, `peeking == false`, and seeker is
in Alert/Searching/Chase state.

```
proximity_distance   = distance from seeker position to hiding_spot.transform.position
is_near_spot         = proximity_distance <= hiding_spot_awareness_radius

/// seeker_state >= Alert is valid iff SeekState enum is declared in ascending severity order:
// Unaware = 0, Alert = 1, Searching = 2, Chase = 3, Caught = 4
// This comparison means: apply penalty when seeker is Alert, Searching, or Chase.
if (is_near_spot AND is_in_hiding_spot AND NOT peeking AND seeker_state >= Alert):
    proximity_factor     = 1 - (proximity_distance / hiding_spot_awareness_radius)
    creeping_dread_delta = creeping_dread_rate * proximity_factor * delta_time
    seeker.suspicion    += creeping_dread_delta
```

| Variable | Type | Range | Source | Description |
|---|---|---|---|---|
| `hiding_spot_awareness_radius` | float | tuning | tuning | Range within which seeker passively accumulates suspicion toward occupied spot (meters) |
| `proximity_factor` | float | 0.0–1.0 | computed | 1.0 at point-blank; 0.0 at radius boundary |
| `creeping_dread_rate` | float | tuning | tuning | Maximum suspicion/sec when seeker is at point-blank range of the spot |

**Expected output**: 0 (seeker outside radius or Unaware) to `creeping_dread_rate` suspicion/sec.

At recommended values (`creeping_dread_rate = 3.0/sec`, `radius = 2.5m`):
- Seeker standing directly outside spot for 10 seconds → 30 suspicion points (Unaware → Searching)
- Seeker walking past at average patrol speed (2 sec within radius at avg proximity_factor 0.5) → 3 suspicion points (minor, not immediately dangerous)

**Edge case — seeker exits radius during accumulation**: Penalty stops immediately the frame `is_near_spot` becomes false.

**Edge case — multiple seekers near one spot**: Each seeker evaluates independently. Suspicion accumulates on each seeker separately.

---

### F7. Chase Auto-Rise

Applied each FixedUpdate tick while the seeker is in Chase state, regardless of
whether LoS is established. Replaces the normal decay logic for Chase state —
suspicion does not decay during Chase unless the patience condition is met.

```
// Applied every FixedUpdate while seeker_state == Chase
// Ignores whether visual or audio detection is active —
// Chase seeker's suspicion rises automatically toward Caught.
suspicion += chase_auto_suspicion_rate * Time.fixedDeltaTime

// Patience decay override (Rule S-7):
// If seeker_state == Chase AND no LoS this tick AND no audio event since last tick,
// increment chase_no_input_timer. When timer >= chase_lost_patience_seconds,
// disable auto-rise and apply decay_rate_chase instead.
// chase_no_input_timer resets to 0 on any LoS or audio event.
```

| Variable | Type | Range | Source | Description |
|---|---|---|---|---|
| `chase_auto_suspicion_rate` | float | tuning | tuning | Suspicion points/sec added during Chase regardless of LoS input |
| `chase_no_input_timer` | float | 0–∞ | runtime | Per-seeker timer tracking consecutive seconds without LoS or audio; stored on `SuspicionMeter` component |
| `chase_lost_patience_seconds` | float | tuning | tuning | Seconds of silence + no-LoS required before patience expires and auto-rise stops |

**Expected behavior**: At `chase_auto_suspicion_rate = 5.0/sec`, a seeker entering
Chase at suspicion 85 reaches Caught-ready (100) in 3.0 seconds of continued
exposure. Breaking LoS and staying silent for `chase_lost_patience_seconds` (3.0s)
flips the seeker to decay mode at `decay_rate_chase` (0.5/sec), enabling the
~50-second full reset defined in F4.

---

## Edge Cases

| Scenario | Expected Behavior | Rationale |
|----------|------------------|-----------|
| Player enters hiding spot during Chase | Visual detection zeroes immediately. Seeker continues to last known position. Auto-rise continues. At last known position, proximity penalty activates if within radius. Player is NOT safe — must wait out proximity and silence. | Chase seeker doesn't magically know player hid, but isn't easily fooled. |
| Two seekers detect player simultaneously | Independent suspicion meters. Both can enter Chase. HUD shows highest value. Caught triggers on the first seeker to enter catch range. Second simultaneous Caught event is ignored (Game Manager disables after first). | Seekers are independent threats. No shared pool. |
| Player in fully dark room (light = 0.0) | `light_factor = 0` → visual_suspicion_delta = 0 for all seekers. LoS raycasts still run (consistent performance cost). Audio detection unaffected. Seeker behavior unchanged. | Full darkness = visual immunity. Audio is the only threat. Seekers don't carry lights. |
| Seeker hears a distraction during Chase | Audio spike is applied normally. However, Chase auto-rise overwhelms any spike effect. **Seeker does NOT update nav target to sound origin while in Chase.** Pursuit continues. Distractions cannot break an active Chase. | Chase is unbreakable by distractions. Distraction is a pre-Chase tool. This is a locked design decision. |
| Player makes noise while inside hiding spot | `NoiseEvent` emits at the hiding spot's attach transform position. Audio propagates normally. Combined with proximity penalty if seeker is nearby, suspicion can rise rapidly while hidden. | Being hidden is not being silent. |
| Suspicion exactly at a threshold boundary | Rising transitions use strict greater-than (`>`). Falling transitions use strict less-than (`<`). Exact equality does not trigger. No single-frame oscillation possible. | Prevents degenerate boundary toggling. |
| Player exits hiding spot while seeker is within proximity radius | `hide_modifier` reverts to 1.0 immediately. Visual detection formula resumes. This is a high-risk moment and intentionally so. | |
| Seeker state jumps over an intermediate state from audio spike | Valid. State `Enter()` methods initialize fresh without assuming prior state. | Must not assume seeker was in Alert before Searching. |

---

## Dependencies

| System | Direction | Nature of Dependency |
|--------|-----------|---------------------|
| Sound Propagation Model | Detection depends on | Provides attenuated intensity and audibility per seeker per event |
| Light Source System | Detection depends on | Provides light level at player position each frame via `GetLightLevelAtPosition(Vector3) → float`. **Blocking contract**: the Light Source System GDD must define whether this is a synchronous per-FixedUpdate call or a push-event model before Detection System implementation begins, as call frequency (once per seeker × FixedUpdate) has performance implications. |
| Player Noise Emitter | Detection depends on | Provides noise events with intensity; drives audio detection |
| Player Hiding (Hiding Spot System) | Detection depends on | Provides `IsHidden` bool, `IsPeeking` bool, and hiding spot position. Both states are consumed by F2 (`hide_modifier`) and F6 (penalty activation gate). |
| Player Movement | Detection depends on | Provides player crouch state (`IsCrouching` bool) for `crouch_stealth_multiplier` in F2 (Rule V-5) |
| Seeker AI State Machine | Depends on Detection | Consumes `DetectionOutput` to drive navigation and animation |
| HUD | Depends on Detection | Subscribes to `OnSuspicionChanged` and detection events |
| Adaptive Music | Depends on Detection | Subscribes to `OnGlobalThreatLevelChanged` |
| Level Timer + Stats | Depends on Detection | Subscribes to `OnDetectionEvent` for stat counting |

---

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect of Increase | Effect of Decrease |
|---|---|---|---|---|
| `base_detection_rate` | 15.0 suspicion/sec | 8–25 | Caught faster at ideal exposure; raises difficulty ceiling | More time at full exposure; more forgiving |
| `distance_falloff_exponent` | 2.0 | 1.0–3.0 | Close range more dangerous; max range nearly blind | Flatter curve; distance less impactful |
| `angle_falloff_exponent` | 2.0 | 1.0–3.0 | Edge of FOV very safe; strong reward for skirting the cone | Flat cone; no safe edge |
| `light_sensitivity_exponent` | 0.8 | 0.4–1.5 | Darkness less protective; dim light is dangerous | Darkness extremely protective; any shadow conceals |
| `peek_visibility_modifier` | 0.15 | 0.05–0.35 | Peeking is more dangerous | Peeking nearly as safe as hiding |
| `audio_base_spike` | 20.0 suspicion pts | 10–35 | Loud sounds more punishing | Audio events are minor nudges |
| `audio_spike_exponent` | 1.5 | 0.8–2.5 | Only loud sounds cause major spikes | Even faint sounds cause large spikes |
| `detection_cooldown_duration` | 1.5 sec | 0.5–3.0 | Longer pause before decay; cannot pulse in/out of detection | Decay begins immediately; pulsing viable |
| `decay_rate_unaware` | 8.0 suspicion/sec | 4.0–15.0 | Early near-misses cleared even faster | Early exposures have lasting cost |
| `decay_rate_alert` | 3.0 suspicion/sec | 1.0–6.0 | Alert clears quickly | Alert lingers; triggering a stop-and-look has serious time cost |
| `decay_rate_searching` | 1.5 suspicion/sec | 0.5–3.0 | Searching clears faster; more recovery windows | Searching effectively never decays |
| `decay_rate_chase` | 0.5 suspicion/sec | 0.1–1.5 | Chase begins to decay with brief LoS breaks | Suspicion locked during chase; only long hiding breaks it |
| `hysteresis_gap` | 10.0 suspicion pts | 5.0–20.0 | State reverts require larger drops; machine is "sticky" | Reverts happen near upward threshold; flickering risk |
| `hiding_spot_awareness_radius` | 2.5 m | 1.0–4.0 | Seekers detect occupied spots from farther; spots require better timing | Seeker must nearly touch the spot; spots are very safe |
| `creeping_dread_rate` | 3.0 suspicion/sec | 0.5–8.0 | Lingering seeker dangerous very quickly | Creeping dread negligible; spots are safe regardless of lingering |
| `crouch_stealth_multiplier` | 0.4 | 0.1–0.7 | Crouching less useful visually; harder to hide in plain sight | Crouching nearly conceals player from visual detection |
| `searching_visual_detection_multiplier` | 1.5 | 1.0–3.0 | Much more dangerous to be spotted during an active search | Searching adds no extra visual pressure beyond normal |
| `searching_audio_spike_multiplier` | 1.5 | 1.0–3.0 | Noise punished harder during active search | No audio escalation during search; same as normal |
| `seeker_hearing_threshold` | 0.35 | 0.0–**0.95 max** | Seeker requires louder sounds; walking more forgiving. **Hard cap 0.95** — ≥ 1.0 causes division by zero in F3; rejected at asset import | Even faint sounds detected; quiet movement unsafe |
| `chase_auto_suspicion_rate` | 5.0 suspicion/sec | 2.0–10.0 | Chase harder to escape; suspicion races to Caught | More opportunity to break Chase by hiding |
| `alert_scan_duration` | 3.0 sec | 1.5–6.0 | Longer "on guard" window before seeker resumes patrol | Seeker resumes quickly; Alert is a minor inconvenience |
| `search_radius` | 8.0 m | 4.0–15.0 | Wider search net; player must move far from last known position | Narrow search; a short relocation is enough to avoid Searching |
| `search_speed_multiplier` | 1.25 | 1.0–2.0 | Searching seeker closes faster; less time to reposition | Searching feels same speed as patrolling |
| `chase_speed_multiplier` | 1.6 | 1.2–2.5 | Chase very fast; sprinting required to maintain distance | Chase speed close to patrol; easier to break by walking |
| `chase_nav_update_interval` | 0.15 sec | 0.05–0.5 | More accurate pursuit; higher NavMesh compute cost per seeker | Less responsive; player can juke around corners |
| `catch_radius` | 1.2 m | 0.5–2.0 | Seeker grabs from farther; tight maneuvering still punished | Must nearly touch player; precise movement can survive close passes |
| `catch_dwell_time` | 0.15 sec | 0.05–0.5 | More reliable catch; brief contact not lethal | Instant; any entry into radius = caught immediately |
| `chase_lost_patience_seconds` | 3.0 sec | 1.0–8.0 | Chase lingers even when hiding; harder to break via cover | Chase breaks quickly; ducking around a corner is enough |

---

## Visual/Audio Requirements

| Event | Visual Feedback | Audio Feedback | Priority |
|-------|----------------|---------------|----------|
| **Sound detected** (seeker hears event) | Brief audio-wave icon above seeker (world space, fades over 1.5s) | Seeker "what was that?" vocalization; footsteps pause briefly | High (vocalization), Medium (icon) |
| **Alert raised** (suspicion > 25) | Seeker eye icon: closed/grey → open/yellow; suspicion meter fills to Alert band; single yellow screen-edge vignette pulse | Soft tension stinger; music → Alert layer | **Critical** |
| **Spotted — partial** (LoS active, Alert or Searching range) | FOV cone on floor (projected from head bone) partially fills yellow where player intersects | Music tension rises incrementally | **Critical** (cone is load-bearing for Pillar 3) |
| **Chase started** (suspicion > 85) | Eye icon flashes red; red directional indicator on screen edge pointing at seeker; suspicion meter pulses red; continuous red vignette | Loud chase stinger; seeker chase shout; music → Chase layer (crossfade, not cut) | **Critical** |
| **Seeker lost player** (Chase → Searching) | Eye icon: red → yellow; red vignette fades over 1.5s; chase indicator: red → yellow | Relief-adjacent "lost" stinger (tense, not celebratory); music → Searching layer | High |
| **Seeker fully calmed** (suspicion → 0) | Eye icon closes/grey; suspicion meter drains; all vignette clears | Music → patrol base state; optional soft "safe" stinger if all seekers return to Unaware | High |
| **Caught** | Full-screen blackout / flash; respawn or game-over screen | Catch stinger (seeker vocalization + impact); music stops | **Critical** |

**FOV cone implementation note**: The cone must be parented to the seeker's head bone and rotate with the head (not the body). This means the cone tracks head-turns during Alert scan, providing accurate legibility at the cost of a more complex floor-projection shader. This is a locked decision. The cone uses a custom material with soft edge falloff. It must be visible even at light level 0.0 — it is a UI element, not a light source.

---

## UI Requirements

| Information | Display Location | Update Frequency | When Shown |
|------------|-----------------|-----------------|------------|
| Suspicion meter (highest seeker value) | Center-bottom HUD | Every frame (visual lerp to smooth) | Always |
| State encoded in meter color | Suspicion meter fill: grey / yellow / orange / red | On state change event | Always |
| Seeker eye icon (per seeker) | Above seeker head, world space | On state change event | Always (may fade at very long distances) |
| FOV cone (per seeker) | Floor-projected from head bone, world space | Every frame (rotates with head) | Always |
| Chase direction indicator | Screen edge, pointing at chasing seeker | Every frame during Chase | Chase state only |
| Noise meter (player emission) | Bottom-left HUD — driven by Player Noise Emitter, not Detection System | Every frame (smoothed) | Always |
| Near-miss flash | Brief flash on suspicion meter | On event: suspicion crossed Alert then decayed within 3 seconds | On event only |

**Design principle**: The HUD should feel sparse. If the player is reading UI instead of reading the world, the HUD is too dominant. The primary legibility signals are the seeker's body language and audio — the HUD confirms what the world already told you.

---

## Acceptance Criteria

- [ ] **AC-01** Place player in FOV cone, direct LoS, light = 1.0. Suspicion rises within 1 second. **Pass**: meter rises. **Fail**: stays flat.
- [ ] **AC-02** Place player in FOV cone, direct LoS, light = 0.0. Walk for 5 seconds. Suspicion does not rise from visual. **Pass**: stays flat. **Fail**: rises.
- [ ] **AC-03** Player behind solid wall, out of LoS, in seeker audio range. Trigger a walk footstep event. Suspicion rises. **Pass**: rises. **Fail**: stays flat.
- [ ] **AC-04** Throw a distractable object 15m from player and seeker. Seeker turns toward impact position (not player) and enters Alert. **Pass**: seeker faces impact, enters Alert. **Fail**: seeker faces player or stays Unaware.
- [ ] **AC-05** Seeker reaches Alert. Player leaves LoS, stays silent. Suspicion decays fully to Unaware within 20 seconds. **Pass**: decays to 0. **Fail**: stuck above 0.
- [ ] **AC-06** Trigger Chase. While in Chase, throw a distraction far from player. Seeker continues pursuing player — does not redirect to distraction. **Pass**: seeker continues toward player. **Fail**: seeker redirects.
- [ ] **AC-07** Player enters hiding spot while seeker has direct LoS. After entering, visual contribution to suspicion stops. **Pass**: visual delta stops. **Fail**: meter continues rising at visual rate.
- [ ] **AC-08** Player enters hiding spot. Seeker walks within `hiding_spot_awareness_radius`. Suspicion rises with no sound events. **Pass**: rises from proximity penalty. **Fail**: stays flat.
- [ ] **AC-09** Two seekers detect player via audio simultaneously. Each seeker's suspicion rises independently. HUD shows the higher value. **Pass**: independent meters, HUD shows max. **Fail**: shared meter or average.
- [ ] **AC-10** Seeker catches player (in catch_radius for ≥ catch_dwell_time). `OnPlayerCaught` fires exactly once. Respawn/game-over screen appears. **Pass**: fires once, screen appears. **Fail**: fires multiple times or screen absent.
- [ ] **AC-11** In fully dark room (light = 0.0), FOV cone visualization remains rendered on floor. **Pass**: cone visible. **Fail**: cone disappears.
- [ ] **AC-12** Two seekers in Chase simultaneously. Screen vignette active. Both eye icons red. Direction indicator points at nearest chasing seeker. **Pass**: vignette active, both red, indicator shows nearest. **Fail**: any element absent.
- [ ] **Performance** Detection system FixedUpdate cycle completes within 0.5ms with 4 simultaneous seekers active (measured via Unity Profiler, release build).
- [ ] **No hardcoded values** All tuning knob values live in seeker data assets or the Detection System ScriptableObject. No magic numbers in code.
- [ ] **AC-13** While in Chase, player breaks LoS and stays silent. After exactly `chase_lost_patience_seconds` (3.0s default), seeker transitions from Chase to Searching. **Pass**: transition occurs at patience expiry with no LoS/audio. **Fail**: seeker stays in Chase indefinitely or transitions before patience expires.

---

## Open Questions

| Question | Owner | Resolution Path |
|----------|-------|----------------|
| OQ-1: What is the right proximity penalty radius and rate? The recommended `2.5m / 3.0/sec` is a hypothesis — a seeker 2.5m from a wardrobe for 10 seconds reaching Searching feels right but needs validation. | Game Designer | Playtest Level_1 with 3+ configurations. Measure how long players feel "tense but not cheated" inside a spot. |
| OQ-2 (resolved): Distraction during Chase — **locked as unbreakable**. No further testing needed unless playtest data contradicts the decision. | — | Resolved 2026-03-04. |
| OQ-3: FOV cone floor-projection implementation — locked as "follows head bone." The shader to project a head-bone-parented cone cleanly onto a non-flat floor needs a visual prototype before shipping. | Lead Programmer | Prototype the cone shader in Sprint 1 before any other visual systems reference it. |
| OQ-4: "Heard something" behavioral response at very low audio intensities — should the seeker head-turn even when `audio_suspicion_spike` is near zero? | Game Designer | Prototype in Sprint 1 alongside detection system. Define `min_behavioral_audio_threshold` as a tuning knob if head-turns are implemented. |
| OQ-5: Catch dwell time and radius legibility — is `catch_dwell_time = 0.15s` readable as fair, or should it be 0.5s with a visible catch-radius indicator? | Game Designer | Playtest with 5+ fresh players. Record whether they felt catches were fair. Adjust before Vertical Slice. |
