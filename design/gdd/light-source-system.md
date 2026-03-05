# Light Source System

> **Status**: Approved
> **Author**: game-designer + systems-designer
> **Last Updated**: 2026-03-05
> **Implements Pillar**: The Room Has Rules (Pillar 1), Silence Is a Tool (Pillar 2), Legible Jeopardy (Pillar 3)

---

## Overview

The Light Source System (LSS) is a foundation-layer system in UNSEEN responsible
for answering one query per entity per FixedUpdate: "What is the ambient light
level at this world position?" It returns a normalized float (0.0 = full darkness,
1.0 = fully lit) that the Detection System applies as a direct modifier to visual
suspicion accumulation. In MVP scope, the LSS is a static zone query system:
level designers place trigger volumes tagged with a `LightZone` component, each
storing a single `ambientLightLevel` float. The system maintains a per-entity list
of zones currently overlapping that entity and returns the highest value among
them (brightest-wins overlap resolution). When an entity is outside all zones, a
global `defaultAmbientLightLevel` fallback is returned. No light levels change at
runtime in MVP — zones are placed once at design time and never modified during
play. Interactive light sources (breakable torches, carriable lanterns, extinguishable
flames) and push-event notification are explicitly deferred to the Vertical Slice
milestone, with their design documented in this GDD as a forward-compatibility
contract.

---

## Player Fantasy

The player should feel like they are reading the room, not guessing at it.

**The shadow as a tool.** Early in Level_1, the player discovers that crouching
inside the dark archway between two lit corridors stops the suspicion meter from
rising even as a seeker stares in their general direction. No tooltip announced
this. The player moved into shadow and watched the meter hold. That discovery —
"darkness is protection, and I can see where the dark is" — is the foundational
teaching moment the Light Source System exists to deliver. Darkness in UNSEEN is
not incidental ambiance. It is a resource the player manages actively. The
cognitive act of scanning a room for dark zones before moving is the same skill
loop as scanning for carpet runners in the Sound Propagation Model. Both are
versions of the same player behavior: reading the room's physics before committing
to a path.

**Legible jeopardy in the dark.** The player must always know their current
exposure level. Standing in a dim corridor, they should feel approximately how
visible they are — not precisely to two decimal places, but enough to make
a confident decision: "this shadow is deep enough" or "this is too open." Pillar 3
(Legible Jeopardy) is load-bearing here. A player who cannot estimate their light
exposure cannot make meaningful choices. A player who can estimate it — because
the HUD indicator and the visual environment are consistent and readable — feels
the competence satisfaction of SDT (Self-Determination Theory) every time they
correctly judge a shadow's depth.

**The two textures of darkness.** The Light Source System underpins two distinct
emotional registers: the security of deep darkness (the player reaches a zone
with `ambientLightLevel = 0.0` and feels momentarily safe, a resource spent) and
the anxiety of marginal light (a zone at 0.3 offers reduced-but-not-zero
detection, forcing the player to weigh partial cover against time pressure). Both
registers are valuable. Deep darkness should be rationed by level design — not
every room should have a zero-light zone. The gradient between lit and unlit is
where the game's most interesting decisions live.

**Vertical Slice extension:** When interactive lights arrive, the fantasy expands
to include the satisfying violence of extinguishing a torch to carve out new dark
territory, or the vulnerability of carrying a lantern that broadcasts your position
to every seeker in the room. These extensions do not change the core fantasy — they
add new expressions of the same darkness-as-resource concept.

*MDA Framework: the LSS primarily serves the Discovery aesthetic (the player
discovers which zones provide cover) and the Challenge aesthetic (managing
exposure level under time pressure). Secondary: Sensation — the visual darkening
of the character model in shadow provides tactile confirmation of a mechanical
state change.*

*Self-Determination Theory: Autonomy — the player chooses which shadows to use
and when. Competence — correctly reading light level and acting on it is a
learnable skill that grows across sessions.*

---

## Detailed Rules

### Section 1: Scope Declaration

**MVP scope (implemented in Sprint 1):**
- Static ambient light zone volumes (`LightZone` component on trigger colliders)
- Per-zone `ambientLightLevel` float (0.0–1.0), authored at level design time
- Synchronous query API: `GetLightLevelAtPosition(Vector3)` returning float
- Brightest-wins overlap resolution
- Global `defaultAmbientLightLevel` fallback when outside all zones
- No runtime modification of light levels

**Vertical Slice scope (designed here, implemented later):**
- Breakable light sources: player interaction destroys a light source, setting its
  zone's `ambientLightLevel` to 0.0 permanently for the remainder of the level
- Carriable light sources: player picks up a torch or lantern, creating a dynamic
  zone that follows the player's position; zone radius and intensity are
  configurable per light asset
- Extinguishable sources: environmental triggers (wind, water) set a light source
  to extinguished state
- Push-event model: `OnLightStateChanged(LightZone zone, float newLevel)` event
  emitted to notify dependent systems without polling

---

### Section 2: LightZone Component

**Rule LZ-1.** A `LightZone` is a MonoBehaviour component placed on a GameObject
that also has a Collider set to `Is Trigger = true`. The collider defines the
zone's physical volume — any entity whose center-of-mass enters the trigger volume
is considered "inside" the zone.

**Rule LZ-2.** `LightZone` exposes exactly one serialized field for MVP:

```csharp
[SerializeField] [Range(0f, 1f)] float ambientLightLevel = 1.0f;
```

The `[Range(0f, 1f)]` attribute enforces valid values in the Unity Inspector.
The default value is 1.0 (fully lit) because unintentionally dark zones are a
harder error to detect during level design than unintentionally bright ones
(fail-visible principle: default toward the state that exposes gameplay errors
at the most noticeable time).

**Rule LZ-3.** `LightZone` colliders are placed on a dedicated `LightZone`
physics layer. This layer does not interact with player movement physics, seeker
navigation, or the `EnemyDetection` or `SoundOcclusion` layers. It is trigger-only
and is queried by the `LightSourceSystem` component, not by Physics.Overlap calls
from other systems.

**Rule LZ-4.** A `LightZone` volume does not need to align with visible light
pools in the rendered scene. The LSS is an abstraction layer — visual lighting is
authored independently by the environment artist. Level designers are responsible
for aligning LightZone volumes with the visual intent of the scene. Misalignment
between visual lighting and zone boundaries is a level design authoring error, not
a system error. (See Open Questions OQ-LSS-01 for tooling to assist alignment.)

**Rule LZ-5.** Zones may overlap arbitrarily. There is no limit on zone count in
a scene. The system resolves overlapping zones by returning the maximum
`ambientLightLevel` among all zones the queried position is inside (brightest-wins
— see Section 3).

**Vertical Slice addition:** `LightZone` will gain two additional serialized fields
when interactive lights are implemented:
```csharp
// Vertical Slice only — do not implement in MVP
[SerializeField] bool isInteractive = false;
[SerializeField] LightSourceBehavior behavior = LightSourceBehavior.Static;
// LightSourceBehavior enum: Static, Breakable, Carriable, Extinguishable
```

---

### Section 3: LightSourceSystem Component

**Rule LSS-1.** `LightSourceSystem` is a `SceneSingleton<LightSourceSystem>` —
scene-bound, does not auto-create, must be placed in the scene by level designers.
It is the sole gateway between the `LightZone` volumes and all consuming systems.
No system queries `LightZone` components directly.

**Rule LSS-2.** On scene load (`Awake`), `LightSourceSystem` locates all `LightZone`
components in the scene via `FindObjectsByType<LightZone>(FindObjectsSortMode.None)`
and caches them in a `List<LightZone>`. This cache is built once and not rebuilt
at runtime in MVP. If a zone is added or removed at runtime (Vertical Slice
scenario), the system must expose a `RegisterZone(LightZone)` /
`UnregisterZone(LightZone)` pair for dynamic registration.

**Rule LSS-3.** `LightSourceSystem` exposes one public method for MVP:

```csharp
public float GetLightLevelAtPosition(Vector3 worldPosition)
```

This method is the complete public API for MVP. No other public methods are
exposed. The method is stateless — it does not cache per-position results between
calls.

**Rule LSS-4.** `LightSourceSystem` holds one ScriptableObject reference:

```csharp
[SerializeField] LightSourceData lightSourceData;
```

`LightSourceData` stores the global `defaultAmbientLightLevel` (fallback) and any
future zone configuration overrides. This keeps all tuning values data-driven and
out of the component.

**Rule LSS-5.** The Detection System is the primary caller of
`GetLightLevelAtPosition`. It calls the method once per seeker per FixedUpdate,
passing the **player's world position** (not the seeker's position). This is
because the light modifier in the detection formula represents how much light is
falling on the player — the subject being observed — not on the observer. (See
Key Design Decision below for full rationale.)

**Rule LSS-6.** The `LightSourceSystem` does not know about seekers, the player,
or the Detection System. It answers a position query and returns a float. All
semantic interpretation belongs to the caller.

---

### Section 4: Zone Membership Tracking

**Rule ZM-1.** Zone membership — which zones a given position is inside at query
time — is determined by a point-in-bounds test rather than by trigger callbacks.
For MVP, `GetLightLevelAtPosition(Vector3 pos)` iterates the cached zone list and
tests each zone's collider using `Collider.bounds.Contains(pos)` as a fast AABB
pre-pass, followed by `Collider.ClosestPoint(pos) == pos` for non-box shapes if
the AABB passes. This avoids reliance on OnTriggerEnter/Exit callbacks, which
can be missed on scene load or if the queried position is not a physics-enabled
rigidbody.

**Rule ZM-2.** For MVP, all `LightZone` colliders are expected to be Box Colliders
or Sphere Colliders. These shapes support efficient exact containment testing.
Mesh Collider-based zones are not supported in MVP due to the cost of exact
containment testing on arbitrary meshes.

**Rule ZM-3.** If the performance cost of per-query zone iteration becomes a
bottleneck at scale (see Acceptance Criteria AC-LSS-09), the implementation may
switch to a Physics.OverlapPoint approach against the `LightZone` physics layer.
This is a programmer implementation decision — the GDD does not prescribe the
internal containment algorithm, only the required output behavior and performance
budget.

---

### Section 5: Integration Contract with Detection System

**Rule IC-1.** The Detection System calls `GetLightLevelAtPosition` using a
**synchronous poll model**. This resolves the blocking contract flagged in the
Detection System GDD.

**Rationale for synchronous poll (MVP):** Light levels do not change at runtime
in MVP (zones are static). A push-event model would add complexity
(subscriber registration, event dispatch, dirty-flag management) with zero
gameplay benefit — the light level at any position is always the same value every
frame. The synchronous poll costs at most one method call + a brief zone-list
iteration per seeker per FixedUpdate. At MVP scale (1 seeker type, ~10 zones per
room), this is negligible. Push-events become valuable only when interactive
lights arrive (Vertical Slice), because they allow the Detection System and other
dependents to react to a light state change without polling every frame. Defer
push-events to Vertical Slice when `LightZone.isInteractive = true` requires
notification.

**Rule IC-2.** The Detection System passes the **player's world position** as the
query argument, not the seeker's position. The light modifier in Formula F2 of
the Detection System GDD represents light exposure on the observed target (the
player). A seeker in a lit corridor looking at a player in a dark alcove applies
the player's `light_level = 0.0`, yielding `light_factor = 0.0` and zero visual
suspicion delta. This is physically and gameplay-logically correct: the seeker
cannot see what the light does not illuminate.

**Rule IC-3.** The interface contract between the LSS and the Detection System is:

```csharp
// Called by: Detection System, once per seeker per FixedUpdate
// Argument: player's current world position (center of mass + player_light_sample_height offset)
// Returns: normalized ambient light level [0.0, 1.0]
//   0.0 = full darkness (visual suspicion delta = 0 regardless of LoS)
//   1.0 = fully lit (light factor has no dampening effect)
// Threading: main thread only (Unity physics callbacks)
// Allocation: none (no heap allocation per call)
float lightLevel = LightSourceSystem.Instance.GetLightLevelAtPosition(playerPosition);
```

**Rule IC-4.** The `light_factor` applied in Detection System Formula F2 is:
```
light_factor = light_level ^ light_sensitivity_exponent
```
Where `light_sensitivity_exponent` is a tuning knob owned by the Detection System
(default 0.8). The LSS does not own or apply this exponent — it returns the raw
`light_level` float. The Detection System applies the curve.

**Vertical Slice addition:** When interactive lights are implemented, the LSS will
emit a C# event on any zone state change:
```csharp
// Vertical Slice only — do not implement in MVP
public static event Action<LightZone, float> OnLightStateChanged;
// Fired when: a LightZone's ambientLightLevel changes (torch destroyed,
// lantern picked up, flame extinguished)
// Arguments: the affected zone, the new light level
```
The Detection System subscribes to this event to invalidate any cached light
level values rather than polling every frame. For MVP, no caching and no event
exist — the poll is the full interaction.

---

### Section 6: Overlap Resolution

**Rule OR-1.** When the queried position is inside multiple overlapping `LightZone`
volumes simultaneously, `GetLightLevelAtPosition` returns the **maximum**
`ambientLightLevel` among all overlapping zones (brightest-wins).

**Rationale for brightest-wins:** The alternative — darkest-wins (minimum value)
— would mean an incorrectly authored or accidentally overlapping dark zone could
create unexpected patches of near-invisible detection cover in otherwise lit areas.
Brightest-wins is the fail-visible direction: a mis-authored overlap produces
more light, which the player perceives as normal and the level designer will see
during QA as "this area is unexpectedly bright." Unexpected brightness is a level
design error that is immediately obvious to a human eye. Unexpected darkness is an
invisible exploit that only surfaces during adversarial play. The game's detection
legibility depends on players trusting that a lit area is lit and a dark area is
dark — brightest-wins preserves that trust in the failure case.

**Rule OR-2.** When the queried position is inside zero `LightZone` volumes, the
system returns `lightSourceData.defaultAmbientLightLevel` (the global fallback).
This fallback represents the baseline ambiance of areas that level designers have
not explicitly zoned — the ambient light of dungeon ceiling torches, moon through
windows, etc. Default value: 0.5 (dim but not dark). See Tuning Knobs.

**Rule OR-3.** If two overlapping zones have identical `ambientLightLevel` values,
either may be returned (the result is the same). The iteration order of the zone
cache is not specified as a design requirement — only that the maximum is returned.

---

### Section 7: Per-Entity Position Sampling

**Rule PS-1.** The Detection System queries `GetLightLevelAtPosition` once per
seeker per FixedUpdate, using the **player's world position** as the argument.
This models the light falling on the player as observed from any direction.

**Rule PS-2.** The query uses the player's `transform.position` (center of mass,
at foot level) plus an upward offset of `player_light_sample_height` (tuning knob,
default 0.9 m) to approximate the player's center-of-body height. This prevents
the sample point from being inside floor geometry when the player is standing on
a zone boundary.

**Rule PS-3.** For MVP, the seeker's own light level is not queried. The seeker's
visibility in the scene is a rendering concern, not a gameplay concern. A future
seeker variant that reacts to being lit (becomes more alert when illuminated) would
require a seeker-position query, designed at that time.

**Rule PS-4.** If the Hiding Spot System adds a "peek" mechanic where the player
partially exposes themselves from cover, the peeking position (the peek anchor
point on the hiding spot) is used as the query position instead of the player's
center of mass. The Hiding Spot System is responsible for providing the correct
query position to the Detection System in peek state. The LSS has no concept of
peek — it receives a position and returns a float. (See Open Questions OQ-LSS-03.)

---

### Section 8: Visual and Audio Requirements

This section specifies the player-facing feedback that makes the LSS's output
legible to the player. Darkness as a resource is only usable if the player can
read their current light exposure level. This is not optional — it is a Pillar 3
(Legible Jeopardy) hard requirement.

**Visual Feedback — HUD Light Indicator (Required for MVP):**
The HUD must display a discrete light exposure indicator. Recommended
implementation: a small icon that shifts between three visual states based on
the current `light_level` returned for the player's position:

| Light Level Range | Icon State | Player Reading |
|---|---|---|
| 0.0 – 0.25 | Dark (eye closed / extinguished candle) | "I am in deep shadow — visual detection at greatly reduced rate" |
| 0.26 – 0.65 | Dim (half-open eye / dim flame) | "I am in partial shadow — detection is reduced but not zero" |
| 0.66 – 1.0 | Lit (open eye / bright flame) | "I am in full light — detection rate is normal or near-normal" |

The icon updates every frame (not just FixedUpdate) so the player sees transitions
immediately when crossing zone boundaries. The HUD reads the light level from the
same `LightSourceSystem.Instance.GetLightLevelAtPosition(playerPosition)` call,
not from the Detection System.

**Visual Feedback — Character Model Darkening (Recommended for MVP):**
The player character's renderer(s) apply a real-time darkening effect proportional
to `1.0 - light_level`. At `light_level = 0.0`, the character model is rendered
at minimum brightness (near-black silhouette). At `light_level = 1.0`, the model
is rendered normally. Implementation approach: a shader parameter on the player
material, updated each frame by a `PlayerLightVisualizer` component. This is
an authoring concern for the environment/character artist — the GDD specifies the
requirement and the data source, not the shader implementation.

The character darkening reinforces the HUD indicator with a world-space cue. A
player who looks at their own model and sees it nearly invisible can confidently
infer "I am well-hidden." This is the same multi-channel feedback loop used in
Mark of the Ninja (on-screen shadow meter) and Thief (light gem) — two reference
designs for legible stealth exposure.

**Audio Feedback — Zone Entry/Exit (Optional for MVP, Recommended):**
A subtle ambient audio shift when the player crosses into a significantly darker
zone (threshold: `light_level` drops by >= 0.3 in one zone transition). A faint
muffled ambient sound or reverb shift reinforces the spatial transition. This is
a request to the Audio Designer — not a hard LSS requirement, but a UX enhancement
that materially aids discovery of the light-level mechanic on first encounter.

---

## Formulas

### F-LSS-1: Zone Membership Test

For a given query position `p` and zone `z`:

```
is_inside(p, z) = z.Collider.bounds.Contains(p)
                  AND (z.Collider is BoxCollider
                       OR z.Collider.ClosestPoint(p) == p)
```

For MVP, the AABB test (`bounds.Contains`) is the primary fast path. The
`ClosestPoint` equality test is used as an exact fallback for non-box shapes
(Sphere Colliders). The ClosestPoint of a point inside a collider is the point
itself.

**Variable Definitions:**

| Variable | Type | Range | Source | Description |
|---|---|---|---|---|
| `p` | Vector3 | world space | caller | World position of the queried entity (player center + height offset) |
| `z` | LightZone | — | zone cache | A registered LightZone component |
| `z.Collider.bounds` | Bounds | — | Unity | Axis-aligned bounding box of the zone collider |
| `z.Collider.ClosestPoint(p)` | Vector3 | world space | Unity | Closest point on or inside the collider to position p; equals p when p is inside |

---

### F-LSS-2: Overlap Resolution (Brightest-Wins)

```
light_level_raw = MAX { z.ambientLightLevel : is_inside(p, z) == true }
                  for all z in zone_cache

// If no zone contains p:
light_level_raw = lightSourceData.defaultAmbientLightLevel

// Final return value:
return clamp(light_level_raw, 0.0, 1.0)
```

**Variable Definitions:**

| Variable | Type | Range | Source | Description |
|---|---|---|---|---|
| `zone_cache` | List\<LightZone\> | — | LightSourceSystem | All registered LightZone components in the current scene |
| `z.ambientLightLevel` | float | 0.0–1.0 | LightZone serialized field | Authored light level for this zone |
| `lightSourceData.defaultAmbientLightLevel` | float | 0.0–1.0 | LightSourceData ScriptableObject | Global fallback when position is outside all zones |
| `light_level_raw` | float | 0.0–1.0 | computed | Maximum zone value, or fallback |

**Example — player inside two overlapping zones:**
- Zone A: `ambientLightLevel = 0.8` (lit room center)
- Zone B: `ambientLightLevel = 0.2` (shadow cast by column, overlapping Zone A)
- Result: `MAX(0.8, 0.2) = 0.8`
- Player is in the lit portion — the column's shadow zone cannot override the
  room's ambient lighting.

**Example — player outside all zones:**
- `defaultAmbientLightLevel = 0.5`
- Result: `0.5`
- Player is in an unzoned area; dim ambient dungeon light applies.

**Example — player in a single dark zone:**
- Zone C: `ambientLightLevel = 0.0` (deep shadow under a staircase)
- Result: `0.0`
- At `light_level = 0.0` in Detection System F2: `light_factor = 0.0^0.8 = 0.0`.
  Visual suspicion delta = 0 regardless of LoS. Full visual immunity.

---

### F-LSS-3: Detection System Integration

The Detection System uses the LSS output as:

```
light_level       = LightSourceSystem.GetLightLevelAtPosition(player_world_position)
light_factor      = light_level ^ light_sensitivity_exponent
visual_delta      = base_detection_rate
                  * distance_factor
                  * angle_factor
                  * light_factor           // LSS output applied here
                  * hide_modifier
                  * crouch_modifier
                  * state_multiplier
```

**Variable Definitions (LSS-owned):**

| Variable | Type | Range | Source | Description |
|---|---|---|---|---|
| `light_level` | float | 0.0–1.0 | LSS GetLightLevelAtPosition | Normalized ambient light at player position |

**Variable Definitions (Detection System-owned, shown for context):**

| Variable | Type | Range | Source | Description |
|---|---|---|---|---|
| `light_factor` | float | 0.0–1.0 | computed by Detection System | Nonlinear curve of light_level via exponent |
| `light_sensitivity_exponent` | float | 0.5–2.0 | Detection System tuning knob | Shape of the light curve; default 0.8 |

**Curve behavior at default exponent = 0.8:**

| `light_level` (from LSS) | `light_factor` (= 0.8 power) | Visual Detection Rate |
|---|---|---|
| 0.0 | 0.000 | 0% (full darkness = visual immunity) |
| 0.1 | 0.158 | 15.8% |
| 0.2 | 0.276 | 27.6% |
| 0.3 | 0.381 | 38.1% |
| 0.5 | 0.574 | 57.4% |
| 0.65 | 0.709 | 70.9% |
| 0.8 | 0.837 | 83.7% |
| 1.0 | 1.000 | 100% (fully lit) |

**Design note on exponent = 0.8 (default):** An exponent below 1.0 produces a
concave-up curve: for intermediate `light_level` values, `light_factor` is
*higher* than the equivalent linear value. At `light_level = 0.3`, a linear
model (exponent = 1.0) gives 30% detection rate; the default exponent of 0.8
gives `0.3^0.8 = 38.1%` — detection is slightly higher than a naive reading of
"30% lit" implies. This is intentional: dim corridors are not truly safe. Only
near-total darkness (light_level ≤ ~0.2) provides meaningfully reduced detection.
The curve discourages players from settling for any dim shadow and rewards those
who find genuinely dark zones — reinforcing Pillar 2 (Silence Is a Tool) by
making darkness a resource worth seeking rather than a marginal advantage.

To make partial darkness *more* protective than linear instead (concave-down
curve), raise `light_sensitivity_exponent` above 1.0. At exponent = 2.0,
`0.3^2.0 = 0.09` — a player at 30% illumination is detected at only 9% of max
rate, making any dim shadow feel strongly safe. Do not change from the default
until playtesting demonstrates the current feel is wrong.

---

## Edge Cases

| ID | Scenario | Expected Behavior | Rationale |
|---|---|---|---|
| EC-LSS-01 | **Player on the exact boundary of a zone** | The containment test may produce floating-point inconsistencies at boundary positions. Treat boundary as inclusive (inside the zone). If the player oscillates between inside/outside on alternating FixedUpdate ticks, the HUD indicator uses the `hud_lightlevel_hysteresis_band` (default 0.05, see Tuning Knobs) — it only changes display state when `light_level` has changed by >= `hud_lightlevel_hysteresis_band` from the last displayed value. | Flickering is a UX concern, not a gameplay correctness concern. The detection formula handles sub-0.05 changes gracefully since `light_factor = light_level^0.8` is continuous. |
| EC-LSS-02 | **Multiple overlapping zones, all at 0.0** | Returns 0.0. MAX(0.0, 0.0, ...) = 0.0. Player has full visual immunity. No edge case — brightest-wins with all-zero values behaves correctly. | Trivial case of the overlap formula. |
| EC-LSS-03 | **LightSourceSystem is absent from the scene** | Detection System guards against null instance and returns fallback `light_level = 1.0` (fail-lit). No NullReferenceException thrown. Detection continues normally — seekers see the player in full light, making the missing component obvious during QA. | Fail-lit is the fail-visible direction. If LSS is missing, the game is harder but not broken. The QA signal is clear: "detection in dark areas is unexpectedly high." |
| EC-LSS-04 | **Zone with `ambientLightLevel = 0.0` with unobstructed LoS from seeker** | `GetLightLevelAtPosition` returns 0.0. Detection System computes `light_factor = 0.0^0.8 = 0.0`. `visual_suspicion_delta = 0.0`. Seeker does not accumulate suspicion from sight regardless of LoS geometry. This is correct and intentional — a zero-light zone grants complete visual immunity. Level designers placing `light_level = 0.0` zones in a seeker's patrol path are granting the player a guaranteed safe zone. | Pillar 1 (The Room Has Rules): the player must be able to trust that a zero-light zone means zero visual detection. |
| EC-LSS-05 | **Seeker and player in different light zones — seeker lit, player dark** | `GetLightLevelAtPosition` is always called with the player's position. The seeker's own light level is not queried. A seeker in a lit corridor looking at a player in a `light_level = 0.0` alcove returns 0.0 and applies zero visual detection rate. | See Rule PS-1. Querying player position models light-on-the-observed, which is the correct physical and gameplay model for stealth visibility. |
| EC-LSS-06 | **Zone count in a scene is very large (100+ zones)** | Zone-list iteration scales linearly with zone count. At 1 seeker, 100 zones: 100 AABB containment tests per FixedUpdate. AABB `bounds.Contains` is cheap arithmetic — this takes nanoseconds. If zone count reaches 500+, profile first. The implementation may switch to a spatial hash or Physics.OverlapPoint without changing the behavioral specification. | Performance concern, not a design concern. |
| EC-LSS-07 | **Player is inside a hiding spot** | `GetLightLevelAtPosition` uses the player's world position. The LSS has no concept of "the player is hidden." The Hiding Spot System's visual detection bypass (Detection System Rule V-6, `hide_modifier = 0.0`) already suppresses visual detection entirely while hidden — the LSS does not need to special-case this. The two modifiers are independent and non-conflicting. | The Detection System's hide check makes `visual_suspicion_delta = 0` regardless of `light_factor`. No interaction conflict. |
| EC-LSS-08 | **`defaultAmbientLightLevel` is set to 0.0** | Any position outside all zones returns 0.0. Valid authoring choice for a pitch-dark dungeon design. Level designers must be aware: unzoned areas are completely safe from visual detection. Not a system error; a level design authoring decision. | Document as a level design warning: test all unzoned areas after setting defaultAmbientLightLevel to 0.0. |
| EC-LSS-09 | **The LSS is queried from Update (HUD) and FixedUpdate (Detection System) simultaneously** | `GetLightLevelAtPosition` is stateless and may be called from any Unity event function. MVP zones are static so both callers receive identical results for the same position. No threading concern. HUD polling from Update is valid and gives smoother indicator transitions than FixedUpdate-rate polling. | Stateless query design means call-site timing is irrelevant for correctness. |

---

## Dependencies

### What the LSS Depends On

| System | Dependency Type | Data Required | Direction | Notes |
|---|---|---|---|---|
| `LightSourceData` ScriptableObject | Configuration source | `defaultAmbientLightLevel` float (global fallback) | LSS reads | Stored at `Assets/_Project/Scripts/Data/LightSourceData.asset`. Owns all LSS tuning values. |
| Unity Physics (Collider API) | Runtime | `Collider.bounds.Contains(Vector3)`, `Collider.ClosestPoint(Vector3)` | LSS reads | Used for zone containment testing. No Physics scene query in MVP — pure Collider API math. |
| Level designers | Authoring dependency | Correct placement and sizing of `LightZone` trigger volumes | LSS reads at scene load | LSS assumes zones are correctly authored. Misaligned zones are a level design error. |
| Unity Scene Lifecycle | Runtime | `Awake` for zone cache initialization | LSS depends on | Zone cache is built in `Awake`. Zones added after `Awake` are not registered in MVP. |

### What Depends on the LSS

| System | Dependency Type | Data Consumed | Direction | Notes |
|---|---|---|---|---|
| Detection System | Primary consumer | `GetLightLevelAtPosition(Vector3) → float`, once per seeker per FixedUpdate | LSS is queried by | The only system that uses LSS output for gameplay logic in MVP. Detection System GDD Rule V-4 and Formula F2 define how `light_level` is used. |
| HUD (LightIndicatorUI) | Secondary consumer | `GetLightLevelAtPosition(Vector3) → float`, once per Update for the player's position | LSS is queried by | HUD reads directly to ensure Update-rate responsiveness. Does not go through Detection System. |
| PlayerLightVisualizer component | Tertiary consumer | `GetLightLevelAtPosition(Vector3) → float`, once per Update | LSS is queried by | Applies character model darkening. Thin visual-only component; no gameplay logic. |
| Environmental Interaction System | Future consumer (Vertical Slice) | Will write to `LightZone.ambientLightLevel` when interactive lights are triggered | Will write to LSS | Not implemented in MVP. When breakable/extinguishable lights are added, Environmental Interaction triggers zone state changes that LSS propagates via push-event. |

### Interface Contract Summary

**LSS provides (MVP):**
```csharp
public float GetLightLevelAtPosition(Vector3 worldPosition);
// Returns: float [0.0, 1.0]
// Allocation: none
// Thread: main thread only

// Vertical Slice additions (not in MVP):
public static event Action<LightZone, float> OnLightStateChanged;
public void RegisterZone(LightZone zone);
public void UnregisterZone(LightZone zone);
```

**LSS requires from callers:**
- `worldPosition` must be a valid Unity world-space coordinate
- Callers must null-check `LightSourceSystem.Instance` before calling

---

## Tuning Knobs

All values live in the `LightSourceData` ScriptableObject
(`Assets/_Project/Scripts/Data/LightSourceData.asset`) or in the `LightZone`
component's serialized fields. No values may be hardcoded.

| Parameter | Asset | Default | Safe Range | Effect of Increase | Effect of Decrease |
|---|---|---|---|---|---|
| `defaultAmbientLightLevel` | LightSourceData | 0.5 | 0.0–0.8 | Unzoned areas brighter; less darkness advantage in hallways | Unzoned areas darker; risk of unintended zero-detection corridors |
| `LightZone.ambientLightLevel` (per zone) | LightZone component | 1.0 | 0.0–1.0 | Zone is brighter; less visual protection | Zone is darker; at 0.0 = full visual immunity |
| `player_light_sample_height` | LightSourceData | 0.9 m | 0.5–1.5 m | Sample point higher; zones with low ceilings sampled differently | Sample point lower; more likely to clip floor-level zone boundaries |
| `hud_lightlevel_hysteresis_band` | LightSourceData | 0.05 | 0.01–0.15 | HUD indicator more stable at zone boundaries | More responsive but may flicker at boundaries |

**Tuning priority for first playtest:** `defaultAmbientLightLevel` → per-zone
`ambientLightLevel` values in Level_1 → `player_light_sample_height` if players
report unexpected light readings near floor-level zone boundaries.

**Level designer guidance:** The most impactful tuning in the LSS is not in the
ScriptableObject — it is in the placement and sizing of individual `LightZone`
volumes in each scene. Zone boundaries are the primary tool for sculpting which
positions offer which degrees of visual protection. A zone boundary is a game rule,
not an aesthetic guideline.

---

## Acceptance Criteria

All criteria are verifiable in Unity Editor Play Mode or Edit Mode. "Default values"
refers to the Tuning Knobs table above.

- [ ] **AC-LSS-01** Place a single `LightZone` with `ambientLightLevel = 0.3`. Call `GetLightLevelAtPosition` from a position clearly inside the zone's collider bounds. **Pass**: returns 0.3 (±0.001). **Fail**: returns any other value.

- [ ] **AC-LSS-02** Place two overlapping `LightZone` volumes: Zone A = 0.7, Zone B = 0.2. Call `GetLightLevelAtPosition` from a position inside both zones. **Pass**: returns 0.7 (the maximum). **Fail**: returns 0.2 (minimum), 0.45 (average), or any other value.

- [ ] **AC-LSS-03** Call `GetLightLevelAtPosition` from a position outside all `LightZone` volumes. **Pass**: returns `lightSourceData.defaultAmbientLightLevel` (0.5 at default). **Fail**: returns 0.0, 1.0, or any value other than the configured default.

- [ ] **AC-LSS-04** Place a `LightZone` with `ambientLightLevel = 0.0`. Position player inside the zone. Confirm `visual_suspicion_delta = 0` despite confirmed LoS. **Pass**: suspicion does not increase while player is in the 0.0 zone and seeker has LoS. **Fail**: suspicion increases.

- [ ] **AC-LSS-05** Position player at the boundary of a `LightZone`. Move across the boundary 10 times rapidly. **Pass**: HUD indicator does not flicker on every frame; transitions are stable (hysteresis applied). **Fail**: indicator flickers on consecutive frames at boundary.

- [ ] **AC-LSS-06** Remove `LightSourceSystem` from the test scene. Detection System attempts to call `GetLightLevelAtPosition`. **Pass**: Detection System returns fallback `light_level = 1.0`; no NullReferenceException; no crash. **Fail**: exception or crash; or detection rate is incorrectly 0.

- [ ] **AC-LSS-07** Call `GetLightLevelAtPosition` from `Update` (HUD) and from `FixedUpdate` (Detection System) simultaneously. **Pass**: both callers receive identical results for the same position; no stale data. **Fail**: results differ between callers for the same position on the same frame.

- [ ] **AC-LSS-08** Code review: confirm `ambientLightLevel` is `[SerializeField] [Range(0f, 1f)] float` on `LightZone`, and `defaultAmbientLightLevel` is in `LightSourceData` ScriptableObject. **Pass**: no hardcoded light level float literals in LSS code. **Fail**: any hardcoded value found.

- [ ] **AC-LSS-09** Performance test: 50 `LightZone` volumes, 3 simultaneous callers (Detection System, HUD, PlayerLightVisualizer). **Pass**: total LSS evaluation time < 0.1ms per FixedUpdate (Unity Profiler, Development Build). **Fail**: >= 0.1ms.

- [ ] **AC-LSS-10** Player inside a hiding spot inside a `LightZone` with `ambientLightLevel = 1.0`. **Pass**: suspicion does not increase while `PlayerHiding.IsHidden == true`, even with `light_level = 1.0`. **Fail**: suspicion increases while fully hidden.

- [ ] **AC-LSS-11** Player walks from a `light_level = 1.0` zone into a `light_level = 0.0` zone. **Pass**: HUD indicator transitions to "dark" state within 1 Update frame of crossing the zone boundary; character model visually darkens. **Fail**: HUD indicator does not update or updates more than 2 frames after zone crossing.

- [ ] **AC-LSS-12** Experiential criterion: in Level_1, playtest with 3 participants who have never played UNSEEN. Without explanation, can participants identify which areas reduce detection risk based on visual cues? **Pass**: >= 2 of 3 participants correctly move toward darker areas when avoiding a seeker within 5 minutes of play. **Fail**: participants cannot identify dark zones as safety resources without prompting.

---

## Open Questions

| ID | Question | Owner | Resolution Path | Priority |
|---|---|---|---|---|
| OQ-LSS-01 | Should the Unity Editor provide a visual gizmo overlay showing zone boundaries and their `ambientLightLevel` values during scene editing? Zone boundaries are invisible in the scene view, making it difficult to verify alignment between visual lighting and zone placement. A gizmo drawing collider bounds colored by light level (green = dark, red = lit) would substantially reduce authoring errors. | Lead Programmer | Implement as Editor-only `OnDrawGizmos` override on `LightZone`. Low implementation cost, high level design benefit. Recommend including in Sprint 1 tooling. | High |
| OQ-LSS-02 | What is the `defaultAmbientLightLevel` for Level_1 specifically? 0.5 is the global default, but the actual feel of unzoned areas depends on Level_1's layout and lighting. If most of Level_1 is explicitly zoned, the default may need to be 0.6–0.7 to match visual lighting in transitional areas. | Level Designer | Test Level_1 with zones authored on key areas. Identify unzoned positions. Measure whether 0.5 matches the visual impression. Adjust per-scene if needed via a per-scene override field on the LightSourceSystem component. | Medium |
| OQ-LSS-03 | Does the peeking mechanic need a position-aware light query? When peeking, the player's center of mass is inside the hiding spot (which may be dark), but the peeked body part is exposed to a lit room. Should the peek sample position be the peek anchor (exposed position) rather than the player's center? | Game Designer + Systems Designer | Prototype peeking in Level_1 with a hiding spot spanning a light zone boundary. If peeking from a dark spot into a lit room returns `light_level = 0.0` (sample at hidden position), the light modifier undercounts detection risk. Fix: Hiding Spot System provides the peek anchor position to the Detection System as the LSS query argument. Resolve before Hiding Spot System GDD is written. | High |
| OQ-LSS-04 | Should the LSS support per-seeker light-level queries for future seeker variants (e.g., a "shadow seeker" more effective in darkness)? | Game Designer | Defer to Seeker Variant design phase (Vertical Slice). The current `GetLightLevelAtPosition(Vector3)` API already supports any position query — no LSS change required. Document in the Seeker AI GDD as a note that the LSS API can be queried for any world position. | Low |
| OQ-LSS-05 | How should carriable light sources (Vertical Slice) interact with the detection model? A player carrying a lantern creates a moving zone raising their own `light_level` — trading darkness safety for visibility in dark areas. The UI must clearly distinguish between ambient zone lighting and the player's own carried light. | Game Designer | Defer to Vertical Slice scope. The detection integration is identical (LSS returns a float; Detection System applies it). The UI distinction is a HUD design problem, not an LSS problem. | Low |
