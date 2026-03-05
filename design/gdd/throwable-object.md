# Throwable Object

> **Status**: Approved
> **Author**: game-designer
> **Last Updated**: 2026-03-05
> **Implements Pillar**: Silence Is a Tool (Pillar 2), The Room Has Rules (Pillar 1), Legible Jeopardy (Pillar 3)
> **Design Order**: #7 — Feature Layer

---

## 1. Overview

The Throwable Object is UNSEEN's primary active distraction tool and the first expression of Pillar 2 (Silence Is a Tool) as an offensive capability. The player finds a single throwable item in the level as a world object, picks it up via the Player Interaction System, carries it until they choose to deploy it, then hurls it toward a targeted position using a dotted-line arc preview. On impact with any surface, the throwable emits a `NoiseEvent` at its impact world position via the Sound Propagation Model's static event bus. If the Seeker AI is currently in the Searching state and hears that event within its maximum hearing range, it abandons its current search and navigates to the noise origin — the core distraction mechanic. The throwable is a one-time consumable: once it has landed, it remains in the world but can no longer be picked up or re-thrown. All numeric values are stored in a `ThrowableData` ScriptableObject; nothing is hardcoded. For MVP, the system supports exactly one throwable type and one carried item at a time; it is designed to slot cleanly into the Stealth Toolkit / Gadgets system in the Vertical Slice milestone.

---

## 2. Player Fantasy

**Target MDA Aesthetics**: Challenge (timing and aiming a throw under pressure), Expression (choosing *when* and *where* to deploy the tool — a tactical decision with permanent cost), Discovery (learning that the seeker's search behavior can be redirected, and internalizing the arc preview enough to throw precisely).

**What throwing feels like:**

The throwable is not a weapon. It is a lie the player tells the seeker — a manufactured sound at a location of the player's choosing. The fantasy is that of the prisoner who rattles a distant cell door to draw the guard away. The power is entirely informational and spatial: the player knows where the noise will land; the seeker does not know the noise is false. This asymmetry of knowledge is UNSEEN's core tension in mechanical form.

**The moment the throw arc appears** is the tactical window. The dotted line snakes through the air toward the landing marker. The player is holding their breath: if they commit to this throw, the object is gone. Is the landing position far enough from the wardrobe to draw the seeker away from it? Is the surface loud enough to be heard from this angle? The arc preview satisfies Pillar 3 — the player can see the outcome before they commit — and enables the kind of deliberate, planful throwing that satisfies Pillar 1. The arc is not a convenience; it is legibility made physical.

**The moment of impact** must feel satisfying and diegetically audible. The object strikes stone — a sharp crack that carries. The seeker's head snaps toward the sound. The player exhales and moves. That four-second sequence — aim, commit, impact, redirect — is the 30-second micro-loop's highest-stakes beat. It must feel like a precision tool used with confidence, not a panic move.

**The permanent cost** is load-bearing. When the throwable lands, it is spent. The player cannot recover it. This design decision makes the choice to throw feel weighty in a way a recoverable item never could. Pillar 2 (Silence Is a Tool) requires that tools have cost. An unlimited throwable becomes a crutch; a one-use throwable is a decision.

**SDT anchors:**
- **Autonomy**: the player chooses when to deploy, where to aim, and whether to use the throwable at all. Multiple viable strategies exist (throw early to create a safe crossing window; hold it in reserve for the escape phase; aim at a specific surface for maximum radius).
- **Competence**: the arc preview enables skill growth. A first-session player aims roughly; an experienced player aims at a specific surface tile to maximize the noise radius. The system rewards mastery without requiring it.

---

## 3. Detailed Rules

### 3.1 State Machine

The throwable exists in one of four discrete states. All state logic is owned by `ThrowableController`. No other system interrogates internal throwable state directly — they interact only via the `IInteractable` interface and the `NoiseEmitter` event bus.

```
WorldResting → (pickup via IInteractable.OnInteractComplete) → Carried
Carried      → (throw via PlayerInputHandler.OnThrowPerformed)        → InFlight
InFlight     → (collision with any surface)                  → Landed
```

There is no return path from Landed. The state machine is a one-way chain.

---

### 3.2 WorldResting State

**Entry condition:** Level start (placed as a scene object), OR level reset (if checkpoint system restores world state — this integration is owned by the Checkpoint System; see Section 6 dependencies).

**Behavior:** The throwable is a static world object resting on a surface. It has:
- An active Rigidbody set to kinematic (`isKinematic = true`). The throwable does not fall or roll — it rests at its authored position. The Rigidbody will be enabled as non-kinematic on transition to InFlight.
- An active Collider (typically a SphereCollider with radius matching the art asset) on the `InteractableLayer` physics layer, making it queryable by the Player Interaction System.
- `IInteractable` active with `CanInteract = true`, `RequiresHold = false`, `PromptLabel = "Take"`, `PromptIconKey = "pick_up"`.
- An `InteractionPromptAnchor` child Transform above the object for the world-space pickup prompt.

**Transition trigger:** `IInteractable.OnInteractComplete(PlayerController interactor)` is called by the Player Interaction System when the player taps interact while the throwable is the targeted interactable.

**On transition to Carried:**
1. Set `CanInteract = false` immediately. The throwable exits the Player Interaction System's targeting pool.
2. Move the throwable's Transform to the `CarryAnchor` child Transform on the `PlayerController`. Parent it to the player's carry socket.
3. Set `Rigidbody.isKinematic = true` and disable the Collider component. The object is now purely a visual "held item" — physics are suspended.
4. Disable the `InteractionPromptAnchor` (or deactivate its world-space prompt if pooled) — the object is no longer a world interactable.
5. Set `ThrowableController.CurrentState = ThrowableState.Carried`.
6. Notify `ThrowInputReceiver` (see 3.3) to begin listening for throw input.

**Important:** The throwable does NOT emit a `NoiseEvent` when picked up. The Player Noise Emitter owns the noise of interaction (via `PlayerInputHandler.OnInteractPerformed`). See Dependencies Section 6.

---

### 3.3 Carried State

**Entry condition:** Transition from WorldResting (pickup complete).

**Behavior:** The throwable is visually held by the player. It follows the player's `CarryAnchor` socket position each frame (via parented Transform, no manual position update needed). The Rigidbody and Collider remain disabled.

**Arc preview:** As soon as Carried is entered, `ThrowArcRenderer` activates. The arc preview is shown continuously while in Carried state. It updates every frame in `LateUpdate` using the current camera forward as the throw direction. The preview is rendered as a `LineRenderer` with a dotted/dashed URP-compatible material using `ThrowableData.ArcPreviewMaterial`. The preview consists of `ThrowableData.ArcPreviewSampleCount` world-space points sampled along the ballistic trajectory (see Formula F-TO-1). A spherical landing marker (a world-space decal or a small sphere mesh) is placed at the final predicted impact point — the last sample point before the ballistic trajectory would intersect geometry (sampled via non-allocating Physics queries; see 3.4 and Formula F-TO-2).

**Arc preview rendering specifics:**
- `ArcPreviewSampleCount`: default 24 points along the trajectory.
- Update frequency: every `LateUpdate` frame (no throttling — must match camera movement to avoid latency between player look and arc preview update, which would break the legibility contract of Pillar 3).
- The `LineRenderer` uses a 2-point-per-segment dotted URP material (dash length = `ArcPreviewDashLength`, gap length = `ArcPreviewGapLength`, both in world units). A scrolling UV material is explicitly NOT used — the dots must be spatially stable, not animated. An animated preview is distracting during the aiming window.
- The landing marker is a separate child GameObject on `ThrowableController`, activated only in Carried state. Its world position is set to the `predictedLandingPoint` computed each `LateUpdate`. Scale: `ThrowableData.LandingMarkerScale` (default 0.2 m radius sphere).
- If the arc trajectory sample hits no geometry within `ThrowableData.MaxThrowDistance` (i.e., the throw exits the level bounds or points at open sky), the arc is clamped at `MaxThrowDistance` and the landing marker is placed at the extrapolated terminal point with a distinct color (`ArcPreviewNoSurfaceColor`) indicating "no valid surface detected." The throw is still valid — the object will fly until it hits something or leaves the level.

**Throw input:** `PlayerInputHandler.OnThrowPerformed` C# event fires when the `Attack` input action `performed` callback triggers. `ThrowableController` subscribes to this event on entry to Carried and unsubscribes immediately after the throw fires or after the throwable leaves Carried state. Only one subscription exists at a time.

**Transition trigger:** `PlayerInputHandler.OnThrowPerformed` fires while `CurrentState == Carried`.

**On transition to InFlight:**
1. Unsubscribe from `PlayerInputHandler.OnThrowPerformed`.
2. Deactivate `ThrowArcRenderer` and the landing marker — the preview disappears the moment the throw commits.
3. Unparent the throwable from the player's `CarryAnchor`. Set world position to `CarryAnchor.position` (the throw origin).
4. Enable the Collider component (now a physics trigger for surface detection — see 3.5).
5. Set `Rigidbody.isKinematic = false`. Enable gravity.
6. Apply throw force: `Rigidbody.linearVelocity = ThrowVelocityVector` (see Formula F-TO-1). Do not use `AddForce` — direct velocity assignment ensures the actual flight path matches the previewed arc exactly, which is the foundational legibility contract. (`Rigidbody.linearVelocity` is the Unity 6 replacement for the deprecated `Rigidbody.velocity` property.)
7. Set `CurrentState = ThrowableState.InFlight`.

---

### 3.4 Arc Preview Sampling (Carried State Technical Detail)

The arc preview samples the theoretical ballistic trajectory in world space using `ThrowableData.ArcPreviewSampleCount` time steps, each separated by `ArcPreviewTimeStep` seconds. These are the same kinematic equations used for the actual flight (Formula F-TO-1), guaranteeing that the preview exactly matches the throw. A preview that approximates the actual flight would violate Pillar 1.

For each of the `N = ArcPreviewSampleCount` sample points:

```
t_i = i * ArcPreviewTimeStep         // i from 0 to N-1
pos_i = throwOrigin + throwDirection * throwSpeed * t_i
       + 0.5 * gravity * t_i^2
```

Where:
- `throwOrigin` = `CarryAnchor.position` at the current frame
- `throwDirection` = Camera forward vector normalized
- `throwSpeed` = `ThrowableData.ThrowSpeed` (m/s)
- `gravity` = `Physics.gravity * ThrowableData.GravityScale` as a Vector3 (Unity's `Physics.gravity` is `(0, -9.81, 0)` by default; multiplying by `GravityScale` scales the magnitude while preserving the downward direction)

**Surface intersection check for landing marker:** Between each consecutive pair of sample points `(pos_i, pos_{i+1})`, `ThrowableController` performs a `Physics.Linecast` using the non-allocating overload with a pre-allocated `RaycastHit` result. If the linecast hits a collider on `ThrowableData.SurfaceDetectionLayerMask`, the `predictedLandingPoint` is set to `hit.point` and the remaining sample points are discarded (the arc terminates at the first intersection). The `LineRenderer` points array is updated to include all samples up to and including the intersection point.

This is a non-allocating operation using a single pre-allocated `RaycastHit` field on `ThrowableController`. No arrays are allocated per frame.

**Maximum arc distance gate:** If no intersection is found across all `N` samples, `predictedLandingPoint` is set to `pos_{N-1}` (the final sample). The LineRenderer shows all `N` points and the landing marker turns `ArcPreviewNoSurfaceColor`.

---

### 3.5 InFlight State

**Entry condition:** Throw input fires while Carried; all transition steps in 3.3 complete.

**Behavior:** The throwable is a live physics Rigidbody. Unity's physics simulation owns its trajectory — `ThrowableController` does not move it manually. The Collider is active and configured as a physics trigger (`isTrigger = true`) to detect surface contact without physics-bouncing off geometry (the throwable stops on first significant contact via `OnTriggerEnter`). 

**Why a trigger rather than a collision callback:**
Using `OnCollisionEnter` with a non-trigger Rigidbody would cause the object to physically bounce off surfaces, potentially ending up far from the landing position the player targeted. A trigger-based contact detection freezes the object at first contact, placing it precisely where the player aimed. This preserves the Pillar 1 contract: the object lands where the arc said it would.

**On `OnTriggerEnter(Collider other)`:**
1. Verify `CurrentState == ThrowableState.InFlight`. If not (race condition), return early.
2. Set `Rigidbody.isKinematic = true` immediately. Freeze the object at the contact position. Set `Rigidbody.linearVelocity = Vector3.zero`. (Unity 6 API — replaces deprecated `Rigidbody.velocity`.)
3. Determine `SurfaceType` from `other` (see 3.6 — Surface Detection).
4. Emit the `NoiseEvent` (see 3.7).
5. Play the impact audio cue via `AudioManager` using `ThrowableData.ImpactSoundId`. Audio variant selection is based on `SurfaceType` if the `SoundLibrary` supports surface-typed impact sounds; otherwise a single impact SFX plays.
6. Set `CurrentState = ThrowableState.Landed`.
7. Disable the Collider. The object is now visually resting but inert.
8. The object remains in the world permanently (it does not despawn or fade in MVP). `CanInteract` remains `false` — it cannot be picked up again.

**Interaction guard during flight:** The Collider is on a physics layer that is NOT the `InteractableLayer`. The `IInteractable.CanInteract` property returns `false` when `CurrentState != ThrowableState.WorldResting`. This double-guard ensures the throwable cannot be targeted by the Player Interaction System during flight under any circumstances.

---

### 3.6 Surface Detection

Surface type at impact is resolved by reading the `SurfaceTypeTag` component on the struck collider, consistent with the SPM's Rule ST-3.

**Detection algorithm (called inside `OnTriggerEnter`):**

```csharp
SurfaceType impactSurface = SurfaceType.Stone; // fail-loud default (SPM Rule ST-3)

if (other.TryGetComponent<SurfaceTypeTag>(out var tag))
{
    impactSurface = tag.Surface;
}
```

This is a single `TryGetComponent` call — non-allocating, O(1). No raycast is needed at this point because the trigger contact already gives us the struck collider directly. A downward raycast would be appropriate for footstep detection (the Player Noise Emitter's use case), but at impact the collider is already known. Using `TryGetComponent` on the contacted collider is more reliable than a raycast at the contact position, which could miss the collider on thin surfaces.

If the struck collider has no `SurfaceTypeTag`, `impactSurface` remains `Stone` — the SPM's fail-loud default. This is intentional: untagged geometry makes a noise as loud as stone, biasing toward detection rather than unearned silence (matching SPM Rule ST-2 design intent).

---

### 3.7 NoiseEvent Emission (Impact)

On impact, `ThrowableController` constructs and emits a single `NoiseEvent`:

```csharp
NoiseEvent impactEvent = new NoiseEvent
{
    WorldPosition  = transform.position,  // throwable's frozen position at impact
    BaseIntensity  = throwableData.ImpactBaseIntensity,
    SurfaceType    = impactSurface
};

NoiseEmitter.Emit(impactEvent);
```

- `WorldPosition` is the throwable's world position at the moment `isKinematic` is set to `true` — the exact landed position.
- `BaseIntensity` is `ThrowableData.ImpactBaseIntensity` (default: 0.85, matching the "Thrown object impact" entry in SPM Rule N-2). This is the design value before SPM surface multiplier application.
- `SurfaceType` is as determined in 3.6.

The SPM then applies its full propagation chain (surface multiplier → distance falloff → wall occlusion) to produce the `AttenuatedIntensity` that the Detection System uses. The throwable does not participate in that chain — it only fires the event.

**The throwable emits exactly one `NoiseEvent` per throw — at impact.** It does NOT emit a `NoiseEvent` at the moment of the throw (that would originate from the player's position, which is incorrect). The throw-origin noise (the sound of the throwing motion) is a separate `NoiseEvent` emitted by the Player Noise Emitter via `PlayerInputHandler.OnThrowPerformed` — see `PlayerNoiseEmitter` throw input handler. This system does not own that event.

---

### 3.8 Landed State

**Entry condition:** `OnTriggerEnter` completes in InFlight.

**Behavior:** The throwable is a static world decoration. The Rigidbody is kinematic, Collider is disabled, `CanInteract = false`. No further behavior. The object stays in its landed position for the remainder of the level.

**Visual:** No special visual state change is required for the landed throwable in MVP. The object is simply present in the world at its impact position. Future iterations may add a small dust particle effect on landing (art/VFX direction, not this system's concern).

---

### 3.9 Carry Limit

In MVP, the player carries exactly one throwable at a time. This is enforced by the level design (one throwable object placed in the level) and by `CanInteract = false` on all throwable instances that are not in `WorldResting` state. There is no inventory system in MVP.

> **Gadget Inventory hook (Vertical Slice):** When the Stealth Toolkit / Gadgets system is designed, `ThrowableController` will need to notify a `GadgetInventory` component on pickup and on depletion. The pickup callback in `OnInteractComplete` should have a clearly commented `// GADGET_INVENTORY_HOOK` marker for the programmer implementing this later. No `GadgetInventory` calls are made in MVP — the hook is documentation only.

---

### 3.10 Interaction with Seeker States

This is reproduced from the Seeker AI GDD for cross-reference and programmer convenience:

| Seeker State | Response to Throwable NoiseEvent |
|-------------|----------------------------------|
| Unaware | Detection System processes the NoiseEvent normally. If `AttenuatedIntensity` exceeds the suspicion threshold, the seeker may transition to Alert, then Searching. |
| Alert | Detection System may escalate to Searching if the NoiseEvent pushes suspicion above the Searching threshold. |
| Searching | **If within hearing range**: `LastKnownPlayerPosition` updates to the noise origin. Searching Phase 1 restarts toward that position. Prior sweep progress is abandoned. This is the primary distraction effect. |
| Chase | **NoiseEvent is ignored.** The Detection System does not change `RequestedState` during Chase. The player cannot throw their way out of a Chase. |
| Caught | Terminal state. No response. |

The Throwable Object system has no direct coupling to the Seeker AI. It emits a `NoiseEvent` via the static event bus. The Detection System and Seeker AI consume that event through their own subscriptions. `ThrowableController` has zero knowledge of seeker state.

---

## 4. Formulas

### F-TO-1: Throw Velocity Vector

The throw velocity applied to the Rigidbody at the moment of release. This vector is also used by the arc preview sampler (Section 3.4) to ensure exact correspondence between the preview and the actual flight.

```
throwDirection      = Camera.main.transform.forward.normalized
throwVelocityVector = throwDirection * ThrowSpeed
```

**Note on gravity:** The Rigidbody uses `useGravity = true` with the Rigidbody's `gravityScale` set to `ThrowableData.GravityScale`. Unity applies gravity as `Physics.gravity * gravityScale` each physics step. The arc preview must use the same effective gravity value:

```
effectiveGravity    = Physics.gravity * ThrowableData.GravityScale  // Vector3, y is negative
```

The arc preview samples the trajectory using Newtonian kinematics at time step `t`:

```
pos(t) = throwOrigin + throwVelocityVector * t + 0.5 * effectiveGravity * t^2
```

Where:
| Variable | Type | Range | Source | Description |
|----------|------|-------|--------|-------------|
| `throwOrigin` | Vector3 | World space | `CarryAnchor.position` at throw frame | Starting position of the throw |
| `throwVelocityVector` | Vector3 | World space | Computed above | Initial velocity applied to Rigidbody |
| `ThrowSpeed` | float | 5–20 m/s | `ThrowableData` | Scalar throw speed along camera forward |
| `ThrowableData.GravityScale` | float | 0.5–3.0 | `ThrowableData` | Rigidbody gravityScale; scales Unity's global gravity |
| `effectiveGravity` | Vector3 | Y-negative | Runtime | `Physics.gravity * GravityScale` |
| `t` | float | 0 → `ArcPreviewSampleCount * ArcPreviewTimeStep` | Loop variable | Time along the trajectory in seconds |

**Example calculation (default values: `ThrowSpeed = 10 m/s`, `GravityScale = 1.0`, `Physics.gravity.y = -9.81`):**

Player faces horizontal (forward = (1, 0, 0)). `throwVelocityVector = (10, 0, 0)`.
`effectiveGravity = (0, -9.81, 0)`.

```
At t = 0.5s:
  pos = (0,0,0) + (10,0,0)*0.5 + 0.5*(0,-9.81,0)*0.25
      = (5.0, 0, 0) + (0, -1.226, 0)
      = (5.0, -1.226, 0)    // 5 m forward, 1.2 m downward arc
```

At `ThrowSpeed = 10 m/s` thrown horizontally from 1.5 m height, the object hits the floor (0 m) at approximately `t ≈ 0.55 s`, landing ~5.5 m from the player. This is the expected throw range for a flat horizontal throw in a typical 6×6 m chamber. Throwing slightly upward (camera tilted 15° up) extends range to ~8 m.

---

### F-TO-2: Predicted Landing Point (Arc Preview)

For the `M` arc preview samples, find the first segment that intersects level geometry:

```
For i = 0 to (ArcPreviewSampleCount - 2):
    pos_i   = pos(i * ArcPreviewTimeStep)
    pos_i+1 = pos((i+1) * ArcPreviewTimeStep)

    hit = Physics.Linecast(pos_i, pos_i+1, SurfaceDetectionLayerMask)

    if hit:
        predictedLandingPoint = hit.point
        set LineRenderer points[0..i+1] and set points[i+1] = hit.point
        BREAK

if no hit found across all segments:
    predictedLandingPoint = pos(ArcPreviewSampleCount * ArcPreviewTimeStep)
    // all N points shown; landing marker at terminal position
```

| Variable | Type | Range | Source | Description |
|----------|------|-------|--------|-------------|
| `ArcPreviewSampleCount` | int | 12–48 | `ThrowableData` | Number of trajectory sample points; more = smoother arc |
| `ArcPreviewTimeStep` | float | 0.02–0.1 s | `ThrowableData` | Time between samples; lower = shorter arc segments, higher precision intersection |
| `SurfaceDetectionLayerMask` | LayerMask | — | `ThrowableData` | Physics layers included in surface detection (floor, walls, props; excludes player, seeker, trigger volumes) |
| `predictedLandingPoint` | Vector3 | World space | Computed | World position where arc preview terminates; used to place landing marker |

**Example (default: `ArcPreviewSampleCount = 24`, `ArcPreviewTimeStep = 0.05 s`):**
Total preview time = 24 × 0.05 = 1.2 seconds of flight simulated. At `ThrowSpeed = 10 m/s`, this covers approximately 12 m of horizontal range before gravity brings the arc to floor level (depending on vertical aim). This is sufficient to preview throws across the full length of the MVP chamber.

---

### F-TO-3: Impact Noise Intensity (Post-SPM)

This formula is owned by the SPM (reproduced here for level-design reference). `ThrowableController` provides only the `BaseIntensity` input.

```
attenuated_intensity =
    BaseIntensity
    * surface_intensity_multiplier[SurfaceType]
    * distance_factor
    * occlusion_factor
```

Where:
| Variable | Type | Source | Reference |
|----------|------|--------|-----------|
| `BaseIntensity` | float (0–1) | `ThrowableData.ImpactBaseIntensity` (default 0.85) | SPM Rule N-2 |
| `surface_intensity_multiplier` | float lookup | SPM Rule ST-1 (Stone=1.00, Wood=1.30, Metal=1.55, Dirt=0.70, Carpet=0.50, Water=1.20, Neutral=1.00) | SPM GDD Section 2 |
| `distance_factor` | float (0–1) | SPM Section 3, F1 | SPM GDD |
| `occlusion_factor` | float (0.3–1.0) | SPM Section 4, F2 | SPM GDD |

**Design reference examples for level designers** (assuming default `ImpactBaseIntensity = 0.85`, seeker at maximum hearing range 15 m with no walls). These examples use a simplified linear `distance_factor = (1 − distance / maxRange)` for illustration only. The authoritative distance attenuation formula is defined in the Sound Propagation Model GDD (Section 3, F1) — consult that document for precise values during level balance work:
- Impact on **Stone** at 8 m: `0.85 * 1.00 * (1 - 8/15) = 0.85 * 0.467 ≈ 0.397` — audible at moderate distance.
- Impact on **Metal** at 12 m: `0.85 * 1.55 * (1 - 12/15) = 1.318 * 0.200 ≈ 0.264` — still audible despite range, because metal amplifies.
- Impact on **Carpet** at 4 m: `0.85 * 0.50 * (1 - 4/15) = 0.425 * 0.733 ≈ 0.311` — audible at close range despite carpet absorption.
- Impact on **Carpet** at 10 m: `0.85 * 0.50 * (1 - 10/15) = 0.425 * 0.333 ≈ 0.142` — near-inaudible. Carpet at distance is a poor distraction surface.

**Design implication:** To reliably redirect the Warden's Searching behavior, the impact `attenuated_intensity` must cross the Detection System's noise-suspicion threshold (check Detection System GDD for the exact threshold value). Level designers should place the throwable in a location where it can reach a stone or metal surface in the intended distraction zone, not a carpeted alcove.

---

## 5. Edge Cases

| # | Scenario | Expected Behavior | Rationale |
|---|----------|------------------|-----------|
| EC-1 | Player attempts to pick up a second throwable while already carrying one | No second throwable exists in MVP (only one is placed in the level). If two were ever present (level design error), the second throwable in WorldResting state would remain `CanInteract = true` and be targetable by the Player Interaction System. `OnInteractComplete` would fire. `ThrowableController` must check its own static guard: `ThrowableController` maintains a `static bool s_anyCarried` flag, set to `true` when any instance enters Carried, reset to `false` when that instance exits Carried. If `s_anyCarried == true` when `OnInteractComplete` fires on the second instance, log a `Debug.LogWarning` and return without transitioning. The first carried throwable is not dropped. This avoids adding a `HasCarriedThrowable` property to `PlayerController`, keeping the dependency boundary clean. This is a level design error, not a gameplay scenario; the `ComponentAuditTool` should flag scenes with multiple throwable instances. |
| EC-2 | Player is caught (Caught state) while carrying the throwable | The `PlayerController` enters its Caught state. The `ThrowableController` does not respond to this directly. The throwable remains parented to the `CarryAnchor`. The GameManager triggers the fail screen / level reset. On reset, the Checkpoint System is responsible for restoring the throwable to `WorldResting` state at its authored position. If the Checkpoint System calls a reset without repositioning the throwable, the throwable must be explicitly reset: unparent, return to authored position, set `isKinematic = true`, enable Collider, set `CurrentState = WorldResting`, set `CanInteract = true`. |
| EC-3 | Player throws the object through a doorway or gap narrower than the throwable's collider | The trigger-based contact detection will fire when the Collider enters any overlapping trigger or Collider on `SurfaceDetectionLayerMask`. If the doorframe clips the throwable during flight, `OnTriggerEnter` may fire prematurely against the doorframe geometry rather than the intended landing surface. To mitigate: `ThrowableController` checks that `other.gameObject` is on a layer included in `SurfaceDetectionLayerMask` before processing the impact. The doorframe must be on an appropriate physics layer. If the premature impact fires on the doorframe, the object lands there — this is physically coherent and Pillar 1-consistent. The player should aim to clear the gap. |
| EC-4 | Thrown object exits the level bounds (no collider hit within arc preview range) | `OnTriggerEnter` never fires. The Rigidbody continues simulating. After `ThrowableData.InFlightTimeoutSeconds` of elapsed InFlight time, `ThrowableController` self-terminates: sets `isKinematic = true`, disables the Collider, sets `CurrentState = Landed`, and does NOT emit a `NoiseEvent` (no surface was struck). The object is frozen at its out-of-bounds position. No gameplay consequence. This is a level design error (level bounds not enclosed) and should be flagged by the level designer. |
| EC-5 | Player throws while the Chase blocking condition is active | The Chase blocking condition is enforced by the Player Interaction System for `IInteractable` pickups, not for throw input. Throw input is bound to the `Attack` action on `PlayerInputHandler`, not the Interact action. Therefore, the Player Interaction System does NOT block the throw. `ThrowableController` checks `PlayerController.IsBeingChased` directly on receiving `OnThrow`. If `IsBeingChased == true`, the throw is suppressed — no state transition, no `NoiseEvent`, no velocity applied. The arc preview remains active but a visual "blocked" indicator appears on the landing marker (`ArcPreviewBlockedColor`). **Design rationale:** The Seeker AI GDD (Section 3.5 — Chase State; Section 3.10 cross-reference table) specifies that the Detection System ignores `NoiseEvents` during Chase — the player cannot throw their way out of a Chase. Suppressing the throw preserves the one-throwable-per-level cost: a throw wasted during Chase is lost forever. Suppressing it with a visual block prevents this waste while reinforcing the Chase-state stakes. |
| EC-6 | Player looks directly upward (camera forward = (0,1,0)) or directly downward when arc preview is active | `throwVelocityVector = Camera.main.transform.forward.normalized * ThrowSpeed`. Directly upward: the object launches straight up, arc samples immediately return downward, and the landing point will be near the player's feet (gravity curves it back). Directly downward: the first arc sample may immediately intersect the floor beneath the player; landing marker appears at or near the player's feet. Both are physically valid behaviors. The player should not throw straight up or down for any useful tactical purpose. No guard needed — let physics resolve it. |
| EC-7 | `OnTriggerEnter` fires multiple times in the same physics step (multi-contact) | `ThrowableController.OnTriggerEnter` guards against re-entry with a state check at the top: `if (CurrentState != ThrowableState.InFlight) return;`. The first contact sets `CurrentState = Landed`. All subsequent calls in the same frame exit immediately. Only one `NoiseEvent` is emitted per throw. |
| EC-8 | Player throws at a surface tagged with `SurfaceType.Water` (puddle on floor) | The surface detection resolves `SurfaceType.Water` from the struck collider's `SurfaceTypeTag`. The `NoiseEvent` is emitted with `SurfaceType = Water`. The SPM applies `surface_intensity_multiplier = 1.20`, making the impact distinctly louder than a stone surface — a discoverable tactical element. Water is an amplifying hazard, consistent with SPM Rule ST-1. The throw is otherwise identical. |
| EC-9 | Player enters a hiding spot while carrying the throwable | The hiding spot transition is managed by the Hiding Spot System. When the player enters the wardrobe, the player character is hidden and interaction is locked. The throwable remains parented to the `CarryAnchor` and follows the player inside. The arc preview remains active (the player can see it inside the hiding spot, though throwing while hidden may have no useful tactical value). The `ThrowableController` does not suppress throws while the player is hidden — if the player throws from inside the hiding spot, the object exits through the wardrobe geometry (collision layers permitting) and lands normally. This is an edge case to resolve with level design (ensure wardrobe interior has no collidable surfaces on `SurfaceDetectionLayerMask`). |
| EC-10 | `ThrowableData` ScriptableObject is null (not assigned to `ThrowableController`) | `ThrowableController.Awake` calls `Debug.Assert(throwableData != null, "ThrowableController: ThrowableData is not assigned.")`. In Editor builds, the Assert halts play mode with a clear error. At runtime, `throwableData == null` means every access will throw a `NullReferenceException`. A null `ThrowableData` is a configuration error — the programmer must assign it in the Inspector. Do not add null guards inside the hot path to mask this. |
| EC-11 | Arc preview `Linecast` hits the player's own Collider | `SurfaceDetectionLayerMask` must exclude the player's physics layer. The player Collider layer is never included in `SurfaceDetectionLayerMask`. If the player walks into the arc ray (unlikely from `CarryAnchor`), the linecast passes through them. This is correct — the player cannot block their own throw arc. |

---

## 6. Dependencies

### 6.1 What This System Requires (Inbound)

| Dependency | System | What Is Required | Direction |
|-----------|--------|-----------------|-----------|
| `IInteractable` | Player Interaction System | `OnInteractComplete(PlayerController)` called on tap interact when throwable is targeted in WorldResting. | Inbound — PIS dispatches to this system |
| `PlayerInputHandler.OnThrowPerformed` | Player | C# event bound to `Attack.performed` input action. Throwable subscribes when Carried, unsubscribes on throw or state exit. | Inbound — Player event bus |
| `PlayerController.IsBeingChased` | Player | Bool property; checked on `OnThrow` to suppress throw during Chase. | Inbound — Player state query |
| `CarryAnchor` | PlayerController | `Transform` child on the player prefab at the desired hold position. Throwable parents to this on pickup. | Inbound — scene hierarchy dependency |
| `NoiseEmitter.Emit()` | Sound Propagation Model | Static dispatch method that raises the `OnNoiseEmitted` event. Throwable calls `NoiseEmitter.Emit(impactEvent)` at impact. | Inbound — SPM contract |
| `SurfaceTypeTag` | Level / Props | Component on physics colliders identifying their surface material. Read via `TryGetComponent` on impact contact. | Inbound — level authoring contract |
| `SurfaceDetectionLayerMask` | Physics Layer Setup | Unity physics layers configuration. Identifies which colliders are valid impact surfaces. Defined in `ThrowableData`. | Inbound — project physics layer configuration |
| `InteractableLayer` | Physics Layer Setup | Unity physics layer for WorldResting state collider. Must match what Player Interaction System queries. | Inbound — project physics layer configuration |
| `ThrowableData` | Data (ScriptableObject) | All numeric tuning values. Must be assigned in Inspector. | Inbound — data asset |
| `AudioManager` | Audio | `AudioManager.Play(SoundID, Vector3)` called at impact. | Inbound — audio system contract |

### 6.2 What This System Provides (Outbound)

| Provided To | System | What Is Provided | Direction |
|------------|--------|-----------------|-----------|
| Sound Propagation Model | Core | `NoiseEvent` struct dispatched via `NoiseEmitter.Emit(impactEvent)` at impact. `WorldPosition` = impact world position. `BaseIntensity` = `ThrowableData.ImpactBaseIntensity`. `SurfaceType` = struck surface. | Outbound |
| Detection System (via SPM) | Core | The SPM-processed `AttenuatedIntensity` derived from the throwable's `NoiseEvent` is passed to the Detection System, which updates the Seeker AI's suspicion level and may trigger a `LastKnownPlayerPosition` update. The throwable does not call the Detection System directly. | Outbound (indirect, via SPM) |
| Player Interaction System | Core | `IInteractable.CanInteract = false` after pickup. The PIS receives this state change on the next targeting frame and stops showing the pickup prompt. | Outbound |
| Seeker AI (via Detection System) | Gameplay | The impact `NoiseEvent`, once processed by SPM and Detection System, may redirect the Seeker from Searching to the noise origin. The throwable has zero direct coupling to the Seeker AI. | Outbound (indirect) |
| Checkpoint System | Core | `ThrowableController.ResetToWorldResting()` public method: unparents the throwable, returns it to its authored world position (stored in `Awake`), sets `Rigidbody.isKinematic = true`, re-enables the Collider, resets `CurrentState = WorldResting`, sets `CanInteract = true`, resets `s_anyCarried = false`. The Checkpoint System calls this on level restore. | Outbound |
| Stealth Toolkit / Gadgets | Vertical Slice (future) | Pickup notification and depletion notification at `OnInteractComplete` and state transition to Landed. Marked in code with `// GADGET_INVENTORY_HOOK`. | Outbound (not implemented in MVP) |

### 6.3 Bidirectional System Table

| System | This System → That System | That System → This System |
|--------|--------------------------|--------------------------|
| **Sound Propagation Model** | Emits `NoiseEvent` at impact | Processes `NoiseEvent`; no return value to throwable |
| **Player Interaction System** | Implements `IInteractable`; sets `CanInteract = false` on pickup | Calls `OnInteractComplete` on tap interact |
| **Player Noise Emitter** | Does NOT emit throw-origin noise (PNE owns that) | PNE independently emits throw-origin `NoiseEvent` via `PlayerInputHandler.OnThrowPerformed` subscription |
| **Seeker AI** | Indirectly redirects seeker via `NoiseEvent` → SPM → Detection System | No dependency; seeker knows nothing about throwable existence |
| **Checkpoint System** | Must be reset to WorldResting on checkpoint restore | Checkpoint System calls throwable reset API on level restore |
| **AudioManager** | Calls `AudioManager.Play` at impact | Returns nothing; fire-and-forget |

### 6.4 Ownership Boundaries (Explicit)

| Item | Actual Owner |
|------|-------------|
| Throw-origin noise (sound of throwing motion) | Player Noise Emitter (subscribes to `PlayerInputHandler.OnThrowPerformed`) |
| Impact audio clip selection and playback | AudioManager + `ThrowableData.ImpactSoundId` |
| Detection consequence of the `NoiseEvent` | Detection System (via SPM processing chain) |
| Seeker navigation update on distraction | Seeker AI (responding to Detection System output) |
| Gadget slot count and multi-throwable inventory | Stealth Toolkit / Gadgets (Vertical Slice — not MVP) |
| Arc preview shader / material | Art pipeline (`ThrowableData.ArcPreviewMaterial` — authored, not this system's concern) |

---

## 7. Tuning Knobs

All values are fields on `ThrowableData` (`Assets/_Project/Scripts/Data/ThrowableData.cs`), serialized as a ScriptableObject and assigned in the Inspector on each `ThrowableController` instance. No value is hardcoded in `ThrowableController.cs`.

### 7.1 Throw Mechanics

| Knob | Type | Default | Safe Range | Category | Extreme Low Effect | Extreme High Effect |
|------|------|---------|------------|----------|--------------------|---------------------|
| `ThrowSpeed` | float | 10.0 m/s | 5–20 m/s | Feel | Very short range — object lands at player's feet; useless as distraction | Near-hitscan throw; arc is nearly flat; landing feels unpredictable |
| `GravityScale` | float | 1.0 | 0.5–3.0 | Feel | Nearly flat arc; throw feels weightless; hard to judge vertical aim | Extremely steep arc; throw range dramatically reduced; arc unusable at range |
| `ImpactBaseIntensity` | float | 0.85 | 0.4–1.0 | Curve | Quiet impact; unreliable distraction at moderate range | Extremely loud; seeker always hears it regardless of distance or surface; trivializes distraction |

### 7.2 Arc Preview

| Knob | Type | Default | Safe Range | Category | Extreme Low Effect | Extreme High Effect |
|------|------|---------|------------|----------|--------------------|---------------------|
| `ArcPreviewSampleCount` | int | 24 | 12–48 | Feel | Jagged, angular preview; hard to read arc shape | Smooth arc; higher GPU LineRenderer vertex cost; negligible in dungeon scenes. **Mobile budget:** recommend ≤20 (16–20 linecasts/frame); at 48 the per-frame linecast count reaches 47, which risks exceeding a 1.0 ms LateUpdate budget on mid-range mobile hardware. |
| `ArcPreviewTimeStep` | float | 0.05 s | 0.02–0.1 s | Feel | Very short segments; high precision intersection; high per-frame linecast count (up to 47 linecasts/frame) | Fewer, longer segments; less precise landing marker; arc looks "chunky" |
| `ArcPreviewDashLength` | float | 0.25 m | 0.1–0.5 m | Feel | Fine dotted line; hard to see in dark dungeon | Coarse dashes; reads as a thick dashed line; may look imprecise |
| `ArcPreviewGapLength` | float | 0.15 m | 0.05–0.3 m | Feel | Nearly solid line; loses dotted character | Very gappy; hard to follow the arc trajectory |
| `LandingMarkerScale` | float | 0.2 m (radius) | 0.1–0.4 m | Feel | Nearly invisible landing point | Too prominent; draws the eye away from the seeker |
| `ArcPreviewMaterial` | Material | (dotted URP material) | — | Config | — | — |
| `ArcPreviewNoSurfaceColor` | Color | Orange/amber | — | Feel | Visually indistinct from normal arc; no signal that the throw exits bounds | — |
| `ArcPreviewBlockedColor` | Color | Red-gray desaturated | — | Feel | Looks like normal arc; player doesn't know throw is suppressed during Chase | — |

### 7.3 Surface Detection and Collision

| Knob | Type | Default | Safe Range | Category | Extreme Low Effect | Extreme High Effect |
|------|------|---------|------------|----------|--------------------|---------------------|
| `SurfaceDetectionLayerMask` | LayerMask | Default + Prop + Architecture | — | Config | Too few layers: throws pass through walls | Too many layers: may hit trigger volumes or unwanted geometry |
| `InFlightTimeoutSeconds` | float | 5.0 s | 2.0–10.0 s | Gate | Out-of-bounds objects freeze too quickly; may freeze mid-air if frame rate is poor | Rigidbody simulates for a long time before cleanup; wastes physics budget |

### 7.4 Audio

| Knob | Type | Default | Safe Range | Category | Notes |
|------|------|---------|------------|----------|-------|
| `ImpactSoundId` | SoundID (enum) | `SoundID.ThrowableImpact` | — | Config | `SoundLibrary` must define this entry. Surface-typed variants are optional (art direction deferred). |

### 7.5 Interaction Contract (per IInteractable)

These are fixed by the design specification and should NOT be changed without updating the Player Interaction System GDD:

| Property | Fixed Value | Notes |
|----------|------------|-------|
| `RequiresHold` | `false` (tap) | Pickup is always a tap. Deliberate — the player should be able to grab the throwable quickly without holding. |
| `PromptLabel` | `"Take"` | Max 12 chars. |
| `PromptIconKey` | `"pick_up"` | Must match `PlayerInteractionData.PromptIcons` registry. |
| `HoldDuration` | N/A | `RequiresHold == false`; `HoldDuration` is not used. |

---

## 8. Acceptance Criteria

### Functional (pass/fail, verifiable in Play Mode or unit test)

- [ ] **AC-01 — Pickup transitions to Carried correctly:** Throwable in WorldResting at 1.0 m in front of player. Player taps interact. Throwable is parented to `CarryAnchor`, `CanInteract == false`, `CurrentState == Carried`, Rigidbody is kinematic, Collider is disabled. Prompt disappears.
- [ ] **AC-02 — Arc preview appears immediately on pickup:** On the frame of pickup, `ThrowArcRenderer` is active. `LineRenderer` has `ArcPreviewSampleCount` points. Landing marker is positioned at the predicted landing point.
- [ ] **AC-03 — Arc preview matches actual flight:** Set `GravityScale = 1.0`, `ThrowSpeed = 10.0 m/s`. Aim horizontally at a flat floor 6 m away. Arc preview shows landing point at distance X. Throw. Object lands within 0.2 m of X. (Tolerance accounts for one physics step of overshoot after trigger fires.)
- [ ] **AC-04 — NoiseEvent emitted at impact position:** Throw object toward a floor 4 m away. On landing, subscribe a test listener to `NoiseEmitter.OnNoiseEmitted`. Verify: `NoiseEvent.WorldPosition` is within 0.2 m of the object's landed position, `BaseIntensity == ThrowableData.ImpactBaseIntensity`, `SurfaceType` matches the struck floor's `SurfaceTypeTag`.
- [ ] **AC-05 — ThrowableController NoiseEvent originates at impact, not throw origin:** Subscribe a test listener to `NoiseEmitter.OnNoiseEmitted`. Throw the object at a surface 5 m away. Record the player's world position at throw time. Verify: the `NoiseEvent` emitted by `ThrowableController` has `WorldPosition` within 0.2 m of the landed object's position — NOT within 0.2 m of the player's throw-origin position. Note: the Player Noise Emitter may independently fire a throw-origin `NoiseEvent`; this is correct and expected. The test asserts that `ThrowableController` does not contribute a `WorldPosition` near the player position. (Distinguish the two events by `WorldPosition`; if only one event fires, assert it equals the impact position.)
- [ ] **AC-06 — Surface type detection is correct:** Three floors tagged `Stone`, `Metal`, `Carpet`. Throw to each. Verify `NoiseEvent.SurfaceType` matches each floor's tag.
- [ ] **AC-07 — Untagged surface defaults to Stone:** Floor with no `SurfaceTypeTag` component. Throw to it. Verify `NoiseEvent.SurfaceType == SurfaceType.Stone`.
- [ ] **AC-08 — Throw suppressed during Chase:** Set `PlayerController.IsBeingChased = true`. Carry the throwable. Press Throw. Verify: object remains parented to `CarryAnchor`, `CurrentState` remains `Carried`, no `NoiseEvent` fires, landing marker changes to `ArcPreviewBlockedColor`.
- [ ] **AC-09 — Landed throwable is not interactable:** After landing, approach the throwable and aim at it. Verify: `CanInteract == false`, no prompt is shown by the Player Interaction System.
- [ ] **AC-10 — Only one NoiseEvent per throw:** `OnTriggerEnter` fires twice in the same frame (simulated via two overlapping trigger colliders at landing zone). Verify: `NoiseEmitter.OnNoiseEmitted` is invoked exactly once.
- [ ] **AC-11 — InFlight timeout fires and cleans up:** Set `InFlightTimeoutSeconds = 1.0 s`. Throw at open sky (aim upward past all geometry). Wait 1.0 s. Verify: `CurrentState == Landed`, Rigidbody is kinematic, no `NoiseEvent` was emitted, no exception thrown.
- [ ] **AC-12 — No hardcoded values:** Code review of `ThrowableController.cs` finds zero numeric literals. All values reference `ThrowableData` fields.
- [ ] **AC-13 — No per-frame GC allocations:** Unity Profiler capture during Carried state (arc preview active, player moving). `ThrowableController.LateUpdate` shows 0 B GC Alloc per frame.
- [ ] **AC-14 — Arc preview linecasts do not use allocating overload:** Code review confirms `Physics.Linecast` is called with the non-allocating `(Vector3, Vector3, out RaycastHit, LayerMask)` overload. The `out RaycastHit` variable is a pre-allocated field on `ThrowableController`, not a new struct per call.
- [ ] **AC-15 — Seeker redirects on impact during Searching:** Seeker in Searching state, last known position = Point A. Throw object to Point B (within seeker hearing range). Verify: Seeker AI abandons sweep at A and navigates toward B.
- [ ] **AC-16 — Seeker does NOT redirect during Chase:** Seeker in Chase. Throw object anywhere within hearing range. Verify: seeker's `NavMeshAgent.destination` continues updating to the player's current position, not the thrown object's landing position.
- [ ] **AC-21 — Chase-block indicator is visually distinct:** Set `PlayerController.IsBeingChased = true`. Carry the throwable and aim at any surface in default dungeon lighting. Verify: the landing marker and arc preview change to `ArcPreviewBlockedColor`, and the blocked state is visually distinguishable from the normal-preview state at a glance. PASS: a first-time playtester, unprompted, reports "something changed on the arc" or does not attempt to throw because of the visual. FAIL: tester is unaware the throw is suppressed and presses the throw button expecting the object to launch.

### Experiential (validated via observed play sessions)

- [ ] **AC-17 — Arc preview is readable in dungeon lighting:** First-time playtester in a dungeon chamber with default URP lighting. Target: ≥90% of testers can identify where the throw will land from the arc preview before throwing, without instruction.
- [ ] **AC-18 — Throw cost is felt:** After 15 minutes of play, ask: "How did you feel about using the throwable?" PASS: players describe the decision to throw as deliberate or meaningful. FAIL: players describe it as trivial or report they forgot it was one-time use. If >30% fail: evaluate whether the landing marker or object's visual state after landing communicates "spent" effectively enough. Consider a desaturation or "X" overlay on the landed object.
- [ ] **AC-19 — Distraction mechanic is discoverable without instruction:** Chamber with one throwable, one seeker in Searching. No onboarding text. Target: ≥65% of first-time playtesters throw the object and observe the seeker redirect within the same session. (This threshold is lower than pickup discoverability because the causal chain — throw → land → seeker moves — has more steps to observe.)
- [ ] **AC-20 — Throw feels physically coherent:** After throwing, ask: "Did the object land where you expected?" Target: ≥80% of responses are "yes" or "close enough." If <80%: diagnose whether the arc preview mismatches actual flight (physics frame timing issue — verify F-TO-1 implementation) or whether `ThrowSpeed`/`GravityScale` values feel counterintuitive (tuning issue).

---