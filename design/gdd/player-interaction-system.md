# Player Interaction System

> **Status**: Approved
> **Author**: game-designer
> **Last Updated**: 2026-03-05
> **Implements Pillar**: The Room Has Rules (Pillar 1), Legible Jeopardy (Pillar 3)
> **Design Order**: #6 — Foundation Layer

---

## 1. Overview

The Player Interaction System is UNSEEN's unified "press E" framework. It is the single intermediary between the player's interact input and every interactable object in the world — hiding spots, throwables, objective tokens, switches, gadgets, and any future interactive prop. The system runs a cone-filtered overlap query each frame to identify the highest-priority interactable in the player's forward arc, manages tap and hold interactions independently, drives the world-space prompt that tells the player what they can do, and enforces blocking conditions (Chase state, airborne) that prevent interaction at inappropriate moments. It does not own what happens when an interaction fires — each downstream system (Hiding Spot, Throwable, Objective, Environmental Interaction) owns its own response. This system owns only the detection, selection, hold timing, prompt display, and dispatch. All configurable values live in a `PlayerInteractionData` ScriptableObject; nothing is hardcoded in MonoBehaviour fields.

---

## 2. Player Fantasy

**Target MDA Aesthetics**: Challenge (can I reach the objective before the seeker turns?), Discovery (learning which objects respond to interaction), Expression (choosing when to engage the environment as a tactical decision).

When the Player Interaction System is working correctly, the player experiences this:

**Clarity of opportunity.** A faint object highlight and a small icon appear the instant an interactable enters range and the player faces it. The player does not hunt for a prompt — the world announces itself. They can plan their next move before they arrive.

**Weight of commitment.** The wardrobe requires a held press. During that half-second of holding, the player is exposed — the seeker may turn. That held input is a micro-drama. The decision to begin it is meaningful; releasing before it completes is a valid escape. The system creates tension through time, not through UI.

**Fluency.** After two encounters, the player never thinks about the interaction system. They think about the seeker. The system is a transparent layer between intention and action — it must never surprise, confuse, or delay. "I looked at it and pressed E" is the complete mental model. It must remain that simple even when five interactable types exist.

**Pillar 3 alignment (Legible Jeopardy).** The interaction prompt is a world signal, not a HUD overlay. The player reads it from their peripheral vision the same way they read the seeker's posture — it is part of the spatial language of the room. The prompt must be visible in the environment without requiring the player to pull focus from the threat.

*SDT anchor: **Autonomy** — the player must always know what interactions are available so their choice to engage or bypass is informed, never accidental.*

---

## 3. Detailed Rules

### 3.1 IInteractable Interface (Authoritative Definition)

All interactive world objects implement `IInteractable`. This interface is the complete contract; no other interface or base class is required by this system.

```csharp
namespace HideAndSeek
{
    public interface IInteractable
    {
        // --- State ---

        /// <summary>
        /// True if this object can currently be interacted with.
        /// When false, the object is skipped during targeting and the prompt
        /// is either hidden or shown in a disabled visual state.
        /// </summary>
        bool CanInteract { get; }

        /// <summary>
        /// Determines whether interaction requires a sustained hold (true)
        /// or a momentary tap (false).
        /// </summary>
        bool RequiresHold { get; }

        /// <summary>
        /// Duration in seconds the player must hold the interact input before
        /// OnInteractComplete is called. Ignored if RequiresHold is false.
        /// Must return a value > 0 when RequiresHold is true.
        /// </summary>
        float HoldDuration { get; }

        /// <summary>
        /// Short label shown in the world-space interaction prompt.
        /// Examples: "Hide", "Take", "Exit". Max 12 characters.
        /// Displayed alongside the interaction type icon.
        /// </summary>
        string PromptLabel { get; }

        /// <summary>
        /// Icon key that maps to a sprite in PlayerInteractionData.PromptIcons.
        /// Identifies the interaction category visually (e.g., "hide", "pick_up",
        /// "collect", "switch"). Must match a key defined in the data asset.
        /// </summary>
        string PromptIconKey { get; }

        // --- Callbacks ---

        /// <summary>
        /// Called by PlayerInteraction when a tap interaction is confirmed
        /// (RequiresHold == false) or when a hold interaction completes
        /// (RequiresHold == true, hold timer >= HoldDuration).
        /// The interactable owns all downstream behavior from this point.
        /// </summary>
        void OnInteractComplete(PlayerController interactor);

        /// <summary>
        /// Called each frame during an active hold, with normalized progress
        /// [0, 1]. Used to drive interactable-local VFX (e.g., door handle
        /// turning). Optional — default implementation may be empty.
        /// </summary>
        void OnInteractProgress(float normalizedProgress);

        /// <summary>
        /// Called if the player releases the interact button before HoldDuration
        /// is met, or if the interactable leaves range during a hold.
        /// The interactable should reset any in-progress VFX.
        /// Optional — default implementation may be empty.
        /// </summary>
        void OnInteractCancelled();
    }
}
```

**Migration note for existing stub:** The current `IInteractable` in `Assets/_Project/Scripts/Infrastructure/Interfaces/IInteractable.cs` defines only `CanInteract` and `Interact(PlayerController)`. This GDD supersedes that stub. The programmer implementing this system must update `IInteractable.cs` and update `InteractableBase` to implement the new interface. `Interact(PlayerController)` is replaced by `OnInteractComplete(PlayerController)`. `InteractableBase` provides empty default implementations of `OnInteractProgress` and `OnInteractCancelled` so concrete subclasses only override what they need.

**`InteractionPromptAnchor` validation requirement:** Every interactable prefab must include a child `Transform` named `InteractionPromptAnchor` positioned at the appropriate world-space prompt location for that object. `InteractableBase.Awake` must `Debug.Assert(interactionPromptAnchor != null, ...)` to catch missing anchors at scene load in Editor builds. A `ComponentAuditTool` rule should also be added to flag interactable prefabs missing this child Transform during development.

---

### 3.2 Targeting: Cone-Filtered Proximity

**Decision: Hybrid cone (overlap sphere + forward-angle filter).**

Rationale: A pure overlap sphere allows the player to interact with objects behind them, which breaks Pillar 3 (Legible Jeopardy) — the player cannot see the object whose prompt is showing. A pure center-screen raycast fails when the player stands flush against a large interactive object (wardrobe face) and the camera ray misses the trigger collider. The cone model is the standard stealth game solution: proximity gates the candidate set, angle filters to the facing arc, then nearest wins. This matches what players internalize as "I looked at it."

**Rule T-1.** On every `Update` frame while interaction is not blocked (see 3.4), `PlayerInteraction` calls `Physics.OverlapSphereNonAlloc` centered on the player position, with radius `InteractRadius`, using the pre-allocated `Collider[]` buffer and the `InteractableLayer` mask. This produces the candidate set.

**Rule T-2.** For each candidate collider, the system checks:
  - a. `TryGetComponent<IInteractable>` — must succeed.
  - b. `interactable.CanInteract` — must be true.
  - c. The angle between the player's forward vector and the normalized direction from the player to the interactable's world position must be ≤ `InteractAngle` degrees (the half-angle of the detection cone).

**Rule T-3.** Among all candidates passing T-2, the target is the one with the smallest distance to the player. Distance is measured from the player's world position to the candidate's `Collider.bounds.center`. If two candidates share the same nearest distance (rare, resolved by buffer order), the first one returned by the physics query wins.

**Rule T-4.** If no candidates pass T-2, the current target is cleared. The interaction prompt is hidden. Any active hold is cancelled.

**Rule T-5.** Target is re-evaluated every `Update` frame. The current target can change mid-frame if a closer or better-angled candidate enters range. If the target changes while a hold is in progress, the hold is cancelled and `OnInteractCancelled` is called on the previous target.

**Rule T-6.** The targeting sphere is centered on `PlayerInteraction.transform.position` (the player's foot position). The angle check uses the player's forward vector (camera forward projected onto the XZ plane and normalized — so vertical camera angle does not penalize large interactables below the player's eye line).

---

### 3.3 Tap vs. Hold Interaction

Interaction input is split into two distinct event paths:

**Path A — Tap (RequiresHold == false):**
- Triggered when `PlayerInputHandler.OnInteractPerformed` fires (the existing `performed` callback, which fires on button-down).
- If a valid target exists at the moment of the event, `OnInteractComplete` is called immediately on the target.
- No timer is involved.
- Examples: collecting an objective token, picking up a throwable, flipping a light switch.

**Path B — Hold (RequiresHold == true):**
- Hold tracking begins when `PlayerInputHandler.OnInteractStarted` fires (new event: the `started` phase of the Interact action — see 3.3a).
- `PlayerInteraction` increments a `holdTimer` each `Update` frame while:
  - i. The interact button is held (tracked via `OnInteractStarted` / `OnInteractReleased`).
  - ii. The same hold-eligible target remains in range and in the cone.
  - iii. The player is in a non-blocked state (see 3.4).
- Each frame during hold, `target.OnInteractProgress(holdTimer / target.HoldDuration)` is called to drive interactable-local VFX.
- When `holdTimer >= target.HoldDuration`, `target.OnInteractComplete(interactor)` is called and the hold timer resets.
- If the button is released before completion, `target.OnInteractCancelled()` is called and `holdTimer` resets to zero.
- Examples: entering a wardrobe hiding spot, opening a locked gate.

**Rule TH-1.** A tap interaction and a hold interaction cannot be simultaneously active. If `OnInteractPerformed` (tap) fires while a hold is in progress on a tap-eligible target that is different from the hold target, the tap is processed for the tap target and the hold is not interrupted (they are on different targets). If the same target is both tap and hold eligible — which by design cannot occur since `RequiresHold` is a boolean per-object — this conflict cannot arise.

**Rule TH-2.** A hold does not auto-trigger if the player walks into range of a hold interactable while already holding the button. The hold timer only starts on a new `started` event from the Input System after the target becomes active.

#### 3.3a Required Input System Changes

The current `PlayerInputHandler.OnInteractPerformed` is bound to `Interact.performed`. This GDD requires two additional events:

| New Event | Input Phase | Description |
|-----------|------------|-------------|
| `OnInteractStarted` | `started` | Button begins to be held. Used to begin hold timing. |
| `OnInteractReleased` | `cancelled` | Button released. Used to cancel hold timing. |

The existing `OnInteractPerformed` (performed) is retained and continues to drive tap interactions. The three events together give `PlayerInteraction` full hold-state awareness.

**Managed-timer approach:** The Interact action in `InputSystem_Actions.inputactions` remains configured as a default Press interaction (not the built-in Hold interaction). `PlayerInteraction` manages hold timing internally so that different objects can have different `HoldDuration` values without requiring separate Input Action configurations. `OnInteractStarted` (button-down) begins hold timing for hold-eligible targets. `OnInteractPerformed` (performed, same frame as started for Press actions) routes immediately to tap for tap-eligible targets based on `target.RequiresHold`.

**Event ordering note:** In the Unity Input System's event callbacks, `started` fires before `performed` within the same input update cycle. `PlayerInteraction` must read `target.RequiresHold` inside `OnInteractStarted` to decide whether to begin a hold timer. `OnInteractPerformed` processes tap dispatch only if `target.RequiresHold == false` at the moment it fires, avoiding any double-dispatch on tap-eligible targets.

---

### 3.4 Blocking Conditions

The following player states prevent interaction entirely. When blocked, the targeting query still runs so the prompt remains visible (the player can see what they would interact with) but the prompt shows a blocked/unavailable visual state and no input is processed.

| Blocking Condition | Source | Rationale |
|-------------------|--------|-----------|
| Player **is being chased** | `PlayerController.IsBeingChased == true` | Interacting during active chase would trivialize the threat; the seeker's response must be felt |
| Player is **airborne** (jump arc) | `PlayerMovement.IsGrounded == false` | Prevents interacting mid-jump; interaction requires stable body position |
| Player is in **Caught** transition | `PlayerController.CurrentState == CaughtState` | Terminal state; no interaction meaningful |

> **`PlayerController.IsBeingChased`** is a `bool` property set to `true` by the Detection System (via GameManager or a direct C# event) when any active seeker's `RequestedState` transitions to Chase, and cleared when all seekers exit Chase. It is NOT equivalent to the seeker's own Chase behavioral state — it is a player-side flag maintained for external systems to query without coupling to the Seeker AI's internal state machine.

**States that DO allow interaction:**

| State | Interaction Allowed | Notes |
|-------|--------------------|----|
| Standing (Idle) | Yes | Standard case |
| Walking | Yes | Player can pick up while moving |
| Crouching | Yes | Crouch-walk into a hiding spot is the primary stealth pattern |
| Hiding (Inside) | Yes — exit only | When inside a hiding spot, the only valid IInteractable in range is the hiding spot's own exit trigger. The Hiding Spot System is responsible for ensuring no other interactable is reachable from inside. |
| Alert / Searching (Seeker state — player free) | Yes | Player urgency is player-driven |

**Rule B-1.** The blocking check runs before the targeting query is processed for input. The targeting query still executes and the prompt updates, but `HandleInteract`, `HandleHoldStart`, and `HandleHoldRelease` all return early if any blocking condition is active.

**Rule B-2.** If a hold is in progress when a blocking condition activates (e.g., the player enters Chase while mid-hold on a wardrobe), the hold is immediately cancelled. `OnInteractCancelled` fires on the target and `holdTimer` resets.

---

### 3.5 Interaction Prompt Display

The interaction prompt communicates:
1. That an interactable is in range and targeted
2. What the interaction will do (label)
3. Whether it is a tap or hold interaction (icon)
4. Whether the object is currently unavailable despite being nearby (disabled state)
5. Hold progress during an active hold

**Prompt components (world-space billboard, anchored to interactable):**

Each interactable prefab contains a `InteractionPromptAnchor` child Transform at a world position appropriate for the object (above a barrel, at eye level on a wardrobe door, above a floor token). The `InteractionPromptUI` component is instantiated from the prompt prefab in `PlayerInteractionData` and positioned at this anchor.

| Component | Visual | When Shown |
|-----------|--------|-----------|
| Action icon | Sprite from `PromptIcons[PromptIconKey]` | Always when targeted |
| Action label | Short text (max 12 chars), world-space TextMeshPro | Always when targeted |
| Input indicator | "E" key glyph (tap) or fill-ring icon (hold) | Always when targeted |
| Disabled indicator | Icon desaturated, "X" overlay, label grayed | `CanInteract == false` and player within `DisabledPromptRadius` |
| Hold progress ring | Fills over `HoldDuration` seconds | During active hold only |

**Rule P-1.** The prompt is always drawn in world space, billboarded to face the camera. It is NOT a screen-space HUD element. This satisfies Pillar 3 — the player reads the prompt from within the 3D scene without shifting focus to a screen UI layer.

**Rule P-2.** Only one prompt is visible at a time — the current target's prompt. All other interactable prompts in range are hidden. This prevents prompt clutter in areas with multiple interactables.

**Rule P-3.** The prompt fades in over `PromptFadeInTime` seconds when a new target is acquired and fades out over `PromptFadeOutTime` seconds when the target is lost. A minimum stagger of one frame prevents flicker when the target changes rapidly.

**Rule P-4.** The object outline/highlight (URP Render Objects renderer feature with stencil) activates on the current target simultaneously with the prompt. The highlight color distinguishes interactive state: `HighlightColorActive` for available, `HighlightColorDisabled` for `CanInteract == false`. The outline does not appear on non-targeted in-range interactables.

**Rule P-5.** When interaction is blocked (see 3.4), the prompt remains in its current visible state but the input indicator dims to `BlockedInputOpacity` to signal that input is not being processed.

---

### 3.6 Disabled Interactable State

An interactable reports `CanInteract == false` when it is temporarily or permanently unavailable (already collected, locked, requires prerequisite).

**Rule D-1.** Disabled interactables are excluded from targeting (Rule T-2b) but may still show a disabled prompt if the player is within `DisabledPromptRadius` and facing them (same cone filter). This tells the player "this object exists and matters, but you cannot use it right now." Without a disabled prompt, the player cannot distinguish between "nothing here" and "something here that is locked."

**Rule D-2.** A disabled prompt never shows the hold-progress ring. It shows the action icon and label in a grayed/desaturated state with the "X" overlay.

**Rule D-3.** `DisabledPromptRadius` is always ≤ `InteractRadius`. The disabled prompt never appears farther away than normal interaction range. Default: `DisabledPromptRadius == InteractRadius`.

**Rule D-4.** An interactable that transitions from `CanInteract == false` to `CanInteract == true` while the player is already in range immediately becomes eligible for targeting on the next `Update` frame. No re-entry event is required.

---

## 4. Formulas

### F-PI-1: Targeting Cone Filter

```
playerForward_XZ      = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized
directionToCandidate  = Vector3.ProjectOnPlane(
                            (candidate.bounds.center - playerPosition),
                            Vector3.up
                        ).normalized

angleToCandiate       = Vector3.Angle(playerForward_XZ, directionToCandidate)

candidateIsInCone     = angleToCandiate <= InteractAngle
```

| Variable | Type | Range | Source | Description |
|----------|------|-------|--------|-------------|
| `playerForward_XZ` | Vector3 | Unit vector | `transform.forward` projected to XZ | Player forward on horizontal plane |
| `directionToCandidate` | Vector3 | Unit vector | Candidate bounds center → player | Horizontal direction to candidate |
| `angleToCandiate` | float | 0–180° | Computed | Angle between forward and direction to candidate |
| `InteractAngle` | float | 30–90° | `PlayerInteractionData` | Half-angle of detection cone; default 60° |

**Example:**
Player faces north (forward = (0,0,1)). Candidate A is 1.2 m north-east (direction = (0.71, 0, 0.71), angle = 45°). Candidate B is 0.8 m east (direction = (1, 0, 0), angle = 90°). With `InteractAngle = 60°`:
- Candidate A: 45° ≤ 60° → passes. Distance = 1.2 m.
- Candidate B: 90° ≤ 60° → fails. Excluded.
- Result: Candidate A is the target.

---

### F-PI-2: Target Priority (Nearest Wins)

```
priority(candidate)   = Vector3.Distance(playerPosition, candidate.bounds.center)
target                = candidate with minimum priority score
```

No dot-product weighting — pure nearest distance among cone-filtered candidates. A weighted scheme (favoring more directly-forward objects) was evaluated and rejected: it produces unintuitive results when a close object is slightly off-center vs. a farther object dead-ahead, violating Pillar 1 (learnable, predictable behavior).

| Variable | Type | Range | Description |
|----------|------|-------|-------------|
| `playerPosition` | Vector3 | World space | `PlayerInteraction.transform.position` |
| `candidate.bounds.center` | Vector3 | World space | Physics collider bounds center of candidate |

---

### F-PI-3: Hold Completion

```
holdTimer            += Time.deltaTime   // each Update frame while button held and target stable

normalizedProgress    = Mathf.Clamp01(holdTimer / target.HoldDuration)
// passed to target.OnInteractProgress(normalizedProgress) each frame

interactionComplete   = holdTimer >= target.HoldDuration
```

| Variable | Type | Range | Source | Description |
|----------|------|-------|--------|-------------|
| `holdTimer` | float | 0 → HoldDuration | Runtime | Accumulated hold time in seconds; resets on complete or cancel |
| `target.HoldDuration` | float | 0.3–2.0s | `IInteractable` implementor | Per-object required hold time |
| `normalizedProgress` | float | 0.0–1.0 | Computed | Passed to `OnInteractProgress` each frame for VFX |
| `interactionComplete` | bool | — | Computed | True when `holdTimer >= target.HoldDuration` |

**Example:** Wardrobe with `HoldDuration = 0.5 s`. Player begins hold at t=0. At t=0.25 s: `normalizedProgress = 0.5`, `OnInteractProgress(0.5)` called. At t=0.5 s: complete, `OnInteractComplete` fires. If button released at t=0.3 s: `OnInteractCancelled` fires, timer resets to 0.

---

### F-PI-4: Disabled Prompt Eligibility

```
showDisabledPrompt =
    !interactable.CanInteract
    AND distance(playerPosition, candidate.bounds.center) <= DisabledPromptRadius
    AND angleToCandiate <= InteractAngle
```

A disabled object shows its prompt only if it would have been in the targeting cone had it been active. This ensures the player only sees disabled prompts for objects they are deliberately facing.

---

## 5. Edge Cases

| Scenario | Expected Behavior | Rationale |
|----------|------------------|-----------|
| Player begins hold on wardrobe; seeker transitions to Chase mid-hold | On the next `Update` frame after Chase state activates: `OnInteractCancelled` fires immediately, `holdTimer` resets, prompt input indicator dims. The wardrobe remains interactable — if Chase ends, the player may retry. | Rule B-2. Blocking conditions cancel active holds to prevent the player from "banking" a hide entry before Chase resolves. |
| Two interactables equidistant and both in cone (e.g., two adjacent barrels) | `OverlapSphereNonAlloc` returns candidates in implementation-defined order. `PlayerInteraction` selects the first candidate at the minimum distance. If distances are exactly equal, the first in the `_hitBuffer` array wins. Deterministic per session; may vary across Unity versions. | Level designers must not place two interactables of the same type within 0.5 m without intentional design. Use `OverlapChecker` editor tool to flag this during authoring. |
| Player inside hiding spot — what is interactable? | The Hiding Spot System sets its own `CanInteract` to `true` for the exit trigger and ensures all other nearby `IInteractable` objects return `CanInteract == false` while the player is inside. `PlayerInteraction` requires no special-case Hiding logic — it targets whatever `CanInteract` reports. | Single-responsibility: `PlayerInteraction` runs the targeting and dispatch. `HidingSpot` controls its own availability state. |
| Player taps a throwable, then immediately faces a wardrobe and holds | Tap fires on throwable (instant); throwable's `CanInteract` becomes false. Next frame, targeting finds the wardrobe. If player holds, hold timer begins normally. No conflict — sequential interactions on different objects. | Hold timer only starts on a new `OnInteractStarted` event after the wardrobe becomes the active target. |
| `_hitBuffer` overflows (more than `HitBufferSize` interactables in range) | `OverlapSphereNonAlloc` silently truncates to the buffer size (default 8). Objects beyond index 7 are not considered. The nearest in-buffer candidate that passes cone filter wins. | `InteractRadius` of 1.5 m makes overflow a level design error (flagged by `ComponentAuditTool` or `OverlapChecker`). Degraded behavior is acceptable; the player still interacts with the closest visible object. |
| `RequiresHold == true` but `HoldDuration == 0` on an interactable | `InteractableBase.Awake` logs `Debug.Assert(HoldDuration > 0)` in Editor builds. At runtime, if `HoldDuration == 0`, `OnInteractComplete` fires immediately on button-down (effectively a tap). Log a warning; do not throw an exception. | Configuration error must not crash the game. Warning surfaces the misconfiguration for the level designer or programmer to fix. |
| Player is inside hiding spot; an external interactable is within `InteractRadius` due to geometry overlap | The Hiding Spot System sets external interactables to `CanInteract == false` while the player is inside. `PlayerInteraction` targets the hiding spot's exit trigger only. If the Hiding Spot System fails to set this, the player may interact with the external object — this is a Hiding Spot System bug, not a Player Interaction System bug. | Ownership boundary: `PlayerInteraction` does not know about "inside vs. outside a hiding spot." It dispatches to whatever `CanInteract` reports. |
| `OnInteractPerformed` fires on the exact frame a target's `CanInteract` flips to false (race condition) | `HandleInteract` re-checks `target.CanInteract` immediately before calling `OnInteractComplete`. If false at call time, the interaction is silently ignored — no double-fire, no crash. | The dispatch-time guard prevents stale targeting from producing an interaction the system no longer permits. |
| Player holds the interact button while airborne (jumped while already holding E) | `OnInteractStarted` fires but `HandleHoldStart` returns early due to the airborne blocking condition (Rule B-1). `holdTimer` does not begin. On landing, `IsGrounded` becomes true, but no hold timer starts automatically — the player must release and re-press (Rule TH-2 governs this). No interaction fires; the prompt remains visible while airborne to indicate the object is still there. | This is the most common accidental "try to interact mid-jump" input. The system correctly ignores it without closing off the option — the player can immediately re-press on landing. |
| `PromptIconKey` returned by an `IInteractable` does not match any key in `PlayerInteractionData.PromptIcons` | The system logs `Debug.LogWarning($"PromptIconKey '{key}' not found in PlayerInteractionData.PromptIcons — using DefaultPromptIcon.")` and displays the `DefaultPromptIcon` sprite instead. No exception. The prompt label and hold/tap indicator are still shown correctly; only the icon falls back. | Configuration error must not crash the game. The fallback icon prevents a blank/null prompt while the warning surfaces the typo for the implementor to fix. |

---

## 6. Dependencies

### 6.1 What This System Requires

| Dependency | System | What Is Required | Direction |
|-----------|--------|-----------------|-----------|
| `PlayerInputHandler` | Player | `OnInteractPerformed` (performed), `OnInteractStarted` (started), `OnInteractReleased` (cancelled) C# events | Inbound |
| `PlayerController` | Player | `IsBeingChased` and `CurrentState` (for Caught check) for blocking conditions; passed as `interactor` to `OnInteractComplete` | Inbound |
| `PlayerMovement` | Player | `IsGrounded` for airborne blocking check | Inbound |
| `IInteractable` | Infrastructure | Interface definition; all interactive objects implement it | Interface contract |
| `InteractableLayer` | Unity Physics | Physics layer mask containing all interactive object colliders | Config |
| `PlayerInteractionData` | Data | ScriptableObject with all tuning knobs and prompt icon registry | Config |
| `InteractionPromptPrefab` | Art/UI | World-space billboard prefab (TextMeshPro + icon Image + fill-ring Image) | Asset |

### 6.2 What This System Provides

| Provided To | System | What Is Provided | Direction |
|------------|--------|-----------------|-----------|
| Hiding Spot System | Gameplay | `OnInteractComplete(PlayerController)` called when hold completes on a hiding spot `IInteractable` | Outbound |
| Throwable Object | Gameplay | `OnInteractComplete(PlayerController)` called when tap fires on a throwable `IInteractable` | Outbound |
| Objective System | Gameplay | `OnInteractComplete(PlayerController)` called when tap fires on an objective token `IInteractable` | Outbound |
| Environmental Interaction | Gameplay | `OnInteractComplete(PlayerController)` called on switches, light sources, etc. | Outbound |
| Stealth Toolkit / Gadgets | Gameplay | `OnInteractComplete(PlayerController)` called on gadget world objects | Outbound |

### 6.3 Ownership Boundaries (Explicit)

The following are explicitly NOT owned by the Player Interaction System:

| Item | Actual Owner |
|------|-------------|
| What happens when a hiding spot is entered | Hiding Spot System |
| What happens when an objective is collected | Objective System |
| What happens when a throwable is picked up | Throwable Object System |
| NoiseEvent emitted at interaction | Player Noise Emitter (subscribes to `PlayerInputHandler.OnInteractPerformed` as a parallel listener) |
| Sound effects that play on interaction | Downstream systems (each owns its own `AudioManager` call) |
| URP outline Renderer Feature configuration | Render pipeline config; `PlayerInteraction` sets the stencil state, but the feature is owned by the render pipeline asset |

---

## 7. Tuning Knobs

All values below are fields of `PlayerInteractionData` (`Assets/_Project/Scripts/Data/PlayerInteractionData.cs`), serialized as a ScriptableObject. No value is hardcoded in `PlayerInteraction.cs` or `InteractableBase.cs`.

| Knob | Type | Default (Warden) | Safe Range | Category | Extreme Low Effect | Extreme High Effect |
|------|------|-----------------|------------|----------|--------------------|---------------------|
| `InteractRadius` | float | 1.5 m | 0.8–2.5 m | Gate | Player must nearly collide with object; frustrating for large objects like wardrobes | Player interacts with objects not visually prominent; breaks legibility |
| `InteractAngle` | float | 60° | 30–90° | Gate | Requires near-pixel-perfect aim; frustrating | Cone becomes hemisphere; player interacts with objects behind them, breaking Pillar 3 |
| `DisabledPromptRadius` | float | 1.5 m | 0.5–2.5 m | Gate | Disabled prompts disappear too early; player can't see what is locked | Has no benefit beyond `InteractRadius`; cap at `InteractRadius` |
| `HitBufferSize` | int | 8 | 4–16 | Gate | Risk of missed candidates in dense areas | Higher per-frame stack memory; diminishing returns above 12 |
| `PromptFadeInTime` | float | 0.1 s | 0.0–0.3 s | Feel | Instant pop; may feel startling | Noticeable lag before prompt appears; player may miss interactable |
| `PromptFadeOutTime` | float | 0.08 s | 0.0–0.2 s | Feel | Instant disappear; no issue | Prompt lingers after target lost; confusing |
| `PromptBillboardOffset` | Vector3 | (0, 0.1, 0) | World units | Feel | N/A — art-authored per interactable via `InteractionPromptAnchor` | N/A |
| `HighlightColorActive` | Color | White (1,1,1,1) | — | Feel | Too dark: highlight invisible | Too saturated: visual noise |
| `HighlightColorDisabled` | Color | Gray (0.4,0.4,0.4,1) | — | Feel | Indistinguishable from active | Too prominent; calls attention to locked objects over available ones |
| `BlockedInputOpacity` | float | 0.5 | 0.1–1.0 | Feel | Input indicator nearly invisible; player can't tell they're blocked | No visual distinction from unblocked; blocking state is illegible |
| `ConeHysteresisBuffer` | float | 0° | 0–15° | Feel | No hysteresis; target drops instantly when angle crosses threshold (default, most predictable) | Large buffer retains targets that are no longer in the intended facing arc; feels sticky | Once a target is acquired, it is held until the player-to-candidate angle exceeds `InteractAngle + ConeHysteresisBuffer`. Set to 0° by default. Increase if playtesting shows targets dropping mid-hold during close-range wardrobe approach animations. |
| `DefaultPromptIcon` | Sprite | (question mark sprite) | — | Config | Fallback sprite shown when `PromptIconKey` does not match any entry in `PromptIcons`. Must be non-null; assign in the ScriptableObject inspector. | N/A |

**Per-interactable knobs (on the object itself, not in PlayerInteractionData):**

| Knob | Location | Default | Safe Range | Notes |
|------|----------|---------|-----------|-------|
| `HoldDuration` | `IInteractable` implementor | 0.5 s (wardrobe) | 0.3–2.0 s | Must be > 0 when `RequiresHold == true`. Wardrobe: 0.5 s. Puzzle gate: up to 1.5 s. |
| `PromptLabel` | `IInteractable` implementor | Object-specific | Max 12 chars | "Hide", "Take", "Collect", "Open", "Exit" |
| `PromptIconKey` | `IInteractable` implementor | Object-specific | Must match `PlayerInteractionData.PromptIcons` | "hide", "pick_up", "collect", "switch" |

---

## 8. Acceptance Criteria

### Functional (pass/fail, verifiable in Play Mode)

- [ ] **AC-01 — Tap fires immediately:** An objective token (tap-eligible) at 1.0 m in front of player. Press interact once. `OnInteractComplete` is called on the same frame the button is pressed. No delay, no double-fire.
- [ ] **AC-02 — Hold completes only after HoldDuration:** Wardrobe (`RequiresHold=true`, `HoldDuration=0.5s`) at 1.0 m. Hold for 0.6 s. `OnInteractComplete` fires at or after t=0.5 s and not before.
- [ ] **AC-03 — Hold cancels on early release:** Same wardrobe. Hold for 0.3 s, release. `OnInteractCancelled` fires; `OnInteractComplete` does not fire.
- [ ] **AC-04 — Cone filter excludes objects behind player:** Tap interactable at 1.0 m directly behind player (180° from forward). Press interact. No interaction fires and no prompt is shown.
- [ ] **AC-05 — Nearest candidate wins:** Interactable A at 0.8 m (in cone), interactable B at 1.4 m (in cone, same angle). Press interact. `OnInteractComplete` fires on A only.
- [ ] **AC-06 — Interaction blocked when being chased:** `PlayerController.IsBeingChased == true`. Tap interactable at 1.0 m. Press interact. No interaction fires. Prompt is visible with dimmed input indicator.
- [ ] **AC-07 — Interaction blocked when airborne:** Player mid-jump (`IsGrounded == false`). Tap interactable 1.0 m ahead. Press interact. No interaction fires.
- [ ] **AC-08 — Disabled interactable shows disabled prompt but is not interactable:** `CanInteract == false` object at 1.0 m. Disabled prompt shown (grayed icon, X overlay). Interact input produces no call to `OnInteractComplete`.
- [ ] **AC-09 — Hold cancels when blocking condition activates mid-hold:** Begin hold on wardrobe (`HoldDuration=0.5s`). At t=0.2 s, trigger Chase state. `OnInteractCancelled` fires. Hold does not complete.
- [ ] **AC-10 — Hold cancels when target leaves cone mid-hold:** Begin hold on wardrobe. Rotate player so wardrobe exits the `InteractAngle` cone. `OnInteractCancelled` fires. `holdTimer` resets. Prompt disappears.
- [ ] **AC-11 — No per-frame GC allocations:** Profile in Unity Profiler with player walking near 3 interactables. `PlayerInteraction.Update` shows 0 B GC Alloc per frame in the Profiler.
- [ ] **AC-12 — Buffer overflow produces no crash:** 10 interactables within `InteractRadius`. Enter Play Mode. No exception thrown. Targeting selects one of the buffered candidates. Game runs normally.
- [ ] **AC-13 — No hardcoded values:** Code review finds zero numeric literals in `PlayerInteraction.cs` or `InteractableBase.cs`. All values reference `PlayerInteractionData` fields.

### Experiential (validated via observed play sessions)

- [ ] **AC-14 — First-time player identifies an interactable without instruction:** Naive player in a room with one wardrobe, no other objects, 60-second timer. Target: ≥80% of testers press interact on the wardrobe without being told what to press.
- [ ] **AC-15 — Player correctly reads disabled state:** Room with one already-collected objective token (disabled). Player enters. Target: ≥80% of testers report "there's something there but I can't get it" rather than "there's nothing here."
- [ ] **AC-16 — Hold input feels intentional:** After 15 minutes with the system, ≤20% of players report entering a hiding spot accidentally. If exceeded, increase `HoldDuration` toward 0.7 s rather than reducing the hold requirement.
- [ ] **AC-17 — Blocked prompt during chase does not read as broken:** After a chase sequence, ask players: "When you couldn't enter the wardrobe during the chase, did you understand why?" PASS: ≥75% of players report understanding the block (saw the dimmed indicator or attributed it to the chase). FAIL: players report "the wardrobe stopped working" or "E wasn't responding." If >25% report confusion, evaluate hiding the prompt entirely during `IsBeingChased` rather than dimming it — retest both approaches with fresh players before committing.
