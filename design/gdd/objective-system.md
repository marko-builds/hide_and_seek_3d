# Objective System

> **Status**: Approved
> **Author**: game-designer
> **Last Updated**: 2026-03-05
> **Implements Pillar**: Two-Beat Tension (Pillar 4), The Room Has Rules (Pillar 1)
> **Design Order**: #9 — Core Layer

---

## 1. Overview

The Objective System is the mechanism by which a player collects the item the seeker is guarding — the relic, artifact, or key that defines what the level is about. Each level contains one or more `ObjectiveToken` prefabs placed by the level designer. These tokens register themselves with the scene-singleton `ObjectiveRegistry` on Awake; no manual wiring is required. The player collects a token by tapping the interact input while within range (delegated entirely to the Player Interaction System). On collection, the token deactivates, audio and VFX fire, and the `ObjectiveRegistry` increments its `CollectedCount`. When all tokens are collected, `ObjectiveRegistry.OnAllObjectivesCollected` fires — this single event is the trigger for Phase 2 escalation, unlocking the Level Exit and alerting all seekers. The system owns nothing beyond token state and the registry: detection avoidance, AI escalation, exit unlocking, and HUD display are all downstream systems that subscribe to the two events this system emits. MVP scope is one token per level; the registry is built for N from day one.

---

## 2. Player Fantasy

**Target MDA Aesthetics**: Discovery (finding the relic after reading the room), Challenge (the moment of collection under threat of exposure), Narrative (the seeker guards this object — it means something).

The Objective System exists to deliver one specific emotional peak: the moment the player's hand closes around the thing they came for.

Phase 1 of each level is a sustained read-the-room experience. The player observes patrol patterns, identifies the relic's position, and constructs a plan. The relic is visible throughout this phase — the player knows where it is, but cannot safely reach it. This sustained visible-but-unreachable tension is the most important framing condition for the collection moment. The relic must be discoverable by exploration and observation (Pillar 1: The Room Has Rules). It should never be randomly hidden — it should be where a guarded thing would logically be: at the center of a patrol loop, behind the point of maximum exposure.

The collection itself is a tap — instantaneous, no hold. This is a deliberate contrast to the hiding spot's hold interaction. Hiding asks for commitment and patience. Collection asks for nerve: the player must close the distance, enter the seeker's patrol territory, and commit to a single decisive input. The speed of the tap means the window of exposure is determined entirely by the seeker's position, not by how long the player holds a button. The danger was always getting there, not the act of taking.

At the instant of collection: the relic vanishes, a brief flash of VFX blooms in the player's peripheral vision, and an audio cue confirms the act. One beat of relief. Then, if this was the last token, the escalation trigger fires — the music shifts, the seekers accelerate, the exit light activates. The player's emotional state inverts from predator-careful-approach to prey-in-flight. This two-beat structure (collect then escape) is Pillar 4 made mechanical: **Two-Beat Tension**. The Objective System is the pivot point between those two beats.

*SDT anchor: **Competence** — the player reading the patrol correctly and timing their approach creates a genuine feeling of skill. The instantaneous tap rewards precise timing over button discipline. **Autonomy** — the player decides the approach route, the timing, and the order of collection when multiple tokens exist.*

---

## 3. Detailed Rules

### 3.1 Component Overview

The Objective System is composed of three classes and one ScriptableObject:

| Component | Type | Responsibility |
|-----------|------|---------------|
| `ObjectiveToken` | `MonoBehaviour : InteractableBase, IInteractable` | The collectible world object. Implements `IInteractable` so the Player Interaction System can dispatch to it. Owns its own collected state, VFX, SFX, and self-deactivation. |
| `ObjectiveRegistry` | `SceneSingleton<ObjectiveRegistry>` | Scene-level tracker. Receives registration from all tokens at Awake, tracks collected count, fires events. |
| `ObjectiveData` | `ScriptableObject` | Per-token configuration data (VFX prefab reference, SFX key, display name, icon sprite). Defined in Section 7. |
| `InteractableBase` | `abstract MonoBehaviour` | Existing project base class for all `IInteractable` objects; provides empty default implementations of `OnInteractProgress` and `OnInteractCancelled`. |

---

### 3.2 ObjectiveToken: IInteractable Implementation

`ObjectiveToken` implements `IInteractable` with these fixed values:

| Property | Value | Rationale |
|----------|-------|-----------|
| `RequiresHold` | `false` | Tap-to-collect: collection is a decisive, nerve-driven act, not a patient one. Contrast with hiding spot's hold. |
| `HoldDuration` | `0` | Ignored because `RequiresHold == false`. |
| `PromptLabel` | `"Take"` | 4 characters, imperative verb. Passes the 12-character limit. Communicates "this is mine to take." |
| `PromptIconKey` | `"collect"` | Must match a key registered in `PlayerInteractionData.PromptIcons`. Art team must add a "collect" icon entry. |
| `CanInteract` | `true` until collected; `false` after | Permanently false once collected. No recovery. |

**Rule OS-1 (Tap Collection).** When `PlayerInteraction` calls `ObjectiveToken.OnInteractComplete(PlayerController interactor)`:
1. If `_isCollected == true`, return immediately without executing any collection logic. (Guard against race conditions — see Edge Cases 5.6.)
2. Set `_isCollected = true`.
3. Set `CanInteract = false`.
4. Call `ObjectiveRegistry.Instance.RegisterCollection(this)`.
5. Play the collection VFX: instantiate the VFX prefab from `ObjectiveData.CollectionVFXPrefab` at the token's world position. The VFX prefab is a self-destroying particle system; `ObjectiveToken` does not manage its lifecycle after instantiation.
6. Play the collection SFX: call `AudioManager.Instance.Play(ObjectiveData.CollectionSFXKey)`.
7. Deactivate this GameObject: `gameObject.SetActive(false)`.

Step 7 must be the final step. Deactivating the GameObject before calling `RegisterCollection` would prevent the registry from receiving a valid reference. Deactivating before VFX/SFX would silence them.

**Rule OS-2 (No OnInteractProgress).** `ObjectiveToken` does not override `OnInteractProgress`. The `InteractableBase` empty default implementation is used. Because `RequiresHold == false`, this callback is never invoked for this object.

**Rule OS-3 (No OnInteractCancelled).** `ObjectiveToken` does not override `OnInteractCancelled`. Because `RequiresHold == false`, no hold can be in progress, and this callback is never invoked for this object.

**Rule OS-4 (Disabled Prompt After Collection).** Once `_isCollected == true`, `CanInteract` returns false. Per PIS Rule D-1, the Player Interaction System will show a disabled prompt (grayed icon, X overlay) if the player re-enters range. This communicates "I already took this" without requiring special-case UI from the Objective System. Level designers must not place a second token at the same position as a collected one.

---

### 3.3 ObjectiveRegistry: Registration and Tracking

**Rule OS-5 (Self-Registration on Awake).** Each `ObjectiveToken` calls `ObjectiveRegistry.Instance.Register(this)` from its `Awake` method. The registry must exist before any token Awakes; `SceneSingleton<ObjectiveRegistry>` is initialized from its own `Awake`. Unity guarantees that `SceneSingleton` Awakes before `ObjectiveToken` Awakes if and only if the registry GameObject's execution order is set earlier than the default. **Implementation requirement:** Set `ObjectiveRegistry`'s script execution order to -100 in Project Settings to guarantee it Awakes before all tokens.

**Rule OS-6 (Registration Closed After Playing).** The registry accepts new `Register` calls only while `GameManager.CurrentState == GameState.Warmup`. Once the state transitions to `Playing`, no new registrations are processed. Any `Register` call received during `Playing` logs a `Debug.LogWarning` and is ignored. This prevents dynamically spawned tokens (not a designed case in MVP) from silently corrupting the total count mid-level.

**Rule OS-7 (Token Count Immutable After Playing).** `TotalCount` is set at the moment `GameState` transitions from `Warmup` to `Playing`. It does not change for the remainder of the level session. If a token is destroyed before collection (e.g., by a bug or out-of-scope editor action), `TotalCount` does not decrement — the level becomes unclearable, which surfaces the error rather than silently patching it.

**Rule OS-8 (RegisterCollection).** When `ObjectiveToken.OnInteractComplete` calls `ObjectiveRegistry.Instance.RegisterCollection(this)`:
1. Increment `_collectedCount`.
2. Fire `OnObjectiveCollected` event (passes no argument — listeners query `CollectedCount` and `TotalCount` directly).
3. If `_collectedCount == _totalCount`: fire `OnAllObjectivesCollected` event (see Rule OS-9).

**Rule OS-9 (Phase Transition — Last Token).** `OnAllObjectivesCollected` fires exactly once per level session, on the frame that `_collectedCount` becomes equal to `_totalCount`. It fires synchronously within `RegisterCollection`, before `ObjectiveToken.OnInteractComplete` returns. Subscribers must not yield or do heavy work inside the event handler — they should schedule their responses using coroutines or queued state changes.

The following systems are the intended subscribers to `OnAllObjectivesCollected`:
- **Level Exit System** — activates the exit interactable (`CanInteract = true` on the exit trigger).
- **Two-Phase Level Structure** — triggers Phase 2 escalation (seeker speed increase, global alert, music shift).

These systems subscribe independently; the Objective System does not know about them. The contract is: the event fires once, carries no argument, and fires on the frame of the final collection.

**Rule OS-10 (Unregister on Destroy).** If an `ObjectiveToken` GameObject is destroyed before collection (not a designed gameplay path, but possible in development), `ObjectiveToken.OnDestroy` must call `ObjectiveRegistry.Instance.Unregister(this)` if `_isCollected == false`. The registry removes the token from its internal list and decrements `_totalCount`. This prevents the level from becoming permanently un-clearable due to a token that no longer exists. A `Debug.LogWarning` is emitted to alert the level designer that a non-collected token was destroyed.

---

### 3.4 HUD Data Contract

The HUD reads two properties from `ObjectiveRegistry` to display the collection counter:

| Property | Type | Description |
|----------|------|-------------|
| `ObjectiveRegistry.CollectedCount` | `int` | Number of tokens collected so far this level. Updated by `RegisterCollection`. |
| `ObjectiveRegistry.TotalCount` | `int` | Total tokens placed in this level. Set at `Playing` state entry. |

The HUD subscribes to `ObjectiveRegistry.OnObjectiveCollected` to know when to refresh. The HUD does not poll these values — it updates only on event. Display format: `[CollectedCount] / [TotalCount]` (e.g., "0 / 1", "1 / 3"). The HUD system owns the visual presentation; the Objective System provides only the data.

---

### 3.5 Level Designer Contract

**Rule OS-11 (Placement).** Level designers place `ObjectiveToken` prefabs directly in the scene. No manual registration step is required. The token's `Awake` handles registration automatically (Rule OS-5).

**Rule OS-12 (Minimum Count).** A level must contain at least one `ObjectiveToken`. A level with zero tokens will have `TotalCount == 0` and `OnAllObjectivesCollected` will fire at level start on the first `Playing` state entry — which will immediately unlock the exit. This is an error condition documented in Edge Cases (Section 5.1).

**Rule OS-13 (InteractionPromptAnchor).** Every `ObjectiveToken` prefab must include a child `Transform` named `InteractionPromptAnchor`, per PIS requirements. Position it approximately 0.3 m above the token's visual center so the prompt hovers above the relic rather than clipping into it. `InteractableBase.Awake` contains a `Debug.Assert` that catches missing anchors at scene load in Editor builds.

**Rule OS-14 (Guard Proximity — Level Design, Not System Behavior).** The seeker "guards" the objective exclusively through level design: patrol routes are authored so the seeker's path passes near or around the token's position. The Objective System does not communicate with the Detection System or Seeker AI. Whether a seeker is near a token at any given moment is an emergent result of patrol timing and the player's approach window — this is the core gameplay challenge of Phase 1. Do not implement proximity detection or "guard radius" behavior in the Objective System; that would replace player skill with system protection.

---

## 4. Formulas

### F-OS-1: Collection Eligibility (Delegated to PIS)

The Player Interaction System owns all geometric and state-based collection eligibility logic. The Objective System does not redefine these rules, but they are documented here for reference:

```
canTarget = distance(playerPosition, token.bounds.center) <= InteractRadius
         AND angleTo(token) <= InteractAngle
         AND token.CanInteract == true
         AND NOT PlayerController.IsBeingChased
         AND PlayerMovement.IsGrounded
```

| Variable | Type | Value / Source | Description |
|----------|------|---------------|-------------|
| `playerPosition` | Vector3 | `PlayerInteraction.transform.position` | Player world-space foot position |
| `token.bounds.center` | Vector3 | `ObjectiveToken` collider bounds center | Token world-space center |
| `InteractRadius` | float | 1.5 m default (from `PlayerInteractionData`) | Maximum distance for interaction candidate selection |
| `InteractAngle` | float | 60° default (from `PlayerInteractionData`) | Half-angle of the detection cone |
| `token.CanInteract` | bool | `true` until collected | Becomes `false` permanently after collection |
| `PlayerController.IsBeingChased` | bool | Set by Detection System | True during active Chase; blocks all interaction (PIS Rule B-1) |
| `PlayerMovement.IsGrounded` | bool | Set by PlayerMovement | False during jump arc; blocks all interaction |

**No formula is owned by the Objective System here.** The values above are owned by PIS. They are reproduced for implementor clarity only.

---

### F-OS-2: Collection Counter

```
CollectedCount  = number of ObjectiveToken instances for which RegisterCollection() has been called
TotalCount      = number of ObjectiveToken instances registered via Register() before Playing state

counterDisplay  = CollectedCount + " / " + TotalCount
```

| Variable | Type | Range | Description |
|----------|------|-------|-------------|
| `CollectedCount` | int | 0 ≤ CollectedCount ≤ TotalCount | Increments by 1 per valid collection. Never decrements. |
| `TotalCount` | int | 1 ≤ TotalCount ≤ MaxTokensPerLevel | Set once at start of Playing state. Immutable thereafter. |
| `MaxTokensPerLevel` | int | 1–8 (gate knob, see Section 7) | Hard cap on tokens a level may contain. Prevents accidental O(n) scaling issues. |

**Example (MVP single-token level):**
- Level loads. One `ObjectiveToken` Awakes: `TotalCount = 1`, `CollectedCount = 0`. Display: "0 / 1".
- Player collects the token: `CollectedCount = 1`. Display: "1 / 1".
- `OnAllObjectivesCollected` fires immediately after display update.

**Example (multi-token level, 3 tokens):**
- Three tokens register: `TotalCount = 3`, `CollectedCount = 0`. Display: "0 / 3".
- Player collects first: `CollectedCount = 1`. Display: "1 / 3".
- Player collects second: `CollectedCount = 2`. Display: "2 / 3".
- Player collects third: `CollectedCount = 3`. Display: "3 / 3". `OnAllObjectivesCollected` fires.

---

### F-OS-3: Phase Transition Trigger Condition

```
phaseTransition = CollectedCount == TotalCount AND TotalCount > 0
```

| Variable | Type | Condition | Description |
|----------|------|-----------|-------------|
| `CollectedCount` | int | Equals `TotalCount` | All tokens have been collected |
| `TotalCount` | int | Greater than 0 | At least one token was placed (prevents fire-on-load for empty levels) |

This condition is evaluated once per `RegisterCollection` call, immediately after `CollectedCount` is incremented. When true, `OnAllObjectivesCollected` fires synchronously before `RegisterCollection` returns.

**Timing precision:** `OnAllObjectivesCollected` fires on the same frame as the player's tap input is processed by `PlayerInteraction.HandleInteract`. The sequence within that frame is:

1. `PlayerInputHandler.OnInteractPerformed` event fires.
2. `PlayerInteraction.HandleInteract` runs, calls `ObjectiveToken.OnInteractComplete`.
3. `ObjectiveToken` sets `_isCollected = true`, calls `RegisterCollection`.
4. `ObjectiveRegistry.RegisterCollection` increments `CollectedCount`, fires `OnObjectiveCollected`.
5. If `CollectedCount == TotalCount`: fires `OnAllObjectivesCollected`.
6. `ObjectiveToken` plays VFX, plays SFX, calls `gameObject.SetActive(false)`.
7. All subscribers to `OnAllObjectivesCollected` complete their synchronous handlers.
8. Frame ends.

The exit becomes interactable and escalation begins no later than the end of the frame the player taps the final token.

---

## 5. Edge Cases

### 5.1 Zero Tokens Placed in Level

**Scenario:** Level designer forgets to place any `ObjectiveToken` prefabs. `TotalCount == 0` when `Playing` state begins.

**Behavior:** `ObjectiveRegistry` detects `TotalCount == 0` when `GameManager.CurrentState` transitions to `Playing`. It immediately logs `Debug.LogError("ObjectiveRegistry: TotalCount is 0. Level has no objective tokens. Level cannot be completed correctly.")`. `OnAllObjectivesCollected` does NOT fire automatically — the guard in F-OS-3 (`TotalCount > 0`) prevents a silent empty-level fire. The exit remains locked. The level is unplayable but not silently broken; the error message surfaces the omission. Additionally, the HUD displays "0 / 0" which is visually anomalous and will be caught during development review.

**Resolution path:** Level designer places at least one `ObjectiveToken` and re-enters Play Mode.

---

### 5.2 All Tokens Already Collected (Re-entering the Level)

**Scenario:** `CollectedCount == TotalCount` when the scene is reloaded (e.g., player dies and respawns, level restarts via Checkpoint System).

**Behavior:** This cannot occur in normal play. Scene reload destroys and re-instantiates all GameObjects. `ObjectiveRegistry` and all `ObjectiveToken` instances are freshly created. `CollectedCount` resets to 0. `TotalCount` resets to 0 and is repopulated on the new Awake cycle. No special handling required.

**Edge within this edge:** If the Checkpoint System reloads only a subset of the scene (an architecture choice outside the Objective System's scope), the reloading authority must ensure `ObjectiveRegistry` is re-initialized. The Objective System provides no partial-reset API; it assumes scene reload for level restart.

---

### 5.3 Player Collects During Non-Playing Game State

**Scenario:** `GameManager.CurrentState` is `Warmup`, `Win`, or `Lose` at the moment `OnInteractComplete` fires on an `ObjectiveToken`.

**Behavior:** `ObjectiveToken.OnInteractComplete` checks `GameManager.Instance.CurrentState` at entry. If the state is not `Playing`, the method returns immediately without executing any collection logic. `_isCollected` remains false. `RegisterCollection` is not called. No VFX or SFX play. No counter update occurs. This condition should never arise in normal play — `PlayerInteraction` only processes input when the `GameManager` is in `Playing` state (blocking conditions include terminal states per PIS Rule B-1 Caught state handling). The check in `OnInteractComplete` is a defensive guard.

---

### 5.4 Two Tokens at the Same World Position

**Scenario:** Level designer accidentally places two `ObjectiveToken` prefabs at identical or nearly identical positions (within 0.5 m of each other).

**Behavior:** Both tokens register independently. `TotalCount == 2`. The Player Interaction System's targeting (PIS Rule T-3) selects the nearest candidate by `Collider.bounds.center` distance; with identical positions, the first in the physics buffer wins. The player can only interact with one token at a time. After collecting the first, the second becomes the new target. Both must be collected to clear the level.

**Level design issue:** The `OverlapChecker` editor tool (already in the project) flags overlapping interactable objects during scene authoring. A `ComponentAuditTool` rule should be added to warn when two `ObjectiveToken` components are within 0.5 m of each other. The Objective System does not merge or deduplicate tokens; it trusts the level designer to place them intentionally.

---

### 5.5 Token Inside Seeker's Active Patrol Route

**Scenario:** The seeker's patrol path passes through or immediately adjacent to the token's position, making a clean collection window very narrow.

**Behavior:** This is intentional level design, not a system error. The Objective System does not detect seeker proximity and takes no special action. The Detection System continues to evaluate the player's exposure based on the seeker's current position and the player's light/noise signature. If the player attempts to collect while in the seeker's detection range, the Detection System will handle suspicion accrual and potential Chase transition. If Chase transitions mid-collection-attempt, PIS Rule B-1 blocks the interaction on the next frame — but because collection is a tap (single-frame dispatch), if `OnInteractComplete` has already fired that frame, the collection is complete and cannot be rolled back. A tap that lands on the same frame as Chase detection fires resolves as: collection succeeds, Chase begins. This is the intended high-skill outcome: the player "got away with it."

---

### 5.6 Chase Block During Collection Attempt (Tap)

**Scenario:** Player's tap input and Chase state activation arrive on the same frame. Which wins?

**Behavior:** Unity's Update ordering determines this, but the PIS handles it deterministically. `PlayerInteraction.Update` runs the blocking check before processing input (PIS Rule B-1). If `IsBeingChased` is true at the top of the Update frame, input dispatch is skipped entirely. If `IsBeingChased` becomes true mid-frame (e.g., Detection System fires a synchronous C# event during the same Update loop), the dispatch-time guard in `PlayerInteraction.HandleInteract` re-checks `CanInteract` on the token immediately before calling `OnInteractComplete`. However, `IsBeingChased` is not re-checked at dispatch time — only `CanInteract` is. If the seeker's Chase activation and the player's tap both occur in the same Update frame, the tap may succeed.

**Design intent:** This "photo-finish" collect is a high-skill moment, not an exploit. The player read the patrol correctly enough to enter range; if they tap before the detection resolves, they earned the collection. The Chase state will immediately escalate tension — they still must escape.

---

### 5.7 ObjectiveToken Destroyed Before Collection

**Scenario:** A token's GameObject is destroyed (not deactivated) before the player collects it. This is not a designed gameplay event in MVP; it would result from a bug or editor action during development.

**Behavior:** `ObjectiveToken.OnDestroy` fires. If `_isCollected == false`, it calls `ObjectiveRegistry.Instance.Unregister(this)` and logs `Debug.LogWarning("ObjectiveToken destroyed before collection. TotalCount decremented.")`. The registry removes the token from its list and decrements `_totalCount`. The `CollectedCount / TotalCount` ratio is preserved, and the remaining tokens can still trigger `OnAllObjectivesCollected`. This prevents a permanently un-clearable level due to a bug destroying a token.

---

### 5.8 ObjectiveRegistry Not Present in Scene

**Scenario:** Level designer forgets to place the `ObjectiveRegistry` GameObject in the scene.

**Behavior:** `ObjectiveToken.Awake` calls `ObjectiveRegistry.Instance.Register(this)`. `SceneSingleton<ObjectiveRegistry>` does not auto-create (by design — it requires serialized scene references and must be authored). If the instance is null, `Register` throws a `NullReferenceException`. This surfaces as a visible error in the console at scene load, immediately directing the level designer to add the registry to the scene. No silent failure.

**Mitigation:** The level template (to be created by the Level Designer) must include the `ObjectiveRegistry` GameObject as a required scene component. A `ComponentAuditTool` rule should flag scenes missing `ObjectiveRegistry`.

---

### 5.9 Rapid Double-Tap on Same Token

**Scenario:** Player double-taps the interact input on the same frame or across two consecutive frames while facing a token.

**Behavior:** First tap: `OnInteractComplete` fires. `_isCollected` is set to true (first step, before any external calls). Second tap (same or next frame): `OnInteractComplete` is called again by PIS (if `CanInteract` has not yet propagated — see Race Condition note below), but the guard at Rule OS-1 step 1 returns immediately. `RegisterCollection` is called exactly once. No double-count. No duplicate VFX/SFX.

**Race condition note:** `CanInteract` is set to false in step 2 of `OnInteractComplete`, before `gameObject.SetActive(false)` in step 7. On the next Update frame after step 7, the token's collider is deactivated and it disappears from the PIS overlap query. Between the first and second tap (if both arrive in the same input update cycle), the `_isCollected` guard is the primary defense. The `CanInteract` flag is the secondary defense (checked by PIS at dispatch time). Both guards are required.

---

## 6. Dependencies

### 6.1 What This System Requires

| Dependency | System | What Is Required | Direction | Notes |
|-----------|--------|-----------------|-----------|-------|
| `IInteractable` | Infrastructure / Player Interaction System | Interface definition. `ObjectiveToken` implements it. PIS owns the interface; Objective System is a consumer. | Inbound | `ObjectiveToken` must conform to all `IInteractable` rules without exception. |
| `InteractableBase` | Infrastructure | Abstract base class providing empty default implementations of `OnInteractProgress` and `OnInteractCancelled`. `ObjectiveToken` extends it. | Inbound | `InteractableBase` must expose a virtual/override pattern compatible with `IInteractable`. |
| `PlayerInteraction` | Player / Player Interaction System | Calls `ObjectiveToken.OnInteractComplete(PlayerController)` when the player taps interact on a token in range. Also enforces Chase/airborne blocking before dispatch. | Inbound | Objective System does not call PIS directly. PIS calls Objective System via the `IInteractable` contract. |
| `PlayerController.IsBeingChased` | Player | Blocking condition checked by PIS. If true, `OnInteractComplete` is never called. Objective System inherits this block for free. | Inbound (via PIS) | No direct dependency on `PlayerController` from `ObjectiveToken`. PIS mediates. |
| `GameManager` | GameLoop | `GameManager.CurrentState` queried by `ObjectiveToken.OnInteractComplete` (defensive guard) and by `ObjectiveRegistry` to close registration at `Playing` state. | Inbound | `ObjectiveToken` uses `GameManager.Instance.CurrentState`. |
| `AudioManager` | Audio | `AudioManager.Instance.Play(SFXKey)` called during collection. | Inbound | `ObjectiveData.CollectionSFXKey` must map to a valid `SoundID` in `SoundLibrary`. |
| `ObjectiveData` | Data (`ScriptableObject`) | Per-token configuration: VFX prefab, SFX key, display name, icon sprite. Assigned to each `ObjectiveToken` via serialized field in the Inspector. | Inbound | See Section 7 for field definitions. |
| `PlayerInteractionData.PromptIcons` | Data (PIS ScriptableObject) | Must contain a `"collect"` key mapped to a sprite. `ObjectiveToken.PromptIconKey` returns `"collect"`. | Inbound | Art team must register this key in `PlayerInteractionData` before the first level is playtested. |

---

### 6.2 What This System Provides

| Provided To | System | What Is Provided | Direction | Notes |
|------------|--------|-----------------|-----------|-------|
| Level Exit System | Gameplay | `ObjectiveRegistry.OnAllObjectivesCollected` event. Fires when all tokens are collected. Level Exit subscribes and activates the exit trigger. | Outbound | The Level Exit System must subscribe to this event in its own `Awake` or `OnEnable`. |
| Two-Phase Level Structure | Gameplay | `ObjectiveRegistry.OnAllObjectivesCollected` event. The Two-Phase system subscribes and triggers Phase 2 escalation (seeker speed, global alert, music). | Outbound | Escalation timing is owned by Two-Phase system. Objective System only fires the signal. |
| HUD | UI | `ObjectiveRegistry.CollectedCount` (int), `ObjectiveRegistry.TotalCount` (int), `ObjectiveRegistry.OnObjectiveCollected` event. HUD subscribes to `OnObjectiveCollected` and reads the two count properties to refresh its display. | Outbound | HUD must not poll per-frame. It updates only on event. |
| Level Timer + Stats | Progression | `ObjectiveRegistry.OnObjectiveCollected` — for recording time-to-first-objective. `ObjectiveRegistry.OnAllObjectivesCollected` — for recording time-to-all-objectives. | Outbound | Level Timer reads timestamps from these events and stores them in the session stats. |

---

### 6.3 Ownership Boundaries (Explicit)

The following behaviors are explicitly NOT owned by the Objective System:

| Behavior | Actual Owner |
|----------|-------------|
| Seeker speed increase after last token | Two-Phase Level Structure |
| Global seeker alert after last token | Two-Phase Level Structure (via Seeker AI's public state API) |
| Exit door unlocking / becoming interactable | Level Exit System |
| HUD counter visual display | HUD System |
| Sound propagation of the collection SFX | Audio Manager + Sound Propagation Model |
| Detection of seeker proximity to the token | Detection System (seeker's patrol; never Objective System) |
| Deciding which seeker "guards" which token | Level design — not any runtime system |
| Session stat recording (time-to-collect) | Level Timer + Stats |

---

### 6.4 Bidirectional Contract Notes

Per design document standards, all dependency directions must be reflected in related GDDs:

- **Player Interaction System GDD (design/gdd/player-interaction-system.md):** Section 6.2 already lists "Objective System" as a downstream recipient of `OnInteractComplete(PlayerController)`. No update required.
- **Systems Index (design/gdd/systems-index.md):** Must be updated to reflect Objective System status as "Designed" once this GDD is approved.
- **Level Exit System GDD (not yet written):** When authored, must document its subscription to `OnAllObjectivesCollected` in its Dependencies section.
- **Two-Phase Level Structure GDD (not yet written):** When authored, must document its subscription to `OnAllObjectivesCollected` in its Dependencies section.
- **HUD GDD (not yet written):** When authored, must document its subscription to `OnObjectiveCollected` and its reads of `CollectedCount` / `TotalCount`.

---

## 7. Tuning Knobs

### 7.1 ObjectiveData ScriptableObject (Per-Token Configuration)

`ObjectiveData` is a new ScriptableObject to be created at `Assets/_Project/Scripts/Data/ObjectiveData.cs`. Each `ObjectiveToken` prefab holds a serialized reference to an `ObjectiveData` asset. Multiple tokens may share the same `ObjectiveData` asset (e.g., all relics in a level look and sound the same) or each may reference a unique asset (e.g., a named artifact with a unique icon).

| Field | Type | Default | Safe Range | Category | Description |
|-------|------|---------|-----------|----------|-------------|
| `DisplayName` | `string` | `"Relic"` | 1–20 characters | Feel | Human-readable name shown in HUD or level summary. Not shown in the world-space prompt (that uses `PromptLabel` on the token). |
| `CollectionVFXPrefab` | `GameObject` | (particle prefab) | Must be non-null | Feel | Self-destroying particle system instantiated at the token's world position on collection. Duration: 1.0–2.0 s. Should not obstruct gameplay — brief flash, not a sustained effect. |
| `CollectionSFXKey` | `SoundID` (enum) | `SoundID.ObjectiveCollect` | Must exist in SoundLibrary | Feel | Audio event played on collection via `AudioManager`. A new `SoundID.ObjectiveCollect` entry must be added to `SoundLibrary.cs`. |
| `ObjectiveIconSprite` | `Sprite` | (relic icon sprite) | Non-null | Feel | Icon displayed in the HUD counter. Distinct from the `PromptIconKey` world-space icon. The HUD uses this to show what type of objective was collected. |

**No curve or gate knobs exist in `ObjectiveData`.** The Objective System's tuning is minimal by design — collection is a binary event, not a graduated system. All difficulty tuning for the collection challenge comes from level design (patrol timing, room geometry) and the Detection System (suspicion thresholds), not from the Objective System itself.

---

### 7.2 ObjectiveRegistry Configuration (Scene-Level Knobs)

| Knob | Location | Type | Default | Safe Range | Category | Description |
|------|----------|------|---------|-----------|----------|-------------|
| `MaxTokensPerLevel` | `ObjectiveRegistry` (serialized field) | int | 8 | 1–12 | Gate | Hard cap on tokens the registry will accept via `Register`. Tokens beyond this cap log a `Debug.LogError` and are rejected. Prevents accidental O(n) issues in scenes with many prefabs. Increase for puzzle-heavy levels. |

---

### 7.3 Per-Token Inspector Fields (On ObjectiveToken Component)

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `_objectiveData` | `ObjectiveData` | (assigned in prefab) | Required. `InteractableBase.Awake` (or `ObjectiveToken.Awake`) must `Debug.Assert` this is non-null. |

---

### 7.4 Inherited Tuning (From Player Interaction System)

The following knobs affect collection feel but are owned by `PlayerInteractionData`, not `ObjectiveData`. They are listed here for the tuner's awareness:

| Knob | Owner | Default | Effect on Collection |
|------|-------|---------|---------------------|
| `InteractRadius` | `PlayerInteractionData` | 1.5 m | How close the player must be to see and tap the token |
| `InteractAngle` | `PlayerInteractionData` | 60° | How directly the player must face the token |
| `PromptFadeInTime` | `PlayerInteractionData` | 0.1 s | How quickly the "Take" prompt appears as the player approaches |

These are not duplicated here. Adjust them in `PlayerInteractionData` if collection feel requires wider range or faster prompt response.

---

### 7.5 Values That Must Never Be Tuned

The following are fixed constants, not tuning knobs. Changing them would break the system's design contract:

| Value | Fixed At | Rationale |
|-------|----------|-----------|
| `RequiresHold` | `false` | The tap-vs-hold distinction between collection and hiding is load-bearing for Phase 1 tension. Making collection a hold would collapse the mechanical differentiation. |
| `PromptLabel` | `"Take"` (12-char max enforced by PIS) | Consistent language across all MVP tokens. May vary per token type in later content — but must be specified per `ObjectiveToken` component, not tuned globally. |
| `PromptIconKey` | `"collect"` | All MVP tokens share the same icon category. Changing this per-token requires adding the new key to `PlayerInteractionData.PromptIcons` first. |

---

## 8. Acceptance Criteria

### Functional (pass/fail, verifiable in Unity Play Mode or Edit Mode)

- [ ] **AC-OS-01 — Single-token collection fires OnObjectiveCollected:** One `ObjectiveToken` in scene. Player approaches and taps interact while in range and facing. `ObjectiveRegistry.OnObjectiveCollected` event fires exactly once. Verify via a test listener component that logs the event. PASS: exactly one log entry. FAIL: zero entries (event didn't fire) or more than one (double-fire).

- [ ] **AC-OS-02 — CollectedCount increments on collection:** Before collection: `ObjectiveRegistry.CollectedCount == 0`. After collection: `ObjectiveRegistry.CollectedCount == 1`. Verify in Play Mode via a debug Inspector watch or test component. PASS: count equals 1. FAIL: count is 0 or greater than 1.

- [ ] **AC-OS-03 — TotalCount is correct at Playing state:** One token placed in scene. `GameManager` transitions to `Playing`. `ObjectiveRegistry.TotalCount == 1`. PASS: value equals number of tokens placed. FAIL: value is 0 or does not match placed count.

- [ ] **AC-OS-04 — OnAllObjectivesCollected fires on last token:** One token in scene. Collect it. `ObjectiveRegistry.OnAllObjectivesCollected` fires exactly once. Verify via test listener. PASS: exactly one log entry, fires on the same frame as collection. FAIL: fires zero or more than once.

- [ ] **AC-OS-05 — OnAllObjectivesCollected does NOT fire before all tokens collected (multi-token):** Three tokens in scene. Collect first: `OnAllObjectivesCollected` must NOT fire. Collect second: must NOT fire. Collect third: must fire exactly once. PASS: event fires only after third collection. FAIL: fires early.

- [ ] **AC-OS-06 — Token deactivates after collection:** Collect a token. On the same frame, the token's GameObject becomes inactive (`gameObject.activeInHierarchy == false`). Verify in Play Mode. PASS: token is invisible and its collider is inactive after collection tap. FAIL: token remains visible.

- [ ] **AC-OS-07 — CanInteract is false after collection:** Collect a token. Move away and return within range. `token.CanInteract` returns false. The Player Interaction System shows a disabled prompt (grayed icon, X overlay) rather than an active "Take" prompt. PASS: disabled prompt visible, no interaction possible. FAIL: active prompt visible after collection.

- [ ] **AC-OS-08 — Chase block prevents collection:** Set `PlayerController.IsBeingChased = true`. Position player within range and facing a token. Tap interact. `OnInteractComplete` is not called. `CollectedCount` remains 0. PASS: no collection occurs. FAIL: collection fires during Chase.

- [ ] **AC-OS-09 — Collection VFX spawns at token position:** Collect a token. A particle system GameObject is instantiated at the token's world position. Verify via Scene view inspection immediately after collection tap. PASS: particle system visible at expected world position. FAIL: no VFX spawned, or spawned at origin (0,0,0).

- [ ] **AC-OS-10 — Collection SFX plays on collection:** Collect a token. An audio event fires for `SoundID.ObjectiveCollect`. Verify via AudioManager debug logging or Unity Audio Mixer inspector. PASS: audio event fires once. FAIL: no audio event.

- [ ] **AC-OS-11 — Double-tap produces no double-collection:** Simulate two `OnInteractPerformed` events arriving on consecutive frames while facing the same token. `CollectedCount` is 1 (not 2). `OnObjectiveCollected` fires once (not twice). PASS: single collection. FAIL: double-count.

- [ ] **AC-OS-12 — Zero tokens logs an error and does not fire phase transition:** Scene with no `ObjectiveToken` prefabs placed. Enter Play Mode. `Debug.LogError` message appears in Console referencing zero token count. `OnAllObjectivesCollected` never fires. The Level Exit (if present) remains locked. PASS: error logged, exit locked. FAIL: phase transition fires on level load.

- [ ] **AC-OS-13 — Token destroyed before collection decrements TotalCount:** Destroy an `ObjectiveToken` GameObject via script while `_isCollected == false`. `ObjectiveRegistry.TotalCount` decrements by 1. A `Debug.LogWarning` is emitted. The remaining tokens can still trigger `OnAllObjectivesCollected` if all remaining tokens are collected. PASS: count decremented, warning logged, level still clearable. FAIL: TotalCount unchanged (level permanently un-clearable).

- [ ] **AC-OS-14 — Registration is closed after Playing state:** Call `ObjectiveRegistry.Instance.Register(someToken)` after `GameManager.CurrentState == Playing`. A `Debug.LogWarning` is emitted. `TotalCount` does not increase. PASS: warning logged, count unchanged. FAIL: count increases silently.

- [ ] **AC-OS-15 — HUD counter updates on each collection:** Three tokens in scene. HUD displays "0 / 3" on level start. After first collection: "1 / 3". After second: "2 / 3". After third: "3 / 3". PASS: each collection immediately updates the HUD display. FAIL: HUD shows stale count, or updates only on level-end event.

- [ ] **AC-OS-16 — No per-frame GC allocations from ObjectiveRegistry:** Profile in Unity Profiler with player walking near three tokens in Play Mode. `ObjectiveRegistry.Update` (if any) shows 0 B GC Alloc per frame. No List/array allocations in the hot path. PASS: 0 B GC Alloc. FAIL: any allocation in steady-state play.

- [ ] **AC-OS-17 — Script execution order ensures registry Awakes before tokens:** Place three tokens in scene. Enter Play Mode. All tokens register successfully (`TotalCount == 3` at Playing state). No `NullReferenceException` from `ObjectiveRegistry.Instance` being null during token Awake. PASS: all tokens registered, no exceptions. FAIL: NullReferenceException or incomplete registration count.

- [ ] **AC-OS-18 — OnAllObjectivesCollected fires on the same frame as the final tap:** Instrument with a frame counter. Record the frame number when the player's interact input is processed. Record the frame number when `OnAllObjectivesCollected` fires. PASS: both frame numbers are identical. FAIL: `OnAllObjectivesCollected` fires on a later frame (asynchronous dispatch introduced somewhere in the chain).

---

### Experiential (validated via observed play sessions — marked with an asterisk)

- [ ] **AC-OS-19* — Player identifies the objective by exploration, not instruction:** Naive player in a test level with one token and one patrol seeker, no UI tutorial. Target: ≥75% of testers visually locate the objective within 60 seconds of the level starting, without being told to look for it. PASS: testers spontaneously approach the token. FAIL: testers wander without goal, requiring instruction.

- [ ] **AC-OS-20* — Phase 2 escalation reads as a consequence of collection:** After collecting the last token and seeing Phase 2 activate (seeker speed increase, music shift, exit light on), ask players: "What caused the game to change?" PASS: ≥80% of testers attribute the change to collecting the relic, not to a timer or seeker behavior. FAIL: players perceive the escalation as random or externally triggered.

- [ ] **AC-OS-21* — Collection feels decisive, not accidental:** After a play session, ask: "Did you ever accidentally collect the objective before you intended to?" PASS: ≤15% of testers report an accidental collection. FAIL: >15% report accidentally triggering collection, suggesting the `InteractRadius` or player approach angle is too permissive relative to the patrol spacing.
