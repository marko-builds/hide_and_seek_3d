# Checkpoint System

> **Status**: Approved
> **Author**: game-designer
> **Last Updated**: 2026-03-06
> **Implements Pillar**: Legible Jeopardy (Pillar 3), The Room Has Rules (Pillar 1)
> **Design Order**: #12 — Foundation Layer

---

## 1. Overview

The Checkpoint System is the failure-recovery backbone of UNSEEN. When a seeker catches the player, the system intercepts the caught event, suspends the gameplay scene, plays a brief CAUGHT transition, resets all seeker states and positions, restores the player to the level's authored respawn point, and resumes play — all without reloading the scene. The system owns one `CheckpointMarker` component (one per level scene, placed by the level designer), one `CheckpointManager` scene component that orchestrates the respawn sequence, and the policy governing what resets and what does not on each respawn. By design, every catch triggers a respawn in MVP — `GameManager.TriggerLose()` is never called from a caught event in MVP; the `LoseConditionEvaluator` stub is superseded by this system. The player retains all objectives collected before the catch, all environmental knowledge, and phase state corresponding to their collection progress — only their position and the seekers' states reset. This design enforces the core philosophy: failure is a setback that resets position and threat, not a punishment that erases progress.

---

## 2. Player Fantasy

**Target MDA Aesthetics**: Challenge (failure acknowledged, lesson delivered), Sensation (the transition from caught to reset is a gut-punch followed by composure), Competence (returning to the room with new knowledge is a skill amplifier, not a time sink).

### Failure as the Teacher, Not the Executioner

The moment of being caught in UNSEEN carries a specific emotional signature. The player is not supposed to feel punished — they are supposed to feel *informed*. The seeker caught them because the player made a mistake in their read of the room: they misjudged the patrol arc, underestimated the catch radius, chose a bad angle. The CAUGHT transition is the moment the game says "here is your failure, clearly labeled." That moment of labeling is critical. A player who does not understand why they were caught learns nothing. A player who understands the exact moment and cause of the failure files that knowledge away and returns to the room with a better model.

The respawn must feel like *reloading a save*, not like *starting over*. There is a precise emotional sequence the system must produce:

1. **Acknowledgment (0.0 – 0.4s):** The catch animation fires. The player sees the seeker halt over them. The world freezes. This brief moment of stillness communicates: "you were caught here, right now, in this spot." The cause is visible and readable (Pillar 3 — Legible Jeopardy).

2. **Transition (0.4 – 1.2s):** The screen fades to black. A CAUGHT title card appears at center screen. Time feels suspended. The player takes a breath — not panic, not frustration, but assessment. The music cuts. The silence is intentional: it is the space where the player replays the last 10 seconds mentally and identifies their mistake.

3. **Return (1.2 – 1.8s):** The screen fades back in. The player is standing at the `CheckpointMarker` position. The seekers have returned to their patrol starting positions. The room is recognizable. The objective collection state is intact. The player is not weaker or less equipped. They are simply back at the start of the room, with the same tools and more knowledge than they had before.

4. **Ready (1.8s onward):** The player is in control. They can immediately observe, plan, and execute again. The HUD indicators are reset. There is no recovery delay, no respawn invincibility window, no confirmation prompt. The game trusts the player to be ready.

**SDT anchor:** Competence — the respawn is explicitly framed as a skill loop, not a punishment loop. The player re-enters the room with a better mental model of the seeker's behavior. Autonomy — the player decides when and how to try again, immediately, with no gate. Relatedness — the player's relationship with the room deepens on each attempt; the room starts to feel known, not foreign.

**What the player must NEVER feel:** randomness, unfairness, wasted time, or opacity. If any respawn produces the sensation "I don't know why that happened" or "I have to do all of that again from scratch?", the system has failed Pillar 3 or the educational-failure philosophy.

---

## 3. Detailed Rules

### 3.1 CheckpointMarker Component

**Rule CP-1 (CheckpointMarker Definition).** `CheckpointMarker` is a simple `MonoBehaviour` with no runtime logic — it is a position anchor authored by the level designer.

```csharp
// Assets/_Project/Scripts/GameLoop/CheckpointMarker.cs
public class CheckpointMarker : MonoBehaviour
{
    [Tooltip("Human-readable label for this checkpoint. Used in editor and debug logs only.")]
    public string checkpointLabel = "Room Start";

    // Runtime: CheckpointManager reads this transform.position on Awake and on each respawn.
    // No other runtime state.
}
```

**Rule CP-2 (One CheckpointMarker Per Level).** Each level scene contains exactly one `CheckpointMarker` GameObject. The position and rotation of this GameObject's `Transform` define the respawn position and facing direction. If a scene contains zero `CheckpointMarker` instances, `CheckpointManager.Awake` logs a `Debug.LogError` and caches the player's initial world position as a fallback (see Edge Case EC-5). If a scene contains more than one, `CheckpointManager.Awake` logs a `Debug.LogWarning`, uses the first found in scene hierarchy order, and ignores all others.

**Rule CP-3 (Placement Authority).** The `CheckpointMarker` is placed by the level designer. Design convention: place it at the room's entry point (the door, the top of the stairs, the alcove opening) — the natural place a returning player would orient from. The marker must not be placed inside a hiding spot, inside a seeker patrol path, or within `catchRadius` (1.2m) of any seeker's `PatrolRoute[0]` position. These constraints are validated by `CheckpointManager.Awake` with `Debug.LogWarning` for violations but without blocking play.

**Rule CP-4 (CheckpointMarker Is Not a Trigger).** The `CheckpointMarker` has no `Collider` component. The player does not "activate" it by walking over it. It is a static position marker, not an interactive object. Multiple-checkpoint progression (mid-level checkpoints) is explicitly out of scope for MVP. Future expansion is supported by the `checkpointLabel` field and the `CheckpointManager`'s position-caching architecture.

---

### 3.2 CheckpointManager Component

**Rule CP-5 (CheckpointManager Definition).** `CheckpointManager` is a `MonoBehaviour`, not a `Singleton<T>` or `SceneSingleton<T>`. It is placed on the level scene's `LevelManager` GameObject alongside `LevelPhaseManager` and `SeekerRegistry`. It does not need cross-scene access; it is destroyed and re-created on scene load. One `CheckpointManager` per scene.

**Rule CP-6 (CheckpointManager Awake).** On `Awake`, `CheckpointManager`:
1. Finds the `CheckpointMarker` in the scene (`FindObjectOfType<CheckpointMarker>()`). Caches its `Transform.position` and `Transform.rotation` as `_respawnPosition` and `_respawnRotation`. Logs an error if not found (Rule CP-2 fallback).
2. Caches a reference to `PlayerController` (via `FindObjectOfType<PlayerController>()`). Logs an error if not found.
3. Does NOT yet subscribe to the caught event. Subscription happens in `OnEnable` to support proper enable/disable lifecycle.

**Rule CP-7 (Event Subscription).** `CheckpointManager.OnEnable` subscribes to `GameManager.OnPlayerCaught`. `CheckpointManager.OnDisable` unsubscribes. `GameManager.OnPlayerCaught` is a new static C# event added to `GameManager` as part of implementing this system:

```csharp
// Addition to GameManager.cs:
public static event Action OnPlayerCaught;

// Called by EnemyController on Caught state entry:
public void NotifyPlayerCaught()
{
    if (CurrentState != GameState.Playing) return;
    OnPlayerCaught?.Invoke();
}
```

`NotifyPlayerCaught()` replaces the previously stubbed `HandlePlayerCaught()` pattern in `LoseConditionEvaluator`. The `LoseConditionEvaluator` stub is superseded by this system and must be disabled (the comment-block TODO is removed; the component remains as an empty placeholder for a future permadeath mode). `GameManager.TriggerLose()` is NOT called from `NotifyPlayerCaught()`. In MVP, every caught event triggers a respawn, never a permanent lose state.

**Rule CP-8 (Idempotency Guard).** `CheckpointManager` maintains a `bool _respawnInProgress` flag. When `HandlePlayerCaught` is called:
- If `_respawnInProgress == true`, return immediately and do nothing. This handles the edge case where two seekers simultaneously enter Caught and both call `GameManager.NotifyPlayerCaught()` in the same frame (see Edge Case EC-6).
- If `_respawnInProgress == false`, set it to `true` and begin the respawn sequence (Rule CP-9).

**Rule CP-9 (Respawn Sequence — Exact Steps).** The respawn sequence is a Coroutine started by `CheckpointManager.HandlePlayerCaught`. The sequence is frame-precise:

**Frame 0 (Caught event received):**
1. Set `_respawnInProgress = true`.
2. Start Coroutine `ExecuteRespawnSequence()`.

**Inside ExecuteRespawnSequence() — Phase A: World Freeze**

3. Freeze all seeker NavMeshAgents: iterate `SeekerRegistry.Instance.GetAll()` and set each seeker's `NavMeshAgent.isStopped = true`. Do not change their state machine or suspicion — the freeze is visual only, preventing them from walking away before the fade.
4. Disable player input: call `PlayerInputHandler.Instance.SetInputEnabled(false)`. The player cannot move, hide, throw, or interact during the respawn sequence.
5. Disable player physics velocity: call `PlayerController.FreezeMovement()` (a new method that zeroes the `Rigidbody.linearVelocity` and `Rigidbody.angularVelocity` and sets `Rigidbody.isKinematic = true` for the duration).

**Wait:** `yield return WaitFor.Seconds(caughtFreezeDelay)` — default 0.4s. This is the brief world-frozen moment where the player sees the seeker's catch animation complete before the screen fades. (See Tuning Knobs, Section 7.)

**Inside ExecuteRespawnSequence() — Phase B: Fade Out and Title Card**

6. Call `RespawnUI.Instance.FadeOut(fadeDuration)` — a new UI method that fades the screen to black over `fadeDuration` seconds (default 0.4s). `RespawnUI` owns the CanvasGroup and the CAUGHT text; `CheckpointManager` calls it by interface, not by direct class reference.
7. Wait for fade to complete: `yield return WaitFor.Seconds(fadeDuration)`.
8. Call `RespawnUI.Instance.ShowCaughtCard()` — displays the CAUGHT text overlay at screen center. The CAUGHT card appears after the fade is complete (screen is fully black). The text is visible for `caughtCardDisplayDuration` seconds (default 0.6s).
9. Wait: `yield return WaitFor.Seconds(caughtCardDisplayDuration)`.

**Inside ExecuteRespawnSequence() — Phase C: State Reset (all on black)**

All state reset operations run while the screen is fully black, invisible to the player. Order is mandatory:

10. **Reset player position and rotation:** Set `PlayerController.transform.position = _respawnPosition` and `PlayerController.transform.rotation = _respawnRotation`. Set `Rigidbody.isKinematic = false` to re-enable physics.
11. **Reset player hiding state:** If `PlayerHiding.IsHiding == true`, call `PlayerHiding.ForceExit()`. This exits the hiding spot, re-enables the player's collider, and fires the hiding spot's `OnHiderExited` event. The hiding spot remains available (it does not become permanently occupied).
12. **Reset player carrying state:** If `PlayerInteraction.IsCarryingThrowable == true`, call `PlayerInteraction.ForceDropCarried()`. The throwable is returned to its object pool at its pre-carry pool-return position (see Edge Case EC-3).
13. **Determine respawn phase state** (see Rule CP-11 and CP-12). This step evaluates `ObjectiveRegistry.CollectedCount` vs. `ObjectiveRegistry.TotalCount` to determine whether Phase 2 must be reset.
14. **Reset all seeker states** (see Rule CP-10):
    a. For each seeker in `SeekerRegistry.Instance.GetAll()`: teleport to `PatrolRoute[0].position` and set rotation to `PatrolRoute[0].rotation`.
    b. Set `NavMeshAgent.isStopped = false` and re-enable NavMeshAgent.
    c. Call `EnemyController.ResetForRespawn()` — a new method on `EnemyController` (see Rule CP-10).
15. **Reset phase state** per Rule CP-11 or CP-12.
16. Increment `_respawnCount` (internal counter for Level Timer + Stats).
17. Fire `OnRespawn` C# event (for Level Timer + Stats and any future subscribers).

**Inside ExecuteRespawnSequence() — Phase D: Fade In and Resume**

18. Call `RespawnUI.Instance.HideCaughtCard()` — removes the CAUGHT text, leaving the screen black.
19. Call `RespawnUI.Instance.FadeIn(fadeDuration)` — fades from black to full view over `fadeDuration` seconds (default 0.4s).
20. Wait for fade in: `yield return WaitFor.Seconds(fadeDuration)`.
21. Re-enable player input: `PlayerInputHandler.Instance.SetInputEnabled(true)`.
22. Set `_respawnInProgress = false`.
23. Coroutine complete — the player is in control.

**Total sequence duration at defaults:** 0.4 + 0.4 + 0.6 + 0.4 = **1.8 seconds**.

---

### 3.3 What Resets on Respawn

**Rule CP-10 (Seeker Reset — Full Specification).** On each respawn, all active seekers reset as follows via `EnemyController.ResetForRespawn()`:

1. **Position teleport:** Set `NavMeshAgent.Warp(PatrolRoute[0].position)`. `NavMeshAgent.Warp()` is used (not `transform.position =`) because it repositions the agent correctly on the NavMesh, avoiding off-mesh placement. If `PatrolRoute[0]` is null or empty, the seeker logs `Debug.LogWarning` and does not teleport — it resumes from its current position.
2. **Rotation reset:** Set `transform.rotation = PatrolRoute[0].rotation`. If no authored rotation exists (identity quaternion on the waypoint), the seeker faces its patrol forward direction at default.
3. **State machine reset:** Force the seeker's `SeekerStateMachine` to enter the `Unaware` state directly, bypassing the normal `DetectionOutput.RequestedState` transition path. This is the one case where the state machine accepts a direct external state injection rather than waiting for Detection System output. The state machine exposes `ForceState(SeekerState targetState)` for this purpose only.
4. **Suspicion reset:** Set `DetectionOutput.SuspicionLevel = 0f` (or call a reset method on the Detection System's per-seeker context). Reset `catchDwellAccumulator = 0f`. Reset `timeSinceLastDirectLoS = 0f`.
5. **Phase 2 fields reset** (conditional — see Rule CP-11 and CP-12): If Phase 2 is also resetting, set `Phase2SuspicionFloor = 0f`, `Phase2SpeedMultiplier = 1.0f`, `Phase2SkipAlertScan = false`. If Phase 2 is NOT resetting, preserve the current Phase 2 field values.
6. **NavMeshAgent resume:** Set `NavMeshAgent.isStopped = false`. The seeker immediately begins patrolling from `PatrolRoute[0]`.
7. **Patrol waypoint index reset:** Reset the seeker's internal patrol waypoint index to 0, so it starts from the beginning of its route.
8. **Animation reset:** Fire `PatrolTrigger` on the seeker's Animator to return it to the patrol locomotion state.
9. **Audio reset:** If the seeker has any active audio cues from Chase or Alert states, stop them. The seeker is silent at respawn start.

**Why teleport, not walk back:** Walking the seeker back to its patrol start would require the seeker to navigate through the same room the player is trying to re-engage with, creating an awkward transitional state where the seeker is moving toward the player's respawn point rather than behaving as the player expects. Teleport happens while the screen is black — the player never sees the discontinuity. The room "resets" cleanly from the player's perspective.

---

### 3.4 What Does NOT Reset on Respawn

**Rule CP-11 (Objective Persistence — The Core Policy Decision).** All collected objectives persist across respawns. This applies unconditionally:

- If the player collected 2 of 3 objectives and was then caught, they respawn with 2 of 3 collected. The uncollected token remains in the room. Phase 1 is ongoing.
- If the player collected all 3 objectives (triggering Phase 2) and was then caught during Phase 2 escape, they respawn in Phase 2. The Level Exit remains unlocked. The seekers are still in their Phase 2 escalated state (suspicion floor = 60, speed multiplier = 1.3).

**Rationale for full objective preservation:**

The alternative (Option B — reset all objectives if caught in Phase 2) would force the player to repeat the Phase 1 collection sequence as a penalty for failing during Phase 2 escape. This violates both the educational-failure philosophy and Pillar 3 (Legible Jeopardy). The player who reached Phase 2 demonstrated sufficient Phase 1 mastery — they collected every objective without being caught. Resetting those objectives doesn't teach them anything about Phase 2 escape; it simply penalizes Phase 1 success and extends the loop artificially. Under full preservation, the player respawns in Phase 2 with the room in its harder configuration. This is harder than respawning in Phase 1, but it is a *legible* difficulty: the player is being asked to solve the specific problem that killed them (Phase 2 escape), not asked to repeat the problem they already solved (Phase 1 collection). The cost of failure in Phase 2 is therefore the harder respawn environment — a proportionate and informative penalty.

**What the player keeps across all respawns:**
- All `ObjectiveToken` collection state (collected tokens remain inactive/deactivated).
- `ObjectiveRegistry.CollectedCount` value.
- Level exit unlock state (if unlocked, it remains unlocked).
- The current `LevelPhase` state (Phase 1 or Phase 2), corresponding exactly to their collection count.

---

### 3.5 Phase State After Respawn

**Rule CP-12 (Phase 1 Respawn — No Phase Reset Needed).** If `ObjectiveRegistry.CollectedCount < ObjectiveRegistry.TotalCount` at the time of catch (the player was caught during Phase 1), the level is still in Phase 1. `LevelPhaseManager.CurrentPhase == Phase1_Find`. No phase reset is required. Seekers are reset per Rule CP-10 with no Phase 2 field modifications. All Phase 2 fields remain at their default values (`Phase2SuspicionFloor = 0f`, `Phase2SpeedMultiplier = 1.0f`, `Phase2SkipAlertScan = false`). The seekers return to their Phase 1 baseline behavior.

**Rule CP-13 (Phase 2 Respawn — Phase 2 Preserved).** If `ObjectiveRegistry.CollectedCount == ObjectiveRegistry.TotalCount` at the time of catch (the player was caught during Phase 2, all objectives collected), `LevelPhaseManager.CurrentPhase == Phase2_Escape`. Seekers reset their positions and suspicion per Rule CP-10, but their Phase 2 fields are re-applied immediately after the state machine reset:

```
// Step executed after EnemyController.ResetForRespawn() in Phase 2 respawn:
foreach (EnemyController seeker in SeekerRegistry.Instance.GetAll())
{
    seeker.Phase2SuspicionFloor = _escalationProfile.SuspicionFloor;   // e.g. 60f
    seeker.Phase2SpeedMultiplier = _escalationProfile.SpeedMultiplier; // e.g. 1.3f
    seeker.Phase2SkipAlertScan = _escalationProfile.SkipAlertScanInPhase2;
}
```

The `_escalationProfile` reference is obtained by `CheckpointManager` from `LevelPhaseManager.EscalationProfile` (a new public getter added to `LevelPhaseManager`). `CheckpointManager` does not own or duplicate the `EscalationProfile` reference — it reads it from `LevelPhaseManager` as the authoritative source.

On the next `FixedUpdate` after respawn, the Detection System's suspicion floor clamp re-applies (per F-TPS-3 in the Two-Phase Level Structure GDD), and seekers whose suspicion is below the floor (60) are snapped to 60, entering Searching exactly as they did on the original Phase 2 trigger. The player respawns at the `CheckpointMarker` in a Phase 2 room. The exit is unlocked. The seekers begin their Phase 2 searching behavior from their patrol start positions.

**Rule CP-14 (LevelPhaseManager State During Phase 2 Respawn).** When respawning in Phase 2, `LevelPhaseManager._phase2Triggered` remains `true`. `LevelPhaseManager.CurrentPhase` remains `Phase2_Escape`. `LevelPhaseManager` is NOT reset or re-initialized. Phase 2 was triggered legitimately and the player's collection state warrants Phase 2. Only the seeker positions and suspicion values reset; the level phase state does not.

**Rule CP-15 (OnPhaseChanged Does Not Refire on Respawn).** The `LevelPhaseManager.OnPhaseChanged` static event does NOT fire again during a Phase 2 respawn sequence. The event has already fired once (on original Phase 2 trigger). Downstream subscribers (HUD, Adaptive Music) already have their Phase 2 state active. Re-firing the event would produce a second Phase 2 visual sting and music transition, which is incorrect. `CheckpointManager` applies the Phase 2 seeker fields directly without routing through `LevelPhaseManager`'s transition sequence.

---

### 3.6 RespawnUI Component

**Rule CP-16 (RespawnUI).** `RespawnUI` is a separate `MonoBehaviour` component (not a `Singleton<T>` — it is scene-bound and found by `CheckpointManager` via `FindObjectOfType<RespawnUI>()` in `Awake`). It owns:
- A full-screen `CanvasGroup` covering the camera (set to `alpha = 0` at level start, blocking raycasts: `blocksRaycasts = true` throughout to prevent accidental clicks during transition).
- A `TextMeshProUGUI` component for the CAUGHT card text.
- Public coroutine-compatible methods: `FadeOut(float duration)`, `FadeIn(float duration)`, `ShowCaughtCard()`, `HideCaughtCard()`.

`CheckpointManager` calls these methods directly; the respawn logic drives the timing. `RespawnUI` does not initiate any state changes — it is a passive display component driven entirely by `CheckpointManager`.

**Rule CP-17 (CAUGHT Card Content).** The CAUGHT title card displays: the word "CAUGHT" in large type at the vertical center of the screen. No catch count, no advice text, no "try again" prompt. The minimalism is intentional: the player is processing the failure, not reading UI. Downstream iteration (adding a brief contextual tip, adding a catch count) requires UX review before implementation.

---

### 3.7 GameManager Integration

**Rule CP-18 (No TriggerLose in MVP).** `GameManager.TriggerLose()` is not called by any caught event in MVP. The Checkpoint System fully handles every caught event with a respawn. `TriggerLose()` remains on `GameManager` for future use (permadeath mode, limited-lives mode). The `LoseConditionEvaluator` stub's comment-block TODO is removed; the script file remains as an empty placeholder.

**Rule CP-19 (GameManager.NotifyPlayerCaught — Idempotency).** `GameManager.NotifyPlayerCaught()` guards against being called twice:
```csharp
public void NotifyPlayerCaught()
{
    if (CurrentState != GameState.Playing) return;
    OnPlayerCaught?.Invoke();
    // Does NOT call TriggerLose() in MVP.
}
```
If `CurrentState` is already `Win` or `Lose`, the caught event is silently dropped. This handles the race condition where the player reaches the exit and collects the last objective on the same frame as a catch (see Edge Case EC-4).

**Rule CP-20 (Respawn Count Tracking).** `CheckpointManager` maintains `public int RespawnCount { get; private set; }` incremented once per successful respawn sequence completion. This is the value `Level Timer + Stats` reads when recording the session's catch count. `OnRespawn` fires `Action OnRespawn` after incrementing, providing a hook for stats systems without coupling.

---

## 4. Formulas

### F-CP-1: Total Respawn Sequence Duration

```
totalRespawnDuration = caughtFreezeDelay + fadeDuration + caughtCardDisplayDuration + fadeDuration
```

| Variable | Type | Default | Source | Description |
|----------|------|---------|--------|-------------|
| `caughtFreezeDelay` | float | 0.4s | `CheckpointData` SO | World-frozen window before fade out begins; allows player to see the catch animation |
| `fadeDuration` | float | 0.4s | `CheckpointData` SO | Duration of each fade (fade-out and fade-in use the same value) |
| `caughtCardDisplayDuration` | float | 0.6s | `CheckpointData` SO | Time the CAUGHT card is displayed at full black; the player's reflection window |

**Example at defaults:**
```
totalRespawnDuration = 0.4 + 0.4 + 0.6 + 0.4 = 1.8 seconds
```

**Expected range:**
- Minimum (fast mode for testing): 0.1 + 0.1 + 0.1 + 0.1 = 0.4s
- Maximum (cinematic): 1.0 + 1.0 + 2.0 + 1.0 = 5.0s
- Target player experience: 1.5 – 2.0s (enough to process failure, not enough to break pacing)

---

### F-CP-2: Respawn Position

```
respawnPosition = CheckpointMarker.transform.position
respawnRotation = CheckpointMarker.transform.rotation
```

There is no offset or interpolation formula. The player's `Transform` is set exactly to the marker's world position and rotation via `NavMeshAgent.Warp()` (if the player has a NavMeshAgent) or direct `transform.position` assignment (if position is controlled by a `CharacterController` or `Rigidbody`). The choice of warp method depends on the player's movement component implementation — this is resolved by the implementing programmer based on the `PlayerController` architecture. The design requirement is: the player must appear at the marker's exact world position, standing on the NavMesh surface, facing the marker's forward direction.

| Variable | Type | Source | Description |
|----------|------|--------|-------------|
| `CheckpointMarker.transform.position` | Vector3 | Scene GameObject | World position of the level designer's authored respawn point |
| `CheckpointMarker.transform.rotation` | Quaternion | Scene GameObject | Facing direction at respawn; should face into the room, away from entry |

---

### F-CP-3: Seeker Reset Position

```
resetPosition = PatrolRoute[0].position
resetRotation = PatrolRoute[0].rotation
```

All seekers warp to their first patrol waypoint (`index 0`) on respawn. `NavMeshAgent.Warp(PatrolRoute[0].position)` is called. Waypoint index is reset to 0 so the patrol loop restarts from the beginning of the authored route.

| Variable | Type | Source | Description |
|----------|------|--------|-------------|
| `PatrolRoute[0].position` | Vector3 | `EnemyController` serialized field | World position of the first waypoint in the seeker's patrol route |
| `PatrolRoute[0].rotation` | Quaternion | `EnemyController` serialized field | Facing direction at patrol start; defines initial seeker orientation post-respawn |

---

### F-CP-4: Phase 2 Respawn Seeker Suspicion Start

For seekers in a Phase 2 respawn, the Detection System applies the suspicion floor on the first `FixedUpdate` after respawn:

```
// On first FixedUpdate after respawn (Detection System suspicion update):
initialSuspicion = 0f                               // set by ResetForRespawn()
floored          = Mathf.Max(0f, Phase2SuspicionFloor)   // = 60f
suspicion        = floored                          // = 60f
```

The seeker starts at suspicion 0 (from the reset), but on the next tick the floor clamps it to 60. The result: every seeker enters Searching within one physics tick of the player's respawn. The player's first second in a Phase 2 respawn is identical in threat level to their first second after the original Phase 2 transition.

---

## 5. Edge Cases

### EC-1: Player Caught During Phase 2 (All Objectives Collected)

**Scenario:** The player collected all objectives (Phase 2 is active) and is caught during Phase 2 escape.

**Behavior:** `CheckpointManager.HandlePlayerCaught` fires. `_respawnInProgress` guard passes. The respawn sequence runs. In Phase C (state reset on black screen): objective collection state is preserved (`ObjectiveRegistry.CollectedCount` remains equal to `TotalCount`). Level exit unlock state is preserved. Seekers reset per CP-10 with Phase 2 fields re-applied per CP-13. `LevelPhaseManager.CurrentPhase` remains `Phase2_Escape`. `OnPhaseChanged` does NOT refire. The player respawns at the `CheckpointMarker` in an active Phase 2 room. No additional HUD flash or audio sting fires. The player sees: the room at full Phase 2 alertness, seekers starting to search from their patrol origins, exit unlocked. Their task is unchanged: reach the exit.

**Design rationale:** This is the highest-stakes respawn scenario. It preserves the player's meaningful progress (Phase 2 was earned) while imposing a real consequence (the harder version of the room, now navigated from the start again). The Phase 2 room is a genuine challenge on re-entry — the player has demonstrated they can reach Phase 2 but not yet solve the escape. The respawn gives them another attempt at the specific problem they failed to solve.

---

### EC-2: Player Caught While Inside a Hiding Spot

**Scenario:** The player is in a hiding spot when the seeker catches them. (By design, seekers do not catch players inside hiding spots if `IHideable.IsConcealed == true` — however, if concealment is partial or the player is transitioning in/out of a spot, a catch may fire.)

**Behavior:** `CheckpointManager` receives the caught event. In Phase C (step 11), `PlayerHiding.ForceExit()` is called before the player position is set. This cleanly exits the hiding spot: the hiding spot's `OnHiderExited` event fires, the hiding spot's `IsOccupied` flag resets to false, the player's collider is re-enabled. The player then teleports to `_respawnPosition`. The hiding spot is fully available for the player's next attempt. No hiding spot state is permanently modified by a mid-hiding catch.

---

### EC-3: Player Caught While Carrying a Throwable

**Scenario:** The player picked up a throwable object and is holding it when caught.

**Behavior:** In Phase C (step 12), `PlayerInteraction.ForceDropCarried()` is called. The throwable is returned to the object pool. It is NOT respawned at its original world position — it returns to the pool at a safe off-screen position ready for the next pool checkout. This means the throwable is no longer in the room when the player respawns. If the throwable was the level's only distraction item and the player needed it, this creates a meaningful consequence of being caught while carrying. Level designers must account for this: if a throwable is essential to solving the room, either provide multiple throwables or place them in a recoverable position. This is a consequence that reinforces the "carry with intent" skill the system is designed to teach.

**Alternative considered and rejected:** Re-spawning the throwable at its original world position on respawn. Rejected because: it removes consequence from being caught while holding an item, and it requires tracking the original world position of every carried item across a respawn, adding state complexity with no design benefit.

---

### EC-4: Player Caught on the Same Frame as Win Condition (Exit Used)

**Scenario:** The player reaches the exit trigger and completes the hold interaction on the same `FixedUpdate` frame that a seeker's `catchDwellAccumulator` crosses `catchDwellTime`. Both `GameManager.TriggerWin()` and `GameManager.NotifyPlayerCaught()` are called in the same frame.

**Behavior:** `GameManager.TriggerWin()` or `GameManager.NotifyPlayerCaught()` will execute first depending on the frame order of their callers (Level Exit System fires `TriggerWin()` via `LevelExit.OnInteractComplete`; the Detection System fires `NotifyPlayerCaught()` via `EnemyController.Caught` state entry). The first call that executes changes `GameManager.CurrentState` to either `Win` or `Playing`-locked-out. The second call checks `CurrentState` and returns without effect:
- If `TriggerWin()` fires first: `CurrentState = Win`. `NotifyPlayerCaught()` checks `CurrentState != Playing`, returns immediately. Player wins. The catch does not trigger a respawn.
- If `NotifyPlayerCaught()` fires first: `OnPlayerCaught` fires, `CheckpointManager` starts the respawn coroutine. `TriggerWin()` fires: `CurrentState != Playing` (it is still `Playing` momentarily — this is a race).

**Resolution:** `CheckpointManager.HandlePlayerCaught()` must check `GameManager.CurrentState == GameState.Playing` before proceeding with the respawn sequence:
```csharp
private void HandlePlayerCaught()
{
    if (GameManager.Instance.CurrentState != GameState.Playing) return;
    if (_respawnInProgress) return;
    // ... proceed
}
```
This guard, combined with `TriggerWin()`'s own `CurrentState != Playing` guard, means only one terminal state can be entered. If win and catch fire simultaneously, whichever changes `GameState` first wins. In practice, the Level Exit System fires from a Hold Interaction callback (on `Update`), and the Detection System fires from `FixedUpdate`. These run in different phases of the same frame — the exact ordering depends on the frame's physics/update scheduling. To make the outcome deterministic: the Win condition takes precedence. `CheckpointManager` adds a final check before executing the respawn: if `GameManager.CurrentState == GameState.Win`, abort the respawn sequence and do nothing.

---

### EC-5: No CheckpointMarker in Scene

**Scenario:** The level designer forgot to place a `CheckpointMarker`, or the scene is a prototype level without one.

**Behavior:** `CheckpointManager.Awake` performs `FindObjectOfType<CheckpointMarker>()`. If result is null: log `Debug.LogError("CheckpointManager: No CheckpointMarker found in scene. Using player's initial position as fallback respawn point. Place a CheckpointMarker in the scene.")`. Cache `PlayerController.transform.position` and `PlayerController.transform.rotation` at the moment of `Awake` as the fallback `_respawnPosition` and `_respawnRotation`. The system remains functional: catches trigger the normal respawn sequence, and the player respawns at their level-load position. This may be an awkward or non-ideal spawn point, but it does not crash or break the game. The error log surfaces the oversight in development without requiring an editor validation step.

---

### EC-6: Multiple Seekers Catch the Player Simultaneously

**Scenario:** Two seekers both have `catchDwellAccumulator >= catchDwellTime` on the same `FixedUpdate` frame. Both call `GameManager.NotifyPlayerCaught()`.

**Behavior:** `GameManager.NotifyPlayerCaught()` fires `OnPlayerCaught` event both times. `CheckpointManager.HandlePlayerCaught()` runs twice. The first call: `_respawnInProgress == false`, proceeds, sets `_respawnInProgress = true`. The second call: `_respawnInProgress == true`, returns immediately. Only one respawn sequence is started. The player experiences a single, normal respawn. (See also Seeker AI GDD edge case: "GameManager must handle `OnPlayerCaught` as idempotent.")

---

### EC-7: Player Caught Immediately After Respawn (Within the Fade-In Window)

**Scenario:** The player respawns at the `CheckpointMarker` position, but a seeker's patrol route passes directly through the `CheckpointMarker` position. The seeker arrives at the respawn point as the player fades in.

**Behavior:** During the respawn sequence Phase D (fade-in), player input is disabled (`SetInputEnabled(false)`) until step 21 (the coroutine's final step). The player's collider is active and their position is set in Phase C (step 10). If a seeker reaches `catchRadius` during the fade-in and the `catchDwellAccumulator` crosses the threshold, a second `NotifyPlayerCaught()` will fire. `CheckpointManager` checks `_respawnInProgress`: if the previous respawn is still completing its fade-in, `_respawnInProgress == true` and the second catch is dropped. The fade-in completes. After `_respawnInProgress = false` (step 22), any subsequent catch is processed normally.

**Design mitigation:** Rule CP-3 requires the `CheckpointMarker` to not be placed within `catchRadius` (1.2m) of any seeker's `PatrolRoute[0]` position. Level designer validation (warning in `Awake`) enforces this convention. If the warning fires, the level designer must move either the marker or the seeker's patrol start.

---

### EC-8: Rapid Successive Catches (Stress Case)

**Scenario:** A seeker catches the player immediately after `_respawnInProgress = false` (within a frame or two of the previous respawn completing). The player is caught again before they have meaningfully engaged with the room.

**Behavior:** This is a valid gameplay outcome, not an error state. `_respawnInProgress == false`, so the second catch triggers a second full respawn sequence. `RespawnCount` increments to 2. From the player's perspective: they respawned, immediately got caught, and are respawning again. This is not prevented by the system — it is the correct, legible feedback that the `CheckpointMarker` placement is problematic (too close to a seeker's starting position) or that the player is not yet ready to engage with this room's difficulty. The catch count recorded in stats will flag this scenario in playtesting.

---

### EC-9: CheckpointManager Cannot Find SeekerRegistry

**Scenario:** `SeekerRegistry.Instance` is null at respawn time (not placed in scene, or destroyed before CheckpointManager runs).

**Behavior:** In step 14 of the respawn sequence (seeker reset), `CheckpointManager` null-checks `SeekerRegistry.Instance`. If null: log `Debug.LogError("CheckpointManager: SeekerRegistry not found. Seekers will not reset on respawn.")`. Skip seeker reset steps 14a-14i. The respawn sequence continues and completes normally: the player returns to the `CheckpointMarker`, but seekers remain in their current states and positions. This is a degraded but non-crashing behavior. The error log surfaces the configuration problem immediately in development.

---

## 6. Dependencies

### 6.1 What This System Requires

| Dependency | System | What Is Required | Direction | Notes |
|-----------|--------|-----------------|-----------|-------|
| `GameManager.OnPlayerCaught` (new static event) | GameLoop | Static `Action` event fired by `GameManager.NotifyPlayerCaught()` when a seeker enters Caught state | Inbound | New event and method to be added to `GameManager.cs`. Replaces `LoseConditionEvaluator` TODO stub. |
| `GameManager.CurrentState` | GameLoop | Read-only access to confirm `Playing` state before acting on caught event | Inbound | Prevents respawn from firing during Win or Lose states. |
| `SeekerRegistry.GetAll()` | Two-Phase Level Structure | List of all active `EnemyController` instances in the scene | Inbound | Used to iterate seekers for reset. `SeekerRegistry` is owned by Two-Phase Level Structure GDD. |
| `EnemyController.ResetForRespawn()` (new method) | Seeker AI | Per-seeker method that resets position, state machine, suspicion, waypoint index, and animation | Inbound | New method to be added to `EnemyController.cs`. Must also expose `PatrolRoute[0]` for warp target. |
| `EnemyController.Phase2SuspicionFloor` | Seeker AI / Two-Phase Level Structure | Writable float property on `EnemyController`; re-applied by `CheckpointManager` during Phase 2 respawn | Bidirectional | Already defined in Two-Phase Level Structure GDD Rule TPS-7. |
| `EnemyController.Phase2SpeedMultiplier` | Seeker AI / Two-Phase Level Structure | Writable float property; re-applied during Phase 2 respawn | Bidirectional | Already defined in Two-Phase Level Structure GDD Rule TPS-7. |
| `EnemyController.Phase2SkipAlertScan` | Seeker AI / Two-Phase Level Structure | Writable bool; re-applied during Phase 2 respawn | Bidirectional | Already defined in Two-Phase Level Structure GDD Rule TPS-7. |
| `LevelPhaseManager.CurrentPhase` | Two-Phase Level Structure | Read to determine whether respawn is Phase 1 or Phase 2 | Inbound | `CheckpointManager` reads `LevelPhaseManager.Instance.CurrentPhase` (or subscribes to `OnPhaseChanged` to track locally). |
| `LevelPhaseManager.EscalationProfile` (new public getter) | Two-Phase Level Structure | Read to re-apply Phase 2 seeker values during Phase 2 respawn | Inbound | New public getter to be added to `LevelPhaseManager`: `public EscalationProfile EscalationProfile => _escalationProfile;` |
| `ObjectiveRegistry.CollectedCount` | Objective System | Read to determine phase state at respawn time | Inbound | Already a public property on `ObjectiveRegistry`. |
| `ObjectiveRegistry.TotalCount` | Objective System | Read to determine phase state at respawn time | Inbound | Already a public property on `ObjectiveRegistry`. |
| `PlayerHiding.IsHiding` / `ForceExit()` | Player System | Checks and exits any active hiding state on respawn | Inbound | `ForceExit()` is a new method to be added to `PlayerHiding`. |
| `PlayerInteraction.IsCarryingThrowable` / `ForceDropCarried()` | Player System | Checks and drops any carried throwable on respawn | Inbound | `ForceDropCarried()` is a new method to be added to `PlayerInteraction`. |
| `PlayerInputHandler.SetInputEnabled(bool)` | Player System | Disables/re-enables player input during respawn sequence | Inbound | New method to be added to `PlayerInputHandler`. |
| `PlayerController.FreezeMovement()` | Player System | Zeroes velocity and sets kinematic during respawn freeze | Inbound | New method to be added to `PlayerController`. |
| `RespawnUI` | UI | Full-screen fade and CAUGHT card display | Inbound | New component to be created. `CheckpointManager` finds it via `FindObjectOfType<RespawnUI>()`. |
| `CheckpointData` ScriptableObject | Data | Timing values for the respawn sequence (delays, durations) | Inbound | New ScriptableObject defined in Tuning Knobs section. |

---

### 6.2 What This System Provides

| Provided To | System | What Is Provided | Direction | Notes |
|------------|--------|-----------------|-----------|-------|
| `Level Timer + Stats` | Progression | `CheckpointManager.RespawnCount` counter; `OnRespawn` C# event | Outbound | Level Timer + Stats subscribes to `OnRespawn` to record catch/respawn counts for session stats. |
| `Win / Game Over Screens` | UI | The absence of a `TriggerLose()` call means the Game Over screen does NOT display on catch in MVP. The Checkpoint System owns this routing decision. | Outbound (exclusion) | Game Over screen is only triggered by a future permadeath/lives system, not by MVP caught events. |
| `AudioManager` | Audio | (Indirect) — `RespawnUI` or `CheckpointManager` may call `AudioManager.Instance.Play(SoundID.RespawnStinger)` on fade-in. This is deferred to audio design. | Outbound | No audio SoundID is defined here; audio team must add `SoundID.CaughtSting` and `SoundID.RespawnStinger` to `SoundLibrary`. |

---

### 6.3 GDDs That Must Be Updated

| GDD | Required Update |
|-----|----------------|
| `design/gdd/seeker-ai.md` Section 6 (Dependencies) | Add Checkpoint System as a consumer of `EnemyController.ResetForRespawn()`. Document the new method requirement. |
| `design/gdd/seeker-ai.md` Section 3.6 (Caught Behavior) | Update: the seeker calls `GameManager.NotifyPlayerCaught()` (not `OnPlayerCaught()` directly — the method is `NotifyPlayerCaught`). |
| `design/gdd/two-phase-level-structure.md` Section 5.4 (Level Reloaded Mid-Phase 2) | Update: Phase 2 does NOT reload the scene on catch in MVP. The Checkpoint System handles catch without scene reload. This edge case description is now incorrect and must be revised. |
| `design/gdd/two-phase-level-structure.md` Section 6.2 | Add Checkpoint System as a reader of `LevelPhaseManager.EscalationProfile`. |
| `design/gdd/systems-index.md` | Update Checkpoint System status from "Not Started" to "Designed". Update progress tracker counts. |

---

## 7. Tuning Knobs

All timing values are fields on `CheckpointData`, a new ScriptableObject at `Assets/_Project/Scripts/Data/CheckpointData.cs`. No value is hardcoded in `CheckpointManager` or `RespawnUI`. One `CheckpointData` asset is serialized on the `CheckpointManager` inspector field.

```csharp
[CreateAssetMenu(menuName = "UNSEEN/CheckpointData")]
public class CheckpointData : ScriptableObject
{
    [Header("Respawn Sequence Timing")]
    [Tooltip("Seconds the world freezes after catch before fade begins. Player sees catch animation. Default: 0.4s.")]
    [Range(0.1f, 1.0f)]
    public float caughtFreezeDelay = 0.4f;

    [Tooltip("Duration of each screen fade (fade-out and fade-in share this value). Default: 0.4s.")]
    [Range(0.1f, 1.0f)]
    public float fadeDuration = 0.4f;

    [Tooltip("Seconds the CAUGHT title card is displayed at full black. Player reflection window. Default: 0.6s.")]
    [Range(0.1f, 2.0f)]
    public float caughtCardDisplayDuration = 0.6f;
}
```

| Knob | Category | Default | Safe Range | Extreme Low Effect | Extreme High Effect | Rationale for Default |
|------|----------|---------|-----------|-------------------|--------------------|-----------------------|
| `caughtFreezeDelay` | Feel | 0.4s | 0.1–1.0s | Near-instant freeze into fade; player doesn't see the catch animation resolve; failure feels instant and opaque | Long freeze; player stares at the static caught pose for an uncomfortable duration; frustrating | 0.4s is enough to see the seeker halt and the catch animation trigger (estimated 3-4 frames at 60fps visible before the fade). Provides legibility without dwelling. |
| `fadeDuration` | Feel | 0.4s | 0.1–1.0s | Very fast cut to black; jarring, more like a hard cut than a fade; can feel harsh | Slow fade; extends the total respawn time significantly; feels like the game is slow | 0.4s matches standard game fade convention. Fast enough to not feel sluggish, slow enough to not feel jarring. |
| `caughtCardDisplayDuration` | Gate | 0.6s | 0.1–2.0s | Player barely registers the CAUGHT text before the fade-in begins; no reflection window | Card holds for 2 seconds; forces a long pause; breaks flow for experienced players | 0.6s is the target reflection window — enough to read the word, enough to breathe before returning. Post-launch iteration may expose this value to a player-facing accessibility option for players who find it too short or too long. |

**Values that must NOT be changed without design review:**

- `caughtFreezeDelay` must not be set below 0.1s. A freeze below one frame duration (0.016s at 60fps) provides no legibility window — it becomes an invisible "catch teleport" that breaks Pillar 3.
- `fadeDuration` must not be set to 0. A zero-duration fade is a hard cut that bypasses the visual transition entirely, making the respawn feel like a crash or glitch.
- `caughtCardDisplayDuration` must not be set to 0. The CAUGHT card must display for at least one frame — its existence communicates to the player that they were caught (not that the game crashed). 0.1s minimum is enforced by the `Range` attribute.

---

## 8. Acceptance Criteria

### Functional

- [ ] **AC-01 — Respawn fires on caught event:** When a seeker's Detection System fires `RequestedState == Caught` and `EnemyController` calls `GameManager.NotifyPlayerCaught()`, the `CheckpointManager.HandlePlayerCaught()` coroutine starts within the same frame. Verified by log or test that `_respawnInProgress` becomes `true` before the next frame.
- [ ] **AC-02 — Player position correct after respawn:** After the respawn sequence completes, `PlayerController.transform.position` equals `CheckpointMarker.transform.position` within 0.01 units (floating-point tolerance). Verified in a test scene with known marker and player positions.
- [ ] **AC-03 — Player rotation correct after respawn:** After respawn, `PlayerController.transform.forward` equals `CheckpointMarker.transform.forward` within 0.001 (dot product tolerance). Player faces the marker's authored direction.
- [ ] **AC-04 — Player input disabled during respawn:** During the respawn coroutine (after step 4, before step 21), `PlayerInputHandler.InputEnabled == false`. Verified by checking the flag at each phase of the coroutine in a test runner.
- [ ] **AC-05 — Seeker states reset on respawn:** After respawn, all seekers in `SeekerRegistry.GetAll()` are in `Unaware` state, at `PatrolRoute[0].position` (within NavMesh snap tolerance), with `SuspicionLevel == 0` (before the first FixedUpdate applies any floor). Verified per seeker.
- [ ] **AC-06 — Seeker waypoint index reset:** After respawn, each seeker's internal patrol waypoint index is 0. On the first FixedUpdate after respawn, the seeker moves toward `PatrolRoute[0]` (or immediately toward `PatrolRoute[1]` if it is already at index 0 and arrives). Verified by observing seeker movement direction post-respawn.
- [ ] **AC-07 — Objective collection persists — Phase 1:** If 2 of 3 objectives are collected and the player is caught, after respawn: `ObjectiveRegistry.CollectedCount == 2`, `ObjectiveRegistry.TotalCount == 3`, and the two collected tokens remain deactivated (`gameObject.activeSelf == false`). The uncollected token remains active and interactable.
- [ ] **AC-08 — Objective collection persists — Phase 2:** If all 3 objectives are collected (Phase 2 active) and the player is caught, after respawn: `ObjectiveRegistry.CollectedCount == 3`, `LevelPhaseManager.CurrentPhase == Phase2_Escape`, level exit remains unlocked, and Phase 2 seeker fields are re-applied (`Phase2SuspicionFloor == 60f`, `Phase2SpeedMultiplier == 1.3f`).
- [ ] **AC-09 — Phase state correct after Phase 1 respawn:** When caught in Phase 1, respawn sets seekers' `Phase2SuspicionFloor == 0f` and `Phase2SpeedMultiplier == 1.0f`. Seekers return to baseline Phase 1 behavior (can return to Unaware, no speed multiplier).
- [ ] **AC-10 — Phase state correct after Phase 2 respawn:** When caught in Phase 2, respawn re-applies `Phase2SuspicionFloor = 60f` and `Phase2SpeedMultiplier = 1.3f` to all seekers after `ResetForRespawn()`. On the next FixedUpdate, seekers' suspicion is floored to 60, entering Searching.
- [ ] **AC-11 — Respawn delay and fade timing:** Measured with a frame counter or `Time.time` log: `caughtFreezeDelay` elapses before fade begins; `fadeDuration` matches the actual CanvasGroup alpha animation duration; `caughtCardDisplayDuration` elapses before the fade-in begins. Total sequence duration equals `F-CP-1` within ±2 frames at 60fps.
- [ ] **AC-12 — CAUGHT card displays and clears:** The CAUGHT TextMeshProUGUI is visible (`gameObject.activeSelf == true` or `alpha == 1`) during `caughtCardDisplayDuration` and not visible before fade-out completes or after fade-in begins.
- [ ] **AC-13 — Double-catch guard works:** Two simultaneous `NotifyPlayerCaught()` calls in the same frame result in exactly one respawn sequence. `RespawnCount` increments by exactly 1. `_respawnInProgress` guard verified by unit test or log.
- [ ] **AC-14 — Win-catch race condition resolved correctly:** If `TriggerWin()` fires before `NotifyPlayerCaught()` in the same frame, `CheckpointManager.HandlePlayerCaught()` checks `GameManager.CurrentState == Win` and returns without starting the respawn sequence. The Win screen displays, no respawn occurs. (Tested by authoring a scene where the exit and a seeker catch radius overlap exactly at the player's test position.)
- [ ] **AC-15 — Hiding state cleared on respawn:** If the player is hiding when caught, `PlayerHiding.IsHiding == false` after respawn. The hiding spot's `IsOccupied` flag returns to false. Verified in a test with the player inside a hiding spot when the caught event fires.
- [ ] **AC-16 — Throwable dropped on respawn:** If the player is carrying a throwable when caught, `PlayerInteraction.IsCarryingThrowable == false` after respawn. The throwable is returned to the object pool. Verified in a test with a carried throwable at catch time.
- [ ] **AC-17 — No CheckpointMarker fallback logs error:** A test scene with no `CheckpointMarker` produces exactly one `Debug.LogError` in the console. The player respawns at their initial position. No exception or crash.
- [ ] **AC-18 — TriggerLose never called on MVP caught event:** In MVP, a full play-through that includes being caught multiple times does not result in `GameManager.CurrentState == Lose` unless a separate Lose trigger (future permadeath system) fires. Verify via state inspection that `CurrentState` remains `Playing` through all catch events.

### Experiential (validated via observed play sessions)

- [ ] **AC-19 — Failure is legible:** After a caught event, ≥80% of playtesters can identify the approximate cause of the catch when asked immediately after the CAUGHT card displays. If playtesters report "I don't know why I got caught" at rates above 20%, the `caughtFreezeDelay` or the seeker's catch animation legibility requires iteration.
- [ ] **AC-20 — Respawn does not feel like a restart:** After 3+ respawns in the same room, ≥70% of playtesters describe the respawn as "going back to try again" rather than "starting over." Playtesters should verbalize that they feel their objective collection is preserved when tested in a Phase 2 respawn scenario.
- [ ] **AC-21 — Respawn pacing feels appropriate:** Playtesters do not describe the respawn sequence as "too slow" or "too fast" in post-session feedback at ≥70% agreement. If more than 30% find the default 1.8s sequence too slow, reduce `caughtCardDisplayDuration`. If more than 30% find it too fast, increase it.
- [ ] **AC-22 — Phase 2 respawn communicates challenge, not confusion:** Playtesters caught in Phase 2 understand that they are respawning into an active Phase 2 room (seekers faster and more alert). They do not expect Phase 1 behavior from the seekers post-respawn. Validated by asking "what do you expect to happen when you respawn after that?" before the fade-in completes.

---

*See `design/gdd/systems-index.md` for dependency context. The Checkpoint System occupies the Foundation Layer of the dependency map — it depends on GameManager, Seeker AI (via SeekerRegistry and EnemyController), and Two-Phase Level Structure (via LevelPhaseManager and EscalationProfile). No gameplay systems depend on it directly; Level Timer + Stats subscribes to its `OnRespawn` event for session statistics.*
