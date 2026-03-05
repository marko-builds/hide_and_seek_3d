# Hiding Spot System

> **Status**: Approved
> **Author**: game-designer
> **Last Updated**: 2026-03-05
> **Implements Pillar**: The Room Has Rules (Pillar 1), Legible Jeopardy (Pillar 3), Silence Is a Tool (Pillar 2)
> **Design Order**: #8 — Feature Layer

---

## 1. Overview

The Hiding Spot System is UNSEEN's sanctuary mechanic — the set of rules governing how the player enters, occupies, peeks out of, and exits interactable concealment volumes (wardrobes, barrels, shadow alcoves) that reduce or eliminate the seeker's visual detection probability. Entry and exit are mediated by the Player Interaction System's `IInteractable` contract; the hiding spot itself communicates the player's concealment state to the Detection System via the `IHideable` interface. While inside and not peeking, visual line-of-sight detection is zeroed entirely. Peeking raises that floor to a small but non-negligible modifier, creating a legible risk gradient the player can actively manage. The proximity penalty from the Detection System means that hiding is not a safe room — a seeker who reaches the spot's last-known position and stands nearby will build suspicion passively, enforcing the "get out before they search you out" decision. For MVP, the system supports one concrete hiding spot type (`Wardrobe`) with a door animation and a snapped attachment position. The architecture is designed to extend cleanly to barrel, shadow, and any other future types through the same `HidingSpot` + `InteractableBase` composition pattern, with zero changes to the Detection System or Player Interaction System interfaces. All numeric values live in `HidingSpotData`; nothing is hardcoded.

---

## 2. Player Fantasy

**Target MDA Aesthetics**: Sensation (the lurch of relief when the door closes and darkness swallows you), Challenge (the live calculation of "do I peek now, or stay still?"), Discovery (learning that hiding is not safe — the seeker circles and the dread builds — and that timing your exit is its own puzzle).

**What hiding should feel like:**

The moment the wardrobe door clicks shut is one of the most viscerally satisfying beats in a stealth game — and one of the most psychologically loaded. The player is not safe. They are committed. They traded mobility for concealment, and now they must listen. The audio environment inside the wardrobe is the game — footsteps growing louder, the seeker's rhythm telling you where it is, the suspicion dread accumulating in the HUD as proximity tightens. This is Pillar 2 (Silence Is a Tool) expressed as patience rather than action.

**Peeking as active danger management:** The player can choose to peek, which partially exposes them but restores their ability to read the room visually. This is a deliberate risk trade: more information costs more detection exposure. Satisfying peek design requires the player to feel that they are making a real tactical choice, not just holding a button for a free look. The peek's elevated `hide_modifier` (0.15 vs. 0.0) makes that cost diegetically legible — the seeker reacts more, the suspicion bar moves, and the player understands why.

**The exit as the highest-stakes moment:** Exiting the hiding spot is when the player is most vulnerable. The door animation is not just visual polish — it is a commitment window. Once the player triggers the exit, the door opens, and they are exposed before they can move. Watching that door open while a seeker is audibly close is UNSEEN's purest expression of tension. The player should feel the weight of timing.

**Pillar alignment:**
- **Pillar 1 (The Room Has Rules):** The rules of hiding are learnable and consistent. "Fully hidden = invisible. Peeking = reduced visibility. Seeker nearby = slow suspicion build." No exceptions, no randomness.
- **Pillar 2 (Silence Is a Tool):** Hiding is an active tool, not a passive safe room. The player deploys it, uses it, and must decide when to abandon it.
- **Pillar 3 (Legible Jeopardy):** The player can always read their danger level. While hiding, the suspicion meter is visible, the proximity dread is visible, and peeking restores visual awareness at a visible cost.

**SDT anchors:**
- **Autonomy**: the player chooses when to enter, whether to peek, and when to exit. Multiple timing strategies are viable (enter early and wait; enter mid-pursuit; peek to time the exit; exit blind and sprint).
- **Competence**: experienced players learn to read audio cues from inside the wardrobe to judge when peeking is safe, and when proximity dread means they must exit now or be caught.

---

## 3. Detailed Rules

### 3.1 State Machine

The player's relationship to a hiding spot progresses through four discrete states. `PlayerHiding` is the state owner. No other system reads `PlayerHiding` internal state directly — they consume `IHideable.IsHidden` and `PlayerHiding.IsPeeking` (see 3.1a).

```
Hidden:  IsHidden=true,  IsPeeking=false
Peeking: IsHidden=true,  IsPeeking=true
                    |
                    v (Interact tap — exit)
None (not hiding):  IsHidden=false, IsPeeking=false
```

Full state diagram:

```
[None]
  |
  | Interact hold completes (RequiresHold=true, HoldDuration=entryHoldDuration) on unoccupied HidingSpot
  v
[Entering] — door opens → player snaps to AttachTransform → door closes
  |
  | EnterSequence complete
  v
[FullyHidden]   (IsHidden=true, IsPeeking=false)
  |         |
  |         | Peek input HELD
  |         v
  |     [Peeking]   (IsHidden=true, IsPeeking=true)
  |         |
  |         | Peek input RELEASED
  |         v
  |     [FullyHidden]
  |
  | Interact tap (from FullyHidden or Peeking)
  v
[Exiting] — door opens → ExitHidingSpot() → player repositioned → door closes → PlayerMovement restored
  |
  | ExitSequence complete
  v
[None]
```

Transitions are guarded by `_isTransitioning` (see Rule HS-2). The `Entering` and `Exiting` pseudo-states are internal to `Wardrobe`'s door animation coroutine; from `PlayerHiding`'s perspective, `IsHidden` flips atomically at the midpoint of each sequence (see 3.4).

---

### 3.1a IsPeeking: Interface Decision

**Decision: `IsPeeking` is added to `PlayerHiding` only, NOT to `IHideable`.**

Rationale: `IHideable` is the minimal read interface consumed by the Detection System. The Detection System requires only two facts to compute visual detection:
- Is the player visually blocked? (`IsHidden`)
- Which hiding spot is occupied, for proximity distance? (`CurrentHidingSpot.transform.position`)

The Detection System reads `IsPeeking` from a direct reference to `PlayerHiding` (which it already holds via `IHideable` cast, or via a separate `PlayerHiding` reference). Adding `IsPeeking` to `IHideable` would force every future `IHideable` implementor to expose a peeking concept — inappropriate for hiding types that have no peek mechanic (e.g., a shadow alcove that provides flat concealment). Keeping `IsPeeking` on `PlayerHiding` alone preserves `IHideable` as a minimal, stable interface.

**Updated `IHideable` interface (unchanged from current stub):**

```csharp
namespace HideAndSeek
{
    public interface IHideable
    {
        bool IsHidden { get; }
        HidingSpot CurrentHidingSpot { get; }
    }
}
```

**`PlayerHiding` additions required by this GDD:**

```csharp
// On PlayerHiding.cs — ADD this property:
public bool IsPeeking { get; private set; }

// On PlayerHiding.cs — ADD these events:
public event Action OnHidingEntered;
public event Action OnHidingExited;
public event Action<bool> OnPeekingChanged;  // bool = new IsPeeking value
```

The Detection System caches a `PlayerHiding` reference at startup (not `IHideable`) to read `IsPeeking`. The `IHideable` interface reference is retained for any system that needs only the minimal contract.

---

### 3.2 Entry Rules

**Rule HS-1 (Entry eligibility).** The player may enter a hiding spot if ALL of the following are true:
- a. The spot's `HidingSpot.IsOccupied == false`.
- b. `Wardrobe._isTransitioning == false` (door animation is not in progress).
- c. `Wardrobe.CanInteract == true` — which the `Wardrobe` component derives from (a) AND (b).
- d. `PlayerController.IsBeingChased == false`. Entry is blocked during active Chase via the Player Interaction System's blocking conditions (Rule B-1 in Player Interaction GDD). The wardrobe prompt shows dimmed during Chase but the player cannot enter.
- e. The Player Interaction System has completed a hold of `HidingSpotData.entryHoldDuration` seconds before dispatching `OnInteractComplete`. `Wardrobe.RequiresHold` returns `true` when the player is not currently inside — making entry a deliberate hold (default 0.5 s). This exposure window is load-bearing for the Player Interaction System's Player Fantasy (PIS GDD Section 2: *"The wardrobe requires a held press. During that half-second of holding, the player is exposed — the seeker may turn."*). Do not reduce below 0.3 s without playtest evidence.

**Rule HS-1a (Wardrobe IInteractable computed properties).** `Wardrobe` caches a `PlayerHiding` reference in `Awake` (via `PlayerController` on the same prefab, or `FindObjectOfType<PlayerHiding>()` for MVP). The following properties are computed each frame:

```csharp
private bool IsPlayerInsideThis =>
    _playerHiding.IsHidden && _playerHiding.CurrentHidingSpot == _hidingSpot;

public override bool RequiresHold   => !IsPlayerInsideThis;   // hold to enter, tap to exit
public override float HoldDuration  => _data.entryHoldDuration;
public override string PromptLabel  => IsPlayerInsideThis ? "Exit" : "Hide";
public override string PromptIconKey => IsPlayerInsideThis ? "exit" : "hide";
```

See Section 7.5 for the full IInteractable contract table.

**Rule HS-2 (Transition lock).** When the `EnterSequence` or `ExitSequence` coroutine begins, `_isTransitioning` is set to `true`. This causes `CanInteract` to return `false`. The lock releases only when the coroutine completes. This prevents the player from triggering a second interact mid-animation.

**Rule HS-3 (Enter sequence).** The `EnterSequence` coroutine on `Wardrobe` executes in this order:
1. `_isTransitioning = true`; `Animator.SetBool("IsOpen", true)`. `// AUDIO_HOOK: AudioManager.Play(SoundID.WardrobeOpen, transform.position)`
2. `yield return WaitFor.Seconds(_data.doorAnimationDuration)` — wait for door open animation.
3. `HidingSpot.TryEnter(playerHiding)` — if this returns `false` (spot became occupied mid-animation, e.g., two players in future co-op), abort sequence, close door, release lock. Log warning. This edge case is handled by Rule EC-5.
4. `playerHiding.EnterHidingSpot(this)` — `PlayerHiding` freezes the Rigidbody, disables `PlayerMovement`, and snaps the player's Transform to `HidingSpot.AttachTransform`.
5. `IsHidden` flips to `true`. `OnHidingEntered` event fires.
6. `Animator.SetBool("IsOpen", false)`. `// AUDIO_HOOK: AudioManager.Play(SoundID.WardrobeClose, transform.position)`
7. `yield return WaitFor.Seconds(_data.doorAnimationDuration)` — wait for door close animation.
8. `_isTransitioning = false`. `CanInteract` returns `true` (so exit is now available).

**Atomicity guarantee:** `IsHidden` becomes `true` at step 5, AFTER the player is snapped and before the door closes. The Detection System's visual bypass activates at this exact moment. The player is not visually hidden while the door is still open (steps 1–4).

---

### 3.3 Exit Rules

**Rule HS-4 (Exit eligibility).** While `IsHidden == true`, tapping Interact triggers the exit sequence regardless of peek state. Exit is NOT blocked by Chase — this is by design. If the seeker is chasing and the player entered hiding (visual detection zeroed), the player must be able to exit as a tactical choice. Blocking exit would trap them. The risk of exiting during Chase is the player's to manage.

**Rule HS-5 (Exit sequence).** The `ExitSequence` coroutine on `Wardrobe` executes in this order:
1. `_isTransitioning = true`. If `IsPeeking == true`, forcibly end peek: `PlayerHiding.EndPeek()`. Camera returns to inside-wardrobe position before door opens (avoids a jarring camera cut mid-animation).
2. `Animator.SetBool("IsOpen", true)`. `// AUDIO_HOOK: AudioManager.Play(SoundID.WardrobeOpen, transform.position)`
3. `yield return WaitFor.Seconds(_data.doorAnimationDuration)` — wait for door open.
4. `playerHiding.ExitHidingSpot()` — `PlayerHiding` restores the Rigidbody, re-enables `PlayerMovement`, and teleports the player to `HidingSpot.ExitPosition`.
5. `IsHidden` flips to `false`. `IsPeeking` flips to `false` (if not already). `OnHidingExited` event fires.
6. `Animator.SetBool("IsOpen", false)`. `// AUDIO_HOOK: AudioManager.Play(SoundID.WardrobeClose, transform.position)`
7. `yield return WaitFor.Seconds(_data.doorAnimationDuration)` — wait for door close.
8. `HidingSpot.Exit()` is called — clears `IsOccupied`. `_isTransitioning = false`.

**Rule HS-6 (PIS targeting lock while hidden).** While `PlayerHiding.IsHidden == true`, the Player Interaction System must not query for new `IInteractable` targets via its cone overlap. The PIS cone query is suspended — only the currently-occupied spot's `Wardrobe` component (already the active target from the moment `OnInteractComplete` fired) remains targetable. This prevents nearby `IInteractable` objects (a throwable resting on the floor beside the wardrobe, for example) from being targeted and prompted while the player is inside. The Hiding Spot System provides `PlayerHiding.IsHidden` as the signal; the Player Interaction System owns the implementation of this targeting lock. See PIS GDD edge case: *"When inside a hiding spot, the only valid IInteractable in range is the hiding spot's own exit trigger."*

**Exit position:** `HidingSpot.ExitPosition` is `transform.position + exitOffset`, where `exitOffset` is authored per-spot in `HidingSpotData` via the Inspector. This is an authored value, not dynamically computed. Rationale: dynamic computation from the wardrobe's "front face" would require reliable forward-vector conventions from level designers, which are error-prone. An authored offset is explicit, verifiable by the level designer, and can be visualized with a Scene View gizmo in `HidingSpot.OnDrawGizmosSelected`. Typical offset: `(0, 0, 1.2)` in local space — 1.2 m in front of the wardrobe door. The `HidingSpot` component draws a sphere gizmo at `ExitPosition` in Editor so the level designer can see the exit landing zone.

---

### 3.4 Camera States

**Inside wardrobe (FullyHidden, not peeking):**
The camera is locked to a fixed world-space position and rotation defined by `HidingSpot.CameraAnchor` — a child `Transform` on the `HidingSpot` prefab, positioned inside the wardrobe at approximately chest height, facing the door interior. `PlayerCameraController` disables its normal look-follow behavior and hard-locks to this Transform on entry. The player sees the wardrobe interior (back panel, hanging fabric, crack of light at the door) — an authored static view.

Rationale for static camera inside: A freely rotating camera inside a wardrobe would clip through walls, produce disorienting geometry, and give the player no meaningful spatial information (the seeker is outside). The static view satisfies Pillar 3 by ensuring the HUD elements (suspicion meter, proximity dread indicator) are fully readable in a stable frame, and sets up the peek moment as a deliberate shift from "blind but safe" to "can see but exposed."

**Peeking:**
The camera translates from `CameraAnchor` to `PeekCameraAnchor` — a second child `Transform` on the `HidingSpot` prefab, positioned at the door crack, slightly outside the wardrobe boundary along the door-facing axis. The camera does NOT restore player-driven look during peek; instead, it allows rotation only on the Y axis within `_data.peekYawRange` degrees centered on the wardrobe's forward vector. This gives the player a narrow horizontal scan arc to locate the seeker.

Rationale for constrained peek rotation: Full camera freedom while peeking would let the player rotate to face away from the seeker (the door side), which makes no physical sense and would allow peeking through walls. The constrained yaw arc (default ±30°) mirrors the physical constraint of peering through a door crack and provides exactly the information the player needs — what's directly in front of the wardrobe — without giving omniscient spatial awareness.

`PlayerCameraController.EnterPeekMode(HidingSpot spot)` and `PlayerCameraController.ExitPeekMode()` are the calls `PlayerHiding.BeginPeek()` and `PlayerHiding.EndPeek()` make. The camera transitions between `CameraAnchor` and `PeekCameraAnchor` over `_data.peekTransitionDuration` seconds (default 0.15 s) using a linear lerp.

---

### 3.5 Peek Input Binding

**Decision: Peek is triggered by holding the Crouch input (`InputSystem_Actions` Player map, `Crouch` action) while inside a hiding spot.**

Rationale: Three options were evaluated.

- **Option A — Dedicated Peek action (e.g., Right Mouse Button):** Requires a new input binding, adds cognitive load for the player to remember a new key, and consumes a prominent button just for a hiding-specific mode. Rejected because the hiding mechanic is MVP-limited to one spot type; the cost of a new binding is disproportionate.
- **Option B — Hold Interact while inside to peek:** Interact is already the exit trigger (tap). A hold-vs-tap disambiguation on the same button while inside the spot creates an ambiguous interaction model — the player who taps to exit and holds to peek will accidentally peek when trying to exit. Rejected because it violates Pillar 1 (predictable, learnable rules).
- **Option C — Crouch held while inside activates peek (Recommended):** The `Crouch` action already exists in the Player map. Crouching while inside a wardrobe has no movement function (movement is disabled), so repurposing it for peek has zero conflict. The mental model is physically intuitive: you crouch to look through the crack. The `PlayerHiding` component subscribes to `PlayerInputHandler.OnCrouchStarted` and `OnCrouchCancelled` while `IsHidden == true`; it unsubscribes on exit. No new input action is needed. `Crouch` is a hold-type action in the input asset — held = peek, released = stop peeking.

**Implementation note:** `PlayerMovement` currently processes crouch input directly. While `IsHidden == true`, `PlayerMovement` is disabled by `PlayerHiding.EnterHidingSpot()`, so `Crouch` input is not consumed by movement. `PlayerHiding.EnterHidingSpot()` subscribes to `PlayerInputHandler.OnCrouchStarted` and `OnCrouchCancelled`; `PlayerHiding.ExitHidingSpot()` unsubscribes. No changes to `PlayerInputHandler` are required. The `started` and `cancelled` phases of the `Crouch` action fire on press-down and release respectively for any action type (Button or Hold) — this implementation does not require `Crouch` to be specifically configured as a Hold interaction type in the input asset.

**Peek rules:**
- **Rule PK-1.** Peek begins when `Crouch` input `started` fires while `IsHidden == true AND _isTransitioning == false`. `PlayerHiding.BeginPeek()` is called.
- **Rule PK-2.** `BeginPeek()` sets `IsPeeking = true`, calls `PlayerCameraController.EnterPeekMode(CurrentHidingSpot)`, fires `OnPeekingChanged(true)`.
- **Rule PK-3.** Peek ends when `Crouch` input `cancelled` fires, OR when the exit sequence begins (Rule HS-5 step 1). `PlayerHiding.EndPeek()` sets `IsPeeking = false`, calls `PlayerCameraController.ExitPeekMode()`, fires `OnPeekingChanged(false)`.
- **Rule PK-4.** Peek cannot begin while `_isTransitioning == true` (door mid-animation). This prevents the camera from lerping toward a peek position while the door animation is in progress.
- **Rule PK-5.** The player CAN interact (tap Interact to exit) while peeking. The exit sequence calls `EndPeek()` first before opening the door.

---

### 3.6 Detection System Integration Contract

This section documents the exact contract between the Hiding Spot System and the Detection System. The Detection System GDD (Approved) defines the rules; this section confirms the Hiding Spot System's obligations.

**What the Hiding Spot System exposes to the Detection System:**

| Property | Source | Type | Detection System Rule Served |
|----------|--------|------|------------------------------|
| `PlayerHiding.IsHidden` | `PlayerHiding` | bool | Rule V-6: when `IsHidden=true AND IsPeeking=false`, `hide_modifier = 0.0`, LoS bypassed entirely |
| `PlayerHiding.IsPeeking` | `PlayerHiding` | bool | Rule V-6 (peek variant): when `IsPeeking=true`, `hide_modifier = peek_visibility_modifier` (default 0.15) |
| `PlayerHiding.CurrentHidingSpot.transform.position` | `HidingSpot.transform` | Vector3 | Formula F6: proximity penalty uses this as the spot center for distance calculation |

**Audio detection is unaffected by hiding.** Per Detection System Rule H-2, `IsHidden` does not suppress sound. The player's footstep noise emitter is disabled by `PlayerMovement` being disabled (no movement = no footstep events), but this is a side effect of movement disablement, not an explicit noise suppression. A player who triggers a noise while inside (e.g., future gadget use) will still emit that noise normally.

**Seeker search behavior at proximity (cross-system contract):** When a seeker in Chase state reaches the last-known player position and the player is hiding, the Detection System Formula F6 activates a passive suspicion accumulation on that seeker. The seeker's response to that accumulation (the "search the spot" behavior — a deterministic pause and look-around animation lasting `_seekerData.searchDwellTime` seconds) is owned by the Seeker AI's `SearchState`. The Hiding Spot System does not trigger or coordinate that animation. The contract is: the Hiding Spot System provides `CurrentHidingSpot.transform.position`; the Detection System computes proximity factor; the Seeker AI's state machine responds to the resulting suspicion output. These are three separate concerns in three separate systems.

---

### 3.7 ConcealmentModifier Reconciliation

**Decision: Repurpose `HidingSpotData.concealmentModifier` as a type-identification value for future spot types. Remove its use in the Detection System for MVP.**

The current `HidingSpotData.concealmentModifier = 0.1f` field conflicts with the Detection System's binary approach (`hide_modifier = 0.0` when fully hidden, `0.15` when peeking). For MVP (Wardrobe only), this value is not consumed by any system. Rather than removing it and losing the field for future use, it is repurposed:

- **For MVP (Wardrobe):** `concealmentModifier` is unused. The Detection System ignores it. It remains in `HidingSpotData` but is documented in the Inspector as "Future use — partial cover modifier for shadow/alcove types."
- **For future partial-cover types (shadow alcove, low barrel):** `concealmentModifier` will replace the binary `0.0` full-hide with a per-spot visual modifier. A shadow alcove might set `concealmentModifier = 0.4` — the player is not fully hidden but is significantly harder to see. The Detection System will require an update at that milestone to read this value when the spot type is `PartialCover`.
- **`HidingSpotData` gains a `HidingSpotType` enum field:** `enum HidingSpotType { FullCover, PartialCover }`. For MVP, all spots are `FullCover`. The Detection System reads `HidingSpotType` to decide whether to use the binary 0.0 path or the `concealmentModifier` path. This costs zero runtime overhead for MVP and avoids a breaking GDD change when partial-cover types are introduced.

**Migration Required — `HidingSpot.cs` stub update:** The current `HidingSpot.cs` stub defines `ConcealmentModifier` as a plain `float` field and `ExitPosition` as `transform.position + exitOffset`. Both must be replaced:
1. Add `[SerializeField] private HidingSpotData _data;` to `HidingSpot`.
2. Remove the plain `float ConcealmentModifier` and `Vector3 exitOffset` fields.
3. Update the `ExitPosition` property to: `public Vector3 ExitPosition => transform.TransformPoint(_data.exitOffset);`
4. Expose `ConcealmentModifier` as `public float ConcealmentModifier => _data.concealmentModifier;` and `HidingSpotType` as `public HidingSpotType HidingSpotType => _data.hidingSpotType;` for the Detection System to read.

**Updated `HidingSpotData` (authoritative definition):**

```csharp
[CreateAssetMenu(fileName = "HidingSpotData", menuName = "UNSEEN/Data/HidingSpotData")]
public class HidingSpotData : ScriptableObject
{
    [Header("Cover Type")]
    public HidingSpotType hidingSpotType = HidingSpotType.FullCover;

    [Header("Concealment")]
    [Tooltip("FullCover: unused (Detection System zeros hide_modifier). " +
             "PartialCover: visual hide modifier [0.0-1.0] passed to Detection System.")]
    [Range(0f, 1f)]
    public float concealmentModifier = 0.1f;

    [Header("Exit")]
    [Tooltip("Local-space offset from HidingSpot.transform.position for the player exit landing position.")]
    public Vector3 exitOffset = new Vector3(0f, 0f, 1.2f);

    [Header("Door Animation (Wardrobe-specific)")]
    [Tooltip("Duration in seconds for door open and door close animations.")]
    public float doorAnimationDuration = 0.35f;

    [Tooltip("Duration in seconds the player must hold Interact to enter this spot. " +
             "This is the exposure window — the player is visible while holding. " +
             "Must match the tension intent of the Player Interaction System GDD Section 2.")]
    [Range(0.3f, 1.0f)]
    public float entryHoldDuration = 0.5f;

    [Header("Peek")]
    [Tooltip("Half-arc in degrees the player can rotate the camera left/right while peeking.")]
    [Range(10f, 60f)]
    public float peekYawRange = 30f;

    [Tooltip("Duration in seconds for the camera lerp from CameraAnchor to PeekCameraAnchor.")]
    [Range(0.05f, 0.5f)]
    public float peekTransitionDuration = 0.15f;
}

public enum HidingSpotType
{
    FullCover,    // MVP: visual hide_modifier = 0.0 when IsHidden=true
    PartialCover  // Future: visual hide_modifier = concealmentModifier when IsHidden=true
}
```

---

### 3.8 Throw-While-Hiding Cross-System Boundary

**The player CAN throw a throwable while inside a hiding spot.**

This is an explicit design decision, not an oversight. Rationale:

The Throwable Object GDD (Approved) suppresses throw only when `PlayerController.IsBeingChased == true`. `IsHidden` is not a throw-suppression condition. Inside the hiding spot, `PlayerMovement` is disabled, but `ThrowableController` subscribes directly to `PlayerInputHandler.OnThrowPerformed` and has no dependency on `PlayerMovement`. The throw input fires, the arc preview appears, and the player can throw.

This interaction is intentional: throwing from inside a hiding spot is a high-skill distraction move. The player can hear the seeker approaching, aim the arc through the door opening (arc preview still renders), throw to a distant surface, and redirect the seeker before exiting — all without leaving the spot. The downside is that the throw arc preview renders in worldspace and may clip through the closed wardrobe door (a visual artifact). The level designer is responsible for ensuring throw targets are reachable from the wardrobe's authored position; the programmer should note that no arc geometry suppression is applied while `IsHidden == true`.

**If playtesting reveals that this interaction feels exploitative** (players trivially avoiding exits by throwing safely from inside without risk), the mitigation is: set `ThrowableController._throwSuppressed = true` when `PlayerHiding.IsHidden == true`. This flag already exists via the `IsBeingChased` pattern. This change requires a one-line addition to `ThrowableController.OnThrowPerformed()` and a new subscription to `PlayerHiding.OnHidingEntered/Exited`. The GDD flags this as a tuning decision to revisit after first playtest, not a day-one lock.

---

### 3.9 Multi-Type Extension Pattern

MVP implements `Wardrobe` only. The extension architecture for future spot types is:

**Composition pattern:** Each hiding spot type is a `MonoBehaviour` that:
1. Owns a `HidingSpot` child component (the data and occupancy state).
2. Extends `InteractableBase` (which implements `IInteractable` boilerplate).
3. Overrides `protected override void OnInteracted(PlayerController interactor)` to implement type-specific entry/exit logic.

Future types:
- `Barrel.cs` — no door animation; instant snap-in with a crouch blend. Override: skip door coroutine, directly call `TryEnter` and `EnterHidingSpot`.
- `ShadowAlcove.cs` — no physical container; player stands in a dark corner. Override: no animation; `HidingSpotData.hidingSpotType = PartialCover`. The Detection System uses `concealmentModifier` instead of zeroing `hide_modifier`. No `CameraAnchor` — player retains free camera (since they are not inside a container).

The `HidingSpot` component is type-agnostic. It does not know whether it belongs to a `Wardrobe` or a `Barrel`. The `PlayerHiding` component is also type-agnostic — `EnterHidingSpot(HidingSpot spot)` works identically regardless of the spot type. Only the entry/exit coroutine logic in the type-specific MonoBehaviour differs.

---

## 4. Formulas

### F-HS-1: Detection System Visual Modifier (Fully Hidden)

Applies when `IsHidden == true AND IsPeeking == false AND hidingSpotType == FullCover`.

```
hide_modifier = 0.0
visual_detection_score = base_detection_rate * hide_modifier * light_modifier * distance_modifier
                       = base_detection_rate * 0.0 * [any] * [any]
                       = 0.0
```

Visual detection contribution is zero. The player accumulates no visual suspicion while fully hidden in a FullCover spot.

| Variable | Type | Range | Source |
|----------|------|-------|--------|
| `hide_modifier` | float | 0.0 (fully hidden) | Detection System Rule V-6 |
| `base_detection_rate` | float | 0.0–1.0 per second | Detection System `EnemyData` |
| `visual_detection_score` | float | 0.0 | Computed |

---

### F-HS-2: Detection System Visual Modifier (Peeking)

Applies when `IsHidden == true AND IsPeeking == true`.

```
hide_modifier = peek_visibility_modifier    // from DetectionSystemData, default 0.15
visual_detection_score = base_detection_rate * hide_modifier * light_modifier * distance_modifier
```

| Variable | Type | Range | Source |
|----------|------|-------|--------|
| `peek_visibility_modifier` | float | 0.05–0.35 | `DetectionSystemData` ScriptableObject |
| Default value | — | 0.15 | Detection System GDD |

**Example:** `base_detection_rate = 1.0`, `light_modifier = 0.8`, `distance_modifier = 0.9`, `peek_visibility_modifier = 0.15`. `visual_detection_score = 1.0 × 0.15 × 0.8 × 0.9 = 0.108 per second`. Approximately 9.3 seconds of continuous peeking at close range in bright light before reaching the Alert threshold (suspicion = 30). This is meaningfully more dangerous than fully hidden but still survivable for short peek windows.

---

### F-HS-3: Proximity Penalty While Hiding

Applies when `IsHidden == true AND IsPeeking == false AND seeker_state >= Alert AND proximity_distance < hiding_spot_awareness_radius`.

This formula is defined in the Detection System GDD (Formula F6). Reproduced here for implementer reference:

```
proximity_distance = Vector3.Distance(seeker.transform.position,
                                      playerHiding.CurrentHidingSpot.transform.position)

proximity_factor = 1.0 - (proximity_distance / hiding_spot_awareness_radius)

suspicion_rate_addition = creeping_dread_rate * proximity_factor
```

| Variable | Type | Range | Source |
|----------|------|-------|--------|
| `proximity_distance` | float | 0.0 → `hiding_spot_awareness_radius` | Runtime computed |
| `hiding_spot_awareness_radius` | float | 1.0–4.0 m | `DetectionSystemData` ScriptableObject |
| `proximity_factor` | float | 0.0–1.0 | Computed: 1.0 at spot center, 0.0 at radius edge |
| `creeping_dread_rate` | float | 2.0–8.0 suspicion/second | `DetectionSystemData` ScriptableObject |

**Example (using Detection System GDD defaults: `hiding_spot_awareness_radius = 2.5 m`, `creeping_dread_rate = 3.0 suspicion/s`):** Seeker stands 1.25 m from spot center: `proximity_factor = 1.0 - (1.25/2.5) = 0.5`. `suspicion_rate_addition = 3.0 × 0.5 = 1.5 suspicion/s`. At this rate, from Alert (suspicion 30), Chase triggers (suspicion 80) after approximately 33 seconds. The player has a substantial but not unlimited exit window — meaningful pressure without being oppressively fast.

---

### F-HS-4: Exit Position

```
ExitPosition = HidingSpot.transform.TransformPoint(HidingSpotData.exitOffset)
```

`TransformPoint` converts the local-space `exitOffset` to world space, correctly accounting for the hiding spot's rotation and scale. A wardrobe rotated 45° in the scene will place the exit point 1.2 m in front of its door, not 1.2 m along the world Z axis.

| Variable | Type | Source |
|----------|------|--------|
| `exitOffset` | Vector3 | `HidingSpotData` ScriptableObject (local space) |
| `ExitPosition` | Vector3 | `HidingSpot.transform.TransformPoint(exitOffset)` |

Default `exitOffset` = `(0, 0, 1.2)`. This places the exit 1.2 m in front of the wardrobe's local forward axis. Level designers who rotate a wardrobe to face a different direction do not need to update `exitOffset` — the `TransformPoint` conversion handles it automatically.

**`HidingSpot.ExitPosition` property:**
```csharp
public Vector3 ExitPosition => transform.TransformPoint(_data.exitOffset);
```
This replaces the previous stub definition of `ExitPosition = transform.position + exitOffset`, which did not account for rotation.

---

### F-HS-5: Camera Peek Lerp

```
t_normalized = Mathf.Clamp01(elapsed / peekTransitionDuration)
cameraPosition = Vector3.Lerp(CameraAnchor.position, PeekCameraAnchor.position, t_normalized)
cameraRotation = Quaternion.Lerp(CameraAnchor.rotation, PeekCameraAnchor.rotation, t_normalized)
```

Applied each `Update` frame in `PlayerCameraController` while `IsLerpingToPeek == true` or `IsLerpingFromPeek == true`.

| Variable | Type | Range | Source |
|----------|------|-------|--------|
| `elapsed` | float | 0 → `peekTransitionDuration` | Runtime |
| `peekTransitionDuration` | float | 0.05–0.5 s | `HidingSpotData` |
| `t_normalized` | float | 0.0–1.0 | Computed |

---

### F-HS-6: Peek Yaw Clamp

```
rawYaw = PlayerInputHandler.LookInput.x * lookSensitivity * Time.deltaTime
clampedYaw = Mathf.Clamp(currentYawOffset + rawYaw,
                          -peekYawRange,
                          +peekYawRange)
currentYawOffset = clampedYaw
cameraRotation = PeekCameraAnchor.rotation * Quaternion.Euler(0, currentYawOffset, 0)
```

`currentYawOffset` is reset to 0 when peek begins (camera starts centered on wardrobe forward).

| Variable | Type | Range | Source |
|----------|------|-------|--------|
| `peekYawRange` | float | 10–60° | `HidingSpotData`, default 30° |
| `currentYawOffset` | float | `-peekYawRange` → `+peekYawRange` | Runtime state on `PlayerCameraController` |

---

## 5. Edge Cases

| ID | Scenario | Expected Behavior | Rationale |
|----|----------|------------------|-----------|
| EC-1 | Player taps Interact on wardrobe that is already occupied by another player (future co-op) | `HidingSpot.TryEnter` returns `false`. `Wardrobe.EnterSequence` aborts before calling `PlayerHiding.EnterHidingSpot`. Door does not open. `CanInteract` remains `true` on the next frame (spot might free up). No error, no state corruption. | `TryEnter` is the occupancy guard. The wardrobe's `CanInteract` is derived from `!IsOccupied AND !_isTransitioning`. If the spot is occupied, `CanInteract == false` and the Player Interaction System will not dispatch to it — EC-1 can only occur if two interacts dispatch simultaneously, which is a single-player impossibility. For co-op architecture, this is the correct guard. |
| EC-2 | Seeker transitions to Chase while player is mid-EnterSequence (door is open, player not yet inside) | Chase blocks interaction input (Player Interaction GDD Rule B-2), so this sequence should not begin during Chase. However, if Chase triggers mid-animation (latency between Detection System update and interaction block): the `EnterSequence` coroutine completes as normal. `IsHidden` flips to `true` on step 5, which immediately zeros visual detection. The seeker in Chase continues to the last-known position (the wardrobe location). Proximity penalty activates. This is valid gameplay: the player made it into the spot just in time. | Blocking mid-animation exit would leave the player frozen with the door open and no way to finish or escape — worse than completing the sequence. |
| EC-3 | Player triggers exit while seeker is directly in front of the wardrobe door | Exit sequence opens the door. Player teleports to `ExitPosition` (1.2 m in front of door). If the seeker is standing at that position, the player spawns inside the seeker's detection radius at close range. Detection System immediately applies full `base_detection_rate` with proximity bonuses. The player will likely be caught. This is correct: exiting into a seeker is a fatal mistake, not a bug. No special case handling. | Pillar 1: the room has rules. The rule is "don't open a wardrobe into a seeker." The legible danger of exiting is why Pillar 3 (Legible Jeopardy) requires the player to be able to peek before exiting. |
| EC-4 | Player holds Crouch input before entering (already crouching) and enters hiding spot while Crouch is held | Crouch held → `PlayerHiding` subscribes to crouch events on `EnterHidingSpot`. Since the `Crouch` action is already in `started` state when the subscription occurs, Unity Input System does not re-fire a `started` event. `PlayerHiding` must read `PlayerInputHandler.IsCrouchHeld` (a bool property reflecting current physical button state) on `EnterHidingSpot` and call `BeginPeek()` immediately if `true`. This prevents the player from entering and being non-peeking even though they physically hold Crouch. | Without this check, the player would have to release and re-hold Crouch to activate peek after entering, which is unintuitive and violates Pillar 1. |
| EC-5 | `HidingSpot.TryEnter` returns `false` mid-EnterSequence (concurrent access race) | `Wardrobe.EnterSequence` checks the return value of `TryEnter` (step 3). On `false`: close the door immediately (`Animator.SetBool("IsOpen", false)`, `WaitFor.Seconds(doorAnimationDuration)`), release `_isTransitioning`. `PlayerHiding` is never called. `IsHidden` remains `false`. Log a warning in Editor builds: `"HidingSpot.TryEnter returned false mid-sequence — spot may have been occupied by a concurrent interaction. Aborting entry."` | Single-player: this cannot normally occur. The `CanInteract` gate prevents double-entry. This guard exists for future co-op and for editor testing where a developer might manually call `TryEnter` from a test harness. |
| EC-6 | Player is peeking when `doorAnimationDuration` is very small (near 0) and spams Interact | `_isTransitioning == true` blocks re-entry during any sequence. Even at `doorAnimationDuration = 0.01 s`, the transition lock holds for the full coroutine execution. No double-sequence. Spamming Interact while transitioning produces no calls to `OnInteractComplete` because `CanInteract == false`. | Transition lock is the single guard for all sequence re-entry scenarios. |
| EC-7 | `CameraAnchor` or `PeekCameraAnchor` Transform is missing from the HidingSpot prefab | `PlayerHiding.EnterHidingSpot` (or `PlayerCameraController.EnterPeekMode`) calls `Debug.Assert(cameraAnchor != null, "HidingSpot is missing CameraAnchor child Transform — add it to the prefab.")` in Editor builds. At runtime without Assert: the camera does not move (remains at player position, which may clip through wardrobe geometry). Log an error. The player can still enter and exit; the experience is visually broken but not a crash. | Prefab validation in Editor prevents this from reaching playtesting. The `ComponentAuditTool` should include a rule: `HidingSpot prefabs must have child Transforms named "CameraAnchor" and "PeekCameraAnchor"`. |
| EC-8 | Player exits while falling (ExitPosition is over a ledge in the level) | `ExitPosition` is authored per-spot. If `exitOffset` places the exit over a void, the player falls to death/checkpoint. This is a level design error. `HidingSpot.OnDrawGizmosSelected` draws the exit sphere gizmo to make the exit zone visible during authoring. Level designers must verify exit position during scene setup. No runtime mitigation — teleporting to a "safe" computed position would be more surprising than landing on the authored spot. | Pillar 1: authored positions are what the system says they are. The gizmo is the prevention mechanism; runtime fallback would mask errors. |
| EC-9 | Player enters hiding spot during Jump arc (airborne) | Player Interaction GDD Rule B-1: `IsGrounded == false` blocks interaction entirely. `CanInteract` returns `true` on the wardrobe (it is available) but the Player Interaction System will not dispatch. The prompt remains visible with a dimmed input indicator. The player lands, the block clears, and on the next Interact tap the sequence begins normally. | Prevents the player from snapping into a wardrobe mid-jump, which would produce a jarring physics pop and break the sense of physical inhabitation. |
| EC-10 | `doorAnimationDuration` is set to 0 in `HidingSpotData` | `Wardrobe.Awake` fires `Debug.Assert(_data.doorAnimationDuration > 0f, ...)` in Editor builds. At runtime: `WaitFor.Seconds(0)` yields one frame (Unity coroutine semantics — `WaitForSeconds(0)` waits one frame minimum). The door animation is instant but the sequence still executes correctly. Visually, the door pops rather than animates. Not a crash; the hidden state is correctly applied. | Zero duration is a configuration error but should not break the state machine. |
| EC-11 | Player throws a throwable while inside the hiding spot (arc clips door geometry) | The throw fires normally (see Section 3.8). The arc preview renders in world space and may visually clip through the closed door. This is an accepted visual artifact for MVP. The `NoiseEvent` is emitted at the impact position, not at the wardrobe. The seeker responds to the impact point. No gameplay correctness issue. | The throw-while-hiding interaction is intentionally permitted. The visual clipping is a known MVP limitation documented here for the programmer to note. Post-MVP, the arc preview could be suppressed while `IsHidden == true` if the visual artifact is considered unacceptable. |
| EC-12 | Level unload while player is inside hiding spot | `PlayerHiding.OnDisable` (which fires on level unload) calls `ExitHidingSpot()` without the door sequence — immediate cleanup. `IsHidden = false`, `IsPeeking = false`, `PlayerMovement` re-enabled (even if the component is also being destroyed, no-op). No hanging coroutines: `Wardrobe` is destroyed with the scene; Unity cancels all running coroutines on a destroyed MonoBehaviour automatically. | Prevents `IsHidden = true` being carried across scene loads, which would leave the player in a frozen, invisible state in the next scene. |

---

## 6. Dependencies

### 6.1 What This System Requires

| Dependency | System | What Is Required | Direction | Integration Point |
|-----------|--------|-----------------|-----------|-------------------|
| `IInteractable` / `InteractableBase` | Player Interaction System | `OnInteractComplete(PlayerController)` dispatched to `Wardrobe` on hold complete (entry, `RequiresHold=true`) or tap (exit, `RequiresHold=false`). `CanInteract`, `RequiresHold`, `PromptLabel`, `PromptIconKey` queried each frame. See Section 7.5. | Inbound | `Wardrobe` extends `InteractableBase`, implements `IInteractable` |
| `PlayerController.IsBeingChased` | Player | Bool property; entry blocked by Player Interaction System when true. No direct read from this system. | Inbound (via PIS) | Player Interaction System blocking rule B-1 |
| `PlayerHiding.EnterHidingSpot` / `ExitHidingSpot` | Player | Methods on `PlayerHiding` that freeze/restore Rigidbody and PlayerMovement. | Internal (same player prefab) | Called by `Wardrobe.EnterSequence/ExitSequence` |
| `PlayerCameraController` (**new component — must be created**) | Player | `MonoBehaviour` on the player prefab. Minimum required API: `EnterHideMode(HidingSpot spot)` — locks camera to `spot.CameraAnchor`, disables look-follow; `ExitHideMode()` — restores look-follow; `EnterPeekMode(HidingSpot spot)` — transitions camera to `spot.PeekCameraAnchor`, enables constrained yaw; `ExitPeekMode()` — returns camera to `CameraAnchor`. Internal state needed: `IsLerpingToPeek` and `IsLerpingFromPeek` bool flags (used in F-HS-5 lerp). | Internal (same player prefab) | Called by `PlayerHiding` during all hide/peek state transitions |
| `PlayerInputHandler.OnCrouchStarted` / `OnCrouchCancelled` | Player | C# events for peek input. Subscribed when `IsHidden=true`, unsubscribed on exit. | Inbound | Subscribed in `PlayerHiding.EnterHidingSpot` |
| `PlayerInputHandler.IsCrouchHeld` | Player | Bool property for initial peek state on entry (EC-4). | Inbound | Read once in `PlayerHiding.EnterHidingSpot` |
| `WaitFor.Seconds` | Utilities | Cached `WaitForSeconds` to avoid GC in coroutines. | Inbound | Used in `Wardrobe.EnterSequence/ExitSequence` |
| `HidingSpotData` | Data | ScriptableObject with all tuning knobs. | Config | Serialized reference on `HidingSpot` component |
| `HidingSpotRegistry` (SceneSingleton) | GameLoop | Auto-registers each `HidingSpot` on `OnEnable`. | Inbound | `HidingSpot.OnEnable/OnDisable` call register/unregister |
| `Animator` (Wardrobe prefab) | Unity / Art | `"IsOpen"` bool parameter for door animation. | Inbound | `Wardrobe.EnterSequence/ExitSequence` |
| `InteractableLayer` | Unity Physics | Physics layer for interactable collider queries (used by Player Interaction System to find the wardrobe). | Config | Must be set on `HidingSpot` trigger collider |

### 6.2 What This System Provides

| Provided To | System | What Is Provided | Direction |
|------------|--------|-----------------|-----------|
| Detection System | Core | `PlayerHiding.IsHidden` (bool), `PlayerHiding.IsPeeking` (bool), `PlayerHiding.CurrentHidingSpot.transform.position` (Vector3) | Outbound — read by Detection System each FixedUpdate |
| Detection System | Core | `HidingSpotData.hidingSpotType` (enum) — informs whether to use binary 0.0 or `concealmentModifier` | Outbound — read via `HidingSpot.HidingSpotData` |
| HUD System | UI | `PlayerHiding.OnHidingEntered`, `PlayerHiding.OnHidingExited`, `PlayerHiding.OnPeekingChanged` events — for hiding-state-based HUD updates (e.g., show proximity dread indicator while hidden) | Outbound — event bus |
| Throwable Object | Gameplay | No change to `ThrowableController` — throw is permitted while hiding (see 3.8). If suppression is added, `PlayerHiding.OnHidingEntered/Exited` provides the hook. | Outbound — optional future hook |
| Checkpoint System | Future | `HidingSpotRegistry` provides the list of all `HidingSpot` instances; the Checkpoint System queries this to reset `IsOccupied` on respawn. | Outbound — registry read |
| Seeker AI | Gameplay | `HidingSpotRegistry` (via Detection System) provides spot positions for proximity penalty calculation. No direct Seeker AI dependency on this system. | Outbound — via Detection System |

### 6.3 Ownership Boundaries (Explicit)

| Item | Actual Owner |
|------|-------------|
| Seeker "look around" animation at proximity | Seeker AI `SearchState` — Hiding Spot System only provides position |
| Suspicion math for proximity penalty | Detection System Formula F6 |
| Audio inside the wardrobe (muffled seeker footsteps) | Audio system — this GDD does not specify audio implementation |
| Camera look-follow (disabled while hiding) | `PlayerCameraController` — Hiding Spot System calls enable/disable; camera owns its own logic |
| UI indicator while hiding (suspicion meter, dread bar) | HUD System — `PlayerHiding` events are the signal; HUD owns the display |
| Checkpoint reset of `IsOccupied` | Checkpoint System |

---

## 7. Tuning Knobs

All values are fields of `HidingSpotData` (`Assets/_Project/Scripts/Data/HidingSpotData.cs`) unless noted. No value is hardcoded in `Wardrobe.cs`, `HidingSpot.cs`, or `PlayerHiding.cs`.

| Knob | Type | Default | Safe Range | Category | Extreme Low Effect | Extreme High Effect |
|------|------|---------|-----------|----------|--------------------|---------------------|
| `doorAnimationDuration` | float | 0.35 s | 0.1–0.8 s | Feel | Door pops instantly — no sense of physical weight or commitment window | Door takes too long — player is exposed for too long during entry and exit; frustrating when a seeker is close |
| `entryHoldDuration` | float | 0.5 s | 0.3–1.0 s | Feel | Entry feels like a tap — no deliberate exposure window; violates PIS GDD Player Fantasy design intent (minimum 0.3 s) | Hold excessively long; frustrating when the seeker is far away and the player just wants to hide |
| `exitOffset` | Vector3 | (0, 0, 1.2) local | Z: 0.5–2.0 m | Gate | Exit landing too close to door — player immediately re-triggers door interact on the next frame (Z < 0.6 m risks this) | Exit landing too far — player may land outside the walkable area; check with NavMesh bake per level |
| `peekYawRange` | float | 30° | 10–60° | Feel | Player can barely rotate view; peek is nearly useless for locating the seeker | Player can see almost 180° while peeking; risk of peeking is reduced too much, undermining the cost |
| `peekTransitionDuration` | float | 0.15 s | 0.05–0.5 s | Feel | Camera snaps to peek position instantly — disorienting, no clear "pushing the door open" feel | Transition so slow that the peek is over before the player sees anything useful |
| `hidingSpotType` | enum | FullCover | FullCover / PartialCover | Gate | (no "too low") | (no "too high") — this is a binary type selection, not a range |
| `concealmentModifier` | float | 0.1 | 0.0–1.0 (PartialCover only) | Curve | 0.0 = same as FullCover (player is fully invisible in a partial cover — breaks intended risk gradient) | 1.0 = no concealment benefit (partial cover is worthless) |

**Detection System knobs that affect Hiding Spot behavior (live in `DetectionSystemData`, not `HidingSpotData`):**

| Knob | System | Default | Effect on Hiding Spot |
|------|--------|---------|----------------------|
| `peek_visibility_modifier` | Detection System | 0.15 | Visual detection rate multiplier while peeking — governs how dangerous peeking is |
| `hiding_spot_awareness_radius` | Detection System | 2.5 m | Radius within which a seeker builds passive suspicion on an occupied spot |
| `creeping_dread_rate` | Detection System | 3.0 suspicion/s | Rate of passive suspicion accumulation at spot center; drives urgency to exit |

---

### 7.4 Audio

Two `SoundID` entries must be defined in `SoundLibrary`:

| Event | SoundID | Trigger Point |
|-------|---------|---------------|
| Wardrobe door opens | `SoundID.WardrobeOpen` | EnterSequence step 1; ExitSequence step 2 |
| Wardrobe door closes | `SoundID.WardrobeClose` | EnterSequence step 6; ExitSequence step 6 |

Audio variant selection (e.g., creaky vs. smooth door) is art direction — deferred. For MVP, one audio clip per event is sufficient.

---

### 7.5 Interaction Contract (per IInteractable)

These are computed properties on `Wardrobe`, varying by player state. Do not change without updating the Player Interaction System GDD.

| Property | Entry State (player NOT inside) | Exit State (player inside) | Notes |
|----------|---------------------------------|---------------------------|-------|
| `RequiresHold` | `true` | `false` | Hold to enter (exposure window), tap to exit |
| `HoldDuration` | `HidingSpotData.entryHoldDuration` (default 0.5 s) | N/A | Must be ≥ 0.3 s. Owned by `HidingSpotData`. |
| `PromptLabel` | `"Hide"` | `"Exit"` | Max 12 chars. |
| `PromptIconKey` | `"hide"` | `"exit"` | Must match `PlayerInteractionData.PromptIcons` registry. |
| `CanInteract` | `!IsOccupied AND !_isTransitioning` | `!_isTransitioning` | Same logic; both block during door animation. |

---

## 8. Acceptance Criteria

### Functional (pass/fail, verifiable in Play Mode)

- [ ] **AC-01 — Entry sequence executes in order:** Place player 1.0 m from unoccupied wardrobe. Hold Interact for `entryHoldDuration` (0.5 s) until `OnInteractComplete` fires. Verify sequence: door opens, door-open animation plays for `doorAnimationDuration` seconds, player snaps to `AttachTransform`, `IsHidden` becomes `true`, door closes, `_isTransitioning` becomes `false`. All six steps must occur in this exact order with no step skipped.
- [ ] **AC-02 — CanInteract false during transition:** Begin `EnterSequence`. Mid-animation, check `Wardrobe.CanInteract`. Must return `false`. Attempt another hold mid-animation. No second sequence begins. `OnInteractComplete` is not called a second time.
- [ ] **AC-03 — IsHidden true only after snap:** Place a Detection System query at every frame. `IsHidden` must be `false` while the door is open (steps 1–4 of EnterSequence) and `true` only after `EnterHidingSpot` is called (step 5). Verify using a Debug.Log in `PlayerHiding.set_IsHidden`.
- [ ] **AC-04 — Exit sequence executes in order:** While `IsHidden=true`, tap Interact. Verify: door opens, player teleports to `ExitPosition`, `IsHidden` becomes `false`, door closes, `HidingSpot.IsOccupied` becomes `false`. Player Movement is active after sequence completes.
- [ ] **AC-05 — Exit NOT blocked during Chase:** Set `PlayerController.IsBeingChased = true`. While `IsHidden=true`, tap Interact. Exit sequence fires normally. Player exits the wardrobe. Verify `IsHidden == false` after sequence completes.
- [ ] **AC-06 — Entry IS blocked during Chase:** Set `PlayerController.IsBeingChased = true`. Player NOT hiding. Begin holding Interact on wardrobe. The Player Interaction System cancels the hold immediately (`OnInteractCancelled` fires on `Wardrobe`; PIS Rule B-2). No `EnterSequence` begins. `IsHidden` remains `false`. Prompt shows in dimmed state.
- [ ] **AC-07 — Peek activates on Crouch held while inside:** Enter hiding spot. Hold Crouch. Verify `IsPeeking == true`, camera lerps toward `PeekCameraAnchor` over `peekTransitionDuration`. Camera Y rotation responds to Look input, clamped to `±peekYawRange`.
- [ ] **AC-08 — Peek deactivates on Crouch released:** While peeking, release Crouch. Verify `IsPeeking == false`, camera lerps back to `CameraAnchor`.
- [ ] **AC-09 — Peek blocked during transition:** Begin `EnterSequence`. Before step 5 completes, hold Crouch. `BeginPeek` is not called (`_isTransitioning == true`). `IsPeeking` remains `false`.
- [ ] **AC-10 — Entry when Crouch already held triggers peek:** Hold Crouch. While holding, enter wardrobe (player was crouching externally). After `EnterHidingSpot` completes, `IsPeeking` must be `true` immediately (EC-4 handling via `IsCrouchHeld` check).
- [ ] **AC-11 — Visual detection zero while fully hidden:** Set up Detection System with `base_detection_rate = 1.0`, `light_modifier = 1.0`, `distance_modifier = 1.0`. Player enters wardrobe (`IsHidden=true`, `IsPeeking=false`). Run one FixedUpdate. `suspicion_delta` for that frame must be 0.0.
- [ ] **AC-12 — Visual detection non-zero while peeking:** Same setup as AC-11. Player begins peeking (`IsPeeking=true`). Run one FixedUpdate. `suspicion_delta` must be `base_detection_rate × peek_visibility_modifier × light_modifier × distance_modifier`. Expected: 1.0 × 0.15 × 1.0 × 1.0 = 0.15 per second (delta per FixedUpdate ≈ 0.15 × fixedDeltaTime).
- [ ] **AC-13 — Proximity penalty active when seeker within radius:** Seeker in Alert state, standing 1.5 m from occupied wardrobe (`hiding_spot_awareness_radius = 3.0 m`, `creeping_dread_rate = 4.0`). `IsPeeking=false`. Run FixedUpdate. Suspicion delta for proximity penalty must be `4.0 × (1 - 1.5/3.0) = 2.0 suspicion/s` (or scaled by `fixedDeltaTime` per frame).
- [ ] **AC-14 — ExitPosition accounts for wardrobe rotation:** Rotate wardrobe 90° in the scene. Enter and exit. Player lands 1.2 m in the local forward direction of the rotated wardrobe, not 1.2 m along world Z. Verify by measuring player position after exit.
- [ ] **AC-15 — HidingSpotRegistry populated on OnEnable:** Place two `HidingSpot` instances in scene. Enter Play Mode. `HidingSpotRegistry.Instance.HidingSpots.Count == 2`. Disable one. Count drops to 1.
- [ ] **AC-16 — Occupied spot is not enterable by a second interaction:** `IsOccupied=true`. Approach wardrobe. `CanInteract` returns `false`. No prompt shown. Player cannot enter. `IsOccupied` remains unchanged.
- [ ] **AC-17 — No GC allocations in hiding system Update paths:** Profile `PlayerHiding.Update` (if any), `Wardrobe.Update` (if any), and `PlayerCameraController` peek lerp. Zero bytes GC Alloc per frame confirmed in Unity Profiler.
- [ ] **AC-18 — Throw fires normally while inside hiding spot:** Player carries throwable, enters hiding spot. Press Throw. `NoiseEvent` fires at impact position. `ThrowableController.CurrentState == Landed`. Player remains inside wardrobe (`IsHidden == true`).
- [ ] **AC-19 — No hanging state on scene unload:** Player is inside wardrobe when scene unloads. In the next scene (or on reload), `PlayerHiding.IsHidden == false`, `PlayerMovement` is enabled, and `IsPeeking == false`.

### Experiential (validated via observed play sessions)

- [ ] **AC-20 — Player correctly understands hiding is not permanent safety:** After 5 minutes with the system, including at least one seeker proximity event: ≥80% of testers report "I had to decide when to leave the wardrobe" rather than "I hid and waited safely." If testers report the wardrobe as a permanent safe room, reduce `creeping_dread_rate` minimum floor or add a visual dread indicator inside the wardrobe.
- [ ] **AC-21 — Peek feels like a meaningful risk decision:** Tester debrief after session. Target: ≥70% of testers who used peek report they "thought about it before peeking" rather than "just held it the whole time." If ≥30% hold peek constantly with no consequence, increase `peek_visibility_modifier` until the risk is perceptible, or add an audio cue on seeker suspicion increase while peeking.
- [ ] **AC-22 — Exit timing is the player's legible responsibility:** When players are caught immediately after exiting into a seeker, debrief: "Did you know the seeker was there before you exited?" Target: ≥75% say yes and accept responsibility ("I should have peeked first"). If ≥25% say "I had no way to know," the peek camera's yaw range or view position needs adjustment to expose the seeker before the exit trigger is reachable.
- [ ] **AC-23 — Door animation duration feels right:** In a session without tutorial: ≥80% of players complete a hide (full entry sequence) within the first 3 wardrobe approaches. If ≥20% fail to complete the entry (walk away mid-sequence, confused), reduce `doorAnimationDuration` toward 0.2 s. If ≥30% comment the door "feels instant/cheap," increase toward 0.5 s.

---

*End of Hiding Spot System GDD*
