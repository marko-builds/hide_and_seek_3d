# Level Exit System

> **Status**: Approved
> **Author**: game-designer
> **Last Updated**: 2026-03-05
> **Implements Pillar**: Two-Beat Tension (Pillar 4), Legible Jeopardy (Pillar 3)
> **Design Order**: #10 — Feature Layer

---

## 1. Overview

The Level Exit System is the physical destination of Phase 2 in UNSEEN — the interactable object that ends the level when the player reaches it after collecting all objectives. Each level contains exactly one `LevelExit` prefab placed by the level designer. The exit begins every level in a **Locked** state: visible, identifiable as the exit, but not interactable. When `ObjectiveRegistry.OnAllObjectivesCollected` fires, the exit transitions to an **Unlocked** state through a VFX/SFX sequence, its `CanInteract` property flips to true, and the player can now use it. Using the exit calls `GameManager.Instance.TriggerWin()`, fires the `OnExitUsed` C# event, and ends the level. The system owns only two responsibilities: managing the locked/unlocked state and dispatching the win trigger. Everything downstream — win screen display, session stats, seeker escalation — belongs to other systems that subscribe to the exit's event or receive the GameManager signal. All configurable values live in a `LevelExitData` ScriptableObject; nothing is hardcoded.

---

## 2. Player Fantasy

**Target MDA Aesthetics**: Challenge (navigating a now-fully-alerted dungeon to reach a specific point), Sensation (the visceral relief of the exit glow snapping on after the last relic is taken), Narrative (the exit is what the seekers were preventing you from reaching — their purpose and yours collide here).

### The Exit as a Spatial Anchor

Phase 1 of every UNSEEN level is a read-the-room experience. The player surveys the chamber, locates the objective, and constructs a plan. During this survey, the Level Exit must be visible and legible: a sealed door, a dark gate, a dormant portal — something the player looks at during their initial observation pass and immediately understands as "that is how I leave." This legibility is not a UI affordance; it is spatial design and visual language. The exit's Locked visual state communicates its nature at a distance without requiring proximity interaction.

The exit being present but locked throughout Phase 1 creates a persistent subtext: "You know where you're going. You cannot go yet." This is not frustrating because the player also knows why — the objective is the key. The distance between the player and the exit is a measure of what they still have to accomplish. This makes the exit a compass.

### The Phase 2 Start Signal

The moment Phase 2 begins is, more than anything else, the moment the exit light turns on. The player collects the last relic; the `OnAllObjectivesCollected` event fires; the exit responds. From across the room — or from around a corner, through a grating, from a distant visual line — the player sees or hears the exit activate. This spatially anchors the Phase 2 escalation: "Go there. Go now."

This is Pillar 4 (Two-Beat Tension) made physical. The seeker escalation and music shift are atmospheric. The exit glow is actionable. The player's goal inverts from "careful approach to the relic" to "sprint to that light." The exit is the destination that gives Phase 2 its urgency and its direction.

### The Exit Use: Hold Interaction

**Design decision: the exit uses a hold interaction (`RequiresHold == true`, `HoldDuration` from `LevelExitData`, default 0.8 s).**

This decision requires justification because it diverges from the objective token's tap, and the choice has real consequences for the Phase 2 experience.

The objective tap is a nerve-driven, single-frame act: close the distance, commit. The danger was reaching the token; the act of taking is instantaneous. Collection rewards precise approach timing.

The exit hold is structurally different. By Phase 2, the level has changed. Seekers are alerted, potentially converging. The player who sprints across the chamber and arrives at the exit is not yet safe — they must stand at the exit and hold for 0.8 seconds. This is intentional: the exit demands a micro-moment of commitment at a time when commitment is costly. The 0.8 seconds while the hold completes is a final beat of legible jeopardy. The player can hear the seeker. They can see whether they have time. The hold ring filling under pressure is a form of dramatic tension that a tap interaction cannot produce.

The hold also prevents accidental exit triggering mid-Phase 2 traversal. A player sprinting past the exit while the seeker is nearby should not inadvertently end the level. The hold guarantees the exit use is deliberate.

**Comparison to hiding spot hold (0.5 s default):** Hiding is entered under pressure and requires a quick commitment. Exit is the terminal act and merits a slightly longer, more ceremonial beat. The 0.8 s default is close to the hiding spot's 0.5 s to avoid frustration, but notably longer to signal "this is the end."

*SDT anchors: **Autonomy** — the player chooses when to commit to the hold relative to the seeker's position. **Competence** — successfully holding through a near-catch moment delivers a peak competence signal: "I made it with the seeker right there."*

### The Chase Decision: Override PIS Rule B-1 for the Exit

**Design decision: the Level Exit overrides PIS Rule B-1 (Chase blocks all interaction). The exit remains interactable (`CanInteract == true`) during active Chase.**

PIS Rule B-1 blocks all `IInteractable` dispatch when `PlayerController.IsBeingChased == true`. For most interactables this is correct design: using a hiding spot during a chase requires breaking the chase first, which forces the player to manage seeker state. That rule creates depth.

The exit is a terminal act. There is no further gameplay after exit use. The reason PIS Rule B-1 exists is to prevent interaction from trivializing the seeker threat — but trivialization requires continued gameplay. At the exit, the level is ending. The question is not "should the player manage seeker state?" but "should the player be punished for reaching the exit while being chased?"

Accepting PIS Rule B-1 for the exit would create a situation where the player has navigated the entire Phase 2 gauntlet, reached the door, and is blocked from winning because a seeker entered Chase in the last 10 meters. That is not legible jeopardy — it is an opaque soft wall. The player performed a skilled Phase 2 run and reached the objective location; blocking them there violates Pillar 3.

The "last-second escape" override instead creates a cinematically satisfying narrative: the player sprints to the exit with the seeker in pursuit, initiates the hold, the seeker closes in, and the level ends. This is the intended climax of a Phase 2 run. The hold interaction (0.8 s) provides the final tension beat; Chase does not need to block it.

**Implementation requirement:** `LevelExit.CanInteract` explicitly ignores `PlayerController.IsBeingChased` in its implementation. This is the only `IInteractable` in the game with this override. The implementor must ensure this is documented and not "fixed" as an accidental omission.

---

## 3. Detailed Rules

### 3.1 Component Overview

| Component | Type | Responsibility |
|-----------|------|---------------|
| `LevelExit` | `MonoBehaviour : InteractableBase, IInteractable` | Two-state exit object. Locked by default. Subscribes to `ObjectiveRegistry.OnAllObjectivesCollected` to unlock. Dispatches `GameManager.TriggerWin()` and `OnExitUsed` on interact complete. |
| `LevelExitData` | `ScriptableObject` | Per-level configuration: VFX prefab references, SFX key references, prompt label variants, display name, hold duration. |
| `InteractableBase` | `abstract MonoBehaviour` | Project-wide base class; provides empty default implementations of `OnInteractProgress` and `OnInteractCancelled`. `LevelExit` inherits these and overrides only what it needs. |

---

### 3.2 LevelExit: IInteractable Implementation

`LevelExit` implements `IInteractable` with the following properties:

| Property | Locked State (Phase 1) | Unlocked State (Phase 2) | Rationale |
|----------|----------------------|--------------------------|-----------|
| `CanInteract` | `false` | `true` | Locked: cannot be used. Unlocked: can be used. Chase does NOT override to false (see Section 2). |
| `RequiresHold` | `false` | `true` | Locked: no hold because no interaction is possible. Unlocked: hold required. See Section 2 design rationale. |
| `HoldDuration` | `0` (ignored) | From `LevelExitData.ExitHoldDuration` (default 0.8 s) | Hold duration is only meaningful in unlocked state. |
| `PromptLabel` | From `LevelExitData.LockedPromptLabel` (default: `"Locked"`) | From `LevelExitData.UnlockedPromptLabel` (default: `"Exit"`) | Each state communicates its nature. |
| `PromptIconKey` | `"exit"` | `"exit"` | Same icon category; visual state differentiation handled by `CanInteract == false` disabled rendering. |

**Why show a disabled prompt in Locked state?** PIS Rule D-1 governs this: when `CanInteract == false`, the prompt is shown in a disabled state (desaturated icon, X overlay, grayed label) if the player is within `DisabledPromptRadius` and facing the exit. The locked prompt communicates "this is the exit and you cannot use it yet" — it does not suppress information, it contextualizes it. This serves Pillar 3 (Legible Jeopardy): the player who walks up to the locked exit before collecting the objective understands the goal rather than experiencing confusion.

**Prompt label in locked state:** `"Locked"` with padlock icon variant (handled by the disabled state rendering in PIS). The X overlay from PIS disabled rendering is sufficient; no additional locked-specific icon is required if the art team provides a distinct locked visual state on the exit mesh/material. If the art team does not differentiate the mesh state, consider `"Locked"` label as the primary legibility signal.

---

### 3.3 Locked State (Phase 1)

**Rule LE-1 (Default Locked State).** `LevelExit` is in the **Locked** state by default on Awake. `_isUnlocked = false`. `CanInteract` returns false. No player interaction is processed.

**Rule LE-2 (Visual State: Locked).** In Locked state, the exit displays its Locked visual: the `LevelExitData.LockedMeshVariant` material or shader state is applied. A `LockedParticleSystem` (from `LevelExitData`) runs in idle-loop mode on the exit — a subtle ambient effect (dim, cool-colored, minimal) that makes the exit identifiable from a distance without overwhelming Phase 1 atmosphere. This particle system is started in Awake and loops until the unlock transition fires.

**Rule LE-3 (Disabled Prompt).** When the player enters `DisabledPromptRadius` and faces the exit while it is locked, PIS Rule D-1 displays the disabled prompt: desaturated `"exit"` icon, X overlay, and the `LevelExitData.LockedPromptLabel` text (default: `"Locked"`). The player sees this, understands the exit exists, and understands they cannot use it. No further logic is required from the Level Exit System — PIS drives this display from `CanInteract == false`.

---

### 3.4 Unlock Trigger and Transition

**Rule LE-4 (Subscription).** `LevelExit.Awake` subscribes to `ObjectiveRegistry.Instance.OnAllObjectivesCollected`. If `ObjectiveRegistry.Instance` is null at Awake, a `Debug.LogError` is emitted (see Edge Cases 5.4). The subscription uses a private method `HandleAllObjectivesCollected()` called synchronously when the event fires.

**Rule LE-5 (Unlock Sequence).** When `HandleAllObjectivesCollected()` is invoked:
1. Set `_isUnlocked = true`.
2. Set `CanInteract = true`.
3. Stop `LockedParticleSystem` (stop, do not destroy — pooled or deactivated via `ParticleSystem.Stop()`).
4. Start `UnlockParticleSystem` (from `LevelExitData.UnlockVFXPrefab`) instantiated at the exit's world position. This is a one-shot effect (not looping) that plays for approximately 1.5–2.5 s before self-destroying.
5. Start `UnlockedAmbientParticleSystem` (from `LevelExitData.UnlockedAmbientVFXPrefab`) as a looping particle system that replaces the locked idle loop — brighter, warmer-colored, clearly "active."
6. Apply `LevelExitData.UnlockedMeshVariant` material/shader state to the exit mesh — this is the primary visual state change that communicates from across the room.
7. Play `LevelExitData.UnlockSFXKey` via `AudioManager.Instance.Play(UnlockSFXKey)` — a distinct audio event (mechanism sound, power surge, gate unsealing) that confirms the unlock to players who may not have a direct line of sight to the exit at the moment of collection.
8. Optionally: invoke a coroutine `PlayUnlockAnimation()` if the exit type has an animated transition (e.g., a gate retracting, a seal breaking). This is authored on the `LevelExit` component and is not required by the system contract — it is an art-driven extension point.

**Unlock transition visual description (for technical artist implementation):**

The locked state is: dim, cool blue-grey particle ambience, mesh material at 20% emission intensity (a dark, dormant glow). The unlock transition is: a burst particle system (white-to-gold, outward explosion, radius 1.5–2.0 m, 0.5 s peak, 2.0 s total, self-destroying) fires simultaneously with a material emission ramp (emission intensity 20% → 100% over 0.6 s, via a tween or a shader parameter driven by `LevelExit.PlayUnlockAnimation`). The unlocked ambient state is: bright, warm (gold-white) particle loop, full emission mesh. The visual difference between locked and unlocked must be readable from 15+ meters in standard dungeon lighting. A player who cannot see the exit but sees the light change on adjacent geometry must register "something changed."

---

### 3.5 Unlocked State (Phase 2) and Win Trigger

**Rule LE-6 (Unlocked Interactability).** In Unlocked state: `CanInteract == true`. PIS targeting includes the exit in the candidate set. When the player enters range and faces the exit, the active prompt shows: `"Exit"` label, `"exit"` icon in active state, hold-ring indicator (since `RequiresHold == true`).

**Rule LE-7 (Chase Does Not Block Exit Use).** `LevelExit.CanInteract` returns true regardless of `PlayerController.IsBeingChased`. This is an explicit override of the normal PIS blocking behavior. See Section 2 for the design rationale. Implementation: `LevelExit.CanInteract` property implementation must NOT check `PlayerController.IsBeingChased`. The PIS dispatch-time blocking (Rule B-1) only suppresses input for the currently targeted object when `IsBeingChased` — but only if `CanInteract` cooperates with the block. Because `LevelExit` is intended to remain interactable during Chase, the PIS blocking must be bypassed for this specific interactable.

**Technical implementation of Chase override:** The PIS `HandleInteract` method checks `IsBeingChased` before dispatching, returning early if true (Rule B-1). To override this for `LevelExit`, one of two approaches is acceptable:
- **Option A (Preferred):** `LevelExit` implements an additional marker interface `IChaseInteractable` (or similar). `PlayerInteraction.HandleInteract` checks for this interface on the current target before applying the Chase block — if the target is `IChaseInteractable`, the Chase block is skipped.
- **Option B:** `PlayerInteraction` exposes a method `SetChaseInteractionOverride(IInteractable target, bool allowDuringChase)` and `LevelExit` registers itself as Chase-exempt during its own Awake.

The programmer implementing this system must choose one option and document the decision. Option A is preferred because it is self-contained: the `IChaseInteractable` interface is the complete contract, and any future interactable that needs Chase exemption can implement it without modifying `PlayerInteraction`.

**Rule LE-8 (OnInteractProgress).** During the hold, `OnInteractProgress(float normalizedProgress)` is called each frame by PIS. `LevelExit` uses this to drive a visual feedback effect: the exit's `ExitProgressParticleSystem` or a shader parameter increases in intensity as `normalizedProgress` increases from 0 to 1. This gives the player tactile confirmation that the hold is proceeding. `LevelExit.OnInteractProgress` must not be empty in the final implementation — a blank hold with no progress feedback is insufficiently readable.

**Rule LE-9 (Win Trigger Sequence).** When `LevelExit.OnInteractComplete(PlayerController interactor)` is called by PIS (hold complete):
1. Set `CanInteract = false` immediately — prevents double-fire if PIS has any re-evaluation latency.
2. Play `LevelExitData.ExitSFXKey` via `AudioManager.Instance.Play(ExitSFXKey)` — the "escape audio" (gate opening, whoosh, escape sting).
3. Start `ExitVFXPrefab` instantiated at the exit's world position — a one-shot VFX confirming exit use (portal flash, door swinging open animation trigger, etc.).
4. Fire `OnExitUsed` C# event (see Rule LE-10).
5. Call `GameManager.Instance.TriggerWin()`.

Steps 4 and 5 must execute in this order. `OnExitUsed` fires before `TriggerWin()` so that subscribers can complete any synchronous bookkeeping (stat recording, etc.) before the GameManager begins the win transition that may interrupt frame execution.

**Rule LE-10 (OnExitUsed Event Contract).** `LevelExit` exposes a public C# event:

```csharp
public event Action OnExitUsed;
```

This event fires synchronously from `OnInteractComplete`, before `GameManager.TriggerWin()`. Subscribers must not yield or perform heavy processing in the handler — schedule any work via coroutine or queued state change. The event carries no argument; subscribers that need the exiting player reference must obtain it from `GameManager` or `PlayerController.Instance` if necessary.

**Intended subscribers to `OnExitUsed`:**
- **Two-Phase Level Structure** — receives the signal that the level is complete; can stop escalation logic, cancel seeker-acceleration coroutines, and halt music changes.
- **Level Timer + Stats** — records the timestamp at exit use for session stat reporting (time-to-exit). Subscribes synchronously; stat recording is instantaneous.

Both subscriptions are synchronous. No async subscribers are permitted for `OnExitUsed`. The event is fire-and-forget from the Level Exit System's perspective.

**Rule LE-11 (One-Shot Use).** Once `OnInteractComplete` fires and `CanInteract` is set to false in step 1, the exit cannot be used again within the same level session. `TriggerWin()` will transition `GameManager.CurrentState` to `Win`, and the Level Exit's own `CanInteract` remains false permanently.

---

### 3.6 Level Designer Contract

**Rule LE-12 (One Exit Per Level).** Each level scene contains exactly one `LevelExit` prefab. Two exits in the same scene results in both subscribing to `OnAllObjectivesCollected` and both firing `TriggerWin()` — `GameManager` must handle `TriggerWin()` idempotently (see Edge Cases 5.5), but two exits is a level design error. A `ComponentAuditTool` rule must flag scenes with more than one `LevelExit` component.

**Rule LE-13 (Placement Requirements).** The Level Exit must be placed at a location:
- Visible from the player's starting position or discoverable in the first 30 seconds of exploration (supports the "compass" fantasy described in Section 2).
- Not immediately adjacent to the objective token(s). The spatial separation between "what I need to collect" and "where I need to go" is the core geography of Phase 1 tension.
- Physically reachable by the player in Phase 2 without requiring a route blocked by the seeker's starting position. The level designer must verify that a viable Phase 2 path exists (not necessarily easy — just possible).

**Rule LE-14 (InteractionPromptAnchor).** The `LevelExit` prefab must include a child `Transform` named `InteractionPromptAnchor`, per PIS requirements. Position it at approximately eye height (1.5–1.8 m) on the exit structure so the prompt is visible at approach distance.

**Rule LE-15 (LevelExitData Assignment).** The `LevelExit` prefab must have a `LevelExitData` asset assigned in the serialized `_exitData` field. `LevelExit.Awake` must `Debug.Assert(_exitData != null, ...)` to catch unassigned data in Editor builds.

---

## 4. Formulas

### F-LE-1: CanInteract Eligibility

`LevelExit.CanInteract` is defined as:

```
CanInteract = _isUnlocked
              AND GameManager.Instance.CurrentState == GameState.Playing
```

Note what is explicitly ABSENT from this formula: `NOT PlayerController.IsBeingChased`. This is the Chase override described in Rule LE-7 and Section 2. It is the only `IInteractable` in the MVP that omits this condition.

| Variable | Type | Source | Description |
|----------|------|--------|-------------|
| `_isUnlocked` | bool | `LevelExit` runtime state | Set to false on Awake; set to true when `OnAllObjectivesCollected` fires |
| `GameManager.Instance.CurrentState` | `GameState` enum | `GameManager` | Must be `Playing`; blocks use during Win, Lose, Warmup |

**Example (Locked, Phase 1):** `_isUnlocked = false` → `CanInteract = false`. Player can see disabled prompt but cannot interact.

**Example (Unlocked, Phase 2, seeker in Chase):** `_isUnlocked = true`, `CurrentState == Playing`, `IsBeingChased = true` → `CanInteract = true`. Player CAN use the exit despite Chase.

**Example (Unlocked, win screen loading):** `_isUnlocked = true`, `CurrentState == Win` → `CanInteract = false`. The win transition has already begun; no second use is possible.

---

### F-LE-2: Unlock Trigger Condition

The unlock condition is fully delegated to the Objective System. The Level Exit system does not evaluate this condition itself:

```
unlock = ObjectiveRegistry.Instance.OnAllObjectivesCollected event fires
```

This event fires when (from Objective System GDD, F-OS-3):

```
CollectedCount == TotalCount AND TotalCount > 0
```

The Level Exit has no internal knowledge of objective counts. It responds to the event. If the event never fires (zero tokens in the level — see Edge Cases 5.4), the exit remains locked for the session.

---

### F-LE-3: Hold Completion (Delegated to PIS)

The hold completion logic is fully owned by the Player Interaction System (PIS F-PI-3). The Level Exit specifies only the duration via `LevelExitData.ExitHoldDuration`.

```
holdTimer            += Time.deltaTime   // each Update frame while button held and target stable
normalizedProgress    = Mathf.Clamp01(holdTimer / target.HoldDuration)
interactionComplete   = holdTimer >= target.HoldDuration
```

| Variable | Value for LevelExit | Source |
|----------|--------------------|---------|
| `target.HoldDuration` | `LevelExitData.ExitHoldDuration` (default 0.8 s) | `LevelExitData` ScriptableObject |
| `normalizedProgress` | Passed to `LevelExit.OnInteractProgress(float)` each frame | PIS; used by exit to drive visual feedback |

**Example:** Exit with default `ExitHoldDuration = 0.8 s`. Player begins hold at t=0. At t=0.4 s: `normalizedProgress = 0.5`, exit progress VFX at 50%. At t=0.8 s: hold complete, `OnInteractComplete` fires, win sequence begins.

---

### F-LE-4: Win Trigger Condition

```
winTrigger = LevelExit.OnInteractComplete called
             AND GameManager.Instance.CurrentState == GameState.Playing
             AND _isUnlocked == true
```

All three conditions must be true when `OnInteractComplete` is called. The guard on `CurrentState` in step 1 of Rule LE-9 (setting `CanInteract = false`) prevents double-trigger within the same session. The `_isUnlocked` guard ensures the exit was properly activated before use (should be structurally impossible given `CanInteract` is false when locked, but defended against anyway).

---

## 5. Edge Cases

### 5.1 Player Reaches the Exit Before Objectives Are Collected

**Scenario:** Player sprints directly to the exit at level start, ignoring the objective. Approaches within `InteractRadius` and faces the exit.

**Behavior:** `CanInteract == false` (exit is locked). PIS Rule D-1 applies: the disabled prompt displays — `"Locked"` label, desaturated icon, X overlay. If the player presses the interact input, PIS dispatch-time check on `CanInteract` prevents `OnInteractComplete` from firing. No interaction occurs. The player receives no error feedback; the locked prompt is the complete communication. The player understands: "I need to do something first."

**No special handling required from the Level Exit System.** This is the designed Phase 1 behavior.

---

### 5.2 Objectives Collected While Player Is Already Standing at the Exit

**Scenario:** Player has pre-positioned at the exit, then collects the final objective (possibly from a distance via a clever throw/interaction chain, or simply by taking the relic and running directly back to the exit in a single quick Phase 2 sprint). When `OnAllObjectivesCollected` fires, the player is already within `InteractRadius` of the exit.

**Behavior:** `HandleAllObjectivesCollected()` fires synchronously. `_isUnlocked` becomes true, `CanInteract` becomes true. On the very next `Update` frame, PIS re-evaluates its targeting query and now finds the exit eligible. The active prompt updates from the disabled state to the active state (with hold ring indicator) within one frame. The player can immediately begin the hold.

**Note on prompt transition speed:** The PIS prompt fade-in (`PromptFadeInTime`, default 0.1 s) means the active prompt appears within 0.1 s of the unlock. This is fast enough to feel instantaneous but provides a smooth visual transition rather than a jarring pop. No special-case code is required in `LevelExit`; PIS Rule D-4 handles this via normal `CanInteract` state change.

---

### 5.3 Exit Used During Win, Lose, or Warmup State

**Scenario:** `OnInteractComplete` is called when `GameManager.CurrentState` is not `Playing`.

**Behavior:** `LevelExit.OnInteractComplete` checks `GameManager.Instance.CurrentState` at the top of the method. If the state is not `Playing`, the method returns immediately. `TriggerWin()` is not called. `OnExitUsed` does not fire. This guard is a defensive backstop — the formula F-LE-1 (`CanInteract` requires `CurrentState == Playing`) means the PIS should never dispatch to the exit outside Playing state. The guard in `OnInteractComplete` is a secondary defense against any edge case in the dispatch ordering.

---

### 5.4 Level Loaded Without ObjectiveRegistry

**Scenario:** The scene does not contain an `ObjectiveRegistry` component. `LevelExit.Awake` calls `ObjectiveRegistry.Instance.OnAllObjectivesCollected += HandleAllObjectivesCollected`.

**Behavior:** `SceneSingleton<ObjectiveRegistry>` does not auto-create; it requires the component to be present in the scene. If `ObjectiveRegistry.Instance` is null, the subscription call produces a `NullReferenceException`, which is caught by a null-check guard in `LevelExit.Awake`:

```csharp
if (ObjectiveRegistry.Instance == null)
{
    Debug.LogError("LevelExit: ObjectiveRegistry not found in scene. " +
                   "Exit will remain locked for entire session. " +
                   "Add ObjectiveRegistry to the scene.");
    return;
}
ObjectiveRegistry.Instance.OnAllObjectivesCollected += HandleAllObjectivesCollected;
```

**Result:** Exit remains locked for the entire session. The level is not completable. The error in the console directs the level designer to add `ObjectiveRegistry` to the scene. No crash.

---

### 5.5 Level Loaded Without LevelExit

**Scenario:** The level designer forgets to place a `LevelExit` prefab in the scene.

**Behavior:** `OnAllObjectivesCollected` fires when all objectives are collected (normal behavior). No `LevelExit` is subscribed to this event. The exit's portion of Phase 2 simply does not exist: no visual unlock signal, no exit interactable, no win condition. The player collects the objective and has no destination.

**Detection:** The `LevelExit` system cannot detect its own absence. The Two-Phase Level Structure system (when written) must validate that a `LevelExit` exists in the scene during its own Awake. Additionally, a `ComponentAuditTool` rule must flag scenes containing `ObjectiveRegistry` but no `LevelExit`.

---

### 5.6 Two LevelExit Components in the Same Scene

**Scenario:** Level designer accidentally places two `LevelExit` prefabs in the scene.

**Behavior:** Both subscribe to `OnAllObjectivesCollected`. Both transition to unlocked. Both display active prompts when the player is nearby. If the player uses either one, it calls `GameManager.TriggerWin()`. `GameManager.TriggerWin()` must be idempotent — if called while already in `Win` state, it logs a `Debug.LogWarning` and returns without double-triggering the win screen. The second `LevelExit.OnInteractComplete` (if the GameManager is already in `Win`) will be blocked by the `CurrentState == Playing` guard at the top of `OnInteractComplete`, which also prevents the second `OnExitUsed` from firing.

**Prevention:** `ComponentAuditTool` rule flags scenes with more than one `LevelExit` component (Rule LE-12).

---

### 5.7 Seeker Reaches the Exit's Position During Phase 2

**Scenario:** During Phase 2, a seeker's patrol route (or Chase pursuit) brings the seeker to the physical position of the Level Exit object. The player is trying to reach the exit while the seeker stands at it.

**Behavior:** The Level Exit has no special interaction with the Seeker AI. The seeker does not "guard" the exit — it follows its patrol route or Chase behavior determined entirely by the Detection System. The seeker standing at the exit is a level design outcome, not a system response. The player must navigate around the seeker using standard stealth mechanics (hiding, distracting, waiting for the patrol gap).

**Design note:** Level designers should be aware that placing the exit in the middle of a patrol loop creates a legitimate (and potentially interesting) "guarded exit" puzzle for Phase 2. This is intentional design space, not a system error. The Level Exit has no patrol-blocking or seeker-redirect behavior; the challenge is 100% level-authored.

---

### 5.8 Player Begins Hold on Exit, Then Seeker Enters Chase

**Scenario:** Player begins the exit hold (t=0.0 s). At t=0.4 s, a seeker enters Chase state against the player. The hold is at 50% completion.

**Behavior:** Because `LevelExit` is `IChaseInteractable` (Rule LE-7), `CanInteract` remains true during Chase. The PIS Chase block (Rule B-1) skips the chase-blocking logic for this target. The hold timer continues. At t=0.8 s the hold completes; `OnInteractComplete` fires; the win sequence begins with the seeker actively chasing. This is the intended "last-second escape" scenario described in Section 2.

**What does NOT happen:** `OnInteractCancelled` does not fire on Chase entry (which would happen for a normal interactable per PIS Rule B-2). The Chase override means no cancellation event.

---

### 5.9 ObjectiveRegistry Fires OnAllObjectivesCollected Multiple Times

**Scenario:** Due to a bug in the Objective System, `OnAllObjectivesCollected` fires more than once per session.

**Behavior:** `HandleAllObjectivesCollected()` is called multiple times. The second call finds `_isUnlocked == true` (already set in the first call). `HandleAllObjectivesCollected` guards against this:

```csharp
private void HandleAllObjectivesCollected()
{
    if (_isUnlocked) return;  // guard against duplicate event fires
    // ... unlock sequence ...
}
```

The unlock sequence (VFX, SFX, material change) fires exactly once. No duplicate particles, no double audio, no corrupted state.

---

## 6. Dependencies

### 6.1 What This System Requires

| Dependency | System | What Is Required | Direction | Notes |
|-----------|--------|-----------------|-----------|-------|
| `ObjectiveRegistry.OnAllObjectivesCollected` | Objective System | C# event. `LevelExit` subscribes in Awake. Fires when all objectives are collected. | Inbound | Subscription requires `ObjectiveRegistry.Instance` to be non-null at Awake. Script execution order must ensure `ObjectiveRegistry` Awakes before `LevelExit`. |
| `IInteractable` | Infrastructure / Player Interaction System | Interface definition. `LevelExit` implements it. | Inbound | `LevelExit` must conform to all `IInteractable` rules, with one explicit exception: `CanInteract` does not check `IsBeingChased`. |
| `InteractableBase` | Infrastructure | Abstract base class; provides empty default implementations. `LevelExit` extends it. | Inbound | `LevelExit` overrides `OnInteractComplete` (required) and `OnInteractProgress` (required for visual feedback). |
| `PlayerInteraction` | Player / Player Interaction System | Calls `LevelExit.OnInteractComplete(PlayerController)` when hold completes. Manages targeting and hold timing. | Inbound (via `IInteractable` contract) | `PlayerInteraction` must respect the `IChaseInteractable` marker interface to skip Chase blocking for the exit. |
| `GameManager` | GameLoop | `GameManager.Instance.TriggerWin()` called on exit use. `GameManager.Instance.CurrentState` checked in `CanInteract` and as a guard in `OnInteractComplete`. | Inbound | `GameManager.TriggerWin()` must be idempotent. |
| `AudioManager` | Audio | `AudioManager.Instance.Play(SFXKey)` called for unlock SFX, exit SFX. | Inbound | `LevelExitData` SFX key values must map to valid `SoundID` entries in `SoundLibrary`. |
| `LevelExitData` | Data (`ScriptableObject`) | Per-exit configuration: VFX prefabs, SFX keys, prompt labels, hold duration, material variants. Assigned via serialized field. | Inbound | See Section 7 for field definitions. |
| `PlayerInteractionData.PromptIcons` | Data (PIS ScriptableObject) | Must contain an `"exit"` key mapped to a sprite. `LevelExit.PromptIconKey` returns `"exit"`. | Inbound | Art team must register this key in `PlayerInteractionData`. |

---

### 6.2 What This System Provides

| Provided To | System | What Is Provided | Direction | Notes |
|------------|--------|-----------------|-----------|-------|
| Two-Phase Level Structure | Gameplay | `LevelExit.OnExitUsed` C# event. Fires on successful exit use, before `TriggerWin()`. Two-Phase system subscribes to halt escalation logic. | Outbound | Synchronous. No async. |
| Level Timer + Stats | Progression | `LevelExit.OnExitUsed` C# event. Used to record time-to-exit in session stats. | Outbound | Synchronous. Stat recording is instantaneous. |
| GameManager | GameLoop | `GameManager.TriggerWin()` call from `OnInteractComplete`. This is the authoritative win signal. | Outbound | Unidirectional call; no return value required. |
| Win / Game Over Screens | UI | Win screen is driven by `GameManager.TriggerWin()`, which the Level Exit calls. The Level Exit does not communicate with the win screen directly. | Outbound (via GameManager) | Indirect. Level Exit → GameManager → Win Screen. |

---

### 6.3 Ownership Boundaries (Explicit)

The following behaviors are explicitly NOT owned by the Level Exit System:

| Behavior | Actual Owner |
|----------|-------------|
| Seeker speed increase / global alert (Phase 2 escalation) | Two-Phase Level Structure |
| Music shift on Phase 2 start | Two-Phase Level Structure (via Adaptive Music) |
| Win screen display | GameManager + Win / Game Over Screens |
| Session stat recording | Level Timer + Stats |
| Deciding when objectives are all collected | Objective System |
| Detecting player proximity to the exit | Player Interaction System (overlap query) |
| Hold timing logic | Player Interaction System |
| Seeker patrol or Chase behavior near the exit | Seeker AI (no awareness of exit position) |

---

### 6.4 Bidirectional Contract Notes — GDDs That Must Be Updated

Per design document standards, dependency directions must be reflected in related GDDs. The following updates are required when this GDD is approved:

- **Objective System GDD (`design/gdd/objective-system.md`):** Section 6.2 already lists "Level Exit System — subscribes to `OnAllObjectivesCollected`" as a downstream recipient. No update required — this dependency is pre-documented.
- **Player Interaction System GDD (`design/gdd/player-interaction-system.md`):** Section 6.2 currently lists existing downstream recipients but not the Level Exit System specifically. The PIS GDD must be updated to add "Level Exit System — `OnInteractComplete(PlayerController)` called on hold completion. Note: `LevelExit` implements `IChaseInteractable` to bypass Chase blocking — PIS must implement this interface check." An update to the PIS GDD is required.
- **Seeker AI GDD (`design/gdd/seeker-ai.md`):** Section 6 (Dependencies) currently notes the Level Exit under "GameManager mediates — None directly." This remains accurate; no update required.
- **Systems Index (`design/gdd/systems-index.md`):** Status for Level Exit System (row #11) must be updated from "Not Started" to "Designed" once this GDD is approved. Progress Tracker counts must be updated.
- **Two-Phase Level Structure GDD (not yet written):** When authored, must document its subscription to `LevelExit.OnExitUsed` and its subscription to `ObjectiveRegistry.OnAllObjectivesCollected` in its Dependencies section.
- **Level Timer + Stats GDD (not yet written):** When authored, must document its subscription to `LevelExit.OnExitUsed` in its Dependencies section.

---

## 7. Tuning Knobs

### 7.1 LevelExitData ScriptableObject (Per-Exit Configuration)

`LevelExitData` is a new ScriptableObject to be created at `Assets/_Project/Scripts/Data/LevelExitData.cs`. Each `LevelExit` prefab holds a serialized reference to a `LevelExitData` asset. In MVP, all levels share the same `LevelExitData` asset (one exit type). Future levels may reference unique assets for variant exit types.

| Field | Type | Default | Safe Range | Category | Description |
|-------|------|---------|-----------|----------|-------------|
| `ExitHoldDuration` | `float` | `0.8 s` | 0.5–1.5 s | Feel | How long the player must hold interact at the unlocked exit. Below 0.5 s approaches tap speed and reduces Phase 2 tension. Above 1.5 s becomes frustrating relative to perceived danger. |
| `LockedPromptLabel` | `string` | `"Locked"` | 1–12 chars | Feel | Label shown in the disabled prompt when exit is locked. Communicates locked state. Must pass PIS 12-character limit. |
| `UnlockedPromptLabel` | `string` | `"Exit"` | 1–12 chars | Feel | Label shown in the active prompt when exit is unlocked. Communicates the action. Must pass PIS 12-character limit. |
| `DisplayName` | `string` | `"Exit"` | 1–30 chars | Feel | Human-readable name for level summary or HUD references. Not shown in the world-space prompt. |
| `LockedAmbientVFXPrefab` | `GameObject` | (dim particle loop) | Non-null | Feel | Looping particle system played on the exit in Locked state. Must be subtle — identifiable but not dominant. Duration: infinite loop. |
| `UnlockVFXPrefab` | `GameObject` | (burst particle) | Non-null | Feel | One-shot particle effect on unlock transition. Self-destroying. Duration: 1.5–2.5 s. Must be visually distinct from locked ambient. |
| `UnlockedAmbientVFXPrefab` | `GameObject` | (bright particle loop) | Non-null | Feel | Looping particle system played on exit in Unlocked state. Must be clearly brighter/warmer than locked variant. Duration: infinite loop. |
| `ExitVFXPrefab` | `GameObject` | (exit flash particle) | Non-null | Feel | One-shot VFX on exit use (level end). Self-destroying. Duration: 0.5–1.0 s. |
| `UnlockSFXKey` | `SoundID` (enum) | `SoundID.ExitUnlock` | Must exist in SoundLibrary | Feel | Audio event on unlock transition. Should be distinct, non-subtle — audible from across the room. `SoundID.ExitUnlock` must be added to `SoundLibrary.cs`. |
| `ExitSFXKey` | `SoundID` (enum) | `SoundID.ExitUse` | Must exist in SoundLibrary | Feel | Audio event on exit use. `SoundID.ExitUse` must be added to `SoundLibrary.cs`. |
| `LockedMaterialVariant` | `Material` | (locked shader state) | Non-null | Feel | Material applied to the exit mesh in Locked state. Low emission. |
| `UnlockedMaterialVariant` | `Material` | (unlocked shader state) | Non-null | Feel | Material applied to the exit mesh in Unlocked state. Full emission. Visual difference must be readable from 15+ meters. |

**No curve or gate knobs exist in `LevelExitData` beyond `ExitHoldDuration`.** The Level Exit has no progression, no scaling, no economic component. Difficulty tuning for Phase 2 comes entirely from the Seeker AI tuning and level geometry, not from the exit itself.

---

### 7.2 Values That Must Never Be Tuned

The following are fixed design decisions, not tuning knobs. Changing them would break the system's design contract:

| Value | Fixed At | Rationale |
|-------|----------|-----------|
| `RequiresHold` (Unlocked state) | `true` | The hold interaction is load-bearing for Phase 2 tension design. Changing to tap would eliminate the final commitment beat and allow accidental level-end triggers during Phase 2 traversal. |
| Chase override (`IsBeingChased` not checked) | Always exempt | This is a deliberate design exception. Treating it as a bug and "fixing" it would create the opaque soft wall at the exit described in Section 2. |
| `CanInteract` in Locked state | Always `false` | The locked exit must never be interactable regardless of any other state. |
| `PromptIconKey` | `"exit"` | Consistent icon category for the exit across all MVP levels. |

---

### 7.3 Inherited Tuning (From Player Interaction System)

The following knobs affect exit feel but are owned by `PlayerInteractionData`, not `LevelExitData`. Listed here for the tuner's awareness:

| Knob | Owner | Default | Effect on Exit |
|------|-------|---------|----------------|
| `InteractRadius` | `PlayerInteractionData` | 1.5 m | How close the player must be to begin the hold |
| `InteractAngle` | `PlayerInteractionData` | 60° | How directly the player must face the exit |
| `DisabledPromptRadius` | `PlayerInteractionData` | 1.5 m | How close the player must be to see the locked prompt |
| `PromptFadeInTime` | `PlayerInteractionData` | 0.1 s | Speed of the prompt state change on unlock (locked → active) |

Adjust these in `PlayerInteractionData` only if the exit interaction feel requires changes that would also affect other interactables system-wide. Do not introduce exit-specific overrides for these values.

---

## 8. Acceptance Criteria

### Functional (pass/fail, verifiable in Unity Play Mode or Edit Mode)

- [ ] **AC-LE-01 — Exit is locked on level load:** Place one `LevelExit` prefab and one `ObjectiveToken` in a test scene. Enter Play Mode. Do not collect the objective. Approach the exit within `InteractRadius`. `LevelExit.CanInteract` is `false`. No active prompt is displayed. Disabled prompt ("Locked" label, X overlay) is visible when facing the exit. PASS: disabled prompt shown, no active prompt, `CanInteract == false`. FAIL: active prompt shown, or `CanInteract == true`, or no prompt visible at all.

- [ ] **AC-LE-02 — Exit unlocks on objective collection:** Same test scene. Collect the objective token. On the same frame as `OnAllObjectivesCollected` fires: `LevelExit._isUnlocked` becomes `true`, `LevelExit.CanInteract` becomes `true`. Verify via debug Inspector watch. PASS: both values flipped to true on the collection frame. FAIL: exit remains locked or unlocks on a later frame.

- [ ] **AC-LE-03 — Unlock VFX fires on collection:** Collect the objective. A particle system from `LevelExitData.UnlockVFXPrefab` is instantiated at the exit's world position within the same frame. Verify via Scene view immediately after collection. PASS: one particle system at exit world position. FAIL: no VFX, or VFX at origin (0,0,0).

- [ ] **AC-LE-04 — Unlock SFX fires on collection:** Collect the objective. `AudioManager.Instance.Play(LevelExitData.UnlockSFXKey)` is called once. Verify via AudioManager debug logging. PASS: exactly one audio event of the correct SFX key. FAIL: no audio event, or wrong key, or multiple events.

- [ ] **AC-LE-05 — Active prompt shows after unlock:** After objective collection, approach the exit within `InteractRadius` and face it. Active prompt shows: `"Exit"` label, `"exit"` icon (not desaturated), hold-ring indicator. PASS: active prompt with correct label and hold indicator visible. FAIL: disabled prompt still shown, or no prompt.

- [ ] **AC-LE-06 — Exit requires hold, not tap:** Unlocked exit. Tap interact (button-down, instant release). `OnInteractComplete` does not fire. No win trigger. PASS: no win. FAIL: win triggered on tap.

- [ ] **AC-LE-07 — Hold completes in ExitHoldDuration:** Unlocked exit. Hold for exactly `LevelExitData.ExitHoldDuration` seconds (0.8 s default). `OnInteractComplete` fires. Win sequence begins. PASS: complete fires at 0.8 s, not before. FAIL: fires early or does not fire.

- [ ] **AC-LE-08 — OnInteractProgress drives visual feedback:** During hold, verify `LevelExit.OnInteractProgress` is called each frame with `normalizedProgress` increasing from 0 to 1. At `normalizedProgress = 0.5` (t = 0.4 s), the exit's visual feedback (particle system or shader parameter) is at approximately 50% intensity. PASS: feedback visibly increases over the hold duration. FAIL: no visual change during hold.

- [ ] **AC-LE-09 — OnExitUsed fires before TriggerWin:** Instrument with a test listener on `OnExitUsed` and a breakpoint/log on `GameManager.TriggerWin()`. Complete the hold. PASS: `OnExitUsed` event fires first; `TriggerWin()` called second, on the same frame. FAIL: `TriggerWin()` fires before or without `OnExitUsed`.

- [ ] **AC-LE-10 — TriggerWin is called exactly once:** Complete the hold. `GameManager.TriggerWin()` is called exactly once per level session. Verify via test listener. PASS: one call. FAIL: zero calls (no win) or more than one (double-trigger).

- [ ] **AC-LE-11 — Exit is usable during Chase:** Set `PlayerController.IsBeingChased = true`. Unlock the exit. Approach and hold for 0.8 s. `OnInteractComplete` fires; win sequence begins. PASS: win triggered despite Chase state. FAIL: interaction blocked, hold does not begin, or `CanInteract` returns false during Chase.

- [ ] **AC-LE-12 — Exit is NOT usable while locked during Chase:** Set `PlayerController.IsBeingChased = true`. Do NOT collect objective (exit is locked). Approach and press interact. `CanInteract == false`; no interaction fires. PASS: no interaction. FAIL: interaction fires on locked exit.

- [ ] **AC-LE-13 — Exit blocked in Win game state:** Complete the hold, triggering win. `GameManager.CurrentState` transitions to `Win`. Attempt to interact with the exit again. `CanInteract` returns `false` (CurrentState != Playing). No second `TriggerWin` call. PASS: second use blocked. FAIL: second win trigger or exception.

- [ ] **AC-LE-14 — ObjectiveRegistry absent logs error and locks exit:** Remove `ObjectiveRegistry` from the scene. Enter Play Mode. Console shows `Debug.LogError` from `LevelExit` about missing registry. Exit remains locked for the entire session. No `NullReferenceException` crash. PASS: error logged, exit locked, no crash. FAIL: exception thrown or exit unlocked.

- [ ] **AC-LE-15 — Unlock sequence fires exactly once (idempotency guard):** Manually fire `ObjectiveRegistry.OnAllObjectivesCollected` twice in rapid succession via a test script. Verify: `_isUnlocked` becomes true exactly once, unlock VFX instantiated exactly once, unlock SFX played exactly once. PASS: single unlock sequence. FAIL: duplicate VFX/SFX.

- [ ] **AC-LE-16 — Disabled prompt visible at DisabledPromptRadius:** Locked exit. Position player exactly at `DisabledPromptRadius` (default 1.5 m) and face the exit. Disabled prompt is visible. Position player at 1.6 m (beyond radius). Disabled prompt disappears. PASS: prompt visible at-or-within radius, absent beyond. FAIL: prompt visible at 1.6 m, or invisible at 1.5 m.

- [ ] **AC-LE-17 — LevelExitData null-check fires in Editor:** Assign no `LevelExitData` to the `LevelExit` component. Enter Play Mode. `Debug.Assert` fires indicating null data. No `NullReferenceException` from `Start` or `Awake`. PASS: assertion fires, descriptive message logged, no crash. FAIL: NullReferenceException or silent failure.

- [ ] **AC-LE-18 — No per-frame GC allocations from LevelExit:** Profile in Unity Profiler during Phase 2 (exit unlocked, player walking nearby). `LevelExit.Update` (if any) shows 0 B GC Alloc per frame. No allocations in the hot path. PASS: 0 B GC Alloc. FAIL: any allocation per frame.

---

### Experiential (validated via observed play sessions — marked with an asterisk)

- [ ] **AC-LE-19* — Exit is identified during initial room survey:** Naive player in a test level with one objective token and one Level Exit, no UI tutorial. Target: ≥80% of testers visually locate and identify the exit as the escape route within 60 seconds of the level starting, without being told what to look for. PASS: testers point at or approach the exit during their survey. FAIL: testers complete Phase 1 without locating the exit, requiring instruction.

- [ ] **AC-LE-20* — Unlock is read as a Phase 2 start signal:** After collecting the last objective and seeing the exit unlock (light change, VFX, SFX), ask players: "What just happened?" PASS: ≥80% of testers attribute the change to collecting the objective and understand they should now go to the exit. FAIL: players are confused by the change, or do not connect it to their collection.

- [ ] **AC-LE-21* — Hold at exit reads as intentional tension, not frustration:** After a Phase 2 run, ask: "When you were at the exit waiting for it to open, how did that feel?" PASS: ≥70% of testers describe it as tense/exciting (positive or constructively tense). FAIL: >30% describe it as frustrating, unclear, or slow. If failed, reduce `ExitHoldDuration` from 0.8 s toward 0.6 s and retest.

- [ ] **AC-LE-22* — Chase-during-exit does not feel like a bug:** Ask players who experienced a Chase → exit-use sequence: "When you used the exit while being chased, did that feel like it worked correctly or like you broke the game?" PASS: ≥85% of testers report it felt correct (tense, cinematic, earned). FAIL: players report it felt like an exploit or a bug — investigate whether the Chase state visual feedback is insufficient to communicate that the exit remains usable during pursuit.
