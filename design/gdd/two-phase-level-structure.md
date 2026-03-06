# Two-Phase Level Structure

> **Status**: Approved
> **Author**: game-designer
> **Last Updated**: 2026-03-05
> **Implements Pillar**: Two-Beat Tension (Pillar 4), The Room Has Rules (Pillar 1), Legible Jeopardy (Pillar 3)
> **Design Order**: #11 — Level Layer

---

## 1. Overview

The Two-Phase Level Structure is the orchestrator of UNSEEN's dramatic arc. Every level in the game is divided into exactly two phases: Phase 1 (Find) and Phase 2 (Escape). In Phase 1, seekers patrol at their baseline speed, the level exit is locked, and the player's goal is to locate and collect every objective token while avoiding detection. The moment the player collects the final token, `ObjectiveRegistry.OnAllObjectivesCollected` fires, and this system transitions the level to Phase 2 simultaneously: the level exit unlocks, all active seekers escalate their alertness and speed, the adaptive music shifts, and a HUD phase indicator fires. In Phase 2, the player must reach the now-active exit while navigating a more dangerous version of the same room. The system owns the `LevelPhase` enum, the `LevelPhaseManager` component (one per scene), the `SeekerRegistry` that tracks all active seekers, the `EscalationProfile` ScriptableObject that defines per-level Phase 2 tuning, and the `OnPhaseChanged` event that downstream systems subscribe to. It does not own win condition logic (Level Exit System and GameManager), individual seeker AI behavior (Seeker AI), objective collection (Objective System), or how the music or HUD respond to the phase change.

---

## 2. Player Fantasy

**Target MDA Aesthetics**: Challenge (the room just became harder), Sensation (the visceral gear-shift when Phase 2 starts), Narrative (I took what they were guarding — now they know).

### The Two-Beat Promise

Pillar 4 (Two-Beat Tension) is the structural spine of UNSEEN. Every level is a complete dramatic unit in two movements:

**Beat 1 — The Hunt.** Phase 1 is predator behavior wearing prey clothing. The player is physically weaker than every seeker in the room, but they control all the information. They observe, they plan, they read patrol timing, they choose a window. The emotional register is sustained, intelligent tension: the feeling of a chess player who is three moves ahead. Danger is present but legible and therefore manageable.

**Beat 2 — The Flight.** Phase 2 reverses the emotional valence. The player now has what the seekers were guarding. The room responds. The emotional register shifts from patience-under-pressure to urgency-with-skill. The player must use everything they learned in Phase 1 — every patrol gap, every shadow, every throwable object — but now with seekers that are faster and alert.

These two beats must feel like the same room with different rules, not like two separate levels. The physical space is identical. The player's tools are identical. Only the seeker parameters and alertness state change. This unity is what makes Phase 2 feel earned: the player's Phase 1 knowledge is now the currency that lets them survive Phase 2.

### The Transition Must Be a Beat, Not a Drift

The Phase 1-to-Phase 2 transition is the most important single moment in every level. It must land with the weight of a film cut, not fade in like a volume ramp.

Pillar 3 (Legible Jeopardy) demands that the player is never surprised by their threat level changing. But legible is not the same as slow. The transition is the *predictable consequence* of the player's own action — they took the relic, and any player who has read the room (Pillar 1: The Room Has Rules) knew this would happen. The seeker escalation is not a surprise. It is the inevitable consequence the player was managing around all of Phase 1. The transition moment should feel like: "I knew this was coming. Here it is. Go."

**The player should feel two things in rapid succession within 0.5 seconds of the final tap:**
1. Relief: the objective is collected, the exit is open, I know my destination.
2. Urgency: the room changed, seekers are faster, I must move.

This emotional whiplash — relief into urgency — is the core sensation this system exists to produce. It must be simultaneous. A transition where relief precedes urgency by more than a second will let the player relax, destroying the Phase 2 opening beat.

### Escalation Must Feel Organic, Not Mechanical

The specific challenge for the escalation approach is that seekers must feel like they *noticed* the player's action, not like a game system switched a variable. A seeker that teleports from Unaware to Chase the instant Phase 2 starts is legible in the mechanical sense but breaks narrative coherence. A seeker that takes 30 seconds to gradually become dangerous loses the drama of the phase shift.

The target is: seekers respond to Phase 2 as if a loud alarm has been triggered in the room. Their behavioral change is immediate (they snap to alert searching behavior), it is distributed (all seekers escalate simultaneously, which communicates "the room reacted, not just one seeker"), and it is proportional (they don't teleport to the player's position — they begin actively searching from their current positions, faster than before). The player observes: seekers stopped their patrol, they're moving toward the objective's last location, they're moving faster. This is how a guarded room responds to a theft.

---

## 3. Detailed Rules

### 3.1 LevelPhase Enum and LevelPhaseManager

**Rule TPS-1 (LevelPhase Enum).** The `LevelPhase` enum has exactly two values:

```csharp
public enum LevelPhase
{
    Phase1_Find,
    Phase2_Escape
}
```

There is no "Phase0_Loading" or "Phase3_Victory" value. The `LevelPhaseManager` does not own level loading or win states. Those belong to `GameManager`.

**Rule TPS-2 (LevelPhaseManager Component).** Each level scene contains exactly one `LevelPhaseManager` component, placed on a dedicated `LevelManager` GameObject in the scene. It is a `MonoBehaviour`, not a `Singleton<T>` or `SceneSingleton<T>`, because it does not need to be accessed from other scenes and its lifetime is bound to the level scene. Other systems that need to subscribe to `OnPhaseChanged` find it via a static event bus (see Rule TPS-6) rather than through a scene reference, eliminating the need for a singleton lookup.

**Rule TPS-3 (Initial State).** `LevelPhaseManager` initializes with `CurrentPhase = LevelPhase.Phase1_Find` in its `Awake`. Phase 2 is never the starting state. There is no configuration option to start a level in Phase 2.

**Rule TPS-4 (Transition Guard).** The transition from Phase 1 to Phase 2 can only fire once per level session. A boolean `_phase2Triggered` is set to true on transition entry. Any subsequent call to the transition method checks this guard and returns immediately. This prevents duplicate escalation if `OnAllObjectivesCollected` fires more than once (a defensive guard against the edge case in Section 5.2).

---

### 3.2 SeekerRegistry

The escalation approach chosen is a **hybrid of Approach B and Approach C**: a `SeekerRegistry` is used to reliably enumerate all active seekers, but escalation is delivered by injecting a forced suspicion floor into the Detection System's per-seeker context rather than bypassing the state machine directly. This design resolves the core tension between the two requirements:

- **Simultaneous escalation (Pillar 4 requirement):** All seekers must respond within the same frame. Approach C alone (gradual suspicion accumulation via a `PhaseContext` flag) cannot guarantee this — seekers at very low suspicion might take several seconds to cross the Alert threshold.
- **Organic state transitions (Pillar 1 and 3 requirement):** Seekers must not teleport to Chase. Their transition must flow through the existing Detection System → state machine pipeline to remain legible and consistent with the rules the player has learned.

**The chosen mechanism:** On Phase 2 trigger, `LevelPhaseManager` calls `SeekerRegistry.GetAll()` and sets a `Phase2SuspicionFloor = 60f` on each seeker's `EnemyController`. The Detection System reads this floor value during its normal suspicion calculation. On the very next `FixedUpdate` after Phase 2 trigger, every seeker whose current suspicion is below 60 has their suspicion snapped to 60 — crossing the Alert threshold (50) and entering Searching via the normal state machine path. Seekers that are already at higher suspicion are unaffected. The suspicion floor remains active for the duration of Phase 2, preventing seekers from ever decaying below 60 (they stay in Searching or above, never returning to Unaware). The speed multiplier is applied independently (see Rule TPS-10).

This approach is legible because: the seeker's transition to Searching is triggered by suspicion crossing thresholds, exactly as the player has learned. The seeker's behavioral response (rotate toward last known stimulus origin, begin search sweep) is identical to what the player has seen in Phase 1. What changes is that the floor is elevated — seekers never go below Alert. The player observes: "all the seekers snapped to alert at once." This communicates "the room noticed" without the seeker teleporting to a different behavioral state through a back-channel API.

**Rule TPS-5 (SeekerRegistry Component).** `SeekerRegistry` is a `SceneSingleton<SeekerRegistry>` placed in the level scene alongside the `LevelPhaseManager` on the same `LevelManager` GameObject. All active `EnemyController` instances in the scene self-register on `Awake` by calling `SeekerRegistry.Instance.Register(this)` and self-unregister on `OnDestroy` via `SeekerRegistry.Instance.Unregister(this)`. This mirrors the `ObjectiveRegistry` registration pattern.

Script execution order requirement: `SeekerRegistry` must have execution order set to -90 (after `ObjectiveRegistry` at -100, before all seekers at default 0). This ensures `SeekerRegistry.Instance` is non-null when the first `EnemyController` Awakes.

**Rule TPS-6 (SeekerRegistry API).** `SeekerRegistry` exposes the following public interface:

```csharp
public class SeekerRegistry : SceneSingleton<SeekerRegistry>
{
    public void Register(EnemyController seeker);
    public void Unregister(EnemyController seeker);
    public IReadOnlyList<EnemyController> GetAll();
    public int ActiveSeekerCount { get; }
}
```

`GetAll()` returns a read-only view of the internal list. Callers must not cache the result across frames — the list may change if a seeker is destroyed.

**Rule TPS-7 (EnemyController Phase2 API — New Fields Required).** The Two-Phase Level Structure requires three new fields on `EnemyController` that do not currently exist. These must be added by the implementing programmer and documented in the Seeker AI GDD's Section 6 (Dependencies):

```csharp
// On EnemyController:
public float Phase2SuspicionFloor { get; set; }   // default 0f; set by LevelPhaseManager to EscalationProfile.SuspicionFloor on Phase2 start
public float Phase2SpeedMultiplier { get; set; }  // default 1.0f; set by LevelPhaseManager from EscalationProfile.SpeedMultiplier
public bool  Phase2SkipAlertScan  { get; set; }   // default false; set by LevelPhaseManager from EscalationProfile.SkipAlertScanInPhase2
```

`Phase2SkipAlertScan` is read by `EnemyController.EnterAlertState()` to conditionally bypass the `alertScanDuration` timer. Placing the flag on `EnemyController` keeps data flow unidirectional: `LevelPhaseManager` writes these values, seeker components read them locally. `EnemyController` must never reference `LevelPhaseManager` directly.

`Phase2SuspicionFloor` is read by the Detection System during suspicion decay calculation. If the current suspicion value would decay below this floor, it is clamped to the floor value instead. `Phase2SpeedMultiplier` is read by `EnemyNavigation` (or the equivalent navigation component) when computing NavMeshAgent speed: `effectiveSpeed = baseSpeed * stateMultiplier * Phase2SpeedMultiplier`.

The Detection System must be updated to apply the floor: in the suspicion decay step, after computing the raw decayed value, clamp to `max(decayedValue, seeker.Phase2SuspicionFloor)`. This is a one-line addition to the Detection System's suspicion update path.

---

### 3.3 Phase 1 to Phase 2 Transition Trigger and Sequence

**Rule TPS-8 (Subscription).** `LevelPhaseManager.Awake` subscribes to `ObjectiveRegistry.Instance.OnAllObjectivesCollected`. If `ObjectiveRegistry.Instance` is null at Awake, a `Debug.LogError` is emitted and the subscription is skipped. A level without `ObjectiveRegistry` cannot trigger Phase 2.

**Rule TPS-9 (Transition Sequence — Frame Precision).** When `HandleAllObjectivesCollected()` is invoked (synchronously from `ObjectiveRegistry.RegisterCollection`, before that method returns), the following steps execute in this exact order within the same frame:

1. Guard check: if `_phase2Triggered == true`, return immediately. No double-trigger.
2. Set `_phase2Triggered = true`.
3. Set `CurrentPhase = LevelPhase.Phase2_Escape`.
4. Retrieve all active seekers: `var seekers = SeekerRegistry.Instance.GetAll()`.
5. For each seeker in `seekers`:
   a. Set `seeker.Phase2SuspicionFloor = _escalationProfile.SuspicionFloor` (default: `60f`).
   b. Set `seeker.Phase2SpeedMultiplier = _escalationProfile.SpeedMultiplier` (default: `1.3f`).
   c. Set `seeker.Phase2SkipAlertScan = _escalationProfile.SkipAlertScanInPhase2` (default: `false`).
6. Fire `OnPhaseChanged(LevelPhase.Phase2_Escape)` static event (see Rule TPS-11).
7. Play Phase 2 audio signal: `AudioManager.Instance.Play(SoundID.Phase2Start)` — a room-wide audio cue (see Rule TPS-12).

Steps 1–7 are synchronous. No coroutines, no deferred calls. The escalation must complete within the same frame as the collection tap to meet the 0.5-second perception window requirement.

**On the next `FixedUpdate` after this frame:** The Detection System's suspicion update runs normally. Any seeker whose current suspicion is below `Phase2SuspicionFloor` (60) has its suspicion clamped to 60. Since 60 > the Alert threshold (50), those seekers immediately receive `RequestedState == Alert` from the Detection System and enter Alert state. If the Detection System's `RequestedState` logic determines that the noise stimulus of "all seekers alert at the same time" (suspicion jumping from e.g. 5 to 60) warrants Searching rather than Alert, this is determined by the Detection System's threshold rules. At a minimum, all seekers are guaranteed to be in Alert or above within one FixedUpdate cycle of the Phase 2 trigger.

**Why the seeker audio cue for Phase 2 may not fire immediately:** Each seeker triggers its Alert/Searching vocalization independently via `EnemyController`'s state machine `OnSeekerStateChanged` event, which fires when the state machine transitions. These vocalizations fire in the FixedUpdate following the Phase 2 trigger, not on the trigger frame itself. This is acceptable — the room-wide `SoundID.Phase2Start` audio cue (step 7 above) fires on the trigger frame and covers the gap.

---

### 3.4 Phase 2 Escalation Behavior (Full Specification)

**Rule TPS-10 (Speed Escalation Mechanism).** The Phase 2 speed multiplier is applied as follows:

- `EnemyController.Phase2SpeedMultiplier` is set from `EscalationProfile.SpeedMultiplier` by `LevelPhaseManager` during the transition sequence (Rule TPS-9, step 5b).
- The component responsible for driving `NavMeshAgent.speed` (whether `EnemyController` directly or a dedicated navigation sub-component — whichever is implemented) computes effective speed as: `NavMeshAgent.speed = patrolSpeed * stateSpeedMultiplier * Phase2SpeedMultiplier` (see Formula F-TPS-1).
- `Phase2SpeedMultiplier` defaults to `1.0f` on `EnemyController.Awake`. It is never reset to `1.0f` by the Level Phase Manager after being set — it persists until the scene is unloaded.
- The speed multiplier applies to all seeker states: Unaware (patrol), Alert, Searching, and Chase. It is a global multiplier on top of the state's own speed factor. This means Phase 2 seekers are faster across all behaviors, not just during Chase.

**Rationale for multiplying all states:** If Phase 2 only sped up Chase, experienced players could outrun Phase 2 by preventing Chase. The room should feel faster as a whole, not just when the seeker has locked on. The patrol-speed increase is the most immediately legible change: the player can see the seeker moving faster before the seeker has even noticed them.

**Rule TPS-11 (Suspicion Floor Mechanism).** The `Phase2SuspicionFloor` value (default: `60f`) acts as a lower bound on the Detection System's per-seeker suspicion decay. The Detection System reads `seeker.Phase2SuspicionFloor` in its suspicion update:

```
// Detection System suspicion decay (modified for Phase 2):
float rawDecayed = currentSuspicion - (decayRate * Time.fixedDeltaTime);
float floored    = Mathf.Max(rawDecayed, seeker.Phase2SuspicionFloor);
suspicion        = floored;
```

This means:
- A seeker in Phase 2 whose suspicion is at 90 (active Chase) can still decay when the player hides — it decays from 90 toward 60, not toward 0. The seeker may drop from Chase back to Searching, but never below Searching.
- A seeker in Phase 2 cannot return to Unaware or Alert. Minimum reachable state is Searching.
- This creates the Phase 2 experience: the player can still lose a seeker from active Chase by hiding, but can never fully deescalate all seekers to patrol. The room stays hot.

**Rule TPS-12 (Phase 2 Audio Signal).** On Phase 2 trigger (Rule TPS-9, step 7), `AudioManager.Instance.Play(SoundID.Phase2Start)` is called. This fires a room-wide audio event that is non-diegetic (not emitted from a world position) — it is an atmospheric sting that signals the phase shift to the player. This is the only non-diegetic audio event fired by gameplay systems in UNSEEN; it is justified because the phase shift is a top-level game-state change, not a localized event.

`SoundID.Phase2Start` must be added to `SoundLibrary.cs`. The audio team's intent: a short, sharp alarm-style sting (1.0–2.0 s), not a musical transition (that is handled by the Adaptive Music System subscribing to `OnPhaseChanged`). The sting must be audible over ambient patrol sounds and the collection VFX audio.

**Rule TPS-13 (Phase 2 Visual Signal — HUD).** `LevelPhaseManager` fires the static `OnPhaseChanged(LevelPhase.Phase2_Escape)` event. `HUDManager` subscribes to this event and displays a Phase 2 indicator: a brief screen-edge pulse (red vignette flash, 0.5 s duration, then fade over 0.3 s) and a text notification (implementation: `HUDManager.ShowPhaseNotification("ALARM")`, display duration from `HUDManager`'s own configuration). The HUD owns the visual presentation; the `LevelPhaseManager` fires only the event.

The HUD phase indicator is a supporting signal, not the primary one. The primary legibility of Phase 2 comes from: (a) seeker behavioral change (they stopped patrolling and are now moving toward the objective), (b) seeker speed increase (visible from across the room), (c) the room-wide audio sting, and (d) the exit light turning on. The HUD flash is a backup for players who are looking away from the action at the moment of transition.

**Rule TPS-14 (OnPhaseChanged Event Contract).** `LevelPhaseManager` exposes a static C# event:

```csharp
public static event Action<LevelPhase> OnPhaseChanged;
```

The event is static so subscribers do not need a scene reference to `LevelPhaseManager`. Because the event is static, it persists between scene loads and can accumulate stale delegates. `LevelPhaseManager.Awake` must null the event before use to clear any delegates that survived from a previous level session:

```csharp
// In LevelPhaseManager.Awake, before any subscriptions are wired:
OnPhaseChanged = null;
```

All subscribers must also handle cleanup: unsubscribe in `OnDestroy` or `OnDisable` to prevent stale delegates from firing in future scenes.

The event fires synchronously in step 6 of the transition sequence (Rule TPS-9). Subscribers must not perform heavy work in their handlers. Permitted: setting a flag, starting a coroutine, triggering an audio system call. Not permitted: NavMesh path recalculation, scene loads, object instantiation chains.

**Intended subscribers and their responsibilities:**

| Subscriber | What It Does on Phase 2 |
|-----------|------------------------|
| `HUDManager` | Displays Phase 2 screen-edge pulse and text notification |
| Adaptive Music System | Transitions to Phase 2 music layer (the escalation music track) |
| Level Timer + Stats | Records timestamp of Phase 2 start for session stats |

Win/Game Over screens do not subscribe — they respond to `GameManager.TriggerWin()` / `GameManager.TriggerLose()`, not to the phase change.

---

### 3.5 Phase 2 Wind-Down on Level Complete

**Rule TPS-15 (Subscription to LevelExit.OnExitUsed).** `LevelPhaseManager.Awake` subscribes to `LevelExit.OnExitUsed`. When this fires (the player has completed the exit hold and the exit has dispatched the win signal), `LevelPhaseManager.HandleExitUsed()` runs.

**Rule TPS-16 (Wind-Down Sequence).** `HandleExitUsed()` executes:

1. For each seeker in `SeekerRegistry.Instance.GetAll()`:
   a. Set `seeker.Phase2SuspicionFloor = 0f` — restores normal suspicion decay so seekers don't hold Searching state during any win transition animation.
   b. Set `seeker.Phase2SpeedMultiplier = 1.0f` — restores normal speed.
   c. Set `seeker.Phase2SkipAlertScan = false` — restores normal Alert scan behavior.
2. Set `CurrentPhase = LevelPhase.Phase1_Find`. (Nominal reset — the level is ending, but this ensures the state is clean if the scene is not immediately unloaded.)
3. Do NOT fire `OnPhaseChanged` on wind-down. Phase changes fire only on escalation. The win transition is owned entirely by `GameManager` responding to `TriggerWin()`.

**Rationale:** The wind-down is a courtesy cleanup. `GameManager.TriggerWin()` fires after `OnExitUsed` (per Level Exit System Rule LE-9 step 5). The GameManager may begin a win screen transition or a scene fade. If seekers are left in their Phase 2 speed/suspicion state during this transition, they may continue animating rapidly in the background, which looks incorrect. Resetting to baseline before the transition animation starts produces clean visual behavior.

---

### 3.6 EscalationProfile ScriptableObject

**Rule TPS-17 (EscalationProfile Definition).** `EscalationProfile` is a `ScriptableObject` at `Assets/_Project/Scripts/Data/EscalationProfile.cs`. Each level scene has a `LevelPhaseManager` component with a serialized `[SerializeField] EscalationProfile _escalationProfile` field, assigned via the Inspector. Multiple levels may share the same `EscalationProfile` asset (for consistent MVP difficulty) or use unique assets (for per-level tuning).

```csharp
[CreateAssetMenu(menuName = "UNSEEN/EscalationProfile")]
public class EscalationProfile : ScriptableObject
{
    [Header("Speed Escalation")]
    [Tooltip("Multiplier applied to all seeker NavMeshAgent speeds during Phase 2. 1.0 = no change. Default: 1.3.")]
    [Range(1.0f, 2.5f)]
    public float SpeedMultiplier = 1.3f;

    [Header("Suspicion Floor")]
    [Tooltip("Minimum suspicion value any seeker can hold during Phase 2. Prevents full deescalation. Default: 60.")]
    [Range(0f, 80f)]
    public float SuspicionFloor = 60f;

    [Header("Alert Behavior Override")]
    [Tooltip("If true, seekers in Phase 2 skip the Alert scan delay and immediately enter Searching when suspicion crosses the Alert threshold.")]
    public bool SkipAlertScanInPhase2 = false;

    [Header("Search Speed Override")]
    [Tooltip("If set above 0, overrides the seeker's EnemyData.searchSpeedMultiplier during Phase 2. Set to 0 to use the seeker's authored value.")]
    [Range(0f, 2.5f)]
    public float SearchSpeedMultiplierOverride = 0f;

    [Header("Chase Speed Override")]
    [Tooltip("If set above 0, overrides the seeker's EnemyData.chaseSpeedMultiplier during Phase 2. Set to 0 to use the seeker's authored value.")]
    [Range(0f, 3.0f)]
    public float ChaseSpeedMultiplierOverride = 0f;
}
```

**Rule TPS-18 (SkipAlertScanInPhase2).** When `EscalationProfile.SkipAlertScanInPhase2 == true`, `LevelPhaseManager` sets `seeker.Phase2SkipAlertScan = true` on each seeker during the Phase 2 transition (Rule TPS-9, step 5c). `EnemyController.EnterAlertState()` reads its own `Phase2SkipAlertScan` field. If `true`, the `alertScanDuration` countdown is skipped — the seeker immediately transitions to Searching. This compresses the player's window to hide between Alert and Searching. `EnemyController` must not reference `LevelPhaseManager` directly; it reads only its own field.

**Rule TPS-19 (SearchSpeedMultiplierOverride and ChaseSpeedMultiplierOverride).** When these values are greater than 0, they replace (not stack with) the `EnemyData.searchSpeedMultiplier` / `EnemyData.chaseSpeedMultiplier` values in the speed computation. When they are 0, the `EnemyData` authored values are used. This allows a level designer to create a Phase 2 where seekers are notably faster during Chase without affecting their patrol speed, for example.

**Rule TPS-20 (Default MVP EscalationProfile Asset).** A default asset named `EscalationProfile_Default` must be created at `Assets/_Project/Data/Escalation/EscalationProfile_Default.asset`. All MVP level scenes reference this asset. Its values are the mandatory minimum escalation — see Section 7 for the default values and their rationale.

---

## 4. Formulas

### F-TPS-1: Phase 2 Effective Seeker Speed

```
effectiveSpeed = patrolSpeed * stateSpeedMultiplier * Phase2SpeedMultiplier
```

Where state speed multipliers are read from `EnemyData` (or overridden by `EscalationProfile` per Rules TPS-18 and TPS-19), and `Phase2SpeedMultiplier` is from `EscalationProfile.SpeedMultiplier` (default: `1.3f`).

| Variable | Type | Default Value | Source | Description |
|----------|------|---------------|--------|-------------|
| `patrolSpeed` | float | 2.0 m/s | `EnemyData` | Warden baseline patrol speed (from Seeker AI GDD Section 7) |
| `stateSpeedMultiplier` | float | 1.0× (Unaware/Alert), 1.3× (Searching), 1.6× (Chase) | `EnemyData` | Per-state multiplier authored on the seeker |
| `Phase2SpeedMultiplier` | float | 1.3× | `EscalationProfile.SpeedMultiplier` | Phase 2 global speed escalation |
| `effectiveSpeed` | float | — | computed | NavMeshAgent target speed in m/s |

**Example calculations at default values:**

| State | Phase 1 Speed | Phase 2 Speed | Increase |
|-------|--------------|--------------|---------|
| Patrol (Unaware) | 2.0 × 1.0 = **2.0 m/s** | 2.0 × 1.0 × 1.3 = **2.6 m/s** | +30% |
| Searching | 2.0 × 1.3 = **2.6 m/s** | 2.0 × 1.3 × 1.3 = **3.38 m/s** | +30% |
| Chase | 2.0 × 1.6 = **3.2 m/s** | 2.0 × 1.6 × 1.3 = **4.16 m/s** | +30% |

For reference: average adult walking speed is approximately 1.4 m/s. The Phase 2 patrol speed of 2.6 m/s is faster than a jogging player (player's base move speed is defined in `PlayerData` — this must be verified relative to player sprint speed to confirm Phase 2 Chase remains escapable-but-threatening). If the player sprint speed is ≤ 4.0 m/s, Phase 2 Chase (4.16 m/s) means the player cannot outrun a Chase; they must break line-of-sight.

---

### F-TPS-2: Phase 2 Start Condition

```
phase2Triggered = (ObjectiveRegistry.CollectedCount == ObjectiveRegistry.TotalCount)
               AND (ObjectiveRegistry.TotalCount > 0)
               AND (_phase2Triggered == false)
```

This is evaluated synchronously inside `LevelPhaseManager.HandleAllObjectivesCollected()`, which is called by `ObjectiveRegistry.OnAllObjectivesCollected`. The condition `TotalCount > 0` is guaranteed by `ObjectiveRegistry` (it does not fire `OnAllObjectivesCollected` when `TotalCount == 0` per Objective System Rule OS-12 and F-OS-3). The `_phase2Triggered == false` guard prevents double-trigger.

| Variable | Type | Source | Description |
|----------|------|--------|-------------|
| `ObjectiveRegistry.CollectedCount` | int | `ObjectiveRegistry` | Number of tokens collected this session |
| `ObjectiveRegistry.TotalCount` | int | `ObjectiveRegistry` | Total tokens placed in the scene |
| `_phase2Triggered` | bool | `LevelPhaseManager` runtime state | True after the first Phase 2 trigger; prevents re-trigger |

---

### F-TPS-3: Suspicion Floor Application (Detection System Integration)

```
// Applied in Detection System suspicion decay step:
rawDecayed   = currentSuspicion - (decayRate * Time.fixedDeltaTime)
suspicion    = Mathf.Max(rawDecayed, seeker.Phase2SuspicionFloor)
```

**Note on threshold values:** The example below assumes Alert threshold = 50 and Chase threshold = 75. These values are authoritative in the Detection System GDD. The default `SuspicionFloor` of 60f is designed to fall between these two thresholds. If the Detection System GDD publishes different threshold values, `SuspicionFloor` must be adjusted to remain between them. The GDD implementor must cross-reference the Detection System GDD before finalizing this value.

**Example — Phase 2 seeker at suspicion 90 (Chase), hides behind cover:**

Assume `decayRate = 8.0` per second (from Detection System tuning), `fixedDeltaTime = 0.02s`, `Phase2SuspicionFloor = 60f`, Alert threshold = 50, Chase threshold = 75.

After 1 second of no detection: `rawDecayed = 90 - (8.0 * 1.0) = 82`. Floor: `max(82, 60) = 82`. Seeker remains in Chase (82 > 75 threshold).
After 2 seconds: `rawDecayed = 90 - 16 = 74`. Floor: `max(74, 60) = 74`. Seeker transitions from Chase to Searching (74 < 75 threshold, 74 > 50 threshold).
After 3 seconds: `rawDecayed = 90 - 24 = 66`. Floor: `max(66, 60) = 66`. Seeker remains Searching (66 > 50 threshold, 66 > floor).
After 3.75 seconds: `rawDecayed = 90 - 30 = 60`. Floor: `max(60, 60) = 60`. Seeker holds at suspicion 60 — minimum reachable state in Phase 2.

The seeker cannot descend below Searching in Phase 2. The player successfully lost the Chase by hiding, but the seeker continues actively Searching.

| Variable | Type | Source | Description |
|----------|------|--------|-------------|
| `currentSuspicion` | float | Detection System runtime state | Per-seeker suspicion [0–100] |
| `decayRate` | float | `EnemyData` | Suspicion decay per second (Detection System GDD — not duplicated here) |
| `Phase2SuspicionFloor` | float | `EnemyController.Phase2SuspicionFloor` (set from `EscalationProfile`) | Minimum suspicion floor in Phase 2; 0f in Phase 1 |
| `suspicion` | float | Detection System output | Clamped suspicion written back to the Detection System state |

---

## 5. Edge Cases

### 5.1 Phase 2 Triggered Before Level Fully Loaded

**Scenario:** `ObjectiveRegistry.OnAllObjectivesCollected` fires before `GameManager.CurrentState` has transitioned to `Playing` — e.g., if a level has zero tokens and the registry misfires on load (this is a bug case documented in Objective System edge case 5.1, which prevents the event from firing when `TotalCount == 0`).

**Behavior:** `LevelPhaseManager.HandleAllObjectivesCollected()` checks `GameManager.Instance.CurrentState` at entry. If the state is not `Playing`, the method logs `Debug.LogWarning("TPS: Phase 2 trigger received outside Playing state. Ignored.")` and returns immediately without executing the transition sequence. `_phase2Triggered` remains false — if `OnAllObjectivesCollected` fires again legitimately during `Playing`, Phase 2 will trigger correctly.

**Note:** The Objective System's `TotalCount > 0` guard in `F-OS-3` makes this scenario impossible under normal operation. The guard in `LevelPhaseManager` is defensive depth.

---

### 5.2 Two Simultaneous OnAllObjectivesCollected Calls

**Scenario:** `OnAllObjectivesCollected` fires twice in the same frame. This is explicitly prevented by the Objective System (Rule OS-9: fires exactly once per session), but a defensive guard is required.

**Behavior:** The `_phase2Triggered` boolean (Rule TPS-4) catches this. The first call executes the full transition sequence and sets `_phase2Triggered = true`. The second call checks the guard at entry and returns immediately without executing any steps. Seekers are escalated exactly once. `OnPhaseChanged` fires exactly once.

---

### 5.3 Player Caught During Phase 2 Transition Frame

**Scenario:** The Detection System fires `RequestedState == Caught` on the same frame as `LevelPhaseManager.HandleAllObjectivesCollected()` executes. Both `EnemyController` (calling `GameManager.OnPlayerCaught()`) and `LevelPhaseManager` (calling escalation and `OnPhaseChanged`) run synchronously in the same frame.

**Behavior:** `GameManager.OnPlayerCaught()` is idempotent (per Seeker AI GDD edge case: "GameManager must handle `OnPlayerCaught` as idempotent"). `GameManager.TriggerLose()` begins the fail transition. `LevelPhaseManager`'s transition completes (Phase 2 is technically triggered), but the level is ending immediately due to the Caught state. The `OnPhaseChanged` event fires, but downstream subscribers (HUD, Music) will be interrupted by the `TriggerLose()` flow before their effects become meaningful.

No special handling is required. The Caught transition to `TriggerLose()` takes precedence over the Phase 2 escalation because `GameManager` owns the game state and both events are synchronous in the same frame — the sequence is deterministic (Detection System FixedUpdate completes before or after the input-driven collection tap depending on frame order, but once the frame resolves, both events are processed, and the GameManager's fail state wins). The player experiences: they collected the relic, the room started to change, they were caught. From a player perspective, this is a valid (if harsh) outcome.

---

### 5.4 Level Reloaded Mid-Phase 2

**Scenario:** The player is caught in Phase 2, the level reloads (Checkpoint System), and the scene re-initializes.

**Behavior:** Scene reload destroys and re-instantiates all GameObjects. `LevelPhaseManager` re-initializes with `CurrentPhase = Phase1_Find` and `_phase2Triggered = false`. `SeekerRegistry` re-initializes with an empty seeker list. All `EnemyController` instances re-Awake with `Phase2SuspicionFloor = 0f` and `Phase2SpeedMultiplier = 1.0f`. Phase 2 state does NOT persist across scene reloads. This is correct behavior: the player restarts from the beginning of Phase 1. No special reset logic is required — scene reload provides full reset.

---

### 5.5 No Seekers in Scene

**Scenario:** A level is authored with zero `EnemyController` prefabs placed. `SeekerRegistry.GetAll()` returns an empty list.

**Behavior:** The transition sequence in Rule TPS-9 step 5 iterates over an empty list — the loop executes zero times. No `NullReferenceException`. `OnPhaseChanged` still fires, the audio sting still plays, and the HUD still shows the phase indicator. The level functions as a "collect-and-exit" puzzle with no seeker threat in either phase. A `Debug.LogWarning("TPS: Phase 2 triggered with zero active seekers. No escalation applied.")` is emitted — this surfaces the likely design oversight without breaking play.

---

### 5.6 Seeker Destroyed During Phase 2

**Scenario:** An `EnemyController` GameObject is destroyed (not a designed gameplay case in MVP) after Phase 2 has started. `SeekerRegistry.Unregister(this)` fires from `EnemyController.OnDestroy`.

**Behavior:** `SeekerRegistry` removes the destroyed seeker from its internal list. The Phase 2 state on the remaining seekers (`Phase2SuspicionFloor`, `Phase2SpeedMultiplier`) is unaffected — it was set per-seeker at Phase 2 start. The destroyed seeker's fields are irrelevant once the object is gone. `SeekerRegistry.GetAll()` on subsequent calls returns only the surviving seekers. `ActiveSeekerCount` reflects the correct live count. No special handling needed; the registry's Unregister-on-Destroy pattern matches `ObjectiveRegistry`'s equivalent.

---

### 5.7 EscalationProfile Not Assigned

**Scenario:** Level designer forgets to assign an `EscalationProfile` to the `LevelPhaseManager` inspector field. `_escalationProfile` is null.

**Behavior:** `LevelPhaseManager.Awake` performs a null check on `_escalationProfile`. If null: `Debug.LogError("TPS: LevelPhaseManager._escalationProfile is null. Assign an EscalationProfile asset in the Inspector. Using safe defaults.")` is logged, and a hardcoded fallback is used: `Phase2SuspicionFloor = 60f`, `SpeedMultiplier = 1.3f`, all override fields at their "no-override" values. The level remains playable with default escalation. The error message surfaces the oversight without breaking the game.

The fallback values are defined as compile-time constants on `LevelPhaseManager`:
```csharp
private const float FALLBACK_SUSPICION_FLOOR    = 60f;
private const float FALLBACK_SPEED_MULTIPLIER   = 1.3f;
```

---

### 5.8 Phase2SpeedMultiplier Applied Before seeker Enters Phase 2

**Scenario:** `Phase2SpeedMultiplier` is set on `EnemyController` in step 5b of the transition sequence (Rule TPS-9). The seeker's NavMeshAgent reads this value on the next FixedUpdate. Between step 5b and the next FixedUpdate, the seeker is in Phase 1 state but with Phase 2 speed values loaded. Duration of this window: ≤ `Time.fixedDeltaTime` (0.02 s at 50 Hz).

**Behavior:** This sub-frame window is acceptable. The seeker's behavioral state does not change until the Detection System applies the suspicion floor on the next FixedUpdate, but its NavMeshAgent speed is already at Phase 2 values. A seeker patrolling at 2.0 m/s in Phase 1 that becomes 2.6 m/s in a 0.02-second window is imperceptible to the player. No special handling needed.

---

## 6. Dependencies

### 6.1 What This System Requires

| Dependency | System | What Is Required | Direction | Notes |
|-----------|--------|-----------------|-----------|-------|
| `ObjectiveRegistry.OnAllObjectivesCollected` | Objective System | Event subscription; fires once when all tokens collected | Inbound | `LevelPhaseManager` subscribes in Awake. Objective System GDD Section 6.2 already documents Two-Phase as a subscriber. |
| `LevelExit.OnExitUsed` | Level Exit System | Event subscription; fires when player completes exit hold | Inbound | `LevelPhaseManager` subscribes in Awake for wind-down. Level Exit GDD Rule LE-10 documents the event contract. |
| `EnemyController.Phase2SuspicionFloor` (new) | Seeker AI | Read/write float property on `EnemyController`; read by Detection System | Bidirectional | New field to be added to `EnemyController`. Seeker AI GDD Section 6 must be updated to add this dependency. |
| `EnemyController.Phase2SpeedMultiplier` (new) | Seeker AI | Read/write float property; read by `EnemyNavigation` component | Bidirectional | New field to be added to `EnemyController`. Seeker AI GDD Section 6 must be updated. |
| `SeekerRegistry` | This System (new) | `SceneSingleton<SeekerRegistry>` for tracking active `EnemyController` instances | Internal | New class to be created. `EnemyController` must call `SeekerRegistry.Instance.Register/Unregister`. Seeker AI GDD Section 6 must be updated. |
| `GameManager.CurrentState` | GameLoop | State check before allowing Phase 2 trigger (edge case guard) | Inbound | Read-only access to `GameManager.Instance.CurrentState`. |
| `AudioManager.Instance.Play(SoundID)` | Audio | Play `SoundID.Phase2Start` on transition | Inbound | `SoundID.Phase2Start` must be added to `SoundLibrary.cs`. |
| `EscalationProfile` ScriptableObject | Data | Per-level Phase 2 escalation configuration | Inbound | Assigned via Inspector field on `LevelPhaseManager`. |
| Detection System (suspicion clamp) | Core | Must read `seeker.Phase2SuspicionFloor` in suspicion decay calculation | Outbound (data contract) | Detection System GDD must be updated to document this integration point in its Dependencies section and Detailed Rules. |

---

### 6.2 What This System Provides

| Provided To | System | What Is Provided | Direction | Notes |
|------------|--------|-----------------|-----------|-------|
| HUD | UI | `LevelPhaseManager.OnPhaseChanged` static event; fires `Phase2_Escape` on transition | Outbound | HUD subscribes to display phase indicator. HUD GDD must document this subscription when authored. |
| Adaptive Music System | Audio | `LevelPhaseManager.OnPhaseChanged` static event | Outbound | Music system subscribes to transition music layers. Adaptive Music GDD must document this when authored. |
| Level Timer + Stats | Progression | `LevelPhaseManager.OnPhaseChanged` static event; timestamp of Phase 2 start | Outbound | Level Timer + Stats GDD must document this when authored. |
| Seeker AI | Gameplay | `EnemyController.Phase2SuspicionFloor` value (set at Phase 2 start) | Outbound (write) | The Two-Phase system sets this field; Seeker AI behavior responds to it through the Detection System. |
| Seeker AI | Gameplay | `EnemyController.Phase2SpeedMultiplier` value (set at Phase 2 start) | Outbound (write) | The Two-Phase system sets this field; EnemyNavigation reads it for speed computation. |
| Detection System | Core | `Phase2SuspicionFloor` data contract (read from `EnemyController`) | Outbound (contract) | Detection System must be updated to read this field during suspicion decay. |

---

### 6.3 GDDs That Must Be Updated

The following existing GDDs require updates to reflect this system's existence and its contracts:

| GDD | Required Update |
|-----|----------------|
| `design/gdd/seeker-ai.md` Section 6 (Dependencies) | Add `LevelPhaseManager` as a dependency; document `Phase2SuspicionFloor` and `Phase2SpeedMultiplier` fields; add `SeekerRegistry` registration requirement in `EnemyController.Awake/OnDestroy` |
| `design/gdd/seeker-ai.md` Section 7 (Tuning Knobs) | Note that `EnemyData.patrolSpeed` is the base for `F-TPS-1`; note `Phase2SpeedMultiplier` as an override path |
| `design/gdd/detection-system.md` Detailed Rules (suspicion decay) | Add `Phase2SuspicionFloor` clamp to the suspicion decay rule; cite this GDD as the source |
| `design/gdd/detection-system.md` Section 6 (Dependencies) | Add `LevelPhaseManager` / `EnemyController.Phase2SuspicionFloor` as an inbound data dependency |
| `design/gdd/level-exit-system.md` Section 6 (Dependencies) | Add `LevelPhaseManager` as a subscriber to `LevelExit.OnExitUsed` (for wind-down) |
| `design/gdd/systems-index.md` | Update Two-Phase Level Structure status from "Not Started" to "In Design"; update progress tracker counts |

---

### 6.4 Ownership Boundaries (Explicit)

The following behaviors are explicitly NOT owned by the Two-Phase Level Structure:

| Behavior | Actual Owner |
|----------|-------------|
| Win condition evaluation | Level Exit System (`LevelExit.OnInteractComplete` → `GameManager.TriggerWin()`) |
| Individual seeker AI state transitions | Seeker AI state machine (driven by Detection System output) |
| Objective collection | Objective System |
| Exit glow on / unlock VFX | Level Exit System |
| Music transition to Phase 2 layer | Adaptive Music System (subscribes to `OnPhaseChanged`) |
| HUD phase indicator visual design | HUD System (subscribes to `OnPhaseChanged`) |
| Seeker behavioral search pattern | Seeker AI (unchanged between Phase 1 and Phase 2; only suspicion floor and speed change) |

---

## 7. Tuning Knobs

### 7.1 EscalationProfile Fields — Safe Ranges and Rationale

All Phase 2 escalation values are fields on `EscalationProfile`. No Phase 2 values are hardcoded anywhere.

| Knob | Category | Default (MVP) | Safe Range | Extreme Low Effect | Extreme High Effect | Rationale for Default |
|------|----------|---------------|-----------|-------------------|--------------------|-----------------------|
| `SpeedMultiplier` | Feel | 1.3× | 1.0–2.0× | Phase 2 feels identical to Phase 1; no drama | Seekers become extremely fast; near-impossible to outrun even in Searching | 1.3× applied to the baseline patrol speed of 2.0 m/s produces 2.6 m/s — noticeably faster but not oppressive. The Seeker AI GDD's `chaseSpeedMultiplier = 1.6×` means Phase 2 Chase is `2.0 × 1.6 × 1.3 = 4.16 m/s`. If player sprint is ~4.5 m/s, the player can barely outrun Phase 2 Chase in a straight line, but cannot sustain it — they must use the environment. |
| `SuspicionFloor` | Gate | 60f | 40–75f | Floor at 40 (near Alert threshold): seekers can return to Alert in Phase 2, spending time stationary scanning rather than actively Searching. Phase 2 feels less dangerous. | Floor at 75 (Chase threshold): seekers immediately enter Chase on Phase 2 start and never leave it. Extreme pressure; no hiding-to-recover. | 60f is between Alert threshold (50) and Chase threshold (75). Seekers cannot fully deescalate below Searching (they won't stand still patrolling), but players can break a Chase by hiding (suspicion decays from Chase toward 60, crossing the 75 threshold and dropping to Searching). |
| `SkipAlertScanInPhase2` | Gate | false | true/false | (false = normal behavior): Seekers spend 2.5s stationary scanning when suspicion first crosses Alert. The Phase 2 start has a brief "hesitation" window where newly alerted seekers are still during their scan. | (true = skips scan): Seekers snap directly to Searching without the 2.5s scan. Phase 2 start is more abrupt and dangerous. | false for MVP — preserves the Alert scan as a small grace window during the phase transition, giving the player a moment to react before seekers start moving toward the objective position. Enable for harder levels. |
| `SearchSpeedMultiplierOverride` | Feel | 0f (use EnemyData) | 0–2.5× | 0 (disabled): uses `EnemyData.searchSpeedMultiplier` (1.3×). Consistent with authored seeker behavior. | >0: overrides `EnemyData` value. Allows Phase 2 Searching to be faster than Phase 1 by a different ratio than the base `SpeedMultiplier`. | 0f for MVP — base `SpeedMultiplier` of 1.3× already applies uniformly. Use this override for levels where a faster Searching state creates a specific puzzle difficulty, without changing the Chase speed ratio. |
| `ChaseSpeedMultiplierOverride` | Feel | 0f (use EnemyData) | 0–3.0× | 0 (disabled): uses `EnemyData.chaseSpeedMultiplier` (1.6×) plus base `SpeedMultiplier`. | >0: overrides `EnemyData` Chase multiplier. | 0f for MVP. Use for late-game levels where the designer wants Phase 2 Chase to be distinctly more threatening than Phase 2 Searching. |

---

### 7.2 Default MVP EscalationProfile Asset Specification

The `EscalationProfile_Default` asset at `Assets/_Project/Data/Escalation/EscalationProfile_Default.asset` must be configured with exactly these values for MVP. All MVP level scenes assign this asset to their `LevelPhaseManager`:

```
SpeedMultiplier:              1.3
SuspicionFloor:               60
SkipAlertScanInPhase2:        false
SearchSpeedMultiplierOverride: 0
ChaseSpeedMultiplierOverride:  0
```

**What this produces in play:**
- All seekers become 30% faster (patrol, search, and chase).
- All seekers cannot deescalate below Searching (suspicion never decays below 60).
- Players who are caught in Chase and hide successfully will cause the seeker to drop from Chase to Searching, but the seeker will continue actively looking for them.
- The alert scan window (2.5s) is preserved, giving the player a brief moment during Phase 2 onset.

This default is intentionally conservative. The minimum viable Phase 2 escalation is: "seekers are faster and they do not stop looking for you." A level designer can apply this asset to any UNSEEN chamber and have a functional Phase 2 without further tuning.

---

### 7.3 Values That Must Not Be Changed Without Cross-Level Re-Testing

The following values, if changed in `EscalationProfile_Default`, require re-testing all levels that use this asset before shipping:

| Value | Reason |
|-------|--------|
| `SuspicionFloor` | Affects whether players can "cool down" the room mid-Phase 2. Raising it reduces the player's recovery options across all levels uniformly. |
| `SpeedMultiplier` | All Phase 2 timing windows (patrol gap traversal, hiding spot approach, exit run) are tuned against the default speed. A speed change invalidates all tested timings. |
| `SkipAlertScanInPhase2` | If set to true for the default asset, all levels lose the Alert grace window simultaneously. This requires full re-playtesting of all Phase 2 runs. |

Per-level `EscalationProfile` assets can be changed freely without affecting other levels.

---

### 7.4 Inherited Tuning From Seeker AI GDD

The following knobs are owned by `EnemyData` (Seeker AI GDD Section 7) but directly affect Phase 2 behavior. They are listed here for the tuner's awareness:

| Knob | Owner | Default | Phase 2 Effect |
|------|-------|---------|----------------|
| `patrolSpeed` | `EnemyData` | 2.0 m/s | Base value that `F-TPS-1` multiplies. Changing this changes all Phase 2 speeds proportionally. |
| `alertScanDuration` | `EnemyData` | 2.5s | Determines the Alert grace window in Phase 2 (when `SkipAlertScanInPhase2 == false`). |
| `searchSpeedMultiplier` | `EnemyData` | 1.3× | Used in Phase 2 Searching speed unless `SearchSpeedMultiplierOverride > 0`. |
| `chaseSpeedMultiplier` | `EnemyData` | 1.6× | Used in Phase 2 Chase speed unless `ChaseSpeedMultiplierOverride > 0`. |

---

## 8. Acceptance Criteria

### Functional (pass/fail, verifiable in Unity Play Mode)

- [ ] **AC-TPS-01 — Phase 2 triggers on OnAllObjectivesCollected:** In a scene with one `ObjectiveToken`, collect it. `LevelPhaseManager.CurrentPhase` equals `Phase2_Escape` on the same frame as the collection tap is processed. Verify via a debug Inspector watch or test listener component. PASS: `CurrentPhase == Phase2_Escape` on the collection frame. FAIL: phase remains `Phase1_Find`, or transitions on a later frame.

- [ ] **AC-TPS-02 — Phase 2 does not double-trigger:** Connect a counter to `OnPhaseChanged`. Collect the last token. Manually fire `ObjectiveRegistry.OnAllObjectivesCollected` a second time via a test button. `OnPhaseChanged` fires exactly once total. `_phase2Triggered` is `true` after the first trigger. PASS: counter shows 1. FAIL: counter shows 2 or more.

- [ ] **AC-TPS-03 — All seekers receive Phase2SuspicionFloor on transition:** Three seekers in scene. Collect the last token. Immediately inspect `EnemyController.Phase2SuspicionFloor` on all three via debug. All equal `EscalationProfile.SuspicionFloor` (default: 60f). PASS: all three values equal 60f. FAIL: any seeker's value remains 0f.

- [ ] **AC-TPS-04 — All seekers receive Phase2SpeedMultiplier on transition:** Three seekers in scene. Collect the last token. Inspect `EnemyController.Phase2SpeedMultiplier` on all three. All equal `EscalationProfile.SpeedMultiplier` (default: 1.3f). PASS: all three values equal 1.3f. FAIL: any value remains 1.0f.

- [ ] **AC-TPS-05 — Seeker NavMeshAgent speed increases in Phase 2:** One seeker in Unaware patrol. Record `NavMeshAgent.speed` during Phase 1 (expected: `2.0 * 1.0 = 2.0 m/s`). Collect the last token. On the next FixedUpdate, record `NavMeshAgent.speed` again. Expected: `2.0 * 1.0 * 1.3 = 2.6 m/s`. PASS: speed equals 2.6 m/s ± 0.01. FAIL: speed is unchanged at 2.0 m/s, or below 2.6.

- [ ] **AC-TPS-06 — Seeker enters Alert or Searching within one FixedUpdate of Phase 2 trigger:** One seeker at suspicion 0 (fully Unaware, patrol state). Trigger Phase 2. Within 1 FixedUpdate cycle (20ms at 50Hz), the seeker's suspicion is clamped to `SuspicionFloor` (60f) by the Detection System. The seeker's `RequestedState` becomes Alert (threshold > 50). The seeker enters Alert state. PASS: seeker is in Alert or Searching state within 1 FixedUpdate. FAIL: seeker remains Unaware after the FixedUpdate.

- [ ] **AC-TPS-07 — Seeker cannot deescalate below SuspicionFloor in Phase 2:** Trigger Phase 2 with `SuspicionFloor = 60f`. Force seeker suspicion to 90 (via test tool). Block all detection (seeker cannot see or hear player). Wait 30 seconds. Inspect seeker suspicion. Must not fall below 60f. Seeker state must remain Searching or above. PASS: suspicion stays ≥ 60, state is Searching or Chase. FAIL: suspicion falls below 60, or seeker returns to Unaware/Alert stationary scan.

- [ ] **AC-TPS-08 — OnPhaseChanged fires with correct argument:** Connect a listener that logs the `LevelPhase` argument. Trigger Phase 2. PASS: listener receives `LevelPhase.Phase2_Escape`. FAIL: listener receives `Phase1_Find`, or event does not fire.

- [ ] **AC-TPS-09 — SoundID.Phase2Start plays on Phase 2 trigger:** Instrument `AudioManager.Instance.Play` with a log. Trigger Phase 2. PASS: `SoundID.Phase2Start` is logged exactly once on the transition frame. FAIL: no audio play call, or wrong SoundID.

- [ ] **AC-TPS-10 — Wind-down restores seeker values on OnExitUsed:** Trigger Phase 2. Then fire `LevelExit.OnExitUsed` (via test or by completing the exit). After `HandleExitUsed()` runs, inspect all seekers. `Phase2SuspicionFloor == 0f` and `Phase2SpeedMultiplier == 1.0f`. PASS: all seeker values restored. FAIL: any seeker retains Phase 2 values after the exit event.

- [ ] **AC-TPS-11 — SeekerRegistry.ActiveSeekerCount matches scene seeker count:** Three seekers in scene. Verify `SeekerRegistry.ActiveSeekerCount == 3` during play. Destroy one seeker via test tool. Verify `ActiveSeekerCount == 2`. PASS: count matches actual live seekers at both check points. FAIL: count is stale or incorrect.

- [ ] **AC-TPS-12 — SeekerRegistry returns empty list when no seekers present:** Level with no `EnemyController` prefabs. `SeekerRegistry.GetAll().Count == 0`. Trigger Phase 2. No `NullReferenceException`. `Debug.LogWarning` emitted referencing zero seekers. PASS: no exception, warning logged, `CurrentPhase == Phase2_Escape`. FAIL: exception thrown.

- [ ] **AC-TPS-13 — Phase 2 not triggered outside Playing state:** Set `GameManager.CurrentState = Warmup` via test tool. Manually invoke `LevelPhaseManager.HandleAllObjectivesCollected()`. `CurrentPhase` remains `Phase1_Find`. `Debug.LogWarning` emitted. PASS: no phase change, warning logged. FAIL: phase changes to Phase2_Escape.

- [ ] **AC-TPS-14 — Level reload resets to Phase 1:** Trigger Phase 2. Reload the level scene. Inspect `LevelPhaseManager.CurrentPhase`. PASS: `CurrentPhase == Phase1_Find`. All seekers have `Phase2SuspicionFloor == 0f`, `Phase2SpeedMultiplier == 1.0f`. FAIL: any Phase 2 values persist after scene reload.

- [ ] **AC-TPS-15 — EscalationProfile null fallback does not crash:** Remove the `EscalationProfile` asset assignment in the `LevelPhaseManager` inspector (set to None). Enter Play Mode. A `Debug.LogError` is emitted. Trigger Phase 2. Level remains playable with fallback values. PASS: `Debug.LogError` logged, Phase 2 transitions using fallback values `(SuspicionFloor=60, SpeedMultiplier=1.3)`, no `NullReferenceException`. FAIL: `NullReferenceException` thrown.

- [ ] **AC-TPS-16 — Phase 2 transition completes within 0.5 seconds of collection tap:** Use frame timestamps. Record the frame number of the collection tap (player input processed). Record the frame number when the first seeker enters Alert state. Calculate elapsed time as frame difference × frame duration. PASS: elapsed time ≤ 0.5 seconds. FAIL: elapsed time > 0.5 seconds.

- [ ] **AC-TPS-17 — Phase2SpeedMultiplier applies to all states:** One seeker. Trigger Phase 2. Observe seeker in Unaware (patrol), then Searching, then Chase states (use debug tools to force transitions). Measure `NavMeshAgent.speed` in each state. Each speed must equal `patrolSpeed * stateMultiplier * 1.3`. PASS: all three states show the 1.3× multiplier applied. FAIL: multiplier applies only to one state (e.g., only Chase).

- [ ] **AC-TPS-18 — SkipAlertScanInPhase2 = true compresses Alert to zero duration:** Create a test `EscalationProfile` with `SkipAlertScanInPhase2 = true`. Assign to `LevelPhaseManager`. Trigger Phase 2. A seeker whose suspicion crosses the Alert threshold must not hold the Alert stationary scan — it transitions directly to Searching. PASS: seeker is observed in Alert state for ≤ 1 frame before transitioning to Searching. FAIL: seeker holds Alert scan for the full `alertScanDuration`.

---

### Experiential (validated via observed play sessions — marked with asterisk)

- [ ] **AC-TPS-19* — Phase 2 transition is perceived as simultaneous with collection:** Playtesters are asked "When did the room change?" after completing their first level. PASS: ≥80% report the change felt simultaneous with or immediately after collecting the objective. FAIL: >20% report a noticeable delay, or report that the room "gradually got more dangerous" rather than changing at a specific moment.

- [ ] **AC-TPS-20* — Seekers' Phase 2 behavior change is legible within 5 seconds:** Playtesters who are observing the room when Phase 2 triggers (not hidden) must be able to describe within 5 seconds what changed about the seekers' behavior, without prompting. Target description: "they got faster" or "they stopped patrolling and started looking for me." PASS: ≥75% of testers identify the seeker behavioral change without being told. FAIL: testers report confusion about why the room feels different.

- [ ] **AC-TPS-21* — Phase 2 is perceived as a consequence of collection, not a random event:** After a session, ask: "Why did the seekers change behavior?" PASS: ≥85% of testers attribute the change to collecting the objective. FAIL: >15% attribute it to a timer, a random event, or "the game decided to change."

- [ ] **AC-TPS-22* — Players use Phase 1 knowledge to navigate Phase 2:** Observe whether players use patrol gaps, hiding spots, or throwable distractions identified in Phase 1 during Phase 2. PASS: ≥60% of players use at least one learned environmental element during Phase 2 escape. FAIL: players panic and run directly for the exit without using environmental knowledge, suggesting Phase 2 escalation is too disorienting to allow strategic play.
