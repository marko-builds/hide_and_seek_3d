# Sound Propagation Model

> **Status**: Approved
> **Author**: game-designer + systems-designer
> **Last Updated**: 2026-03-05
> **Implements Pillar**: The Room Has Rules (Pillar 1), Silence Is a Tool (Pillar 2), Legible Jeopardy (Pillar 3)

---

## Overview

The Sound Propagation Model (SPM) is a foundation-layer system with no upstream
dependencies. Its sole responsibility is answering one question per seeker per
NoiseEvent: "Is this sound audible to this seeker, and at what intensity does it
arrive?" It sits between the Player Noise Emitter (which generates raw sound data)
and the Detection System (which converts audibility into suspicion). The SPM owns
neither the decision to emit a sound nor the suspicion consequences of hearing one
— it owns only the physics of transmission between source and listener.

The SPM is not a true 3D acoustic engine. It is a deliberate simplification
designed around three constraints: the player must be able to predict outcomes
through play, the rules must be consistent across all dungeon environments, and
the system must evaluate in microseconds per seeker since it is triggered
event-by-event rather than per-frame. It models three phenomena: distance falloff
(sound is quieter at range), surface type modification (stone floors are baseline;
metal amplifies; carpet absorbs), and wall occlusion (geometry attenuates sound in
transit). True acoustic effects — diffraction around corners, resonance,
frequency-dependent absorption — are out of scope. What the player learns is simpler
and more usable: loud surfaces betray you, walls muffle but do not silence, and
every meter of distance buys safety in proportion.

---

## Player Fantasy

The player should feel like they are mastering a physical law — not memorizing an
exception table.

**The moment of discovery.** Early in their first level, the player crouches and
steps onto flagstone, then onto a carpet runner. The footstep audio changes audibly
— stone rings, carpet whispers. A seeker is twelve meters away. On stone the
suspicion meter ticks. On carpet it does not. No tooltip explained this. The player
figured it out. That is the payoff the SPM is designed to deliver: *the rules
revealed themselves through observation.* This is Pillar 1 (The Room Has Rules)
functioning as intended — the system is consistent enough that a player can derive
it empirically.

**The resource decision.** The player stands at the threshold between a carpeted
alcove and a fifteen-meter stretch of wooden floorboard. A seeker is patrolling the
far end, currently facing away. They have a throwable object in hand. The question
is not "do I run?" — it is "can I calculate whether six steps on wood at walking
speed will trigger detection at this range?" A player who has internalized the SPM's
surface-loudness hierarchy and rough hearing radius can make that calculation
intuitively. The tension of standing at that threshold, doing the math in their
head, is the primary emotional state Pillar 2 (Silence Is a Tool) is built to
create. Sound is not a hazard the player avoids. It is a budget the player manages.

**The thrown distraction.** The player hurls a loose stone past the guard's head.
It clangs on the far flagstones. The seeker's head snaps toward the impact — the
noise is there, not here. The player slips through the gap. This is the SPM's
highest-expression play: knowing that the thrown object's impact NoiseEvent
originates at the landing position means the player can triangulate a safe corridor
by choosing throw angle and distance. Understanding propagation makes you a
tactician.

**Legible failure.** When a player is caught through sound, they must be able to
reconstruct why. "I forgot the floorboards were louder." "I ran instead of walked."
"The wall was thin and the seeker was close." The SPM must never produce a detection
that the player experiences as arbitrary. Every outcome must have a chain of
observable causes: what surface, what action, what distance, what walls. Pillar 3
(Legible Jeopardy) is only achievable if the SPM's rules are simple enough to hold
in working memory.

---

## Detailed Rules

### Section 1: NoiseEvent Data Contract

**Rule N-1.** A `NoiseEvent` is a value-type struct (C# `struct`, stack-allocated)
emitted by the Player Noise Emitter. It carries exactly three fields consumed by the
SPM:

| Field | Type | Range | Description |
|-------|------|-------|-------------|
| `WorldPosition` | Vector3 | Any world position | The 3D position from which the sound originates. For player-body sounds (footsteps, landing, crouching, interactions, entering/exiting hiding), this is the player's position at emission. For thrown-object impact, this is the impact world position — not the player's position. |
| `BaseIntensity` | float | 0.0–1.0 | The raw loudness before any environmental modification. Defined per-action in the Sound Data asset. |
| `SurfaceType` | SurfaceType (enum) | See Section 2 | Surface classification at the event origin. For footsteps, this is the surface under the player's feet at emit time. For impacts, this is the surface struck. For non-footstep actions (interactions, hiding-spot entry, throw origin), this is `SurfaceType.Neutral`, which applies no surface modifier. |

**Rule N-2.** Base intensity values are not computed by the SPM. They are authored
in the Sound Data ScriptableObject asset by action type and passed as `BaseIntensity`
on the NoiseEvent. The SPM treats base intensity as an opaque input. Reference values
from Detection System Rule A-4, reproduced here as starting points for authoring:

| Action | Approximate Base Intensity | Surface Type Field |
|--------|---------------------------|-------------------|
| Walking on stone | 0.50 (Medium) | Stone |
| Walking on wood / floorboards | 0.65 (Medium-High) | Wood |
| Walking on carpet / soft | 0.25 (Low) | Carpet |
| Sprinting (any surface) | 0.85 (High) | Surface of current floor |
| Crouching walk (any surface) | 0.25 (Low) | Surface of current floor |
| Landing from jump | 0.85 (High — spike) | Surface of landing floor |
| Throwing an object | 0.50 (Medium) | Neutral |
| Thrown object impact | 0.85 (High) | Surface struck at impact |
| Interacting with a prop | 0.25–0.50 (Low–Medium) | Neutral |
| Entering / exiting hiding spot | 0.25 (Low) | Neutral |

**Rule N-2a.** For footstep action types (walk, crouch-walk), the Player Noise
Emitter authors **surface-specific** `BaseIntensity` values in the Sound Data asset.
The N-2 table walking entries show these per-surface bases: stone walk = 0.50, wood
walk = 0.65, carpet walk = 0.25. The SPM's F1 formula then applies the
`surface_multiplier` on top — both mechanisms contribute to the final loudness.
This is intentional: the BaseIntensity captures the acoustic character of the
impact itself (a carpet footfall sounds different from a stone one at the source),
while the multiplier captures how the surface transmits that sound outward. For
non-footstep actions (sprint, throw, interact, hide),
a single surface-agnostic BaseIntensity is used regardless of current floor type.
The consequence of stacking both mechanisms for carpet walk (0.25 × 0.50 = 0.125)
is documented as a design decision in EC-7.

**Rule N-3.** The SPM is invoked once per `NoiseEvent`, synchronously, on the frame
the event is emitted. It evaluates every active seeker in the current scene and
returns a result struct per seeker containing `IsAudible` (bool) and
`AttenuatedIntensity` (float, 0.0–1.0). These are passed directly to the Detection
System for suspicion processing. The SPM does not cache results between events.

**Rule N-4.** The SPM does not emit NoiseEvents. It receives and evaluates them. The
Player Noise Emitter is the sole emitter for player-origin sounds. Thrown-object
impact events are emitted by the Throwable Object component at contact time, using
the impact world position as `WorldPosition`.

---

### Section 2: Surface Type Taxonomy

**Rule ST-1.** The SPM recognizes seven surface type classifications. Each defines a
`surface_intensity_multiplier` that scales the `NoiseEvent.BaseIntensity` before
distance or occlusion attenuation. Stone is the design baseline (1.00); all other
materials are expressed relative to it.

| Surface Type | Enum | Multiplier | In-World Materials | Design Intent |
|-------------|------|-----------|-------------------|---------------|
| Stone | 0 | 1.00 | Dungeon flagstone, stone stairs, castle stone floors | Baseline. Hard, dense, resonant — the standard dungeon floor. Never truly silent. |
| Wood | 1 | 1.30 | Floorboards, wooden bridges, furniture | Amplifies. Boards creak under weight. The loud-surface trap players learn to avoid. |
| Metal | 2 | 1.55 | Grating, iron fittings, metal walkways | Most amplifying. Sharp ring carries far. The surface to avoid at all costs. |
| Dirt | 3 | 0.70 | Loose earth floors, dungeon earthworks | Muffling. Quieter than stone. Not safe, but forgiving relative to hard surfaces. |
| Carpet | 4 | 0.50 | Carpet runners, rugs, woven floor coverings | Most muffling. The player's primary quiet-surface refuge. |
| Water | 5 | 1.20 | Puddles, shallow water pools | Amplifies. Splash is distinctive and carries well. Tactical hazard when unexpected. |
| Neutral | 6 | 1.00 | Applied to all non-footstep actions (throw origin, interactions, hiding-spot entry) | No modification. Used when surface type is behaviorally irrelevant to the sound. |

**Rule ST-2.** Surface type is determined at footstep emission time by a single
downward raycast from the player's foot position. The raycast hits the topmost
surface layer. The collider must have a `SurfaceTypeTag` component storing its
`SurfaceType` enum value. If no tagged collider is hit, the surface defaults to
`Stone`. This default is fail-loud: an untagged surface behaves as the baseline
dungeon material, biasing toward detection rather than unearned silence.

**Rule ST-3.** For thrown-object impacts, surface type is determined by the same tag
lookup on the struck collider. If the struck collider has no `SurfaceTypeTag`, the
impact surface defaults to `Stone`.

**Rule ST-4.** Surface type multipliers are stored in the Sound Data asset
(`Assets/_Project/Scripts/Data/SoundData.asset`), not hardcoded. A level designer
can override a specific floor collider's surface type by placing a `SurfaceTypeTag`
component with the desired enum value, regardless of the material's visual appearance.
This supports puzzle design (a magically silenced stone floor; a supernaturally
resonant carpet). The art-facing surface type and the acoustic surface type are
intentionally decoupled.

---

### Section 3: Distance Attenuation Model

**Rule D-1.** The SPM computes a `distance_factor` (float, 0.0–1.0) based on the
Euclidean 3D distance between the `NoiseEvent.WorldPosition` and the seeker's
listening position. The listening position is the seeker's head bone world position.
If no head bone is available, it is the seeker's `transform.position` offset upward
by `seeker_ear_height` (tuning knob in seeker data asset).

**Rule D-2.** The distance factor follows a tunable-exponent falloff clamped at the
seeker's maximum hearing range. Beyond `seeker_max_hearing_range`, the sound is not
evaluated — the seeker is outside the propagation envelope and `IsAudible` is forced
false without further calculation. Within range:

```
d                     = Euclidean3D(NoiseEvent.WorldPosition, seeker_ear_position)
normalized_distance   = clamp(d / seeker_max_hearing_range, 0.0, 1.0)
distance_factor       = 1.0 - (normalized_distance ^ distance_attenuation_exponent)
```

Default value of `distance_attenuation_exponent = 1.0` (linear) for MVP. At 1.0,
sound fades proportionally with distance — the simplest mental model for players to
internalize ("twice as far = half the intensity"). A non-linear curve (exponent 2.0 =
quadratic) is available as a tuning knob adjustment after playtesting establishes a
baseline; do not change from linear until playtest data motivates the change.

**Rule D-3.** `seeker_max_hearing_range` is a per-seeker parameter stored in the
seeker data asset, defaulting to 15.0 m for the standard Seeker variant. The SPM
ScriptableObject holds a global fallback value; per-seeker assets override it. This
allows seeker variants with sharper or duller hearing without changing propagation
model parameters.

**Rule D-4.** Distance is always three-dimensional (full Euclidean, not horizontal).
A seeker on the floor above hears the player on the floor below at the true diagonal
distance. See Edge Case EC-1 for vertical-separation behavior.

---

### Section 4: Wall Occlusion Model

**Rule W-1.** After distance attenuation, the SPM checks for wall occlusion along the
line from `NoiseEvent.WorldPosition` to the seeker ear position. It counts the number
of solid geometry colliders intersected by this line using a non-allocating raycast
against the `SoundOcclusion` physics layer mask. Each intersected collider is one wall
crossing.

**Rule W-2.** Occlusion is modeled as a multiplicative reduction per wall crossed:

```
wall_count       = number of SoundOcclusion-layer colliders intersecting the line
                   from NoiseEvent.WorldPosition to seeker ear position
occlusion_factor = wall_attenuation_factor ^ wall_count
```

`wall_attenuation_factor` (default: 0.45) means each wall reduces arriving intensity
to 45% of its pre-wall value. Two walls: 0.45² = 0.20. Three walls: 0.45³ = 0.09.
One wall nearly halves intensity; two walls reduce most sounds below the typical
hearing threshold at any moderate room distance. The practical player rule of thumb:
"one wall muffles; two walls mostly silence."

**Rule W-3.** The physics raycast uses `Physics.RaycastNonAlloc` with a pre-allocated
`RaycastHit[]` buffer (recommended buffer size: 8 entries; tuning knob:
`max_wall_count_ceiling`). If the actual wall count exceeds the buffer size, the count
is capped at the ceiling. This is fail-loud: capping underestimates occlusion and may
produce false positives for audibility — the conservative direction for player fairness.

**Rule W-4.** The `SoundOcclusion` physics layer is assigned by level designers to
solid walls and closed doors. Open doors, archways, and windows do not carry this layer
and therefore do not count as wall crossings. A door's occlusion collider must be
disabled when the door is open and re-enabled when closed. The SPM has no concept of
"doors" — it sees only the presence or absence of `SoundOcclusion`-layered colliders.

**Rule W-5.** True acoustic diffraction — sound bending around corners — is not modeled.
A sound source directly around a corner from a seeker (no wall crossing the direct
line) is not occluded by the corner itself. This is a known simplification. Its
behavioral consequence is that corners provide no sound cover unless the geometry
produces an actual wall crossing on the linecast. Level designers must not present
corners as acoustic barriers if they provide no collider crossing. Blind corners with
no SoundOcclusion-layer geometry behind them are acoustically transparent.

---

### Section 5: Attenuated Intensity Computation

**Rule AI-1.** The SPM assembles the final `AttenuatedIntensity` from the three
modifiers in a fixed evaluation order with two early-exit gates:

```
Step 1 — Range gate (early exit):
    d = Euclidean3D(NoiseEvent.WorldPosition, seeker_ear_position)
    if d > seeker_max_hearing_range:
        IsAudible = false
        AttenuatedIntensity = 0.0
        STOP — do not evaluate further for this seeker

Step 2 — Surface modification:
    surface_modified_intensity = NoiseEvent.BaseIntensity * surface_multiplier[NoiseEvent.SurfaceType]

Step 3 — Distance attenuation:
    normalized_distance   = d / seeker_max_hearing_range
    distance_factor       = 1.0 - (normalized_distance ^ distance_attenuation_exponent)
    distance_attenuated   = surface_modified_intensity * distance_factor

Step 4 — Wall occlusion:
    wall_count            = Physics.RaycastNonAlloc(WorldPosition, (seeker_ear_position - WorldPosition).normalized, _wallHitBuffer, d, SoundOcclusion_layer_mask)
    occlusion_factor      = wall_attenuation_factor ^ wall_count
    wall_attenuated       = distance_attenuated * occlusion_factor

Step 5 — Clamp:
    AttenuatedIntensity   = clamp(wall_attenuated, 0.0, 1.0)

Step 6 — Audibility gate:
    IsAudible             = (AttenuatedIntensity >= seeker.HearingThreshold)
```

**Rule AI-2.** `seeker.HearingThreshold` is a per-seeker parameter stored in the
seeker data asset (owned by the Detection System, default 0.35). The SPM reads this
value to compute `IsAudible` as a convenience pre-evaluation. The SPM does not own,
store, or modify the threshold.

**Rule AI-3.** The SPM returns both `IsAudible` and `AttenuatedIntensity` for every
seeker regardless of whether `IsAudible` is true. The Detection System (F3) uses
`IsAudible` as a gate and `AttenuatedIntensity` as the magnitude input if the gate
passes.

**Rule AI-4.** `surface_modified_intensity` in Step 2 can exceed 1.0 before distance
attenuation for high-multiplier surfaces (Metal 1.55 × sprint base 0.85 = 1.32). The
Step 5 clamp handles this. The clamp is applied after all three modifiers so that
surface amplification on a nearby sound propagates its full advantage before being
clipped.

---

### Section 6: Distraction Mechanic (SPM Level)

**Rule DM-1.** When a throwable object impacts a surface, the Throwable Object
component emits a `NoiseEvent` with `WorldPosition` set to the world position of the
impact — not the player's current position. Base intensity is High (approximately
0.85, matching a landing-from-jump spike). Surface type is determined by the struck
surface's `SurfaceTypeTag`.

**Rule DM-2.** The SPM evaluates this impact `NoiseEvent` identically to any other
event: distance is measured from the impact position to each seeker's ear, wall
occlusion is checked along the impact-to-seeker line, and the surface multiplier of
the struck surface is applied.

**Rule DM-3.** The Seeker AI's response to the resulting suspicion spike — turning
toward the `NoiseEvent.WorldPosition` and transitioning to Investigate behavior — is
owned by the Seeker AI system, not the SPM. The SPM does not know that an impact is
a "distraction." It knows only that a `NoiseEvent` exists at a world position, and it
propagates it.

**Rule DM-4.** The tactical distraction mechanic emerges entirely from Rule DM-1:
the seeker investigates the impact location, not the player's location, because the
`NoiseEvent.WorldPosition` encodes the impact point. The player can directly observe
where the thrown object lands, understand that the sound originates there, and predict
which seekers will respond. No hidden displacement or approximation is applied.

---

### Interactions with Other Systems

| System | Direction | Data | Owner |
|--------|-----------|------|-------|
| Player Noise Emitter | SPM reads from | `NoiseEvent` struct (WorldPosition, BaseIntensity, SurfaceType) | Player Noise Emitter emits; SPM evaluates |
| Throwable Object component | SPM reads from | `NoiseEvent` at impact position | Throwable Object emits on contact; SPM evaluates identically to player events |
| SurfaceTypeTag component | SPM reads from | `SurfaceType` enum per collider; resolved by downward raycast at footstep time and by collider lookup at impact time | Level designers author tags; SPM reads |
| Seeker data assets | SPM reads from | `seeker_max_hearing_range` (float), `seeker_ear_height` (float), `seeker.HearingThreshold` (float, owned by Detection System) | Seeker data owns values; SPM reads at evaluation time |
| Detection System | SPM writes to | `SPMResult { bool IsAudible, float AttenuatedIntensity }` per seeker per event | SPM owns attenuation calculation; Detection System owns suspicion response. When processing an audible event, the Detection System retains a reference to the original `NoiseEvent` (including `WorldPosition`) and passes it to the Seeker AI as the investigation target position — this is how seekers face the impact point (AC-SPM-10) without `WorldPosition` needing to appear in `SPMResult`. |
| Footstep Audio system | SPM shares with | `SurfaceType` resolved by the Player Noise Emitter's downward raycast | Player Noise Emitter should expose the resolved `SurfaceType` so Footstep Audio can select the correct clip without a duplicate raycast |

---

## Formulas

### F1 — Surface Type Multiplier

Applied to `NoiseEvent.BaseIntensity` before any distance or occlusion calculation.
The surface type is a discrete enum carried on the `NoiseEvent` struct.

```
surface_modified_intensity = base_intensity * surface_multiplier[surface_type]
```

**Surface Multiplier Table (stored in Sound Data ScriptableObject):**

| Surface Type | Enum | `surface_multiplier` | Rationale |
|---|---|---|---|
| `Stone` | 0 | 1.00 | Baseline. Hard flagstone; reference value for all other calibration |
| `Wood` | 1 | 1.30 | Floorboards resonate; creak amplifies mid-frequency impact |
| `Metal` | 2 | 1.55 | Grating and fittings ring sharply; highest transmission |
| `Dirt` | 3 | 0.70 | Loose earth absorbs impact; dampened thud |
| `Carpet` | 4 | 0.50 | Textile absorbs mid and high frequencies; near-silent footfall |
| `Water` | 5 | 1.20 | Splash is distinctive; liquid resonance slightly louder than stone |
| `Neutral` | 6 | 1.00 | No modification; applied to non-footstep actions |

| Variable | Type | Range | Source | Description |
|---|---|---|---|---|
| `base_intensity` | float | 0.0–1.0 | `NoiseEvent` | Raw intensity assigned at emission by the Player Noise Emitter |
| `surface_type` | SurfaceType enum | 0–6 | `NoiseEvent` | Surface material at the emission position |
| `surface_multiplier[surface_type]` | float | 0.50–1.55 | Sound Data asset | Lookup value indexed by surface type; all seven entries are tuning knobs |
| `surface_modified_intensity` | float | 0.0–1.55 | computed | Intensity after surface scaling; may exceed 1.0 for Metal/Wood at high base intensities — clamped in F4 |

**Note:** `surface_modified_intensity` may exceed 1.0 (Metal × sprint: 1.55 × 0.85 =
1.32). This is intentional — loud sounds on metal should propagate further than the
same action on stone. F4 clamps the final result to [0.0, 1.0].

**Example — Walking footstep, Stone vs. Carpet (same distance):**
- Stone: base = 0.50 × 1.00 = 0.50
- Carpet: base = 0.25 × 0.50 = 0.125
  (Carpet walk uses a lower BaseIntensity by action type AND a lower surface multiplier)
- For comparison using the same base intensity 0.50 on both surfaces:
  - Stone: 0.50 × 1.00 = 0.50
  - Carpet: 0.50 × 0.50 = 0.25 — exactly half, matching the multiplier ratio

**Example — Sprint on Metal:**
- base = 0.85, surface_multiplier = 1.55 → surface_modified_intensity = **1.3175**
- This value is passed into F2 where distance attenuation brings it below 1.0 at any
  non-trivial range. It is not clamped here, preserving the full propagation advantage.

---

### F2 — Distance Attenuation

```
d                           = Vector3.Distance(noise_source_position, seeker_ear_position)  // 3D Euclidean

// Early exit: beyond max range, sound is inaudible without further computation
if (d > seeker_max_hearing_range):
    distance_attenuated_intensity = 0.0
    return

normalized_distance         = d / seeker_max_hearing_range                   // clamped [0.0, 1.0] by early exit
distance_factor             = 1.0 - (normalized_distance ^ distance_attenuation_exponent)
distance_attenuated_intensity = surface_modified_intensity * distance_factor
```

| Variable | Type | Range | Source | Description |
|---|---|---|---|---|
| `d` | float | 0.0–∞ m | computed | Full 3D Euclidean distance from noise origin to seeker ear position |
| `noise_source_position` | Vector3 | world space | `NoiseEvent.WorldPosition` | Impact position for thrown objects; player position for body sounds |
| `seeker_ear_position` | Vector3 | world space | seeker head bone or transform + `seeker_ear_height` | Seeker's listening origin |
| `seeker_max_hearing_range` | float | per-seeker | seeker data asset | Distance beyond which the sound is not evaluated; default 15.0 m |
| `normalized_distance` | float | 0.0–1.0 | computed | d ÷ max range; 0 = point-blank, 1 = exactly at max range |
| `distance_attenuation_exponent` | float | tuning | Sound Data asset | Falloff curve shape; **default 1.0 (linear)** for MVP. 2.0 = quadratic; raise only after linear has been playtested. |
| `distance_factor` | float | 0.0–1.0 | computed | 1.0 at d = 0; 0.0 at d = max range |
| `distance_attenuated_intensity` | float | 0.0–1.55 | computed | Surface-modified intensity scaled by distance; not yet clamped |

**Linear Falloff Reference Table (default, exponent = 1.0, max_range = 15m):**

| Distance | `distance_factor` | Stone walk (base 0.50) | Metal sprint (surface_modified 1.32) |
|---|---|---|---|
| 0 m | 1.00 | 0.50 | 1.32 |
| 3 m | 0.80 | 0.40 | 1.06 |
| 6 m | 0.60 | 0.30 | 0.79 |
| 9 m | 0.40 | 0.20 | 0.53 |
| 12 m | 0.20 | 0.10 | 0.26 |
| 15 m | 0.00 | 0.00 | 0.00 |

With `seeker_hearing_threshold = 0.35` (Detection System default): a stone walk is
heard only below ~5.5m. A metal sprint is heard up to ~11m (before wall occlusion).

**Learnability note:** Linear (exponent = 1.0) is the MVP starting value. Players
intuit "twice as far = half as loud." Quadratic (2.0) is available if playtesting
finds the early-range feel too punishing; it compresses the danger zone toward
point-blank. Do not change until linear has been validated through playtesting.

---

### F3 — Wall Occlusion

```
// Pre-allocated buffer on the SPM component — never allocated per-event
private static readonly RaycastHit[] _wallHitBuffer = new RaycastHit[max_wall_count_ceiling];

wall_count          = Physics.RaycastNonAlloc(
                          origin:      noise_source_position,
                          direction:   (seeker_ear_position - noise_source_position).normalized,
                          results:     _wallHitBuffer,
                          maxDistance: d,
                          layerMask:   wall_layer_mask
                      )

wall_count          = min(wall_count, max_wall_count_ceiling)   // cap to buffer size (fail-loud)
occlusion_factor    = wall_attenuation_factor ^ wall_count
wall_attenuated_intensity = distance_attenuated_intensity * occlusion_factor
```

| Variable | Type | Range | Source | Description |
|---|---|---|---|---|
| `wall_count` | int | 0–`max_wall_count_ceiling` | Physics.RaycastNonAlloc | Number of `SoundOcclusion`-layer colliders crossed by the direct line from source to seeker ear |
| `wall_layer_mask` | LayerMask | — | Sound Data asset | Physics layer(s) designating wall geometry; excludes floor, ceiling, props, entities |
| `wall_attenuation_factor` | float | 0.0–1.0 | Sound Data asset | Multiplier per wall crossed; **default 0.45** — each wall reduces arriving intensity to 45% |
| `occlusion_factor` | float | 0.0–1.0 | computed | `wall_attenuation_factor ^ wall_count`; 1.0 with zero walls |
| `max_wall_count_ceiling` | int | tuning | Sound Data asset | Buffer size cap; set to 8 for MVP; at 0.45^8 ≈ 0.002, effectively zero |
| `wall_attenuated_intensity` | float | 0.0–1.55 | computed | Intensity after wall occlusion; passed into F4 for clamping |

**Worked Example — Walking on Stone (base 0.50), 8m from seeker, 1 wall:**
- F1: 0.50 × 1.00 = 0.50
- F2: normalized = 8/15 = 0.533; distance_factor = 1 − 0.533 = 0.467; distance_attenuated = 0.50 × 0.467 = 0.234
- F3: wall_count = 1; occlusion = 0.45^1 = 0.45; wall_attenuated = 0.234 × 0.45 = **0.105**
- Threshold = 0.35 → inaudible. Seeker in the adjacent room does not hear walking at 8m.

**Worked Example — Sprinting on Metal (base 0.85), 4m, 1 wall:**
- F1: 0.85 × 1.55 = 1.3175
- F2: normalized = 4/15 = 0.267; distance_factor = 0.733; distance_attenuated = 1.3175 × 0.733 = 0.965
- F3: wall_count = 1; occlusion = 0.45; wall_attenuated = 0.965 × 0.45 = **0.434**
- Threshold = 0.35 → **audible** (0.434 > 0.35). A metal sprint 4m away through one wall is heard.

**Worked Example — Two walls:**
- Same metal sprint at 8m, 2 walls: F1 = 1.3175; F2 = 0.467 → 0.615; F3 = 0.45^2 = 0.2025 → 0.125
- Threshold = 0.35 → inaudible. Two walls silence the metal sprint at 8m.

**Wall geometry note:** Wall colliders must be on the `SoundOcclusion` layer
(separate from the `EnemyDetection` layer used by the visual LoS raycast). Level
designers assign this layer to all load-bearing walls, closed-door collision geometry,
and floor slabs between levels that should attenuate vertical sound transmission.

---

### F4 — Final Audibility Determination and Handoff

Combines F1 × F2 × F3 into the final output contract delivered to the Detection System.

```
// Full pipeline:
surface_modified_intensity   = base_intensity * surface_multiplier[surface_type]           // F1
distance_factor              = 1.0 - clamp(d / seeker_max_hearing_range, 0, 1) ^ distance_attenuation_exponent  // F2
occlusion_factor             = wall_attenuation_factor ^ wall_count                         // F3

// Final combination and clamp:
AttenuatedIntensity          = clamp(surface_modified_intensity * distance_factor * occlusion_factor, 0.0, 1.0)

// Handoff to Detection System (read seeker.HearingThreshold from seeker data asset):
IsAudible                    = (AttenuatedIntensity >= seeker.HearingThreshold)
```

**Output contract per seeker per `NoiseEvent`:**

| Field | Type | Range | Consumed By |
|---|---|---|---|
| `AttenuatedIntensity` | float | 0.0–1.0 | Detection System F3: `intensity_excess` calculation |
| `IsAudible` | bool | — | Detection System F3: guard clause; convenience pre-evaluation using seeker's threshold |

**The SPM's responsibility ends at `AttenuatedIntensity`.** The SPM reads
`seeker.HearingThreshold` from the seeker data asset to compute `IsAudible` as a
convenience, but does not own, store, or modify the threshold value. The Detection
System owns the threshold, the suspicion spike formula, and the state consequence.

**Full Pipeline Example — Walking on Wood (base 0.65), 7m from seeker, 1 wall:**

| Step | Operation | Value |
|---|---|---|
| Base intensity | Player Noise Emitter (walk, wood) | 0.65 |
| F1: surface | Wood: 1.30 | 0.65 × 1.30 = 0.845 |
| F2: distance | d=7m, max=15m, exp=1.0: 1−(7/15)=0.533 | 0.845 × 0.533 = 0.450 |
| F3: wall | 1 wall, factor=0.45: 0.45^1=0.45 | 0.450 × 0.45 = 0.203 |
| F4: clamp | clamp(0.203, 0, 1) | **0.203** |
| Handoff | threshold=0.35; 0.203 < 0.35 | IsAudible = **false** |

A seeker in the next room does not hear a walking footstep on wood at 7m through one
wall. Running on metal at the same position: 0.85 × 1.55 × 0.533 × 0.45 = 0.316 —
still below threshold, but much closer to the boundary. A metal sprint 4m away
through one wall (worked example in F3): 0.434 → audible.

---

## Edge Cases

| Scenario | Expected Behavior | Rationale |
|----------|------------------|-----------|
| **EC-1: Player and seeker on different floors (vertical distance)** | SPM uses full 3D Euclidean distance (Rule D-4). A player on the lower floor 4m below and 2m horizontally from a seeker is 4.47m away (sqrt(16+4)). The linecast for wall occlusion runs the same diagonal. If a floor slab between them has the `SoundOcclusion` layer, it counts as one wall crossing. Without a tagged slab, vertical separation is penalized only by distance. Level designers must tag all floor slabs that should act as acoustic barriers. Recommended default: all separating floor slabs are tagged. | Correct physics behavior. Vertical acoustic isolation requires tagged geometry, not just physical separation. |
| **EC-2: NoiseEvent at exact boundary of hearing range** | At `d == seeker_max_hearing_range`, `distance_factor = 0.0`. `AttenuatedIntensity = 0.0`. `IsAudible = false`. Sound at exactly the boundary is inaudible. | Hard outer limit. Players who learn the rough hearing radius can trust that the boundary means safety. The linear falloff means sounds become very quiet well before the hard boundary — the functional "safe distance" is somewhat closer than the hard maximum. |
| **EC-3: Multiple walls between source and seeker** | Each additional `SoundOcclusion`-layered collider compounds the reduction multiplicatively (Rule W-2). At 0.45 per wall: 1 wall = 0.45×, 2 walls = 0.20×, 3 walls = 0.09×. At 3+ walls, virtually all sounds fall below threshold at any room-scale distance. Level designers should validate that no patrol route creates an unintended zero-wall acoustic corridor across a large open space that makes expected-safe positions dangerous. | Compounding multiplicative attenuation means walls stack rapidly. Deliberate design: two walls is meaningful protection; three walls is near-silence. |
| **EC-4: Sound emitted inside a hiding spot** | The SPM has no concept of hiding spots. It evaluates the player's world position as normal. The hiding spot's physical geometry may provide wall crossings if its colliders are tagged with `SoundOcclusion`. If the locker or closet has tagged walls, noise from inside is attenuated. If it does not, noise propagates at full distance attenuation only. This is intentional — acoustic cover from a hiding spot is an emergent property of its physical construction, not a special SPM rule. Level designers who want hiding spots to provide acoustic cover must tag the spot's walls. Hiding spots without acoustic cover (e.g., a shadow alcove) are valid and create a different risk profile: visual safety but full acoustic presence. (Detection System Rule H-2 confirms: audio detection functions normally while hidden.) | Acoustic behavior of hiding spots is a level design decision, not a system special case. |
| **EC-5: Multiple simultaneous NoiseEvents** | Each event is evaluated independently, producing independent result structs per seeker. The SPM queues evaluation in the order events are received. There is no per-event cap at the SPM level. The Detection System (F3) accumulates spikes additively for MVP; `audio_spike_frame_cap` is a Detection System tuning knob candidate if stacking becomes exploitable. | Clean single-responsibility: SPM propagates each event correctly; Detection System owns stacking policy. |
| **EC-6: `wall_count` exceeds `max_wall_count_ceiling` buffer** | Wall count is capped at `max_wall_count_ceiling` (default 8). At 0.45^8 ≈ 0.002, the sound is functionally silent regardless. The cap prevents buffer overflow without gameplay consequence. This is fail-loud behavior: capping underestimates occlusion (conservative toward detection). | Pragmatic cap on physics overhead. No gameplay impact at realistic wall depths. |
| **EC-7: Carpet walking produces `AttenuatedIntensity` below threshold at all distances, including d = 0** | At default values, carpet walk: `BaseIntensity = 0.25`, `surface_multiplier = 0.50` → `surface_modified_intensity = 0.125`. The seeker hearing threshold is 0.35. Since 0.125 < 0.35 at any distance factor (maximum is 1.0 at d = 0), carpet walking is **acoustically undetectable** at default settings. This is intentional design: carpet is the player's primary acoustic refuge. A seeker can only detect a carpet-walking player through visual means. Level designers should ration carpet placement to avoid trivializing acoustic stealth. To reduce carpet advantage if playtesting finds it too strong, raise `surface_multiplier[Carpet]` toward 0.65 or raise the carpet walk `BaseIntensity` toward 0.50 in the Sound Data asset. Do not change until playtesting establishes that carpet inaudibility is problematic. | Deliberate design decision. The carpet walk double-dip (low action BaseIntensity AND low surface multiplier) intentionally stacks two mechanisms to create a clearly safe surface. The player discovers carpet as a reliable refuge — this is a teachable, consistent rule (Pillar 1). Changing the threshold instead of the surface data would affect all sounds, not just carpet. |

---

## Dependencies

### What the SPM Depends On

| System | Dependency Type | Data Required | Notes |
|--------|----------------|---------------|-------|
| Player Noise Emitter | Input source | `NoiseEvent` struct | Sole upstream emitter for player-origin sounds |
| Throwable Object component | Input source | `NoiseEvent` struct at impact position | Emits on contact; SPM evaluates identically |
| `SurfaceTypeTag` component | Physics query | `SurfaceType` enum per surface collider | Lightweight MonoBehaviour on floor/wall colliders; SPM reads via raycast |
| Sound Data asset | Configuration | `surface_multiplier` table, `wall_attenuation_factor`, `wall_layer_mask`, `distance_attenuation_exponent`, `max_wall_count_ceiling` | ScriptableObject at `Assets/_Project/Scripts/Data/SoundData.asset` |
| Seeker data assets | Configuration | `seeker_max_hearing_range`, `seeker_ear_height`, `seeker.HearingThreshold` | Per-seeker ScriptableObjects; SPM reads at evaluation time |
| Unity Physics | Runtime | `Physics.RaycastNonAlloc` against `SoundOcclusion` layer | Non-allocating; pre-allocated `RaycastHit[]` buffer on SPM component |

### What Depends on the SPM

| System | Dependency Type | Data Provided | Notes |
|--------|----------------|---------------|-------|
| Detection System | Primary consumer | `IsAudible` (bool), `AttenuatedIntensity` (float 0.0–1.0) per seeker per event | Detection System Rule A-2 and F3 consume these values. SPM result is the sole input to the audio suspicion spike calculation. |
| Footstep Audio system | Indirect consumer | `SurfaceType` from the shared `SurfaceTypeTag` raycast | Footstep Audio needs surface type to select the correct audio clip. The Player Noise Emitter should expose the resolved `SurfaceType` so the Audio system does not perform a duplicate downward raycast. |

### Interface Contract

The SPM exposes one entry point:

```csharp
void EvaluateNoiseEvent(NoiseEvent noiseEvent, IReadOnlyList<Seeker> activeSeekers, IList<SPMResult> results)
```

The caller (Detection System) owns and pre-allocates the `results` list (capacity =
max active seeker count). The SPM clears it at the start of each call and populates
it with one `SPMResult` per seeker. No heap allocation occurs per call — the list
is reused across events.

Where `SPMResult` is:

```csharp
public struct SPMResult
{
    public Seeker Seeker;
    public bool IsAudible;
    public float AttenuatedIntensity;   // [0.0, 1.0]
}
```

The SPM is stateless per call. The Detection System is responsible for accumulating
suspicion state over time and for retaining the original `NoiseEvent` (including
`WorldPosition`) when passing investigation targets to the Seeker AI.

---

## Tuning Knobs

All values live in the `SoundPropagationData` ScriptableObject or per-seeker data
assets as noted. No values may be hardcoded.

| Parameter | Asset | Current Value | Safe Range | Effect of Increase | Effect of Decrease |
|---|---|---|---|---|---|
| `surface_multiplier[Stone]` | Sound Data | 1.00 | 0.8–1.2 | Stone movement louder; baseline footstep harder to escape | Stone quieter; relative advantage of carpet reduced |
| `surface_multiplier[Wood]` | Sound Data | 1.30 | 1.1–1.6 | Wood sections become audio hazard zones | Wood less distinctive; surface choice matters less |
| `surface_multiplier[Metal]` | Sound Data | 1.55 | 1.3–1.8 | Metal is a strong audio hazard even when crouching | Metal less distinctive; loses "avoid at all costs" feel |
| `surface_multiplier[Dirt]` | Sound Data | 0.70 | 0.5–0.85 | Dirt less forgiving; advantage over stone reduced | Dirt very quiet; dominant safe surface if overused in levels |
| `surface_multiplier[Carpet]` | Sound Data | 0.50 | 0.3–0.65 | Carpet less effective; close-range stealth harder | Carpet nearly silent; any carpeted area trivially safe |
| `surface_multiplier[Water]` | Sound Data | 1.20 | 1.0–1.4 | Water puddles become audio hazards to actively avoid | Water splash indistinguishable from stone; loses tactical meaning |
| `surface_multiplier[Neutral]` | Sound Data | 1.00 | 0.8–1.2 | Non-footstep actions louder (interactions, throws) | Non-footstep actions quieter; less audio variety |
| `distance_attenuation_exponent` | Sound Data | 1.0 | 0.5–3.0 | **Do not raise from 1.0 until linear is playtested.** Higher = sharper drop near max range, gentle close | Gentle falloff; sounds carry further at reduced intensity; faint audibility at long range |
| `wall_attenuation_factor` | Sound Data | 0.45 | 0.20–0.70 | Adjacent rooms safer; walls are strong barriers | Walls nearly transparent; rooms feel acoustically open |
| `max_wall_count_ceiling` | Sound Data | 8 | 4–12 | Finer tracking through deep geometry | May under-count in pathological geometry |
| `seeker_max_hearing_range` | Seeker data | 15.0 m | 5.0–25.0 m | Seekers hear sounds across larger rooms; open areas more dangerous | Seekers only hear nearby sounds; room topology less punishing |
| `seeker_ear_height` | Seeker data | 1.7 m | 1.0–2.2 m | Listening origin higher; affects 3D distance for sounds on lower surfaces | Lower listening origin; sounds from above carry farther |

**Priority tuning order for first playtest:** `wall_attenuation_factor` → surface
multipliers for Metal and Carpet → `seeker_max_hearing_range`. These four define the
acoustic "size" of the dungeon and the reward gradient for surface-aware movement.
`distance_attenuation_exponent` should only be touched if the linear model is
confirmed inadequate through structured playtest data.

---

## Acceptance Criteria

All criteria are verifiable in Unity Editor Play Mode using the Unity Profiler and
Test Runner. "Default values" refers to the Tuning Knobs table above plus
`seeker_hearing_threshold = 0.35` from the Detection System GDD.

- [ ] **AC-SPM-01** Emit `NoiseEvent` (base = 1.0, surface = Stone) at exactly `seeker_max_hearing_range` (15m), zero walls. `AttenuatedIntensity = 0.0`. `IsAudible = false`. **Pass**: value at floating-point epsilon of 0. **Fail**: > 0.
- [ ] **AC-SPM-02** Emit identical events (base = 0.60) at 5m, zero walls, once per surface type. Collect `AttenuatedIntensity` for each. **Pass**: `Metal > Wood > Water > Stone == Neutral > Dirt > Carpet`. **Fail**: any adjacent pair out of order or equal.
- [ ] **AC-SPM-03** Same base intensity (0.60), same distance, Stone vs Metal, zero walls. **Pass**: ratio `Metal / Stone = 1.55` (±0.001). **Fail**: ratio differs by > 0.001.
- [ ] **AC-SPM-04** Walking footstep on Stone (base = 0.50) at 8m, exactly 1 `SoundOcclusion` wall between source and seeker. **Pass**: `AttenuatedIntensity < 0.35`. `IsAudible = false`. **Fail**: `AttenuatedIntensity ≥ 0.35`.
- [ ] **AC-SPM-05** Test scene with exactly 2 `SoundOcclusion` walls crossing the raycast path. **Pass**: SPM reports `wall_count = 2`. **Fail**: 0, 1, or 3+.
- [ ] **AC-SPM-06** Clear line between source and seeker (no `SoundOcclusion` colliders). **Pass**: `wall_count = 0`, `occlusion_factor = 1.0`, result matches distance-only calculation. **Fail**: any wall penalty applied.
- [ ] **AC-SPM-07** Sprint on Metal (base = 0.85), 4m, 1 wall. **Pass**: `AttenuatedIntensity ≈ 0.434` (±0.005). `IsAudible = true`. **Fail**: value differs by > 0.005 or `IsAudible = false`.
- [ ] **AC-SPM-08** Sprint on Metal (base = 0.85), at d = 0 (source at seeker position), zero walls. **Pass**: `AttenuatedIntensity = 1.0` (clamped from 1.3175). **Fail**: value > 1.0.
- [ ] **AC-SPM-09** Noise source directly above seeker (XZ distance = 0m, Y separation = 5m). **Pass**: SPM uses d = 5m for attenuation (not d = 0). **Fail**: system uses XZ-only distance.
- [ ] **AC-SPM-10** Emit distraction object impact. `NoiseEvent.WorldPosition` is the impact location, not the player's current position. Detection System receives the impact position. Seeker AI turns toward impact position. **Pass**: seeker investigates impact point. **Fail**: seeker turns toward player position.
- [ ] **AC-SPM-11** Emit 1 `NoiseEvent` with 4 active seekers. Measure total SPM evaluation time via Unity Profiler (Development Build). **Pass**: < 0.2ms combined for all 4 seekers. **Fail**: ≥ 0.2ms.
- [ ] **AC-SPM-12** Code review: `seeker_hearing_threshold` appears in SPM code only as a read from the seeker data parameter. **Pass**: no hardcoded threshold; no SPM-owned field for the value. **Fail**: threshold hardcoded or stored on SPM component.

---

## Open Questions

| Question | Owner | Resolution Path |
|----------|-------|----------------|
| OQ-1: Is `wall_attenuation_factor = 0.45` well-calibrated for dungeon room spacing? The worked examples show one wall providing meaningful cover at 8m. This needs validation against actual Level_1 room dimensions and typical seeker patrol distances. | Game Designer | Playtest Level_1 with 2–3 wall factor configurations (0.35 / 0.45 / 0.60). Measure whether players feel adjacent rooms are "safe," "risky," or "arbitrary." |
| OQ-2: Does the linear falloff (exponent 1.0) feel intuitive to players, or does close-range detection feel too abrupt? Linear is the MVP default; quadratic (2.0) is available. | Game Designer | Playtest Level_1. If players report that sounds feel "too abrupt" at medium range or "surprisingly audible" up close, consider raising exponent to 1.5 or 2.0. |
| OQ-3: Should thrown-object impact surface type matter for the distraction mechanic? Currently Metal impacts emit 1.55× the base intensity. A stone thrown onto a metal grating creates a louder distraction than one thrown onto carpet — which is correct physics but may create unintuitive puzzle solutions if level designers mix surface types in distraction zones. | Game Designer / Level Designer | Prototype in Level_1 with mixed-surface distraction targets. Assess whether players intuit the surface × impact loudness relationship or find it arbitrary. |
| OQ-4: Should `seeker_ear_height` be a runtime value (e.g., animated head position from bone) rather than a static offset? The Detection System's FOV cone tracks the head bone for visual detection. Audio detection using a static offset is an inconsistency a player might notice if the seeker crouches or leans. | Lead Programmer | Low priority for MVP. Accept static offset until playtest surfaces a fairness complaint. Prototype head-bone-based ear position in Sprint 2 if complaints emerge. |
