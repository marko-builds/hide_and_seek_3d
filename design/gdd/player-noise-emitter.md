# Player Noise Emitter

> **Status**: Approved
> **Author**: game-designer + systems-designer
> **Last Updated**: 2026-03-05
> **Implements Pillar**: Silence Is a Tool (Pillar 2), The Room Has Rules (Pillar 1)

---

## 1. Overview

The Player Noise Emitter (PNE) is the sole origin point of player-body `NoiseEvent`s in UNSEEN. It monitors player state and discrete player actions — movement speed tier, surface contact, jumps, landings, interactions, and hide-spot transitions — constructs a `NoiseEvent` struct with the correct `WorldPosition`, `BaseIntensity`, and `SurfaceType` for each event, and fires the struct onto the static `NoiseEmitter.OnNoiseEmitted` event bus. It sits at the very start of the detection audio pipeline: the Sound Propagation Model (SPM) receives its events, evaluates attenuation per seeker, and the Detection System converts audibility into suspicion. The PNE owns neither attenuation nor suspicion — it owns only the decision of when and with what loudness to announce the player's existence. In parallel, the PNE publishes a smoothed normalized noise level directly to the HUD so the player can see their current noise profile without passing through the detection pipeline. Every intensity value the PNE uses is sourced from the `SoundData` ScriptableObject; nothing is hardcoded.

---

## 2. Player Fantasy

Pillar 2 declares: silence is a budget, not an absence. The Player Noise Emitter is the mechanical implementation of that declaration.

The player should feel like a composer working under a strict tempo and dynamic constraint. Every footfall is a beat. The surface underfoot is the instrument. The speed tier is the volume. The player does not silence themselves by doing nothing — they manage their noise budget by choosing *what* to do, *when* to do it, and *where* to stand when they do it.

The emotional arc the PNE must support, in the order a new player discovers it:

**Discovery: "I make sound."** Early in the first chamber, the player hears their own footsteps and watches the HUD noise indicator pulse. They stop. It stops. They sprint. It spikes. The system is immediately readable. The player is not confused — they are informed.

**Calibration: "Different floors are different risks."** The player steps from stone onto a wooden bridge section and sees the HUD indicator jump. They step back. It drops. They understand: the floor is part of the equation. This is the first moment of Pillar 2 in action — the player is not avoiding noise, they are reading it.

**Mastery: "I can spend noise deliberately."** An advanced player crouches across carpet to break line of sight, then makes a deliberate loud noise — an interaction or a throw — on the far side of the room to redirect a seeker. They are not minimizing noise; they are budgeting it. The PNE must make every emission feel like a conscious decision, not an accident. The system must never surprise the player with unexpected noise. Predictability is not a limitation — it is the prerequisite for mastery.

**Target MDA aesthetics**: Challenge (can I move through this space without triggering detection?), Discovery (learning surface-to-intensity relationships), and Expression (choosing noise deliberately as a tactical tool).

---

## 3. Detailed Design

### 3.1 Emission Responsibility Map

The Player Noise Emitter is responsible for exactly the following events. Any event not listed here is out of scope and must not be handled here.

| Event | Emitted By | WorldPosition | Notes |
|-------|-----------|--------------|-------|
| Walking footstep | Player Noise Emitter | Player world position at emit time | Cadence governed by distance-covered threshold (see 3.3) |
| Crouching-walk footstep | Player Noise Emitter | Player world position | Lower base intensity; same distance threshold but wider spacing (see 3.3) |
| Sprinting footstep | Player Noise Emitter | Player world position | Higher base intensity; narrower step distance (more frequent events at speed) |
| Landing from jump | Player Noise Emitter | Player world position | Single spike event on the first `FixedUpdate` frame the player is grounded after an airborne arc |
| Throw origin (arm effort) | Player Noise Emitter | Player world position at moment of throw input | Represents the physical effort of throwing; a single event |
| Thrown object impact | **Throwable Object component** | Impact world position | NOT the PNE. PNE has zero involvement after the throw-origin event fires |
| Interacting with prop | Player Noise Emitter | Player world position | Fires once per discrete interact input registered by PlayerInputHandler |
| Entering hiding spot | Player Noise Emitter | Player world position at the hiding spot attach point | Fires on hide-enter transition |
| Exiting hiding spot | Player Noise Emitter | Player world position at the hiding spot attach point | Fires on hide-exit transition |

Events that are explicitly NOT the PNE's responsibility:

- Seeker footstep and vocalization events — emitted by Seeker AI components
- Thrown object impact events — emitted by Throwable Object component
- Environmental interaction events (doors opening, objects falling via physics) — emitted by Environmental Interaction system (Vertical Slice scope)
- Any sound effect playing for the player's audio feedback — routed through Footstep Audio system

### 3.2 Input Source: PlayerInputHandler Events

The Player Noise Emitter does not read `InputSystem_Actions` directly. `PlayerInputHandler` is the only class that touches the Input System asset. The PNE subscribes to the following C# events published by `PlayerInputHandler` and related components:

| Event | Publisher | PNE Response |
|-------|----------|-------------|
| `OnMovementChanged(Vector2 input)` | PlayerInputHandler | Updates `isMoving` flag and velocity vector used in footstep cadence tracking |
| `OnSprintChanged(bool isSprinting)` | PlayerInputHandler | Updates speed tier; adjusts active step distance threshold |
| `OnCrouchChanged(bool isCrouching)` | PlayerInputHandler | Updates speed tier; adjusts active step distance threshold |
| `OnJumpStarted()` | PlayerInputHandler | Sets `isAirborne = true`; suspends footstep cadence tracking; captures pre-jump surface type |
| `OnLanded()` | PlayerMovement | Sets `isAirborne = false`; triggers landing spike on next `FixedUpdate` |
| `OnInteractPerformed()` | PlayerInputHandler | Fires a single interact `NoiseEvent` at current player position |
| `OnThrowPerformed()` | PlayerInputHandler (via gadget system) | Fires a single throw-origin `NoiseEvent` at current player position |
| `OnHideEntered()` | PlayerHiding | Fires entering `NoiseEvent` at hide spot position |
| `OnHideExited()` | PlayerHiding | Fires exiting `NoiseEvent` at hide spot position |

The PNE accesses player speed and position through `PlayerMovement` (which it receives via `[SerializeField]` inspector reference — not via `FindObjectOfType`).

### 3.3 Footstep Cadence System: Distance-Covered Threshold

Footstep `NoiseEvent`s emit on a **distance-covered threshold model**, not on a fixed time interval and not on animation events.

**Why distance, not time:**
- A time interval fires at the same rate regardless of movement speed. Faster movement should produce more noise events per second — more steps per second means more sound.
- A distance threshold ensures one noise event per stride regardless of frame rate. "Each step makes noise" is intuitive and teachable (Pillar 1, Pillar 2).
- Sprint naturally produces more events per second than walk because the player covers the threshold distance more quickly — no special-casing required.
- Distance threshold is animation-independent, which is required because animation events cannot be guaranteed to fire at the correct frequency across all frame rates.

**Threshold tracking:**
The PNE maintains a `distanceTraveled` float accumulator. Each `FixedUpdate` while the player is grounded and moving (velocity magnitude above the stationary threshold), the PNE adds `velocity.magnitude * Time.fixedDeltaTime` to the accumulator. When the accumulator meets or exceeds the active step distance threshold for the current speed tier, a footstep `NoiseEvent` fires and the accumulator resets to zero. Any overflow — the amount by which the accumulator exceeded threshold — is discarded, not carried over. This prevents step-bunching at high frame rates.

**Speed tier thresholds:**

| Speed Tier | Active When | Step Distance Threshold | Base Intensity | SurfaceType Field |
|-----------|------------|------------------------|---------------|------------------|
| Walk | `isMoving`, not sprinting, not crouching | `walkStepDistance` (default 1.4 m) | Surface-specific from SoundData | Surface of current floor (raycast) |
| Crouch-Walk | `isMoving` AND `isCrouching` | `crouchStepDistance` (default 2.2 m) | Surface-specific (lower set) from SoundData | Surface of current floor (raycast) |
| Sprint | `isMoving` AND `isSprinting` | `sprintStepDistance` (default 0.9 m) | 0.85 (surface-agnostic) from SoundData | Surface of current floor (raycast) |

Sprint note: The sprint `BaseIntensity` is a single surface-agnostic value (0.85), per SPM Rule N-2a. The SPM's surface multiplier still applies on top. Walk and crouch-walk tiers use surface-specific `BaseIntensity` values authored per surface in `SoundData` (the base captures the acoustic character of the impact itself).

Crouch suppression is implemented through two compounding mechanisms: a lower `BaseIntensity` authored in the SoundData asset, and a wider step distance (fewer events per meter traveled). Both mechanisms are intentional and tunable independently. A player who crouches is emitting fewer noise events over distance, each at a lower intensity — crouch is genuinely quieter in every measurable dimension.

### 3.4 All Player Actions and Their Emission Rules

| Action | Trigger | WorldPosition | BaseIntensity Source | SurfaceType | One-Shot or Cadence? |
|--------|--------|--------------|---------------------|------------|---------------------|
| Walk | `distanceTraveled >= walkStepDistance` while grounded, moving, not sprinting, not crouching | Player position | `SoundData.walkIntensity[SurfaceType]` (surface-specific) | Downward raycast result | Cadence |
| Crouch-Walk | `distanceTraveled >= crouchStepDistance` while grounded, moving, crouching | Player position | `SoundData.crouchWalkIntensity[SurfaceType]` (surface-specific) | Downward raycast result | Cadence |
| Sprint | `distanceTraveled >= sprintStepDistance` while grounded, moving, sprinting | Player position | `SoundData.sprintIntensity` (0.85, surface-agnostic) | Downward raycast result | Cadence |
| Landing from jump | First `FixedUpdate` frame where `isAirborne` transitions false | Player position | `SoundData.landingIntensity` (0.85) | Downward raycast result | One-shot |
| Throw origin | `OnThrowPerformed` fires | Player position | `SoundData.throwOriginIntensity` (0.50) | `SurfaceType.Neutral` | One-shot |
| Interact with prop | `OnInteractPerformed` fires | Player position | `SoundData.interactIntensity` (0.35 default; per-prop override supported) | `SurfaceType.Neutral` | One-shot |
| Enter hiding spot | `OnHideEntered` fires | Hiding spot attach transform position | `SoundData.hideEntryIntensity` (0.25) | `SurfaceType.Neutral` | One-shot |
| Exit hiding spot | `OnHideExited` fires | Hiding spot attach transform position | `SoundData.hideExitIntensity` (0.25) | `SurfaceType.Neutral` | One-shot |

Interaction note: For MVP, a single `SoundData.interactIntensity` value covers all prop interactions. Individual prop types can pass an intensity override when `OnInteractPerformed` fires; otherwise the default is used.

### 3.5 Surface Type Determination

Per SPM Rule ST-2, surface type for footstep events is determined by a single downward `Physics.Raycast` from the player's foot position (`playerPosition + footRaycastOriginOffset`, default `(0, 0.05, 0)` to originate just above the floor) against the `SurfaceDetection` layer mask. The ray fires downward (`Vector3.down`) with length `footRaycastLength` (default 0.3 m).

- If the hit collider has a `SurfaceTypeTag` component: use `SurfaceTypeTag.SurfaceType`.
- If the hit collider has no `SurfaceTypeTag`, or the ray misses entirely: default to `SurfaceType.Stone` (fail-loud: untagged surfaces behave as the baseline dungeon floor, biasing toward detection rather than granting unearned silence).

The raycast fires once per footstep cadence tick (at the moment the accumulator threshold is crossed), not every `FixedUpdate`. This keeps physics overhead proportional to the noise event rate.

The resolved `SurfaceType` is published via a C# event `OnSurfaceTypeResolved(SurfaceType)` on each footstep tick so that the Footstep Audio system can select the correct audio clip without performing a duplicate downward raycast. The PNE owns the authoritative surface type resolution per footstep cycle.

The `Physics.Raycast` call uses the non-allocating signature with a layer mask. No `RaycastAll` or `OverlapSphere` is used. The `RaycastHit` is a value-type local; no heap allocation occurs per footstep.

### 3.6 Static Event Bus Architecture

The PNE fires events onto a static C# event bus defined in the `NoiseEmitter` class:

```csharp
// In NoiseEmitter.cs (namespace HideAndSeek)
public static event Action<NoiseEvent> OnNoiseEmitted;

public static void Emit(NoiseEvent noiseEvent)
{
    OnNoiseEmitted?.Invoke(noiseEvent);
}
```

The PNE calls `NoiseEmitter.Emit(noiseEvent)` every time a qualifying player action occurs. The SPM subscribes to `NoiseEmitter.OnNoiseEmitted` and evaluates attenuation per seeker synchronously in the same frame. The event fires synchronously — no coroutines, no `async/await`, no deferred queuing. `NoiseEvent` is a `struct` (stack-allocated); the event invocation does not allocate on the heap provided subscribers do not capture closures.

`NoiseListener` components on seekers also subscribe to `NoiseEmitter.OnNoiseEmitted`, but they filter by distance as a pre-SPM gate. The PNE is not aware of `NoiseListener` and does not interact with it directly.

### 3.7 HUD Noise Indicator Feed

Per Detection System Rule A-5: the player cannot hear their own `NoiseEvent`s through the Detection System. The HUD noise indicator is driven directly by the PNE, not routed through the SPM or Detection System.

The PNE maintains a `_currentNoiseLevel` float (0.0–1.0) that it updates each time a `NoiseEvent` fires and each `Update` frame for decay. It publishes this value via a separate C# event:

```csharp
public static event Action<float> OnPlayerNoiseLevelChanged;
```

The HUD subscribes to `OnPlayerNoiseLevelChanged` and updates the noise indicator display. The HUD never queries the PNE directly; it reacts to events only (per project convention: all UI reacts to events, never calls gameplay code directly).

**HUD noise level derivation:**
`_currentNoiseLevel` is set to `max(_currentNoiseLevel, BaseIntensity)` on each emission, then decays linearly toward 0.0 over `noiseIndicatorDecayDuration` seconds. If a new `NoiseEvent` fires during decay, the level takes the max of the current and incoming values — the highest recent emission dominates. This prevents the indicator from flickering on rapid footstep cadence.

The level is derived from `BaseIntensity` specifically (not from the SPM's attenuated result) because the HUD shows the player how loud they are intrinsically. Showing the attenuated value would require knowing each seeker's position and would differ per seeker, which is not useful for self-monitoring.

```csharp
// On NoiseEvent emission:
_currentNoiseLevel = Mathf.Max(_currentNoiseLevel, noiseEvent.BaseIntensity);
OnPlayerNoiseLevelChanged?.Invoke(_currentNoiseLevel);

// In Update (decay):
if (_currentNoiseLevel > 0f)
{
    _currentNoiseLevel -= (1f / noiseIndicatorDecayDuration) * Time.deltaTime;
    _currentNoiseLevel = Mathf.Max(0f, _currentNoiseLevel);
    if (Mathf.Abs(_currentNoiseLevel - _lastPublishedLevel) > hudPublishThreshold)
    {
        OnPlayerNoiseLevelChanged?.Invoke(_currentNoiseLevel);
        _lastPublishedLevel = _currentNoiseLevel;
    }
}
```

---

## 4. Formulas

### F-PNE-1: Footstep Cadence (Distance-Covered Threshold)

Each `FixedUpdate`, while the player is grounded and moving:

```
distanceTraveled += velocity.magnitude * Time.fixedDeltaTime
```

When the threshold is met:

```
if (distanceTraveled >= activeStepDistance)
{
    EmitFootstepNoiseEvent()
    distanceTraveled = 0f          // discard overflow; do not carry over
}
```

Where `activeStepDistance` is selected by speed tier:

```
activeStepDistance =
    isCrouching ? crouchStepDistance
  : isSprinting ? sprintStepDistance
  :               walkStepDistance
```

| Variable | Type | Range | Source | Description |
|----------|------|-------|--------|-------------|
| `distanceTraveled` | float | 0.0 – activeStepDistance | PNE runtime state | Accumulated ground distance since last footstep event |
| `velocity.magnitude` | float | 0.0 – sprintMaxSpeed | PlayerMovement | Player world-space velocity at this FixedUpdate |
| `Time.fixedDeltaTime` | float | ~0.02 s | Unity | Fixed timestep duration |
| `walkStepDistance` | float | 0.8–2.5 m | SoundData asset | Distance per step in walk tier (default 1.4 m) |
| `crouchStepDistance` | float | 1.2–3.5 m | SoundData asset | Distance per step in crouch tier (default 2.2 m) |
| `sprintStepDistance` | float | 0.5–1.5 m | SoundData asset | Distance per step in sprint tier (default 0.9 m) |

**Events per second at default values (approximate):**

| Speed Tier | Typical Speed | Events/second |
|-----------|--------------|--------------|
| Walk | ~3.0 m/s | 3.0 / 1.4 ≈ 2.1 events/s |
| Crouch-Walk | ~1.5 m/s | 1.5 / 2.2 ≈ 0.7 events/s |
| Sprint | ~6.0 m/s | 6.0 / 0.9 ≈ 6.7 events/s |

Sprint produces roughly 3× as many noise events per second as walk, compounding the higher per-step `BaseIntensity` (0.85 vs 0.50 on stone). Sprinting is louder in both dimensions — higher per-event intensity and higher event frequency. This is intentional.

**Example — Walk on Stone, 5 seconds:**
- Speed = 3.0 m/s, walkStepDistance = 1.4 m → 10 events; each BaseIntensity = 0.50, SurfaceType = Stone

**Example — Sprint on Wood, 2 seconds:**
- Speed = 6.0 m/s, sprintStepDistance = 0.9 m → 13 events; each BaseIntensity = 0.85, SurfaceType = Wood
- SPM applies Wood multiplier (1.30): surface-modified intensity = 0.85 × 1.30 = 1.105 before distance/occlusion

---

### F-PNE-2: Stationary Threshold

```
isStationary = velocity.magnitude < stationaryVelocityThreshold
```

| Variable | Type | Range | Source | Description |
|----------|------|-------|--------|-------------|
| `stationaryVelocityThreshold` | float | 0.01–0.20 m/s | SoundData asset | Min velocity to register movement; must exceed physics jitter on dungeon geometry (default 0.05 m/s) |

When `isStationary`: `distanceTraveled` is NOT accumulated and NOT reset. It retains its current value so the next step after stopping does not require a full fresh threshold distance to trigger. This prevents an artificial silent gap when the player briefly stops and resumes walking.

> **QA tuning note:** `stationaryVelocityThreshold` is a QA target, not a design constant. It must be set above the maximum physics jitter amplitude measured on the actual dungeon geometry in the test scene. Verify in playtesting before locking.

---

### F-PNE-3: HUD Noise Level

On emission:

```
_currentNoiseLevel = max(_currentNoiseLevel, noiseEvent.BaseIntensity)
```

On `Update` decay:

```
_currentNoiseLevel = max(0.0, _currentNoiseLevel - (1.0 / noiseIndicatorDecayDuration) * deltaTime)
```

Publish condition:

```
if abs(_currentNoiseLevel - _lastPublishedLevel) > hudPublishThreshold:
    OnPlayerNoiseLevelChanged.Invoke(_currentNoiseLevel)
    _lastPublishedLevel = _currentNoiseLevel
```

| Variable | Type | Range | Source | Description |
|----------|------|-------|--------|-------------|
| `_currentNoiseLevel` | float | 0.0–1.0 | PNE runtime state | Current smoothed noise level for HUD display |
| `noiseIndicatorDecayDuration` | float | 0.3–3.0 s | SoundData asset | Time to decay from 1.0 to 0.0 (default 0.8 s) |
| `hudPublishThreshold` | float | 0.005–0.05 | SoundData asset | Min change required to fire publish event (default 0.01) |
| `_lastPublishedLevel` | float | 0.0–1.0 | PNE runtime state | Last published value; suppresses micro-updates |

**Example — Sprint on Wood then stop:**
- Emit: `_currentNoiseLevel` = 0.85. Decay rate = 1/0.8 = 1.25/s.
- After 0.3 s: 0.85 − (1.25 × 0.3) = 0.475
- After 0.68 s: reaches 0.0. HUD indicator fully silent.

---

### F-PNE-4: One-Shot Event Construction

For non-footstep events (landing, throw origin, interact, hide entry/exit):

```
NoiseEvent e;
e.WorldPosition  = GetEmissionPosition(actionType)
e.BaseIntensity  = SoundData.GetBaseIntensity(actionType)
e.SurfaceType    = GetSurfaceType(actionType)
NoiseEmitter.Emit(e)
```

Where:
- `GetEmissionPosition`: player transform position for all actions; hiding spot attach point for hide entry/exit
- `SoundData.GetBaseIntensity`: lookup by action enum key; no hardcoded literal
- `GetSurfaceType`: `SurfaceType.Neutral` for throw, interact, hide; downward raycast result for landing

---

## 5. Edge Cases

| # | Scenario | Expected Behavior | Rationale |
|---|----------|------------------|-----------|
| EC-PNE-01 | Player jumps off a ledge with near-zero drop height and lands within one `FixedUpdate` (no airborne frames). | `isAirborne` is set `OnJumpStarted` and cleared `OnLanded`. If both fire in the same frame, airborne state holds for zero ticks. Landing spike fires on the next `FixedUpdate` where `isAirborne` is false and the transition flag is set. Footstep accumulation was suspended for zero ticks; no step events are lost. Landing spike fires normally. | A zero-arc jump is still a jump and the foot impact still occurs. Landing spike must fire. |
| EC-PNE-02 | Player begins sprinting mid-stride (between two threshold crossings). | Speed tier switches immediately on `OnSprintChanged`. `activeStepDistance` updates to `sprintStepDistance`. `distanceTraveled` accumulator is retained, not reset. Next threshold crossing uses sprint distance. First post-sprint step may fire slightly early or late — imperceptible in play. | Resetting the accumulator would create an artificial silent gap. Retaining it produces a slightly irregular first sprint stride. |
| EC-PNE-03 | Player transitions from crouching to standing mid-stride. | Same as EC-PNE-02: tier switches immediately, accumulator retained. Crouch-to-stand: remaining distance to walk threshold is shorter than a full walk stride, so first standing step may fire sooner than expected. This is correct — standing up is inherently louder and the player should feel the transition promptly. | The asymmetry (stand up = sooner step; crouch down = later step) correctly represents each transition's acoustic character. |
| EC-PNE-04 | Surface type changes mid-step (floor material transitions under the player between cadence ticks). | Downward raycast fires at the moment the threshold is crossed, not continuously. The raycast hits whichever surface is directly below the foot at that instant. No interpolation between surfaces. | One raycast per threshold crossing is the design. Surface blending is out of scope. |
| EC-PNE-05 | Player interacts with a prop while crouching. | `OnInteractPerformed` fires a single interact `NoiseEvent` regardless of crouch state. Crouch state does not suppress or reduce interaction noise. Interaction noise is the sound of the prop, not the player's body weight. | Crouching suppresses footstep noise only. A crouching player does not silently open a door — the door makes noise. |
| EC-PNE-06 | Player's foot overshoots a ledge mid-step (threshold crossed while falling off a ledge, before `OnJumpStarted` fires). | PNE checks `isAirborne` before emitting any footstep. If `isAirborne` is true at threshold-crossing time, the event is suppressed. Accumulator resets to zero. No footstep fires during airborne phase. | Airborne players do not make footstep sounds. The landing spike handles the landing acoustic; accumulated mid-air distance must not produce a phantom step. |
| EC-PNE-07 | Two actions overlap in the same `FixedUpdate` frame (e.g., landing while pressing Interact). | Each action fires its `NoiseEvent` independently in the same frame. `NoiseEmitter.OnNoiseEmitted` fires twice. SPM evaluates each event independently. Detection System may accumulate suspicion from both events in the same tick. HUD `_currentNoiseLevel` takes the max of both BaseIntensity values. | Two simultaneous actions are genuinely louder than one. No deduplication or queuing. |
| EC-PNE-08 | Player is inside a hiding spot and makes an interact or movement input. | PNE still emits `NoiseEvent`s for qualifying actions. Hiding does not suppress noise emission — it only affects the visual detection path (Detection System Rule H-2). A player who fidgets inside a hiding spot is audible. | Being hidden is not being silent. |
| EC-PNE-09 | Player throws while crouching and stationary. | `OnThrowPerformed` fires the throw-origin `NoiseEvent` at player position, BaseIntensity = `SoundData.throwOriginIntensity`, SurfaceType = Neutral. Crouch state has no effect on throw-origin intensity. Impact event is the Throwable Object's responsibility. | Throw effort is a body action, not a footstep. Crouch does not silence it. |
| EC-PNE-10 | Player stands perfectly still; `velocity.magnitude` fluctuates around `stationaryVelocityThreshold` due to physics micro-jitter on uneven geometry. | `stationaryVelocityThreshold` must be set above the maximum jitter amplitude measured on dungeon geometry in the test scene. Accumulator advances only when `velocity.magnitude >= stationaryVelocityThreshold`. Micro-jitter that does not cross the threshold does not advance the accumulator. | Stationary silence must be absolute and reliable. The threshold is a QA tuning target. |
| EC-PNE-11 | Player performs hide entry/exit in rapid succession. | Both `OnHideEntered` and `OnHideExited` fire their `NoiseEvent`s independently. Two events fire in close succession. No cooldown applied between them. | Each hide action is a discrete physical event. If rapid toggling becomes an exploit, a minimum hide duration gates the input at `PlayerHiding` level — outside PNE scope. |
| EC-PNE-12 | `SoundData` ScriptableObject is null or missing at runtime. | PNE performs null check on `SoundData` in `Awake`. If null, logs a Unity error and disables itself (`enabled = false`). No `NullReferenceException` per-frame. No `NoiseEvent`s emitted. Game enters degraded state (no player noise). | Fail gracefully. The null check in `Awake` is an acceptance criterion. |

---

## 6. Dependencies

### Systems This Depends On

| System | Direction | Contract | Notes |
|--------|----------|---------|-------|
| Sound Propagation Model | PNE → SPM | `NoiseEvent` struct via `NoiseEmitter.OnNoiseEmitted` | SPM subscribes to the bus; PNE fires. SPM owns attenuation; PNE owns emission. |
| SoundData ScriptableObject | PNE reads | `BaseIntensity` per action type, step distances, decay durations, thresholds | All numeric values sourced here. No hardcoded floats in the PNE. |
| PlayerInputHandler | PNE subscribes | C# events: `OnMovementChanged`, `OnSprintChanged`, `OnCrouchChanged`, `OnJumpStarted`, `OnInteractPerformed`, `OnThrowPerformed` | PNE does not touch InputSystem_Actions. Subscribes in `OnEnable`, unsubscribes in `OnDisable`. |
| PlayerMovement | PNE reads | `velocity` (Vector3), `isGrounded` (bool); `OnLanded()` C# event | PlayerMovement owns physics; PNE reads state. |
| PlayerHiding | PNE subscribes | `OnHideEntered(Transform attachPoint)`, `OnHideExited(Transform attachPoint)` | PNE receives hiding spot attach point as WorldPosition. |
| Unity Physics | PNE uses | `Physics.Raycast` (non-allocating, layer mask) | One raycast per footstep cadence tick. Never per-frame. |
| SurfaceTypeTag | PNE reads | `SurfaceType` enum property on collider component | PNE performs authoritative surface type resolution for each footstep. |

### Systems That Depend On This

| System | Direction | Contract | Notes |
|--------|----------|---------|-------|
| Sound Propagation Model | Depends on PNE | Receives `NoiseEvent` structs via `NoiseEmitter.OnNoiseEmitted` | SPM evaluates attenuation per seeker on each event |
| HUD (Noise Indicator) | Depends on PNE | Receives `float` (0.0–1.0) via `OnPlayerNoiseLevelChanged` | HUD must not query PNE directly; event-only coupling |
| Footstep Audio System | Depends on PNE | Receives `SurfaceType` via `OnSurfaceTypeResolved(SurfaceType)` on each footstep tick | No duplicate raycast in Footstep Audio |
| Detection System | Indirect | Receives `NoiseEvent` results routed through SPM | PNE feeds the audio detection pipeline at the origin |

---

## 7. Tuning Knobs

All values authored in `SoundData` at `Assets/_Project/Scripts/Data/SoundData.asset`. No values hardcoded in the PNE MonoBehaviour.

| Knob | Category | Default | Safe Range | Effect of Increase | Effect of Decrease |
|------|---------|---------|------------|-------------------|--------------------|
| `walkStepDistance` | Feel | 1.4 m | 0.8–2.5 m | Fewer events per meter; walk feels quieter (fewer events, not lower per-event intensity) | More events per meter; walk feels more frantic |
| `crouchStepDistance` | Feel | 2.2 m | 1.2–3.5 m | Crouch emits even fewer events; stealth approach even quieter | Crouch closer to walk cadence; less acoustic distinction |
| `sprintStepDistance` | Feel | 0.9 m | 0.5–1.5 m | Fewer sprint events; sprint less distinct from walk in frequency | Very frequent sprint events; extremely rapid noise |
| `stationaryVelocityThreshold` | Gate | 0.05 m/s | 0.01–0.20 m/s | Smaller movements register as stationary | Slower movements register as moving; jitter risk |
| `walkIntensity[Stone]` | Curve | 0.50 | 0.30–0.70 | Stone walk louder; main dungeon more dangerous | Stone walk quieter; baseline environment less threatening |
| `walkIntensity[Wood]` | Curve | 0.65 | 0.45–0.80 | Wood louder; surface hazard more punishing | Wood closer to stone; surface choice matters less |
| `walkIntensity[Carpet]` | Curve | 0.25 | 0.10–0.40 | Carpet louder; safe refuge less safe | Carpet nearly silent; trivially safe surface |
| `crouchWalkIntensity[Stone]` | Curve | 0.25 | 0.10–0.40 | Crouching less effective acoustic improvement | Crouching very effective; becomes dominant strategy |
| `crouchWalkIntensity[Carpet]` | Curve | 0.15 | 0.05–0.30 | Carpet crouch-walk louder; double-muffling effect reduced | Near-silent (0.15 × 0.50 SPM multiplier = 0.075 effective) — **see SPM EC-7**: this double-muffling is intentional. Adjust `crouchWalkIntensity[Carpet]` here rather than the surface multiplier if the combination proves too quiet in playtesting. |
| `sprintIntensity` | Curve | 0.85 | 0.65–1.00 | Sprint even louder; dangerous except in clear corridors | Sprint less distinct from walk; trade-off unclear. **Note:** surface-agnostic BaseIntensity (0.85), but SPM surface multiplier still applies. Sprint on Metal = 0.85 × 1.55 = 1.31 effective intensity — the loudest achievable combination. Verify in playtesting that metal corridors are not frustratingly unnavigable at sprint. |
| `landingIntensity` | Curve | 0.85 | 0.60–1.00 | Landing louder; jumping has higher audio cost | Landing quieter; jumps are acoustically cheap |
| `throwOriginIntensity` | Curve | 0.50 | 0.25–0.70 | Throw effort louder; two-part throw sound more prominent | Throw effort barely audible; only impact matters |
| `interactIntensity` (default) | Curve | 0.35 | 0.20–0.60 | Interactions louder; every prop touch is a risk | Interactions quieter; props feel safer to use |
| `hideEntryIntensity` | Curve | 0.25 | 0.10–0.50 | Entering hiding spots louder; seeking shelter is itself a risk | Near-silent entry; hiding is always acoustically safe to enter |
| `hideExitIntensity` | Curve | 0.25 | 0.10–0.50 | Exiting louder; leaving cover is a committed action | Near-silent exit; leaving cover has no audio cost |
| `noiseIndicatorDecayDuration` | Feel | 0.8 s | 0.3–3.0 s | Indicator stays elevated longer; HUD feels "stickier" | Indicator decays instantly; reactive but harsh |
| `hudPublishThreshold` | Gate | 0.01 | 0.005–0.05 | Fewer HUD updates; indicator feels choppy during decay | More HUD updates; potential event flood |
| `footRaycastLength` | Gate | 0.3 m | 0.1–0.5 m | Detects surfaces further below foot; handles slopes | May miss surfaces on steep descents or ledge overhangs |
| `footRaycastOriginOffset` | Gate | (0, 0.05, 0) | Y: 0.02–0.15 m | Originates ray higher above floor; useful on uneven geometry | Ray origin too close to floor; may self-intersect on the player collider |

---

## 8. Acceptance Criteria

| # | Criterion | Test Method | Pass | Fail |
|---|-----------|------------|------|------|
| AC-PNE-01 | Walking on Stone emits `NoiseEvent` with `BaseIntensity = SoundData.walkIntensity[Stone]` (0.50) and `SurfaceType = Stone`. | EditMode unit test: mock PlayerInputHandler events, advance walk distance threshold, assert NoiseEvent values. | Values match SoundData exactly. | Any hardcoded value; wrong SurfaceType. |
| AC-PNE-02 | Stationary player (velocity = 0.0) emits zero footstep `NoiseEvent`s over 5 simulated seconds. | EditMode test: set velocity to 0.0, advance time, assert event count = 0. | Event count = 0. | Any footstep event fires. |
| AC-PNE-03 | Sprinting player emits more footstep events per meter than walking player. | EditMode test: move both 10 m at their respective speeds, count events. | Sprint count > Walk count for same distance. | Sprint count ≤ Walk count. |
| AC-PNE-04 | No footstep `NoiseEvent`s fire during the airborne phase of a jump. | PlayMode test: trigger jump, assert zero footstep events between `OnJumpStarted` and `OnLanded`. | Zero footstep events during airborne. | Any footstep event during airborne phase. |
| AC-PNE-05 | Exactly one landing spike fires on the first `FixedUpdate` after landing, with correct `BaseIntensity`. | PlayMode test: jump, land, assert exactly one landing event with `BaseIntensity = SoundData.landingIntensity` within one fixed timestep of `OnLanded`. | Exactly one event. Correct intensity. Correct frame. | Zero events, >1 event, wrong intensity, wrong frame. |
| AC-PNE-06 | Throw action fires exactly one throw-origin `NoiseEvent` at the player's position with `SurfaceType = Neutral`. | EditMode test: fire `OnThrowPerformed`, assert one event, WorldPosition = player position, SurfaceType = Neutral. | One event. Correct position. SurfaceType = Neutral. | Zero or >1 event; wrong position or SurfaceType. |
| AC-PNE-07 | Untagged floor surface defaults to `SurfaceType.Stone` on footstep raycast. | EditMode test: raycast on collider with no `SurfaceTypeTag`. Assert resolved SurfaceType = Stone. | SurfaceType = Stone. | Any other value or NullReferenceException. |
| AC-PNE-08 | HUD `OnPlayerNoiseLevelChanged` fires when a `NoiseEvent` emits, without SPM being initialized. | EditMode test: subscribe to `OnPlayerNoiseLevelChanged`, fire a NoiseEvent, assert event received with SPM subscriber absent. | HUD event fires. SPM subscriber absent. | HUD event does not fire, or only fires when SPM is active. |
| AC-PNE-09 | HUD noise level decays to ≤ 0.005 within `noiseIndicatorDecayDuration` seconds after the last emission. | EditMode test: emit one event (BaseIntensity = 0.85), advance time by decay duration, assert `_currentNoiseLevel <= 0.005`. | Level ≤ 0.005 at or before decay duration. | Level > 0.005 after decay duration. |
| AC-PNE-10 | `SoundData` null check: PNE disables itself and logs an error without throwing `NullReferenceException`. | EditMode test: assign null to SoundData field, call `Awake`, assert `enabled = false` and no unhandled exception. | PNE disabled. Error logged. No exception. | Exception thrown, or PNE continues with null reference. |
| AC-PNE-11 | Simultaneous landing and interact in the same frame emits exactly two independent `NoiseEvent`s. | PlayMode test: trigger land and interact on the same frame, count received events. | Event count = 2. Both have correct BaseIntensity and SurfaceType. | Event count ≠ 2; wrong intensities or types. |
| AC-PNE-12 | Crouch-walk emits fewer total events than walk over the same 10-meter distance. | EditMode test: simulate 10 m at each speed tier, count events. | Crouch count < Walk count. | Crouch count ≥ Walk count. |
| AC-PNE-13 | `OnSurfaceTypeResolved` fires on each footstep tick with the correct resolved `SurfaceType`. | EditMode test: subscribe to `OnSurfaceTypeResolved`, advance walk distance threshold with Stone below, assert event fires with SurfaceType = Stone. | Event fires. SurfaceType matches raycast. | Event does not fire, or wrong SurfaceType, or Footstep Audio performs a separate raycast. |
| AC-PNE-14 | Playtest: player learns surface-to-noise relationship without a tutorial prompt. | Observed session. Ask player to describe what they notice about the floor and HUD. | Player identifies surface-to-noise relationship unprompted within 5 minutes. | Player does not connect surface type to noise level, or is confused by indicator. |

---

## 9. Visual and Audio Requirements

This section specifies the audio cues the PNE requires from the Audio system for player-side feedback. These are the sounds the player hears confirming their own actions — not detection sounds. This is a contract specification for the Footstep Audio system (systems-index #25).

| Audio Cue | Trigger | SurfaceType Variants Required | Notes |
|-----------|--------|------------------------------|-------|
| Footstep SFX | Each footstep cadence tick (`OnSurfaceTypeResolved`) | Stone, Wood, Metal, Dirt, Carpet, Water | Footstep Audio selects clip by SurfaceType and speed tier. PNE publishes SurfaceType; Footstep Audio owns clip selection. No duplicate raycast. |
| Landing SFX | Landing spike emission | Stone, Wood, Metal, Dirt, Carpet, Water | Landing SFX should be heavier than walk SFX on the same surface (landing is a high-intensity spike). |
| Interact SFX | Interact `NoiseEvent` emission | None (surface-agnostic) | MVP: a single generic interact SFX. Per-prop audio authored by Environmental Interaction system (Vertical Slice). |
| Hide Entry SFX | Hide-enter `NoiseEvent` emission | None (surface-agnostic) | A soft shuffle; distinct from footstep. MVP: single default clip. |
| Hide Exit SFX | Hide-exit `NoiseEvent` emission | None (surface-agnostic) | MVP: can reuse hide entry clip. |

The PNE does not play audio directly. It publishes events; the Footstep Audio system subscribes and plays clips through AudioManager. No `AudioSource.Play` calls exist in the PNE.

---

## 10. UI Requirements

| Requirement | Specification |
|-------------|-------------|
| Data feed | `static event Action<float> OnPlayerNoiseLevelChanged` on `PlayerNoiseEmitter`. Fires when `_currentNoiseLevel` changes by > `hudPublishThreshold` (0.01). |
| Value range | 0.0 (silent) to 1.0 (maximum). Derived from `BaseIntensity`; not SPM-attenuated. |
| Display location | Bottom-left HUD (per Detection System GDD, UI Requirements). |
| Display minimum | Values below `hudDisplayThreshold` (default 0.05) shown as visually silent (indicator at floor). Values above shown as proportional fill. |
| Update timing | Every frame during decay; on each emission spike. Visual lerp is the HUD's responsibility, not the PNE's. |
| Independence | Indicator must function when no seekers exist in the scene and when SPM and Detection System are disabled. PNE-to-HUD path must be independently testable. |
| No per-seeker data | Indicator shows player's intrinsic loudness only. Seeker-relative indicators are the Detection Event Feedback UI's responsibility. |

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|----------|-------|----------|-----------|
| Does `PlayerMovement` publish `velocity` as a property or via a `OnVelocityChanged` event? Decide interface before PNE implementation begins. | lead-programmer | Sprint 1 implementation kickoff | TBD |
| Should per-prop interaction intensity overrides be authored on the prop prefab (a component field) or in a separate SoundData sub-asset? Affects Vertical Slice scope. | game-designer | Vertical Slice planning | TBD |
| `stationaryVelocityThreshold` default value: 0.05 m/s is a starting point. Requires QA measurement of actual physics jitter on dungeon geometry before lock. | qa-lead | First playable build | TBD |
