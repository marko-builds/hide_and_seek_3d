# HUD

> **Status**: Approved
> **Author**: game-designer + ux-designer
> **Last Updated**: 2026-03-06
> **Implements Pillar**: Legible Jeopardy (Pillar 3)

---

## 1. Overview

The HUD is UNSEEN's minimal on-screen information layer: a set of persistent,
peripheral screen elements that let the player make informed stealth decisions
without pulling attention away from the world. It is not a replacement for
world-space legibility. Pillar 3 (Legible Jeopardy) mandates that seeker AI
states are communicated through world-space cues — the seeker's posture,
vocalization, movement speed, and FOV cone on the floor — not through HUD text.
The HUD supplements those world cues with two categories of abstract information
the player cannot read from the 3D environment alone: (1) the player's own
intrinsic noise level and light exposure, which are invisible properties of the
player character, and (2) discrete counters (objectives remaining, current phase)
that are hard to infer from the world without stopping to count. Every element
must be glanceable in 0.5 seconds or less from a peripheral gaze while the player
watches a seeker through a doorway. Anything that demands focus-shift away from
the world is a design failure. In MVP scope, the HUD is composed of seven
elements: Noise Indicator, Suspicion Meter, Light Exposure Indicator, Objective
Counter, Phase 2 Indicator, Chase Vignette, and Gadget Slot UI (stub). A separate
CAUGHT card (owned by `CheckpointManager` via `RespawnUI`) appears during the
respawn sequence and is not part of this system.

---

## 2. Player Fantasy

The player glancing at the HUD should feel like a burglar checking a set of
instruments mounted on their own wrist — not like a player reading a status
screen.

The ideal emotional experience of the HUD:

**Informed calm.** When the player is still and concealed, the HUD indicators
are at floor. The noise indicator rests at zero. The light indicator is dim. The
suspicion meter is absent or drained. The HUD communicates "you are safe right
now" without demanding attention. The player's eyes stay on the world, scanning
for the seeker. The HUD exists in peripheral awareness, not in focus.

**Productive anxiety.** When the player is moving — especially in a new surface,
at sprint, or near a seeker's attention zone — the noise indicator pulses. The
light indicator brightens when they step into a lit corridor. The suspicion meter
fills slightly as they cross a doorway. Each element rises in a way the player
can anticipate because they understand the system's rules. Anxiety is productive
because it is legible: the player is not afraid they are about to be caught, they
are reading exactly how close they are to the threshold and making a decision.

**Mastery through numbers they don't have to read.** An experienced player does
not read the noise indicator numerically. They feel it in their peripheral vision
as a shape — "small and quiet" or "tall and dangerous." This is the correct state.
The HUD is successful when it has been internalized to the point where the player
never consciously reads it, only reacts to its gestalt. Individual elements
should be designed for this peripheral legibility: distinct shapes, positions, and
color-state transitions that communicate through visual weight, not fine detail.

**Target MDA Aesthetics:** Challenge (the player can calibrate risk because they
can read their own exposure), Discovery (learning how the indicators map to
consequences), Expression (experienced players use indicator knowledge to push
limits they understand).

**SDT anchor:** Competence — feedback clarity is a prerequisite for felt skill
growth. The player cannot feel competent if they cannot read their own state. The
HUD is the Competence scaffold: it provides the readable feedback that allows
skill to be expressed and recognized.

---

## 3. Detailed Design

### 3.1 HUD Layout

ASCII layout sketch of the 16:9 MVP HUD. All elements anchor to screen edges
and use normalized screen coordinates. The center zone (roughly 40% of screen
width, 50% of screen height) is reserved for gameplay — no persistent HUD
element occupies it.

```
+------------------------------------------------------------------+
|  [7] GADGET SLOTS                     [5] PHASE 2 INDICATOR      |
|  [ ] [ ]                              "ALARM"  (brief, then gone) |
|                                                                    |
|                                                                    |
|                        (GAMEPLAY ZONE)                            |
|                                                                    |
|                                                                    |
|  [1] NOISE        [3] LIGHT           [4] OBJECTIVES   [2] SUSP.  |
|  |||||             O                   2 / 3            ||||||||| |
+------------------------------------------------------------------+

[6] CHASE VIGNETTE: screen-edge red pulse (all four edges, not center)
```

Element positions (normalized screen space, Unity Canvas with Screen Space Overlay):

| Element | Anchor | Pivot | Position Notes |
|---------|--------|-------|----------------|
| [1] Noise Indicator | Bottom-Left | (0, 0) | 24px margin from left and bottom edges |
| [2] Suspicion Meter | Bottom-Right | (1, 0) | 24px margin from right and bottom edges |
| [3] Light Exposure Indicator | Bottom-Center-Left | (0.5, 0) | Centered horizontally between Noise and Objectives; 24px bottom margin |
| [4] Objective Counter | Bottom-Center-Right | (0.5, 0) | Right of Light Indicator, left of Suspicion; 24px bottom |
| [5] Phase 2 Indicator | Top-Center | (0.5, 1) | 40px from top; slides in from top edge, slides out |
| [6] Chase Vignette | Full-screen overlay | (0.5, 0.5) | Screen-edge only; center is fully transparent |
| [7] Gadget Slots | Top-Left | (0, 1) | 24px from left and top edges |

All HUD elements use a Canvas CanvasGroup component. `HUDManager` controls the
master alpha for hide/show transitions during the respawn sequence.

---

### 3.2 Element 1: Noise Indicator (NoiseIndicatorUI)

**What it shows:** The player's current intrinsic noise level — how loud they are
generating sound at this moment. This is not how detected they are by any seeker.
It is the raw loudness of the player's actions.

**Event source:** `PlayerNoiseEmitter.OnPlayerNoiseLevelChanged` (static event,
`Action<float>`). Value range: 0.0 (silent) to 1.0 (maximum).

**Visual description:** A vertical bar meter, five segments tall, positioned at
bottom-left. Each segment is a thin rectangle (approximately 4px wide, 12px tall,
2px gap between segments). The fill progresses bottom-to-top. The bar reads like a
VU meter on audio equipment — the player who has used any audio software
immediately recognizes the metaphor.

Color scheme per segment level (bottom to top):
- Segments 1–2: White (low noise — walk on carpet, stationary)
- Segments 3–4: Amber (medium noise — walk on stone, crouch on wood)
- Segment 5: Red (high noise — sprint, landing spike)

The color of each segment is fixed by its position in the stack, not by the
current fill level. A fill that reaches segment 4 will show two white and two
amber segments lit. This gives the player a fixed color reference for "danger
zone" without requiring them to read numbers.

**Silent state:** When `_currentNoiseLevel < 0.05` (the `hudDisplayThreshold`),
the bar renders all five segments at 8% alpha (dimmed outline only). The bar does
not disappear — its constant presence reminds the player it is monitoring. An
empty bar communicates "you are silent right now," which is itself useful
information. The transition from dimmed outline to lit segments uses a
`lerpToVisibleDuration` (0.08 s) so the bar does not flicker.

**Behavior:** The `NoiseIndicatorUI` receives the float from the event, which is
already the max-of-current-and-incoming level computed by PNE (see PNE GDD
Section 3.7). The HUD applies a visual lerp to the current display fill value
toward the incoming value at `noiseBarLerpRate` (12.0 per second). This prevents
per-frame jerking when rapid footstep events arrive at 2–6 Hz. On sudden spikes
(sprint landing, interact), the incoming value may be larger than the current
display value — the display snaps upward immediately (no downward lerp on rise)
and then decays at the lerp rate. Visual decay is driven by the display lerp, not
by a separate timer; the PNE's own `_currentNoiseLevel` decay drives the target
value the HUD is lerping toward.

**Upward snap, downward lerp rule:** When `incomingLevel > currentDisplayLevel`:
snap to `incomingLevel` immediately (no lerp). When `incomingLevel < currentDisplayLevel`:
apply `noiseBarLerpRate`. This mirrors how real VU meters behave (fast attack,
slow release) and matches player intuition: the danger registers instantly, the
recovery is gradual.

**Performance:** `NoiseIndicatorUI` does not run in `Update`. It updates only on
`OnPlayerNoiseLevelChanged` events (event-driven, per project convention). The
visual lerp during decay runs via a `Coroutine` started when the event fires and
stopped when `currentDisplayLevel <= 0.05`. No per-frame polling.

---

### 3.3 Element 2: Suspicion Meter (SuspicionMeterUI)

**What it shows:** The highest current suspicion value across all active seekers,
and the seeker attention state associated with that highest-suspicion seeker.
This is the player's danger barometer for the entire room.

**Event source:** `DetectionSystem.OnSuspicionChanged` (static event,
`Action<float, SeekState>`). Float: 0.0–100.0. SeekState: the current state of
the highest-suspicion seeker.

**Visual description:** A horizontal arc meter, positioned at the bottom-right
corner. The arc spans approximately 120 degrees, emanating from the bottom-right
corner inward (like a quarter-circle sweep). At zero fill the arc is empty. At
full fill the arc extends to its maximum extent.

The arc is divided into color bands corresponding to state thresholds:
- 0–24: Invisible (meter hidden, see Silent State below)
- 25–59: Amber fill (Alert band)
- 60–84: Orange fill (Searching band)
- 85–100: Red fill (Chase band)

The color transitions at the exact threshold values (25, 60, 85) with a
cross-fade over 0.15 s to prevent harsh color pops during rapid threshold
crossing.

**State markers:** At the three threshold values (25, 60, 85), thin notch marks
are baked into the meter's artwork — permanent tick marks that communicate "this
is a threshold." The notch at 85 is visually heavier (wider, slightly brighter)
because Chase is the most consequential transition. These notches give the player
a reference for how full the meter needs to be before the next state change —
answering "how close am I?" without displaying a number.

**Numeric value:** The meter does NOT display a numeric suspicion value. The fill
position relative to the notch marks communicates the same information more
intuitively and less analytically. Displaying "47.3" would invite players to
optimize numerically rather than spatially — this breaks the experiential intent.

**Silent state:** When all seekers are Unaware (suspicion < 25 on all), the entire
`SuspicionMeterUI` fades to 0% alpha over `meterFadeOutDuration` (0.6 s). The
meter occupies no visual weight when the player is safe. This is the most
important "silent state" behavior in the HUD: the suspicion meter's absence
actively communicates safety.

On entry to Alert (suspicion crosses 25), the meter fades in over
`meterFadeInDuration` (0.15 s) — fast enough to register immediately but not
jarring. The fast fade-in vs. slow fade-out asymmetry is intentional: danger
appears immediately, safety confirms gradually.

**Behavior on multi-seeker scenarios:** The meter always shows the single highest
suspicion value across all active seekers. It is not an average and not a sum. If
Seeker A is at suspicion 70 (Searching) and Seeker B is at suspicion 30 (Alert),
the meter shows 70 at the Searching color. The SeekState parameter on the event
reflects the state of the seeker at that highest suspicion value, used to drive
the color band selection. If the highest-suspicion seeker changes (e.g., Seeker B
spikes to 90 via a new LoS event), the new maximum publishes on the next
`OnSuspicionChanged` event.

**Behavior:** Display fill value lerps toward the incoming value at
`suspicionBarLerpRate` (8.0 per second) in both directions (rise and fall). The
suspicion meter lerps symmetrically because suspicion rising quickly is a genuine
warning; the player should feel the rise without it being instantaneous. (Unlike
the Noise Indicator, which has an asymmetric snap-up rule, the Suspicion Meter
benefits from a symmetric lerp because suspicion rising over 1–2 seconds is still
fast enough to communicate urgency while giving the player a reaction window.)

**Performance:** Event-driven. `SuspicionMeterUI` subscribes to
`DetectionSystem.OnSuspicionChanged` and starts/stops a decay Coroutine to handle
the visual lerp. No per-frame Update polling.

---

### 3.4 Element 3: Light Exposure Indicator

**What it shows:** The player's current light exposure level — how visible they
are to visual detection right now. A fully dark player (exposure = 0.0) is
visually immune to seekers (Detection System Rule V-4 + F2: `light_factor = 0`
zeroes visual suspicion delta). A fully lit player (exposure = 1.0) is maximally
visible.

**Event source:** `DetectionSystem.OnPlayerLightLevelChanged` (static event,
`Action<float>`). Value range: 0.0 (complete darkness) to 1.0 (full illumination).
This event is defined by this GDD as a contract requirement on the Detection
System. See Section 6 (Dependencies) for the full contract specification.

**Visual description:** A small circular icon, approximately 20px diameter,
positioned at the bottom-center-left. The icon uses a simple filled circle with a
radial gradient: at 0.0 exposure the circle is rendered as a dark outline only
(near-transparent fill, 8% alpha). As exposure increases, the fill brightens from
dark gray through amber to white. At 1.0 exposure the circle is fully white with
a soft glow bloom effect (Unity URP bloom via a small HDR emission value on the
material).

This icon is intentionally small — it is a glance-register indicator, not a
precision instrument. The player does not need to know their exact exposure; they
need to know "I am in shadow" vs. "I am in light." The shape (circle = light
source metaphor) is distinct from the Noise Indicator (vertical bar = volume
metaphor), preventing confusion between the two self-monitoring elements.

**Silent state:** At exposure < 0.05, the indicator renders as a dim outline only.
It does not disappear (like the Noise Indicator, its presence at floor confirms
monitoring is active). When exposure is at floor for > 3.0 seconds, the indicator
dims to 4% alpha — further receding to reduce clutter when the player has been
safely in shadow for an extended period. On any `OnPlayerLightLevelChanged` event
with value >= 0.05, the indicator snaps to its active brightness immediately.

**Behavior:** Snap-up on rise (same rule as Noise Indicator), lerp-down on decay
at `lightExposureLerpRate` (6.0 per second). Light changes in most environments
are relatively gradual (moving from shadow to light), so the lerp rate is slower
than the noise indicator (which needs to react to sharp percussive events). At
room-boundary light transitions — moving through a doorway — the light change may
be abrupt, and the snap-up rule ensures this registers immediately.

**Performance:** Event-driven only. No per-frame polling.

---

### 3.5 Element 4: Objective Counter

**What it shows:** How many objectives the player has collected out of the total
in this level. Format: `[CollectedCount] / [TotalCount]`. Examples: "0 / 1",
"1 / 3", "3 / 3".

**Event source:** `ObjectiveRegistry.OnObjectiveCollected` (static event,
`Action`). On event, query `ObjectiveRegistry.Instance.CollectedCount` and
`ObjectiveRegistry.Instance.TotalCount` to refresh the display. No polling
between events.

**Visual description:** A small horizontal group at bottom-center-right. The group
contains:
1. An icon sprite (`ObjectiveData.ObjectiveIconSprite`) rendered at 24x24 px.
   This is the HUD icon for the relic type in the current level — distinct from
   the world-space prompt icon used by the Player Interaction System.
2. A text label: "[CollectedCount] / [TotalCount]" in the project's HUD font,
   approximately 14px. Color: white. No background panel.

The icon is positioned to the left of the text. The icon and text are grouped
inside a single `HorizontalLayoutGroup` so they scale together.

**Visual feedback on collection:** When `OnObjectiveCollected` fires and the
display updates, a brief scale punch animation plays on the counter group: scale
from 1.0 to 1.2 over 0.08 s, then back to 1.0 over 0.12 s. This "micro-pop"
confirms the collection at the HUD level without a large effect. The animation
plays even if the player is not looking at the counter — it is designed to catch
peripheral attention, not demand focused attention.

**"All collected" state:** When `CollectedCount == TotalCount`, the counter text
turns amber for 1.0 second (matching the suspicion meter's Alert color), then
returns to white. This color beat communicates "objective phase complete" at the
HUD level, complementing the Phase 2 Indicator (Element 5) which fires
simultaneously from a different event subscription. Do not use green — green
implies safety, but collecting all objectives triggers Phase 2 escalation, which
is the opposite of safe.

**Persistent:** The counter is visible throughout Phase 1 and Phase 2. During
Phase 2, `CollectedCount == TotalCount` always, so the counter reads e.g. "3 / 3"
for the duration. This persistent display serves as a reminder that the objective
phase is complete and the player's goal is now the exit, without requiring separate
HUD real estate for a "now escape" reminder.

**Silent state:** The Objective Counter has no silent state — it is always visible
from level start to level exit. It is the player's primary progress indicator and
must always be readable. At a scale of 14px text and 24px icon, it occupies
minimal screen real estate.

**Performance:** Event-driven. `CollectedCount` and `TotalCount` are int properties
on a `SceneSingleton` — a synchronous O(1) read per event.

---

### 3.6 Element 5: Phase 2 Indicator

**What it shows:** A brief notification that Phase 2 (Escape phase) has begun.
The escalation is already communicated through world-space cues (seekers snap to
Alert/Searching, the exit light activates, the Phase 2 audio sting fires).
The HUD indicator is a backup for players whose gaze is away from the world at
the transition moment.

**Event source:** `LevelPhaseManager.OnPhaseChanged` (static event,
`Action<LevelPhase>`). The indicator activates only on
`LevelPhase.Phase2_Escape`. It does not react to `Phase1_Find`.

**Visual description:** A text notification anchored to the top-center of the
screen. Text content: `"ALARM"` (per Two-Phase Level Structure GDD Rule TPS-13:
`HUDManager.ShowPhaseNotification("ALARM")`). The text uses the project HUD font
at 24px, color red, with a subtle drop shadow. The notification slides in from the
top edge of the screen over `phaseIndicatorSlideInDuration` (0.15 s), holds for
`phaseIndicatorDisplayDuration` (2.5 s), then fades out over
`phaseIndicatorFadeOutDuration` (0.5 s).

The animation sequence:
1. Element starts at `anchoredPosition.y = +60` (above screen edge), alpha 0.
2. Slide in: lerp position to `anchoredPosition.y = -40` (just below top edge)
   AND lerp alpha to 1.0 over `phaseIndicatorSlideInDuration` (0.15 s).
3. Hold at final position for `phaseIndicatorDisplayDuration` (2.5 s).
4. Fade out: lerp alpha to 0.0 over `phaseIndicatorFadeOutDuration` (0.5 s).
5. Element disabled after fade completes.

After the fade completes, no persistent Phase 2 indicator remains. The Objective
Counter already reads "N / N" (all collected), providing sufficient ambient
reminder that Phase 2 is active. The Suspicion Meter's elevated fill (seekers
are in Searching with suspicion floor 60) confirms the room is hot.

**"After a few seconds" design decision:** The indicator displays for 2.5 seconds
then fades. This is long enough to register (across multiple glances), short
enough to not dominate the screen for a player who is immediately in motion. Post-
fade, the room's world-state carries the Phase 2 signal — no persistent text label
needed.

**Idempotency:** The indicator fires at most once per level session (guarded by the
`_phase2Triggered` flag on `LevelPhaseManager`). `HUDManager` does not need its
own guard — the event only ever fires once.

**Silent state:** The element is disabled (not destroyed) when not displaying. No
residual visual weight.

---

### 3.7 Element 6: Chase Vignette

**What it shows:** A screen-edge pulse effect indicating that at least one seeker
is in Chase state — the player is the active target of a pursuing seeker. This is
the only seeker-gated HUD element, permitted by the Seeker AI GDD (Section 10)
specifically for Chase and no other states.

**Event source:** `EnemyController.OnStateChanged` (per-instance event,
`Action<SeekState>`). `HUDManager` subscribes to `OnStateChanged` for every seeker
registered in `SeekerRegistry` (on scene load, and on any runtime seeker
registration). The vignette activates when any subscribed seeker transitions to
`SeekState.Chase` and deactivates when no subscribed seekers remain in Chase.

**Visual description:** A full-screen Image component with a radial gradient
texture: fully transparent at the center, ramping to a red tint at the screen
edges. The center 60% of screen area by width (and 60% by height) is completely
transparent — no occlusion of the gameplay critical zone. The vignette effect
exists purely at the screen's four corners and edges.

Vignette properties at peak opacity:
- Color: `#CC1111` (saturated dark red) at screen corners
- Peak alpha at corners: `vignetteMaxAlpha` (0.55, see Tuning Knobs)
- Gradient falloff: Hermite curve from edge to center; fully transparent at
  30% from the center in each axis direction

**Pulse animation (on Chase entry):** When a seeker enters Chase and the vignette
was previously inactive, the vignette pulses in:
1. Alpha lerps from 0.0 to `vignetteMaxAlpha` over `vignetteAttackDuration`
   (0.25 s).
2. Then pulses: alpha oscillates between `vignetteMaxAlpha` and
   `vignettePulseMin` (0.35) at `vignettePulseFrequency` (0.8 Hz — one full
   cycle per 1.25 seconds). This slow pulse communicates sustained danger without
   being stroboscopic.
3. Pulse continues as long as any seeker remains in Chase.

**Removal on Chase exit:** When the last seeker exits Chase (transitions to
Searching, Unaware, or Caught), the vignette fades out over `vignetteReleaseDuration`
(0.4 s) to 0 alpha, then the element is deactivated. The release is longer than
the attack: threat appears fast, safety returns slowly — consistent with the
asymmetric pattern used across all HUD elements.

**Multi-seeker Chase handling:** The vignette is a room-level indicator, not a
per-seeker indicator. `HUDManager` maintains an internal `int _chasingSeekerCount`
counter. On any `OnStateChanged` event:
- If `newState == SeekState.Chase`: increment `_chasingSeekerCount`. If
  `_chasingSeekerCount` transitions from 0 to 1, activate vignette and begin
  pulse animation.
- If previous state was `SeekState.Chase` and new state is not Chase: decrement
  `_chasingSeekerCount`. If `_chasingSeekerCount` reaches 0, begin vignette
  release fade.
- The vignette does not intensify with additional chasers — it is binary
  (active or inactive). Multiple chasers do not create a redder or more opaque
  vignette. The gameplay state is already "maximum danger"; no additional visual
  urgency is required or useful.

**Critical occlusion rule (Seeker AI GDD Section 10):** The vignette must not
occlude gameplay-critical screen areas. The center transparency zone (60% x 60%)
must be tested at native resolution on target hardware to confirm the escape route
ahead of the player is not obscured at any normal camera angle.

**Silent state:** Element is deactivated (alpha 0, GameObject disabled) when no
seeker is in Chase. There is no dimmed-outline floor state; the vignette is fully
absent when safe.

---

### 3.8 Element 7: Gadget Slot UI (Stub)

**What it shows (stub):** Two empty inventory slots representing the player's
gadget carry capacity. The Gadget Inventory GDD has not been written yet (systems
index #16, status: Not Started, scope: Vertical Slice). This element is a visual
placeholder that occupies the correct screen position and communicates slot
presence without functional behavior.

**Event source:** None in MVP. The stub does not subscribe to any events. It is a
purely visual element.

**Visual description:** Two rectangular frames, 40x40 px each, spaced 8px apart,
anchored to the top-left corner. Each frame is rendered as a rounded rectangle
outline in white at 30% alpha. The frame interior is fully transparent. No icon,
no count, no hotkey label. The two empty frames read as "this is where something
will go" — familiar from any inventory-bearing game's HUD language.

**Placeholder label (editor only):** In Editor builds, a small grey text label
"GADGET [STUB]" appears below the slots at 8px font size, visible only in the
Unity Game view, not in standalone builds. This is a development aid for
implementors. Disabled by `#if UNITY_EDITOR` preprocessor.

**Slot count rationale:** Two slots matches the systems-index placeholder for
Gadget Inventory (two throwable-type gadgets are the intended MVP Vertical Slice
scope, derived from the Stealth Toolkit entry at row #14). The slot count should
not be increased or decreased by the HUD system — the Gadget Inventory GDD is the
authority on slot count. If the Gadget Inventory GDD chooses a different slot
count, the stub must be updated to match before Vertical Slice implementation
begins.

**Contract for Gadget Inventory GDD:** When the Gadget Inventory GDD is written,
it must define the following for this element to be fully implemented:
- The event `Action<int, GadgetData>` (or equivalent) fired when a gadget slot
  is filled or emptied
- The event `Action<int>` (or equivalent) fired when a gadget is used (slot
  count decrements)
- `GadgetData` fields required for HUD display: icon sprite, optional count,
  optional cooldown float
- Whether slots support stacking (same gadget type, different counts) or are
  strictly one gadget per slot
- Whether the selected/active slot must be highlighted (if the player can choose
  between two different gadget types)

**Silent state:** The stub slots are always visible (30% alpha outlines) from
level start. They do not have a dimmed state beyond their constant low-alpha
appearance. They cannot communicate "empty" more strongly than they already do —
they are empty boxes.

---

### 3.9 HUD Master State: Respawn Sequence

During the respawn sequence (owned by `CheckpointManager` via `RespawnUI`), the
HUD must go dark so it does not display stale data against the black fade-out.

**Event source (new contract — see Section 6):** `CheckpointManager` fires two
new static events:
```csharp
public static event Action OnRespawnSequenceStarted;
public static event Action OnRespawnSequenceEnded;
```

`HUDManager` subscribes to both. On `OnRespawnSequenceStarted`: the HUD
CanvasGroup alpha fades to 0.0 over `respawnHudFadeDuration` (0.15 s). On
`OnRespawnSequenceEnded`: the HUD CanvasGroup alpha fades back to 1.0 over
`respawnHudFadeDuration` (0.15 s). This timing is subordinate to the
`CheckpointManager`'s own fade sequence — the HUD fades before the screen does,
ensuring no HUD elements appear against a black background while state is being
reset.

`HUDManager` does NOT reset individual element states during the respawn sequence.
When the HUD fades back in after respawn, elements reflect their current data:
- Noise Indicator: decayed to floor (player has not moved since respawn)
- Suspicion Meter: filled to Phase2SuspicionFloor fill if in Phase 2 (60/100 =
  Searching band), or draining to 0 if in Phase 1 (seekers reset to Unaware)
- Light Exposure Indicator: reflects the player's actual light level at the
  respawn position
- Objective Counter: unchanged (objectives persist across respawn)
- Phase 2 Indicator: not re-displayed on respawn (the phase indicator is
  one-shot per level session)
- Chase Vignette: deactivated (seekers reset to Unaware/Searching on respawn;
  `_chasingSeekerCount` resets to 0)

---

## 4. Formulas

### F-HUD-1: Noise Indicator Visual Fill

The Noise Indicator visual fill value is computed each tick of its active
Coroutine:

**On incoming event (spike):**
```
if (incomingLevel > currentDisplayLevel):
    currentDisplayLevel = incomingLevel        // snap up, no lerp
else:
    currentDisplayLevel = Lerp(currentDisplayLevel, incomingLevel, noiseBarLerpRate * deltaTime)
```

**Segment selection (floor to 5 segments):**
```
litSegmentCount = ceil(currentDisplayLevel * 5.0)
litSegmentCount = clamp(litSegmentCount, 0, 5)
```

**Silent floor check:**
```
isVisuallyActive = currentDisplayLevel >= hudDisplayThreshold  (0.05)
segmentAlpha[i] = isVisuallyActive ? 1.0 : 0.08  (dimmed outline)
```

| Variable | Type | Range | Source | Description |
|----------|------|-------|--------|-------------|
| `currentDisplayLevel` | float | 0.0–1.0 | HUD runtime state | Current visual fill; lerps toward incoming value |
| `incomingLevel` | float | 0.0–1.0 | `OnPlayerNoiseLevelChanged` | Value published by PNE |
| `noiseBarLerpRate` | float | 4.0–20.0 | `HUDData` asset | Lerp rate for decay only; spike is instant (default 12.0/s) |
| `hudDisplayThreshold` | float | 0.01–0.10 | `HUDData` asset | Level below which bar is in dimmed-outline state (default 0.05) |
| `litSegmentCount` | int | 0–5 | Computed | Number of segments to render at full alpha |

**Example — Sprint footstep (BaseIntensity = 0.85), then stop:**
- Spike: `currentDisplayLevel` snaps to 0.85. `litSegmentCount` = ceil(0.85 * 5) = 5 (all segments lit, top segment red).
- After 0.1 s: 0.85 − (12.0 × 0.1) = −0.35 → clamped to 0.0...

Correction — the PNE's `_currentNoiseLevel` decays at `1/noiseIndicatorDecayDuration` (1.25/s at 0.8s default). The HUD lerps its display toward that value. After 0.5 s from PNE: PNE level = 0.85 − (1.25 × 0.5) = 0.225. HUD display lerps at 12.0/s toward 0.225. Assuming display was at 0.85 at t=0: after 0.5s the HUD display ≈ 0.225 (the lerp converges quickly at 12.0/s). `litSegmentCount` = ceil(0.225 × 5) = 2 (two white segments lit). After 0.68s PNE level = 0 → HUD decays to 0, below `hudDisplayThreshold`, bar enters dimmed-outline state.

---

### F-HUD-2: Suspicion Meter Fill and Color

**Fill computation (symmetric lerp):**
```
currentFillDisplay = Lerp(currentFillDisplay, incomingSuspicion / 100.0, suspicionBarLerpRate * deltaTime)
```

**Color band selection (by fill value, not suspicion float):**
```
if   (incomingSuspicion < 25):  color = transparent (meter invisible)
elif (incomingSuspicion < 60):  color = Amber   (#F0A000)
elif (incomingSuspicion < 85):  color = Orange  (#E05000)
else:                            color = Red     (#CC1111)
```

Color transitions cross-fade at `suspicionColorCrossfadeDuration` (0.15 s).

| Variable | Type | Range | Source | Description |
|----------|------|-------|--------|-------------|
| `currentFillDisplay` | float | 0.0–1.0 | HUD runtime | Current visual fill (0.0 = empty arc, 1.0 = full arc) |
| `incomingSuspicion` | float | 0.0–100.0 | `OnSuspicionChanged` | Highest suspicion across all seekers |
| `suspicionBarLerpRate` | float | 4.0–16.0 | `HUDData` asset | Lerp rate in both directions (default 8.0/s) |
| `suspicionColorCrossfadeDuration` | float | 0.05–0.3 s | `HUDData` asset | Color band transition duration (default 0.15 s) |

**Meter fade-in/fade-out (alpha only, not fill):**
```
On suspicion crossing 25 (Unaware → Alert):
    targetAlpha = 1.0; lerp alpha at meterFadeRate (1.0/meterFadeInDuration = 1/0.15 = 6.67/s)

On suspicion falling below 25 (all seekers):
    targetAlpha = 0.0; lerp alpha at (1.0/meterFadeOutDuration = 1/0.6 = 1.67/s)
```

**Example — seeker at 30 suspicion (Alert), escalating:**
- At suspicion = 30: fill = 0.30, color = Amber, alpha = 1.0 (faded in at threshold crossing).
- Seeker suspicion rises to 65 over 3.5 s: fill lerps from 0.30 to 0.65 at 8.0/s (converges in < 0.5 s after each update event fires). Color transitions to Orange at the 60-threshold crossing.
- At 0.15 s after crossing 60: color fully Orange. Fill = 0.65.

---

### F-HUD-3: Light Exposure Indicator Brightness

The Light Exposure Indicator uses the same snap-up / lerp-down pattern as the
Noise Indicator, with a different lerp rate:

```
if (incomingExposure > currentDisplayExposure):
    currentDisplayExposure = incomingExposure      // snap up
else:
    currentDisplayExposure = Lerp(currentDisplayExposure, incomingExposure, lightExposureLerpRate * deltaTime)
```

**Circle brightness mapping:**
```
fillAlpha = Lerp(0.08, 1.0, currentDisplayExposure)      // 8% at 0.0, 100% at 1.0
bloomEmission = currentDisplayExposure * maxBloomEmission (2.5 HDR value)
```

The bloom is only perceptible above exposure 0.7 — below that, the HDR emission
value is too low to trigger the URP bloom post-process threshold. This means bloom
appears only when the player is significantly lit, which is the correct threshold
for the "danger: you are very visible" signal.

| Variable | Type | Range | Source | Description |
|----------|------|-------|--------|-------------|
| `currentDisplayExposure` | float | 0.0–1.0 | HUD runtime | Current visual brightness |
| `incomingExposure` | float | 0.0–1.0 | `OnPlayerLightLevelChanged` | Published by Detection System |
| `lightExposureLerpRate` | float | 2.0–12.0 | `HUDData` asset | Lerp rate for decay (default 6.0/s) |
| `maxBloomEmission` | float | 1.0–4.0 | `HUDData` asset | HDR emission value at exposure = 1.0 (default 2.5) |

**Example — player walks from shadow (0.0) into torchlight zone (0.75) in 0.3 s:**
- Snap up: `currentDisplayExposure` = 0.75 immediately on event.
- `fillAlpha` = Lerp(0.08, 1.0, 0.75) = 0.08 + (0.92 × 0.75) = 0.77 (77% alpha — visibly bright).
- `bloomEmission` = 0.75 × 2.5 = 1.875 — above URP bloom threshold; glow visible.
- Player retreats to shadow (exposure drops to 0.05 over 0.5s): display lerps at 6.0/s. After 0.5 s: 0.75 − (6.0 × 0.5) = −2.25 → clamped to 0.05. Display reaches target. `fillAlpha` = 0.08 + (0.92 × 0.05) = 0.126 — near-floor, dimmed outline state.

---

### F-HUD-4: Chase Vignette Opacity

**Pulse during active Chase:**
```
vignettePulsePhase += vignettePulseFrequency * 2 * PI * deltaTime   // radians
vignetteAlpha = Lerp(vignettePulseMin, vignetteMaxAlpha, (sin(vignettePulsePhase) * 0.5 + 0.5))
```

This maps the sine wave's range (−1 to +1) to (0 to 1), then lerps between
`vignettePulseMin` and `vignetteMaxAlpha`.

**Attack (Chase entry):**
```
vignetteAlpha = Lerp(0.0, vignetteMaxAlpha, t / vignetteAttackDuration)
```
Attack lerp precedes the pulse; once `vignetteAlpha >= vignetteMaxAlpha`, the
pulse Coroutine takes over.

**Release (all chasers exit Chase):**
```
vignetteAlpha = Lerp(currentAlpha, 0.0, t / vignetteReleaseDuration)
```
Release starts from whatever alpha the pulse was at when the last chaser exited
Chase — no discontinuity.

| Variable | Type | Range | Source | Description |
|----------|------|-------|--------|-------------|
| `vignetteMaxAlpha` | float | 0.3–0.75 | `HUDData` asset | Peak opacity during pulse (default 0.55) |
| `vignettePulseMin` | float | 0.2–0.55 | `HUDData` asset | Trough opacity during pulse (default 0.35) |
| `vignettePulseFrequency` | float | 0.3–1.5 Hz | `HUDData` asset | Pulse cycles per second (default 0.8 Hz) |
| `vignetteAttackDuration` | float | 0.1–0.5 s | `HUDData` asset | Time to reach peak alpha on Chase entry (default 0.25 s) |
| `vignetteReleaseDuration` | float | 0.2–1.0 s | `HUDData` asset | Time to fade to 0 alpha on all chasers exiting (default 0.4 s) |

**Example — single seeker enters Chase:**
- t = 0: vignetteAlpha = 0.0. Attack begins.
- t = 0.125 s: vignetteAlpha = Lerp(0.0, 0.55, 0.5) = 0.275.
- t = 0.25 s: attack complete. vignetteAlpha = 0.55. Pulse starts.
- t = 1.25 s (one full pulse cycle at 0.8 Hz): alpha oscillates 0.55 → 0.35 → 0.55. Corner is clearly pulsing.
- Seeker exits Chase at t = 2.0 s. Release begins from current alpha (let's say 0.44 mid-pulse).
- t = 2.4 s: release complete. vignetteAlpha = 0.0. Element deactivated.

---

### F-HUD-5: Phase 2 Indicator Animation

```
// Slide-in phase (duration: phaseIndicatorSlideInDuration = 0.15 s)
anchoredPositionY = Lerp(slideStartY, slideEndY, t / phaseIndicatorSlideInDuration)
alpha = Lerp(0.0, 1.0, t / phaseIndicatorSlideInDuration)

// Hold phase (duration: phaseIndicatorDisplayDuration = 2.5 s)
// No change — element at slideEndY, alpha = 1.0

// Fade-out phase (duration: phaseIndicatorFadeOutDuration = 0.5 s)
alpha = Lerp(1.0, 0.0, t / phaseIndicatorFadeOutDuration)
```

| Variable | Type | Range | Source | Description |
|----------|------|-------|--------|-------------|
| `slideStartY` | float | 40–100 px above screen | `HUDData` asset | Starting Y above screen edge (default 60 px above top edge anchor) |
| `slideEndY` | float | −20 to −60 px | `HUDData` asset | Final Y below top edge anchor (default −40 px) |
| `phaseIndicatorSlideInDuration` | float | 0.05–0.5 s | `HUDData` asset | Slide animation duration (default 0.15 s) |
| `phaseIndicatorDisplayDuration` | float | 1.0–5.0 s | `HUDData` asset | Time held at full visibility (default 2.5 s) |
| `phaseIndicatorFadeOutDuration` | float | 0.2–1.0 s | `HUDData` asset | Fade duration (default 0.5 s) |

**Total display time at defaults:** 0.15 + 2.5 + 0.5 = **3.15 seconds**.

---

## 5. Edge Cases

### EC-HUD-01: Three Seekers in Different States Simultaneously

**Scenario:** Seeker A is at suspicion 90 (Chase). Seeker B is at suspicion 65
(Searching). Seeker C is at suspicion 20 (Unaware).

**Expected behavior:** `DetectionSystem.OnSuspicionChanged` publishes the highest
suspicion value across all seekers. The event fires `(90.0, SeekState.Chase)`.
The Suspicion Meter shows fill at 90%, color Red. The Chase Vignette is active
(Seeker A is in Chase). Seeker B and C are not individually visible on the HUD.

**Rationale:** Showing the single worst case is always correct — the player's
decisions should be driven by their most dangerous exposure, not by a composite
view. A meter averaging three seekers would understate the danger from Seeker A
during active Chase.

**If Seeker A's Chase resolves (drops to Searching):** `OnSuspicionChanged` fires
the new highest value, which is now Seeker B's 65 (Searching) or Seeker A's new
decayed value, whichever is higher. The Vignette begins release fade (Seeker A
exited Chase; `_chasingSeekerCount` decrements to 0). The Suspicion Meter color
transitions from Red to Orange. This multi-state transition is handled correctly
by event-driven updates — each `OnSuspicionChanged` event reflects the current
ground truth.

---

### EC-HUD-02: No Objectives in Scene

**Scenario:** Level designer places zero `ObjectiveToken` prefabs in the scene.
`ObjectiveRegistry.TotalCount` = 0 on scene load.

**Expected behavior:** The Objective Counter displays "0 / 0". This is an error
condition (Objective System GDD Rule OS-12: a level with zero tokens is invalid).
The Objective System's own behavior in this case is that `OnAllObjectivesCollected`
fires immediately at Playing state entry, immediately triggering Phase 2. The HUD
counter "0 / 0" is the correct display of accurate data — it surfaces the error
visually rather than hiding it.

**Mitigation:** `HUDManager.Awake` should log a `Debug.LogWarning` if
`ObjectiveRegistry.Instance.TotalCount == 0` at initialization time. This assists
level designer debugging.

---

### EC-HUD-03: Phase 2 Starts with a Seeker Already in Chase

**Scenario:** The player collects the final objective while Seeker A is already
in Chase state (suspicion = 92). Phase 2 triggers. `LevelPhaseManager` sets
`Phase2SuspicionFloor = 60` on all seekers. Seeker A is already above 60.

**Expected behavior:**
- Phase 2 Indicator: fires normally from `OnPhaseChanged`. "ALARM" slides in.
- Suspicion Meter: was already showing fill = 0.92, color = Red. No change — Seeker A is still at 92 and still in Chase. The meter was already in its maximum state.
- Chase Vignette: was already active (Seeker A was in Chase before Phase 2). No change to vignette behavior. `_chasingSeekerCount` remains 1.
- For the other seekers (previously Unaware): their suspicion is snapped to 60 on the next FixedUpdate. `OnSuspicionChanged` fires if their contribution to "highest suspicion" changes — but since Seeker A is still at 92, the published highest value does not change. No HUD update fires for those seekers' snap-to-60.

**Result:** From the HUD's perspective, Phase 2 start with an already-chasing seeker looks identical to a normal Chase — only the "ALARM" indicator distinguishes the phase transition. The Suspicion Meter was already at max and remains there. The Chase Vignette was already active. This is correct: the player was already in maximum danger; Phase 2 escalates the other seekers without the HUD changing state. The "ALARM" indicator is still the correct signal.

---

### EC-HUD-04: All Seekers Caught During Respawn Reset

**Scenario:** The player is caught. While the respawn sequence runs (screen
fades black), all seekers reset to Unaware (suspicion = 0). `OnSuspicionChanged`
fires with value 0.0. The HUD has been faded to 0 alpha by the respawn event.

**Expected behavior:** `SuspicionMeterUI` receives the `OnSuspicionChanged(0.0,
SeekState.Unaware)` event while the HUD master CanvasGroup alpha is 0. The meter
correctly updates its internal state to fill = 0 and begins its `meterFadeOutDuration`
transition — but because the CanvasGroup alpha is 0, nothing is visible. When
`OnRespawnSequenceEnded` fires and the HUD CanvasGroup fades back in, the meter
is already in its correct "Unaware, alpha = 0" state. The HUD fades in cleanly
with no stale suspicion fill visible.

**Chase Vignette:** During respawn reset, all seekers transition away from Chase.
Each `OnStateChanged` event decrements `_chasingSeekerCount`. When it reaches 0,
the vignette release Coroutine starts — but the element is fully invisible (HUD
CanvasGroup alpha = 0), so the Coroutine completing while invisible is harmless.
The vignette is deactivated before the HUD fades back in.

---

### EC-HUD-05: Seeking Seeker Count Drops to Zero (Level With One Seeker Caught)

**Scenario:** In a level with one seeker, the seeker catches the player and
transitions to Caught state. `OnStateChanged(SeekState.Caught)` fires on Seeker A.

**Expected behavior:** From the Seeker AI GDD (Section 3.6), the seeker invokes
`GameManager.OnPlayerCaught()` on entry to Caught state. `CheckpointManager` begins
the respawn sequence immediately. `OnRespawnSequenceStarted` fires, and the HUD
fades to 0 alpha. The Chase Vignette release would also fire simultaneously (the
seeker is no longer in Chase — it is in Caught, which maps to Chase Vignette
deactivation). The vignette release and HUD fade may run concurrently; both
targeting alpha = 0 is correct and harmless.

---

### EC-HUD-06: Light Exposure Event Not Yet Defined at Runtime

**Scenario:** The Detection System has not yet implemented
`OnPlayerLightLevelChanged` (this GDD defines the contract; implementation is
pending). The event is null.

**Expected behavior:** `HUDManager` subscribes in `OnEnable` with null check:
```csharp
if (DetectionSystem.OnPlayerLightLevelChanged != null)
    DetectionSystem.OnPlayerLightLevelChanged += OnLightLevelChanged;
```
The Light Exposure Indicator remains at floor (dimmed outline, 0.0 exposure)
for the entire session. No exception. The HUD degrades gracefully: the element
is present but reports "in complete darkness" — which errs toward under-informing
the player about light exposure rather than crashing. A `Debug.LogWarning` is
emitted in `Awake` if the event field is null at initialization, surfacing the
missing contract to the implementing programmer.

---

### EC-HUD-07: Rapid Phase 2 Indicator Re-trigger Attempt

**Scenario:** A bug or edge case causes `LevelPhaseManager.OnPhaseChanged` to
fire `Phase2_Escape` twice in the same session.

**Expected behavior:** `HUDManager` uses `_phase2IndicatorShown` bool flag. On
receiving `Phase2_Escape`, if `_phase2IndicatorShown == true`, the handler returns
immediately — no second indicator display. The Two-Phase Level Structure GDD
(Rule TPS-4) already prevents the event from firing twice via its own guard, but
the HUD's own guard is a defensive second layer.

---

### EC-HUD-08: Level With More Than 9 Objectives

**Scenario:** A level has 12 objectives (`TotalCount = 12`). The Objective Counter
must display "12 / 12" at completion.

**Expected behavior:** The counter uses a flexible `TextMeshPro` text component
with no fixed character count limit. Two-digit numbers render correctly. The
`HorizontalLayoutGroup` accommodates the wider text. No overflow or truncation.
Level designers should be aware that very high objective counts may widen the
counter beyond its expected footprint — this is a level design constraint, not a
HUD system constraint. Recommendation: do not exceed 9 objectives per level unless
the HUD counter width has been tested at the target resolution.

---

### EC-HUD-09: Zero-Delta Events (Suppressed by Upstream Thresholds)

**Scenario:** The PNE's `hudPublishThreshold` (0.01) suppresses micro-changes in
noise level. The Detection System similarly only fires `OnSuspicionChanged` when
the value changes by > 0.01. Result: the HUD may not receive an event every
FixedUpdate.

**Expected behavior:** This is correct by design. The HUD's Coroutine-based
visual lerp handles the gap between events gracefully — it lerps toward the last
received value until a new event updates the target. There is no visual stutter
or hold because the lerp is continuous within the Coroutine; it does not require
a new event to keep lerping.

---

### EC-HUD-10: Seeker Destroyed Mid-Chase (Hypothetical)

**Scenario:** A seeker in Chase state is destroyed by a level unload, editor
action, or future gameplay system before `OnStateChanged` fires the exit event.

**Expected behavior:** `HUDManager` subscribes and unsubscribes from each seeker's
`OnStateChanged` event in `OnEnable`/`OnDisable` of each seeker (the seeker's
`OnDestroy` should unsubscribe by calling a cleanup method, or the seeker's
`OnDisable` fires before `OnDestroy`). If a seeker is destroyed without firing
`OnStateChanged`, `_chasingSeekerCount` would remain above zero and the vignette
would stay active indefinitely.

**Mitigation:** `HUDManager` subscribes to `SeekerRegistry.OnSeekerUnregistered`
(a new event contract added to `SeekerRegistry` — see Section 6) and decrements
`_chasingSeekerCount` on seeker destruction if that seeker was tracked as chasing.
`SeekerRegistry.OnSeekerUnregistered` fires from `EnemyController.OnDestroy` via
`SeekerRegistry.Instance.Unregister(this)` (already required by Rule TPS-5).

---

## 6. Dependencies

### Systems This Depends On (Upstream)

| System | Direction | Event / Property | Notes |
|--------|-----------|-----------------|-------|
| Player Noise Emitter | Subscribes | `static event Action<float> OnPlayerNoiseLevelChanged` | Fires on `_currentNoiseLevel` change > 0.01. 0.0–1.0. Subscribed in `HUDManager.OnEnable`. |
| Detection System | Subscribes | `static event Action<float, SeekState> OnSuspicionChanged` | Highest suspicion across all seekers. 0.0–100.0. Fires on change > 0.01. |
| Detection System | Subscribes | `static event Action<float> OnPlayerLightLevelChanged` | NEW CONTRACT defined by this GDD — see below. |
| ObjectiveRegistry | Subscribes | `static event Action OnObjectiveCollected` | Fires on each collection. HUD queries `CollectedCount` and `TotalCount` on event. |
| ObjectiveRegistry | Reads | `int CollectedCount`, `int TotalCount` | Read synchronously on `OnObjectiveCollected`. No polling. |
| LevelPhaseManager | Subscribes | `static event Action<LevelPhase> OnPhaseChanged` | Phase 2 Indicator fires on `Phase2_Escape`. Unsubscribed in `OnDestroy`. |
| EnemyController (per seeker) | Subscribes | `event Action<SeekState> OnStateChanged` | Per-instance event. HUDManager subscribes to each seeker via SeekerRegistry.GetAll() on scene load. |
| SeekerRegistry | Subscribes | `static event Action<EnemyController> OnSeekerRegistered`, `static event Action<EnemyController> OnSeekerUnregistered` | NEW CONTRACT — see below. Used to subscribe/unsubscribe per-seeker events dynamically. |
| CheckpointManager | Subscribes | `static event Action OnRespawnSequenceStarted`, `static event Action OnRespawnSequenceEnded` | NEW CONTRACT — see below. Drives HUD master alpha during respawn. |
| ObjectiveData | Reads | `Sprite ObjectiveIconSprite` | Displayed in Objective Counter. Read on `HUDManager.Start` or on first `OnObjectiveCollected` event. |

### Systems That Depend On This (Downstream)

None. The HUD is a pure consumer. No gameplay system reads from the HUD.

---

### New Event Contracts Required by This GDD

The following contracts are defined here and must be implemented by their
respective upstream systems. This GDD is the authoritative specification; upstream
GDDs must be updated to reference these contracts when the systems are implemented.

#### Contract A: `DetectionSystem.OnPlayerLightLevelChanged`

```csharp
// In DetectionSystem.cs (namespace HideAndSeek)
// Required addition: publish the player's current light exposure level
// to the HUD so the player can read their own visual exposure.
// Reference: Detection System GDD Rule V-4 edge case:
//   "Full darkness provides complete visual immunity.
//    The player's light exposure level must be communicated via HUD."

public static event Action<float> OnPlayerLightLevelChanged;
// float: the player's current light exposure level, 0.0 (dark) to 1.0 (full light)
// Derived from: light_level sampled at player position by the Light Source System
//   (this is the same value used in the visual suspicion delta formula F2 as light_factor)
// Publish condition: fire when value changes by > 0.02 (larger threshold than suspicion
//   events because light level changes are typically gradual; 0.02 prevents event floods
//   on smooth transitions)
// Publish timing: each FixedUpdate in which the change threshold is met
// Value range: 0.0–1.0. Never outside this range.
// NOT derived from per-seeker detection output — this is the player's absolute light
//   exposure at their world position, independent of any seeker's LoS or distance.
```

The Detection System already samples `light_level` at the player's position during
its normal visual detection calculation (Formula F2). Publishing this existing
intermediate value via the new event requires no additional light sampling — it is
a read of a value already computed per FixedUpdate.

#### Contract B: `SeekerRegistry.OnSeekerRegistered` and `OnSeekerUnregistered`

```csharp
// Addition to SeekerRegistry.cs (SceneSingleton<SeekerRegistry>)
// Required so HUDManager can subscribe/unsubscribe per-seeker events dynamically
// without polling SeekerRegistry.GetAll() each frame.

public static event Action<EnemyController> OnSeekerRegistered;
// Fires from SeekerRegistry.Register(seeker) immediately after adding to the list.

public static event Action<EnemyController> OnSeekerUnregistered;
// Fires from SeekerRegistry.Unregister(seeker) immediately before removing from list.
// Also fires from EnemyController.OnDestroy via the existing Unregister call (Rule TPS-5).
```

`HUDManager.OnSeekerRegistered` handler: subscribe to `seeker.OnStateChanged`.
`HUDManager.OnSeekerUnregistered` handler: unsubscribe from `seeker.OnStateChanged`
and if that seeker was chasing (`_chasingSeekerCount > 0`), decrement the counter.

#### Contract C: `CheckpointManager.OnRespawnSequenceStarted` and `OnRespawnSequenceEnded`

```csharp
// Addition to CheckpointManager.cs
// Required so HUDManager can hide and show the HUD during the respawn sequence.

public static event Action OnRespawnSequenceStarted;
// Fires at step 1 of ExecuteRespawnSequence() — before any fade, freeze, or state reset.
// HUDManager handler: begin fading HUD CanvasGroup alpha to 0.

public static event Action OnRespawnSequenceEnded;
// Fires at step 22 of ExecuteRespawnSequence() — after SetInputEnabled(true),
// when the sequence is fully complete.
// HUDManager handler: begin fading HUD CanvasGroup alpha back to 1.0.
```

Timing constraint: `OnRespawnSequenceStarted` must fire before `RespawnUI.FadeOut`
is called (step 6 of the respawn sequence) so the HUD has time to fade before the
screen goes black. At default `respawnHudFadeDuration` (0.15 s), firing at step 1
ensures the HUD is fully dark before the screen fade begins at step 6 (which occurs
after `caughtFreezeDelay` = 0.4 s — plenty of margin).

---

### Bidirectional Dependency Note for Upstream GDDs

The following upstream GDDs must be updated to reference this HUD GDD in their
Dependencies sections:
- Detection System GDD: add `OnPlayerLightLevelChanged` to Section 6 ("Systems
  That Depend On This") and add the contract spec to Section 9 (UI Requirements).
- Checkpoint System GDD: add `OnRespawnSequenceStarted` / `OnRespawnSequenceEnded`
  to Section 6 and Section 9.
- SeekerRegistry (Two-Phase Level Structure GDD): add `OnSeekerRegistered` /
  `OnSeekerUnregistered` to the `SeekerRegistry` API definition (Rule TPS-6).

---

## 7. Tuning Knobs

All values authored in `HUDData` ScriptableObject at
`Assets/_Project/Scripts/Data/HUDData.asset`. No values hardcoded in any UI
MonoBehaviour.

| Knob | Category | Default | Safe Range | Effect of Increase | Effect of Decrease |
|------|----------|---------|------------|-------------------|--------------------|
| `noiseBarLerpRate` | Feel | 12.0 /s | 4.0–20.0 | Slower visual decay — bar is "stickier," stays elevated longer after footstep | Faster decay — bar snaps to current PNE value quickly; can feel mechanical |
| `hudDisplayThreshold` | Gate | 0.05 | 0.01–0.10 | Bar enters dimmed-outline state earlier — more time in silent state | Bar stays lit at lower noise levels; barely-audible noise shows as lit |
| `suspicionBarLerpRate` | Feel | 8.0 /s | 4.0–16.0 | Slower fill — meter lags behind actual suspicion; may understate rising danger | Faster fill — meter snaps immediately; less time to react visually |
| `meterFadeInDuration` | Feel | 0.15 s | 0.05–0.4 s | Meter takes longer to appear when danger begins; player may miss brief Alert | Meter snaps in instantly; can feel jarring |
| `meterFadeOutDuration` | Feel | 0.6 s | 0.2–2.0 s | Meter lingers longer after all seekers return to Unaware; slower "safe" signal | Meter disappears immediately when suspicion drops — may feel abrupt |
| `suspicionColorCrossfadeDuration` | Feel | 0.15 s | 0.05–0.3 s | Slower color transition — less jarring at thresholds | Instantaneous color pop at state boundaries; can feel harsh |
| `lightExposureLerpRate` | Feel | 6.0 /s | 2.0–12.0 | Slower indicator response to light changes; feels slightly behind player's real exposure | Faster response; indicator snaps with room boundary crossings |
| `maxBloomEmission` | Feel | 2.5 (HDR) | 1.0–4.0 | Stronger glow at full exposure; more visually striking at max light | Bloom barely visible; light exposure indicator loses its "danger" visual weight at high exposure |
| `vignetteMaxAlpha` | Feel | 0.55 | 0.3–0.75 | Vignette is darker and more alarming; can obscure screen edge detail | Vignette is subtle; may not register in peripheral vision during intense gameplay |
| `vignettePulseMin` | Feel | 0.35 | 0.2–0.55 | Deeper pulse trough — oscillation range is wider; feels more frantic | Minimal alpha variation during pulse; pulse feels like a slow glow rather than a pulse |
| `vignettePulseFrequency` | Feel | 0.8 Hz | 0.3–1.5 Hz | Faster pulse; can feel anxiety-inducing or stroboscopic — must not exceed 3 Hz (accessibility rule) | Slower pulse; feels like a slow-burn danger signal rather than alarm |
| `vignetteAttackDuration` | Feel | 0.25 s | 0.1–0.5 s | Slower attack; vignette builds up rather than snapping in | Very fast attack; vignette appears almost instantly on Chase — potentially startling |
| `vignetteReleaseDuration` | Feel | 0.4 s | 0.2–1.0 s | Vignette lingers longer after Chase ends; suspense maintained | Vignette disappears instantly — sharp contrast may feel jarring after sustained Chase |
| `phaseIndicatorSlideInDuration` | Feel | 0.15 s | 0.05–0.5 s | Slower slide; indicator takes longer to register | Near-instant slide-in; may be missed entirely if player glances away at exact frame |
| `phaseIndicatorDisplayDuration` | Gate | 2.5 s | 1.0–5.0 s | Indicator stays on screen longer; more readable for players who missed the initial flash | Shorter display; increases miss risk for players already moving during Phase 2 entry |
| `phaseIndicatorFadeOutDuration` | Feel | 0.5 s | 0.2–1.0 s | Slower fade — indicator lingers at low alpha before disappearing | Abrupt disappearance; may look like a UI pop |
| `respawnHudFadeDuration` | Feel | 0.15 s | 0.05–0.4 s | HUD takes longer to hide/show around respawn — may overlap with screen fade | HUD fades faster — less chance of stale HUD data visible briefly against the screen fade |
| `lerpToVisibleDuration` | Feel | 0.08 s | 0.03–0.2 s | Slower noise bar activation from dimmed-outline state | Faster activation; any noise level above threshold immediately shows as lit |

**Accessibility constraints (hardcoded, not tuning knobs):**
- `vignettePulseFrequency` must never exceed 3.0 Hz in shipping builds. Frequencies above 3 Hz can trigger photosensitive conditions. Enforce via `[Range(0.3f, 3.0f)]` attribute on the serialized field.
- `vignetteMaxAlpha` must not be raised above 0.75 without confirming center zone transparency is maintained.

---

## 8. Acceptance Criteria

All criteria are testable by a QA tester with access to the Unity Game view and
the Unity Test Runner.

| # | Criterion | Test Method | Pass | Fail |
|---|-----------|-------------|------|------|
| AC-HUD-01 | Noise Indicator snaps up instantly on a sprint footstep `NoiseEvent` (BaseIntensity = 0.85) and all 5 segments light in the same frame the event fires. | PlayMode: sprint player. Observe indicator in first frame of sprint step. | All 5 segments lit within 1 frame of event. Top segment is red. | Any frame delay before full fill; wrong segment count; wrong color. |
| AC-HUD-02 | Noise Indicator decays to dimmed-outline state (visually silent) within `noiseIndicatorDecayDuration` + 0.1 s margin after the last noise event. | PlayMode: trigger one noise event (BaseIntensity = 0.85), stop player, time decay. | Indicator at floor within 0.9 s (0.8 s default + 0.1 s margin). | Indicator still showing active fill after 0.9 s. |
| AC-HUD-03 | Suspicion Meter is invisible (alpha = 0) when all seekers are Unaware (suspicion < 25). | PlayMode: level start before seeker detects anything. | Meter not visible on screen. | Any fill or alpha > 0 on meter. |
| AC-HUD-04 | Suspicion Meter fades in within `meterFadeInDuration` (0.15 s) when any seeker's suspicion crosses 25. | PlayMode: walk player into seeker LoS until suspicion = 26. Time fade-in. | Meter visible at alpha = 1.0 within 0.15 s of threshold crossing. | Meter still invisible after 0.15 s; or appears at wrong alpha. |
| AC-HUD-05 | Suspicion Meter color is Amber at suspicion 30, Orange at suspicion 65, Red at suspicion 90. | PlayMode: use seeker debug override to set suspicion to 30, then 65, then 90. | Colors match specification at each value. | Wrong color at any value; or color fails to transition at thresholds. |
| AC-HUD-06 | Chase Vignette activates within `vignetteAttackDuration` (0.25 s) of any seeker entering Chase state. | PlayMode: trigger Chase (suspicion > 85 with LoS). Time vignette onset. | Vignette alpha >= `vignetteMaxAlpha` within 0.25 s. | Vignette not visible, or takes > 0.4 s to reach max alpha. |
| AC-HUD-07 | Chase Vignette center zone (center 60% × 60% of screen) is fully transparent at all opacity levels. | PlayMode: place a bright-colored quad in screen center during Chase. The quad must be fully visible through the vignette. | Center zone unobscured at all vignette alpha levels. | Any tint or alpha reduction in the center zone. |
| AC-HUD-08 | Chase Vignette pulse frequency stays at or below 3.0 Hz regardless of `vignettePulseFrequency` value in `HUDData`. | EditMode: set `vignettePulseFrequency` to 5.0 Hz. Measure actual pulse rate in PlayMode. | Pulse rate clamped to 3.0 Hz or lower. | Pulse rate exceeds 3.0 Hz with value above 3.0 Hz. |
| AC-HUD-09 | Objective Counter shows "0 / N" on level load before any collection. | PlayMode: check counter at scene start. | Text reads "0 / N" with correct N from `TotalCount`. | Wrong CollectedCount or TotalCount displayed. |
| AC-HUD-10 | Objective Counter updates to "N / M" immediately on `OnObjectiveCollected` event and plays the micro-pop scale animation. | PlayMode: collect one objective. Observe counter and animation. | Text updates within 1 frame of event. Scale punch animation plays (visible to observer). | Text lags event; animation does not play; wrong count. |
| AC-HUD-11 | Phase 2 Indicator ("ALARM") appears within `phaseIndicatorSlideInDuration` (0.15 s) of `OnPhaseChanged(Phase2_Escape)` and disappears before `phaseIndicatorDisplayDuration` + `phaseIndicatorFadeOutDuration` + 0.1 s margin = 3.1 s. | PlayMode: collect all objectives, time indicator appearance and disappearance. | Text appears within 0.15 s; disappears by 3.1 s. | Text does not appear; or persists beyond 3.1 s. |
| AC-HUD-12 | Phase 2 Indicator does not display a second time if `OnPhaseChanged` fires twice in the same session (defensive guard). | EditMode: fire `LevelPhaseManager.OnPhaseChanged(Phase2_Escape)` twice via test harness. | Indicator appears exactly once. `_phase2IndicatorShown` is true after first fire. | Indicator appears twice; second display overlaps or resets. |
| AC-HUD-13 | HUD master CanvasGroup alpha reaches 0 before `RespawnUI.FadeOut` completes (screen goes black). At the default `respawnHudFadeDuration` (0.15 s) and `caughtFreezeDelay` (0.4 s), the HUD finishes fading 0.25 s before the screen fade begins. | PlayMode: trigger catch, observe HUD during respawn sequence. Verify no HUD elements visible against the black screen. | No HUD elements visible at any point during the black screen phase. | HUD elements visible against black during state reset phase. |
| AC-HUD-14 | After respawn in Phase 2, the Suspicion Meter shows fill corresponding to seekers' post-reset state (suspicion floor = 60 → ~0.60 fill, Orange color) without requiring any extra input. | PlayMode: collect all objectives, get caught, respawn. Observe Suspicion Meter immediately after HUD fade-in. | Meter shows ~60% fill, Orange color, within 0.15 s of HUD fade-in. | Meter shows 0 fill; or shows stale Chase-level fill (>85%). |
| AC-HUD-15 | Light Exposure Indicator is at dimmed-outline state in a level room with no light sources at player position. | PlayMode: place player in a dark room (no light sources, light_level = 0.0 at player position). Observe indicator. | Indicator renders as dim outline only (approximately 8% alpha circle). | Indicator shows any fill brightness above threshold. |
| AC-HUD-16 | Light Exposure Indicator snaps to bright state within one FixedUpdate frame of the player stepping into a lit zone (`OnPlayerLightLevelChanged` fires with value >= 0.5). | PlayMode: walk player across a sharp shadow boundary. Observe indicator at boundary crossing frame. | Indicator fills immediately (snap-up rule). | Indicator lerps slowly upward; visible lag at boundary crossing. |
| AC-HUD-17 | `HUDManager.Awake` emits `Debug.LogWarning` if `ObjectiveRegistry.Instance.TotalCount == 0` at initialization. | EditMode: scene with zero `ObjectiveToken` prefabs. | Warning logged. No exception or crash. | No warning; or exception thrown. |
| AC-HUD-18 | Gadget Slot Stub (two frames, 30% alpha) is visible at top-left on level load and does not flash, animate, or change appearance for the duration of the level. | PlayMode: load any level, observe top-left corner for entire session. | Two static dim frames visible throughout. | Frames animate, disappear, or change appearance without a gadget event. |
| AC-HUD-19 | When three seekers simultaneously enter Chase (multi-seeker level), the Chase Vignette activates once and `_chasingSeekerCount` equals 3. The vignette does NOT intensify beyond `vignetteMaxAlpha`. | PlayMode (or EditMode test): fire `OnStateChanged(Chase)` on three seeker mocks simultaneously. | `_chasingSeekerCount` == 3. Vignette alpha == `vignetteMaxAlpha` (not higher). Single pulse animation. | `_chasingSeekerCount` wrong; alpha exceeds `vignetteMaxAlpha`; multiple overlapping animations. |
| AC-HUD-20 | All HUD elements that use Coroutines for visual lerp do not allocate heap memory per-frame. No GC alloc appears for HUD Coroutines in the Unity Profiler during steady-state gameplay (all elements in silent/floor state). | Profiler session: play 2 minutes of a level without collection or detection events. Record GC alloc in HUD-tagged Profiler markers. | GC alloc from HUD during steady state = 0 B/frame. | Any GC alloc attributable to HUD Update or Coroutine paths during steady state. |

---

*End of HUD GDD.*
