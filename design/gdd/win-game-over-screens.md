# Win / Game Over Screens

> **Status**: Approved
> **Author**: game-designer + ux-designer
> **Last Updated**: 2026-03-06
> **Implements Pillar**: Two-Beat Tension (Pillar 4)

---

## 1. Overview

The Win / Game Over Screens system owns the two end-state overlays shown when a
level session terminates. The **Win Screen** (`WinUI`) appears when
`GameManager.TriggerWin()` is called — triggered by a successful Level Exit hold
interaction — and delivers the cathartic exhale that closes the Two-Beat tension
arc of UNSEEN. The **Game Over Screen** (`GameOverUI`) is a stub in MVP: it is
scaffolded and callable, but `GameManager.TriggerLose()` is never invoked by any
gameplay system in MVP (every caught event triggers a Checkpoint respawn instead).
The Game Over Screen will be activated in a future permadeath or limited-lives mode
without requiring structural rework. Both screens are Canvas-based overlays that
subscribe exclusively to `GameManager` static events (`OnWin` and `OnLose`) —
they never poll gameplay state, never call gameplay code, and never listen to
lower-level events such as `LevelExit.OnExitUsed` or `CheckpointManager.OnPlayerCaught`.
The HUD hides itself when either screen appears, by subscribing to the same
`GameManager.OnWin` / `GameManager.OnLose` events. All configurable timing values
live in a `WinGameOverData` ScriptableObject; nothing is hardcoded in the UI
MonoBehaviours.

---

## 2. Player Fantasy

### Win Screen: The Exhale

**Target MDA Aesthetics**: Challenge (the player completed the run), Sensation
(the breath-out after sustained tension), Narrative (the relic is yours — you
outran them).

The Win Screen is earned. The player has just spent Phase 2 navigating a
fully-escalated room — seekers alerted, music changed, heart rate elevated —
held a button at the exit while a seeker closed in, and succeeded. The screen
that follows must honor that arc without extending it. The tension must end
cleanly. The victory must register immediately.

What the player should feel when the Win Screen appears:

**Relief first, then pride.** The first 500 milliseconds after the screen
appears should communicate "you made it" through visual weight alone: the word
ESCAPED fills the center, the palette brightens slightly, and the silence after
the escape audio cuts through. The player exhales. They are no longer in danger.

**Dignity, not spectacle.** This is not a parade. There are no fireworks, no
ranking animations, no social share prompts. UNSEEN is a composed game — the
win is acknowledged with the same quiet authority that the rest of the game
uses. A clean headline, a level name, a stub stats row, two buttons. The
player understands they are done and what their options are.

**Respect for the run.** The stats row — even as a stub in MVP — signals that
the game knows what the player did: how long they took, how many times they
were caught. In Vertical Slice, this row fills with real numbers. In MVP, the
placeholder is present and positioned correctly so the player can anticipate
what will be there.

**SDT anchor (Win Screen):** Competence — the screen confirms the player
accomplished the task. Autonomy — exactly two options, both clearly labeled,
no coercive prompt design.

---

### Game Over Screen: The Reckoning (Future)

**Target MDA Aesthetics**: Challenge (consequence of failure), Submission
(accepting the outcome), Competence (the path to retry is immediate).

The Game Over Screen does not appear in MVP play. It is designed for a future
mode where catches accumulate consequences (permadeath, limited lives). When
it does appear, it must communicate a specific emotional register: gravity
without cruelty.

The player who sees this screen failed at a cost that matters. The screen
should feel weighty — darker, quieter, more subdued than the Win Screen — but
not punishing in its design. The retry option is prominent. The player should
never feel that the game is gloating or making the retry path difficult to find.

The tone is: "You were captured. Here is what happened. Try again?" Not:
"GAME OVER" in dripping red with an unskippable animation.

**SDT anchor (Game Over Screen):** Competence — the retry path is immediate,
no extra gates. Autonomy — the player chooses whether to retry or return to
the menu, with no coercive default timer forcing a choice.

---

## 3. Detailed Design

### 3.1 New GameManager Event Contracts

This GDD requires two new static C# events on `GameManager`. These are the
sole trigger mechanism for both screens. No other system in this GDD subscribes
to lower-level events to initiate screen display.

```csharp
// Addition to GameManager.cs (namespace HideAndSeek)

public static event Action OnWin;
// Fired inside TriggerWin(), before transitioning CurrentState to GameState.Win.
// Fire order within TriggerWin():
//   1. OnWin?.Invoke()        ← WinUI and HUDManager subscribe here
//   2. CurrentState = GameState.Win
// Subscribers must not call TriggerWin() recursively.

public static event Action OnLose;
// Fired inside TriggerLose(), before transitioning CurrentState to GameState.Lose.
// Fire order within TriggerLose():
//   1. OnLose?.Invoke()       ← GameOverUI and HUDManager subscribe here
//   2. CurrentState = GameState.Lose
// MVP NOTE: TriggerLose() is never called in MVP. GameOverUI subscribes
// correctly but receives no event during normal MVP play.
```

**Updated TriggerWin() implementation contract:**

```csharp
public void TriggerWin()
{
    if (CurrentState != GameState.Playing) return; // idempotency guard
    OnWin?.Invoke();
    CurrentState = GameState.Win;
}
```

**Updated TriggerLose() implementation contract:**

```csharp
public void TriggerLose()
{
    if (CurrentState != GameState.Playing) return; // idempotency guard
    OnLose?.Invoke();
    CurrentState = GameState.Lose;
}
```

The idempotency guard (`CurrentState != GameState.Playing`) prevents duplicate
fires if both events are somehow triggered in the same frame (see Edge Cases,
Section 5).

---

### 3.2 Scene Architecture

**Canvas structure:** Both screens live on a single `WinGameOverCanvas`
`GameObject` in the level scene. The `GameObject` holds:

- A `Canvas` component in **Screen Space — Overlay** mode with `sortingOrder = 20`
  (above the HUD, which uses `sortingOrder = 10`; above the CAUGHT card from
  `RespawnUI`, which uses `sortingOrder = 15`).
- A `CanvasScaler` set to **Scale With Screen Size**, reference resolution
  `1920 × 1080`, match width-or-height = 0.5. This ensures both screens scale
  correctly on non-16:9 aspect ratios.
- A `GraphicRaycaster` component (required for button interaction).
- Two child `GameObject`s: `WinPanel` and `GameOverPanel`. Each is a full-screen
  `RectTransform` with `CanvasGroup` for alpha control.

**Default state on Awake:** Both `WinPanel` and `GameOverPanel` start with
`CanvasGroup.alpha = 0` and `CanvasGroup.interactable = false` and
`CanvasGroup.blocksRaycasts = false`. Neither panel is visible or interactive
at scene load. They do not need to be deactivated (just invisible and
non-interactive) — keeping them active ensures their `MonoBehaviour` components
can subscribe to `GameManager` events in `OnEnable`.

**Controller MonoBehaviour:** A single `WinGameOverController` MonoBehaviour
is attached to the `WinGameOverCanvas` `GameObject`. It holds serialized
references to both panels' `CanvasGroup`s and to all interactive UI elements
within each panel. It subscribes to `GameManager.OnWin` and `GameManager.OnLose`
in `OnEnable` and unsubscribes in `OnDisable`. It does NOT use `Singleton<T>` or
`SceneSingleton<T>` — it is a simple scene component with no cross-scene
access requirements.

**Prefab:** The `WinGameOverCanvas` is a single prefab placed in each level
scene. The level name shown on each screen is read at runtime from a
`LevelConfig` ScriptableObject (see Section 3.3, Rule WGO-4) or falls back
to `UnityEngine.SceneManagement.SceneManager.GetActiveScene().name`.

**Sorting order rationale:** The Win/Game Over screens must render above the
HUD (which handles seeker detection feedback and player state). They must also
render above `RespawnUI`'s CAUGHT card, which uses sortingOrder 15. Using
sortingOrder 20 ensures the win/lose states are unambiguously the topmost
visible layer. No other canvas in the scene should use sortingOrder >= 20
without explicit coordination with this system.

---

### 3.3 Win Screen — All Elements and Layout

**Rule WGO-1 (Win Screen Layout).** The `WinPanel` contains the following
elements, laid out vertically with a `VerticalLayoutGroup`. All text uses
TextMeshPro. All elements are center-anchored.

```
+------------------------------------------------------------------+
|                                                                  |
|                                                                  |
|                         ESCAPED                                  |
|                    [win-headline, 64px]                          |
|                                                                  |
|                    Vault of the Warden                           |
|                    [level-name, 24px]                            |
|                                                                  |
|          Time: --:--        Caught: --                           |
|                    [stats-row, 18px, stub]                       |
|                                                                  |
|               [ Play Again ]  [ Main Menu ]                      |
|                    [buttons, 32px label]                         |
|                                                                  |
+------------------------------------------------------------------+
```

Element specifications:

| Element | Content | Font Size | Color | Notes |
|---------|---------|-----------|-------|-------|
| Win Headline | "ESCAPED" | 64 px | White `#FFFFFF` | Uppercase; bold weight; letter-spacing 8px |
| Level Name | From `LevelConfig.DisplayName` (see Rule WGO-4) | 24 px | `#CCCCCC` (light grey) | Normal weight; 1 line max; truncated with `…` if > 30 chars |
| Stats Row | "Time: --:--    Caught: --" | 18 px | `#999999` (muted grey) | Two stub fields; format defined in Section 3.3, Rule WGO-5 |
| Play Again Button | "Play Again" | 20 px label | White label on `#1A1A1A` bg, `#FFFFFF` border | Full implementation; active in MVP |
| Main Menu Button | "Main Menu" | 20 px label | White label on `#1A1A1A` bg, `#FFFFFF` border at 40% alpha | Stub in MVP; disabled (see Rule WGO-7) |

**Spacing:** Vertical gap between elements: headline → level name: 16 px,
level name → stats row: 12 px, stats row → buttons: 32 px. These gaps are
set via the `VerticalLayoutGroup.spacing` and child `LayoutElement` min-height
values. No absolute positioning — all layout is flexible.

**Background:** The `WinPanel` has a full-screen `Image` component behind the
`VerticalLayoutGroup`. The image uses a solid color at `#000000` with alpha
set to `winPanelBackgroundAlpha` (0.85, see Tuning Knobs). This is a near-opaque
dark overlay. The gameplay scene behind it should not be visible through the panel.

**Rule WGO-2 (Win Headline: "ESCAPED").** The win headline is always "ESCAPED."
This word is chosen for thematic consistency: UNSEEN is about a relic thief
escaping a seeker-guarded vault. "ESCAPED" is the past-tense confirmation of the
player's goal. It is short enough to register in a single peripheral glance. It
is distinct from outcome words in other game genres ("YOU WIN", "VICTORY",
"SUCCESS") that carry no narrative weight in UNSEEN's fiction.

**Rule WGO-3 (Button Layout).** The two buttons ("Play Again" and "Main Menu")
are arranged horizontally with 16px gap, centered on the panel. Each button is
a `Button` with a `TextMeshPro` label. Button minimum size: 160 × 48 px.

**Rule WGO-4 (Level Name Source).** The level name displayed on the Win Screen
is read from `LevelConfig.DisplayName`, a string field on a `LevelConfig`
ScriptableObject. `WinGameOverController` reads this in `Awake` from
`LevelConfig` (loaded from `Resources/LevelConfigs/[SceneName]` or injected
via a serialized reference on the prefab). If `LevelConfig` is null or
`DisplayName` is empty or whitespace, the fallback is:
`SceneManager.GetActiveScene().name`, rendered as-is. The level name must
never be empty on the Win Screen — the fallback ensures this. The same source
is used by both the Win Screen and Game Over Screen.

**Rule WGO-5 (Stats Row — Stub Contract).** In MVP, the stats row displays
literal placeholder text: `"Time: --:--    Caught: --"`. This is hardcoded
stub content. The two stub tokens ("--:--" and "--") are replaced by real data
in Vertical Slice when `Level Timer + Stats` (systems-index #17) provides the
following event contract:

```csharp
// Stub contract — defined here, implemented by Level Timer + Stats GDD:
public static event Action<LevelStats> OnStatsReady;
// Fires after TriggerWin() is called, carrying final session stats.

public struct LevelStats
{
    public float TotalTimeSeconds;  // 0–3600; format as MM:SS
    public int   CatchCount;        // 0–99; number of respawns this session
}
```

`WinGameOverController` subscribes to `OnStatsReady` in `OnEnable` and
unsubscribes in `OnDisable`. In MVP, the event is never fired, and the stub
text remains. In Vertical Slice, when the event fires, `WinGameOverController`
formats and populates the stats row text:

```
Time: {MM}:{SS}    Caught: {CatchCount}
```

Where `MM` = `Mathf.FloorToInt(TotalTimeSeconds / 60f).ToString("00")` and
`SS` = `Mathf.FloorToInt(TotalTimeSeconds % 60f).ToString("00")`.

The Game Over Screen uses the same `LevelStats` stub contract and the same
formatting rules for its own stats row.

---

### 3.4 Win Screen — Transition In

**Rule WGO-6 (Win Screen Appearance Sequence).** When `GameManager.OnWin`
fires, `WinGameOverController.HandleWin()` runs the following sequence:

```
Frame 0: GameManager.OnWin fires.
         WinGameOverController.HandleWin() is called synchronously.
         Disable player input: PlayerInputHandler.Instance.SetInputEnabled(false).
         Begin Coroutine WinAppearSequence().

Inside WinAppearSequence():

  Wait: yield WaitFor.Seconds(winAppearDelay)  [default: 0.5 s]
        — This delay allows the LevelExit's ExitVFX and ExitSFX to play
          (Rule LE-9, steps 2–3) before the screen overlays them.

  Fade in WinPanel CanvasGroup:
        Lerp alpha from 0.0 to 1.0 over winFadeInDuration  [default: 0.4 s].
        Simultaneously: lerp WinPanel.CanvasGroup.alpha each frame.

  When alpha >= 1.0:
        Set CanvasGroup.interactable = true.
        Set CanvasGroup.blocksRaycasts = true.
        Focus the "Play Again" button (EventSystem.current.SetSelectedGameObject).
```

**Total time from TriggerWin() to Win Screen fully visible:**
`winAppearDelay + winFadeInDuration` = 0.5 + 0.4 = **0.9 seconds** (at defaults).

**Why a delay before the fade:** The Level Exit system's `OnInteractComplete`
fires the exit VFX and SFX before calling `TriggerWin()` (Rule LE-9, steps 1–4
precede step 5 which calls `TriggerWin()`). However, these effects are
instantaneous fires — they play asynchronously as particle systems and audio.
A 0.5-second delay before the Win Screen overlay fades in gives these effects
space to be seen and heard before the screen is covered. Without the delay, the
Win Screen could appear simultaneously with the exit VFX, colliding visually
and undermining the exit moment. The delay is a Tuning Knob — see Section 7.

**HUD during Win Screen:** The HUD subscribes to `GameManager.OnWin` and
immediately begins fading its `CanvasGroup.alpha` to 0 over `hudWinFadeDuration`
(0.2 s). This is specified as a new behavior in the HUD GDD's dependency section
(see Section 6 of this GDD). The HUD fade and the Win Screen fade-in run
concurrently: the HUD disappears as the Win Screen appears. Because the Win
Screen's `winAppearDelay` is 0.5 s, the HUD will be fully invisible before the
Win Screen begins fading in (HUD fade: 0.2 s < delay: 0.5 s).

---

### 3.5 Win Screen — Buttons

**Rule WGO-7 (Play Again Button).** "Play Again" reloads the active scene
using:

```csharp
SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
```

No transition, no delay — Unity handles the scene reload. The button's `onClick`
handler calls this directly. No pre-reload logic is needed from this system;
game state cleanup is handled by Unity's scene load (all MonoBehaviours are
destroyed and re-initialized).

**Rule WGO-8 (Main Menu Button — Stub).** "Main Menu" is a stub in MVP. The
button is rendered in a visually disabled style (see element specifications
in Section 3.3: border at 40% alpha, label at 60% alpha) and its `Button.interactable`
is set to `false`. It cannot be clicked in MVP. When Main Menu (systems-index #24,
Full Vision scope) is implemented, the button is enabled and its `onClick`
handler calls the Main Menu scene load. Do NOT wire the button to a "do nothing"
or `Debug.Log` handler — leave `interactable = false` and the disabled visual
style communicates the stub status clearly. A `[Tooltip]` on the serialized
button reference in `WinGameOverController` reads: "STUB: Enable and wire when
Main Menu scene exists (Full Vision scope)."

**Rule WGO-9 (Button Input Modalities).**
- **Mouse click:** Default. The `GraphicRaycaster` on the Canvas handles pointer
  events. No additional implementation required.
- **Keyboard navigation:** Tab cycles between the two buttons. Enter/Space
  confirms the selected button. This is automatic with Unity's `EventSystem`
  `StandaloneInputModule` when `EventSystem.current.SetSelectedGameObject` is
  called with a Button reference on Win Screen appearance. The navigation order
  defaults to the sibling order of the buttons in the hierarchy (Play Again
  first, Main Menu second).
- **Controller (stub for MVP):** The `StandaloneInputModule` handles controller
  D-Pad and A-button navigation natively in Unity's Input System. No additional
  controller-specific code is required in MVP. The contract for Vertical Slice:
  the left button (Play Again) is the default selection on screen appear; D-Pad
  horizontal switches selection; A-button (South) confirms.
- **No "press any key" prompt:** The player must use an explicit button press.
  No `anyKey` handler is subscribed. This is intentional: imprecise inputs
  should not accidentally load a new scene.

---

### 3.6 Game Over Screen — Stub Design

**Rule WGO-10 (Game Over Screen: MVP Stub Policy).** The Game Over Screen is
fully scaffolded but produces no visible output during normal MVP play.
`GameManager.TriggerLose()` is never called by any MVP gameplay system (per
Checkpoint System GDD Rule CP-7: "In MVP, every caught event triggers a respawn,
never a permanent lose state"). `GameOverUI.cs` must not throw errors if
`GameManager.OnLose` fires unexpectedly (e.g., via a test harness or Editor
script). The component is always subscribed and always ready — it just never
receives an event in normal MVP play.

**Rule WGO-11 (Game Over Screen Layout).** The `GameOverPanel` mirrors the
`WinPanel` layout with the following differences:

| Element | Win Screen | Game Over Screen |
|---------|------------|-----------------|
| Headline | "ESCAPED" | "CAPTURED" |
| Headline color | `#FFFFFF` (white) | `#CCCCCC` (light grey — subdued) |
| Background alpha | 0.85 | 0.92 (darker — more weight) |
| Primary button label | "Play Again" | "Try Again" |
| Stats row | "Time: --:--    Caught: --" | "Time: --:--    Caught: --" |

The Game Over Screen is intentionally darker and more subdued. The headline
"CAPTURED" matches UNSEEN's relic-theft fiction (the seekers captured the thief).
It does not use inflammatory language ("FAILURE", "DEAD", "YOU LOSE"). The tone
is matter-of-fact: you were captured; do you want to try again?

**Rule WGO-12 (Game Over Screen Transition In).** When `GameManager.OnLose`
fires, `WinGameOverController.HandleLose()` runs an identical sequence to
`HandleWin()`, but using `gameOverAppearDelay` and `gameOverFadeInDuration`
(both default to same values as Win Screen: 0.3 s delay, 0.4 s fade). The
shorter delay (0.3 s vs 0.5 s) is because the Lose state does not have VFX
playing at the moment `TriggerLose()` fires — the Checkpoint system's respawn
flash is its own system. The Game Over Screen should appear quickly after the
loss is triggered.

**Rule WGO-13 (Game Over Screen Buttons).** "Try Again" and "Main Menu" behave
identically to the Win Screen's "Play Again" and "Main Menu" respectively:
- "Try Again": `SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)`.
- "Main Menu": `interactable = false` stub in MVP.

**Rule WGO-14 (Game Over Screen: No Further Catch During Display).** Once the
Game Over Screen is visible (`CanvasGroup.alpha >= 1.0`), player input is
disabled (`PlayerInputHandler.Instance.SetInputEnabled(false)`) and
`GameManager.CurrentState == GameState.Lose`. No additional caught events can
fire because `GameManager.TriggerLose()` has an idempotency guard that returns
early if `CurrentState != GameState.Playing`. The Game Over Screen cannot be
shown twice in one session.

---

### 3.7 HUD Interaction

The HUD must hide itself when either the Win Screen or Game Over Screen appears.
This is a new behavior added to the HUD system as a dependency of this GDD.

**New HUD behavior (required in HUD GDD):** `HUDManager` subscribes to
`GameManager.OnWin` and `GameManager.OnLose` in `OnEnable` and unsubscribes in
`OnDisable`. On either event, `HUDManager` immediately begins fading its master
`CanvasGroup.alpha` to 0.0 over `hudWinLoseFadeDuration` (0.2 s, a new
`HUDData` tuning knob). Once at alpha 0, the HUD `CanvasGroup.interactable` is
set to `false`. The HUD does not restore itself after Win or Lose — the level
session is over.

**Timing coordination:** The HUD fade (0.2 s) completes before the Win Screen
fade-in begins (0.5 s delay + 0.4 s fade). At no point are both fully visible
simultaneously. The HUD disappears, then the Win Screen appears.

---

## 4. Formulas

### F-WGO-1: Win Screen Appearance Timing

**Variables:**

| Variable | Type | Default | Range | Description |
|----------|------|---------|-------|-------------|
| `T_trigger` | float | — | — | Time at which `GameManager.TriggerWin()` is called (t = 0) |
| `winAppearDelay` | float | 0.5 s | 0.1–1.5 s | Delay before Win Screen begins fading in |
| `winFadeInDuration` | float | 0.4 s | 0.1–1.0 s | Duration of CanvasGroup alpha lerp from 0 to 1 |
| `hudWinLoseFadeDuration` | float | 0.2 s | 0.05–0.5 s | Duration of HUD master CanvasGroup alpha lerp to 0 |

**Timing sequence from `TriggerWin()` at t = 0:**

```
t = 0.0 s   TriggerWin() fires.
            GameManager.OnWin fires synchronously.
            WinGameOverController.HandleWin() starts Coroutine.
            HUDManager.OnWin handler starts HUD fade (0.2 s).
            PlayerInputHandler.SetInputEnabled(false) called.

t = 0.2 s   HUD CanvasGroup.alpha = 0.0 (fully invisible).
            [HUD is hidden; exit VFX and SFX from Level Exit are playing]

t = 0.5 s   winAppearDelay elapses.
            WinPanel CanvasGroup.alpha begins lerp: 0.0 → 1.0 at 1/0.4 s.

t = 0.9 s   winAppearDelay + winFadeInDuration = 0.9 s.
            WinPanel CanvasGroup.alpha = 1.0.
            WinPanel.interactable = true.
            WinPanel.blocksRaycasts = true.
            EventSystem focus set to Play Again button.
            Player can now click/press Play Again or Main Menu.
```

**Win Screen fully visible at:** `T_trigger + winAppearDelay + winFadeInDuration`

At defaults: `0 + 0.5 + 0.4 = 0.9 seconds`.

**Alpha lerp formula (each frame during fade-in):**

```
t_elapsed = time since fade-in started (0 to winFadeInDuration)
alpha(t) = Lerp(0.0, 1.0, t_elapsed / winFadeInDuration)
         = t_elapsed / winFadeInDuration
         = t_elapsed / 0.4
```

Clamped to [0.0, 1.0].

**Example at t = 0.65 s (0.15 s into the 0.4 s fade-in):**

```
alpha = (0.65 - 0.50) / 0.40 = 0.15 / 0.40 = 0.375
WinPanel is 37.5% visible. Background overlay is at 0.85 * 0.375 = 0.319 opacity.
```

---

### F-WGO-2: Game Over Screen Appearance Timing

**Variables (same structure as Win Screen, different defaults):**

| Variable | Type | Default | Range | Description |
|----------|------|---------|-------|-------------|
| `gameOverAppearDelay` | float | 0.3 s | 0.0–1.0 s | Delay before Game Over Screen begins fading in |
| `gameOverFadeInDuration` | float | 0.4 s | 0.1–1.0 s | Duration of CanvasGroup alpha lerp from 0 to 1 |

**Timing sequence from `TriggerLose()` at t = 0:**

```
t = 0.0 s   TriggerLose() fires.
            GameManager.OnLose fires synchronously.
            WinGameOverController.HandleLose() starts Coroutine.
            HUDManager.OnLose handler starts HUD fade (0.2 s).
            PlayerInputHandler.SetInputEnabled(false) called.

t = 0.2 s   HUD alpha = 0.0.

t = 0.3 s   gameOverAppearDelay elapses.
            GameOverPanel CanvasGroup.alpha begins lerp: 0.0 → 1.0 at 1/0.4 s.

t = 0.7 s   gameOverAppearDelay + gameOverFadeInDuration = 0.7 s.
            GameOverPanel CanvasGroup.alpha = 1.0.
            GameOverPanel.interactable = true.
            EventSystem focus set to Try Again button.
```

**Game Over Screen fully visible at:** `T_trigger + gameOverAppearDelay + gameOverFadeInDuration`

At defaults: `0 + 0.3 + 0.4 = 0.7 seconds`.

---

### F-WGO-3: Stats Row Formatting

```
// Used by both Win Screen and Game Over Screen in Vertical Slice:

string FormatTime(float totalSeconds)
{
    int mm = Mathf.FloorToInt(totalSeconds / 60f);
    int ss = Mathf.FloorToInt(totalSeconds % 60f);
    return $"{mm:00}:{ss:00}";
}

// Input constraints:
//   totalSeconds: [0, 3600]  (max 60-minute session)
//   catchCount:   [0, 99]    (99 is the display cap; actual count may exceed)

// MVP stub output (no LevelStats event received):
statsRowText = "Time: --:--    Caught: --"

// Vertical Slice output (LevelStats event received):
statsRowText = $"Time: {FormatTime(stats.TotalTimeSeconds)}    Caught: {stats.CatchCount}"

// Example: TotalTimeSeconds = 187.4f, CatchCount = 2
// FormatTime(187.4f): mm = floor(187.4/60) = 3, ss = floor(187.4%60) = 7
// Output: "Time: 03:07    Caught: 2"

// Edge case: CatchCount > 99 — display cap: show "99+" instead of the raw number
if (stats.CatchCount > 99) catchDisplay = "99+";
else catchDisplay = stats.CatchCount.ToString();
```

---

## 5. Edge Cases

### EC-WGO-01: TriggerWin() and TriggerLose() Fire Simultaneously

**Scenario:** A hypothetical bug causes both `GameManager.TriggerWin()` and
`GameManager.TriggerLose()` to be called in the same frame.

**Expected behavior:** `TriggerWin()` fires first (it is called by Level Exit
system which runs in `LevelExit.OnInteractComplete`). `GameManager.CurrentState`
transitions to `GameState.Win` after `OnWin` fires. When `TriggerLose()` is
called immediately after, its idempotency guard checks `CurrentState != GameState.Playing`
— this is now true (`CurrentState == GameState.Win`) — and returns early.
`OnLose` is never fired. The Win Screen appears; the Game Over Screen does not.

**Win takes precedence because it is called first.** The Level Exit system fires
`GameManager.TriggerWin()` in `LevelExit.OnInteractComplete`, which is the only
valid Win trigger in MVP. No system other than the Level Exit calls `TriggerWin()`
in MVP. The simultaneous-fire scenario is a theoretical edge case; in practice,
the idempotency guard prevents it.

---

### EC-WGO-02: Scene Reload Fails (Play Again / Try Again)

**Scenario:** `SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)`
throws an exception or fails silently (e.g., scene is not in build settings,
buildIndex = -1).

**Expected behavior:** `WinGameOverController` wraps the LoadScene call in a
validation check:

```csharp
int buildIndex = SceneManager.GetActiveScene().buildIndex;
if (buildIndex < 0)
{
    Debug.LogError("[WinGameOverController] Active scene is not in Build Settings. " +
                   "Cannot reload. buildIndex = " + buildIndex);
    return; // Do not attempt to load
}
SceneManager.LoadScene(buildIndex);
```

The button visually remains as-is (no additional feedback in MVP). The player
can click again; the same error will fire. This is a development-time error —
it must not occur in a properly configured build.

---

### EC-WGO-03: Win Screen Shown While HUD Is Still Visible

**Scenario:** `GameManager.OnWin` fires but the HUD has not yet faded to 0
(e.g., the HUD fade was delayed or interrupted by another Coroutine).

**Expected behavior:** The HUD fade and Win Screen fade are fully independent
Coroutines. There is no blocking wait between them. At the moment the Win Screen
begins its `winAppearDelay` (0.5 s), the HUD has 0.3 s of margin to complete its
0.2 s fade. If the HUD fade is somehow not started (e.g., `HUDManager` never
received `OnWin`), both the HUD and Win Screen may be visible simultaneously
during the fade-in window. This is a dependency failure, not a WinGameOver system
failure. The Win Screen's opaque background (alpha 0.85 → ~1.0 as it fades in)
will visually obscure the HUD before the Win Screen reaches full opacity. At
`winFadeInDuration` = 0.4 s, the Win Screen background reaches 0.85 opacity
before the fade-in is complete, making HUD elements effectively invisible behind
it. The visual result is acceptable for a development error; in production, the
HUD subscription must be correctly wired.

---

### EC-WGO-04: WinGameOverController Receives OnWin Before Awake Completes

**Scenario:** `GameManager.OnWin` fires before `WinGameOverController.OnEnable`
has run (e.g., extremely fast scene load or out-of-order MonoBehaviour
initialization).

**Expected behavior:** `WinGameOverController` subscribes in `OnEnable`, which
runs after `Awake` in Unity's lifecycle. If `OnWin` fires before `OnEnable`,
the event is missed entirely. The Win Screen does not appear.

**Mitigation:** `WinGameOverController.Start` (which runs after all `Awake`
and `OnEnable` calls) checks `GameManager.Instance.CurrentState`:

```csharp
void Start()
{
    if (GameManager.Instance.CurrentState == GameState.Win)
    {
        Debug.LogWarning("[WinGameOverController] OnWin missed — showing Win Screen immediately.");
        HandleWin();
    }
    else if (GameManager.Instance.CurrentState == GameState.Lose)
    {
        Debug.LogWarning("[WinGameOverController] OnLose missed — showing Game Over Screen immediately.");
        HandleLose();
    }
}
```

This catch-up check in `Start` handles the race condition. In normal play,
`GameManager.CurrentState` is `GameState.Playing` throughout the session, so
the check is a no-op.

---

### EC-WGO-05: Player Input Not Disabled Before Win Screen

**Scenario:** `PlayerInputHandler.SetInputEnabled(false)` is not called (e.g.,
`PlayerInputHandler.Instance` is null because the player GameObject was
destroyed).

**Expected behavior:** The player could theoretically continue moving or
interacting while the Win Screen fades in. However, `GameManager.CurrentState == GameState.Win`
blocks all interaction systems that check state before dispatching (the Player
Interaction System, Level Exit, etc.). The player character may still move
visually, but no further gameplay events can fire. The Win Screen's
`blocksRaycasts = true` prevents button mis-clicks during the fade-in. Log a
`Debug.LogWarning` if `PlayerInputHandler.Instance` is null when `HandleWin()`
runs, but do not stop the Win Screen sequence.

---

### EC-WGO-06: TriggerWin() Called During Respawn Sequence

**Scenario:** The Level Exit fires `TriggerWin()` while a Checkpoint respawn
is in progress (extremely unlikely — the Level Exit requires `GameState.Playing`
and a completed hold interaction while `_respawnInProgress` is true on
`CheckpointManager`).

**Expected behavior:** `LevelExit.CanInteract` returns false during respawn
because `GameManager.CurrentState != GameState.Playing` during the respawn
sequence — `CurrentState` is still `GameState.Playing` during respawn
(the Checkpoint system does not change `CurrentState`). However,
`PlayerInputHandler.SetInputEnabled(false)` is called at step 4 of the respawn
sequence, which prevents the player from triggering any interaction. The hold
interaction cannot complete while input is disabled. Therefore, this scenario
cannot occur in normal play. If it occurs via a test harness: the Win Screen
appears and the respawn Coroutine completes in the background (the scene reload
from "Play Again" cancels all outstanding Coroutines).

---

### EC-WGO-07: Game Over Screen Activated Unexpectedly in MVP

**Scenario:** A test harness, editor script, or future system accidentally calls
`GameManager.TriggerLose()` during MVP play.

**Expected behavior:** The Game Over Screen appears normally. It is fully
scaffolded. "Try Again" reloads the scene. The system produces no errors. This
is correct behavior — the Game Over Screen is functional, just not triggered by
any MVP gameplay system.

---

### EC-WGO-08: CatchCount Overflow in Stats Row

**Scenario:** A player dies 100+ times in a single session. `LevelStats.CatchCount`
exceeds 99.

**Expected behavior:** The stats row displays "Caught: 99+" instead of the raw
integer. See F-WGO-3 for the display-cap formula. The underlying `int` value in
`LevelStats` is uncapped — only the display is capped. No integer overflow risk
within a single session.

---

### EC-WGO-09: Win Screen Appears During Phase 1 (Future-Proofing)

**Scenario:** A future level type or debug shortcut triggers `TriggerWin()`
before Phase 2 starts (e.g., a no-objective test level where the exit is
immediately unlocked).

**Expected behavior:** The Win Screen appears with the same layout. The stats
row (in Vertical Slice) would show a very short time and 0 catches — both valid
values. The "ESCAPED" headline and "Play Again" button are correct for any win
condition. This system has no dependency on the current `LevelPhase` — it
responds only to `GameManager.CurrentState`.

---

### EC-WGO-10: Win Screen Displayed With No Level Name

**Scenario:** `LevelConfig.DisplayName` is empty or whitespace AND the Unity
scene name is also empty (hypothetical edge case in stripped builds).

**Expected behavior:** The level name TextMeshPro element displays a single
non-breaking space (`"\u00A0"`) instead of an empty string, preventing layout
collapse. The `VerticalLayoutGroup` needs at least an invisible line to maintain
spacing. A `Debug.LogWarning` is emitted. In production, every scene must have a
non-empty name and a `LevelConfig` with a non-empty `DisplayName`.

---

## 6. Dependencies

### 6.1 Systems This Depends On (Upstream)

| System | Direction | Event / Property | Notes |
|--------|-----------|-----------------|-------|
| GameManager | Subscribes | `static event Action OnWin` | NEW CONTRACT — defined by this GDD; must be added to GameManager. Fires inside TriggerWin() before state changes. |
| GameManager | Subscribes | `static event Action OnLose` | NEW CONTRACT — defined by this GDD; must be added to GameManager. Fires inside TriggerLose() before state changes. |
| GameManager | Reads | `GameState CurrentState` | Read in Start() as a catch-up check (EC-WGO-04). Read in TriggerWin/TriggerLose idempotency guards. |
| Level Exit System | Indirect | Calls TriggerWin() → fires OnWin | WinUI subscribes to OnWin, NOT to LevelExit.OnExitUsed. Decoupled by design. |
| PlayerInputHandler | Calls | `SetInputEnabled(false)` | Called on HandleWin/HandleLose. Disables all player input for the win/lose session termination. |
| SceneManager (Unity) | Calls | `LoadScene(buildIndex)` | Used by Play Again / Try Again buttons. Validation check in EC-WGO-02. |
| LevelConfig | Reads | `string DisplayName` | Read in Awake. Falls back to SceneManager.GetActiveScene().name. |
| Level Timer + Stats (#17) | Subscribes (stub) | `static event Action<LevelStats> OnStatsReady` | STUB CONTRACT — defined by this GDD; implemented by Level Timer + Stats in Vertical Slice. Stub text shown in MVP. |
| EventSystem (Unity) | Calls | `SetSelectedGameObject(button)` | Called when Win/Game Over panel becomes fully visible. Sets keyboard/controller initial focus. |

### 6.2 Systems That Depend On This (Downstream)

| System | Direction | Dependency | Notes |
|--------|-----------|------------|-------|
| None | — | — | The Win/Game Over screens are pure consumers. No gameplay system reads from them. |

### 6.3 Bidirectional Dependencies — Required Updates to Upstream GDDs

The following upstream GDDs must be updated to reference this system:

**GameManager** (no dedicated GDD; requirements documented here as implementation contracts):
- Add `public static event Action OnWin` to `GameManager.cs`.
- Add `public static event Action OnLose` to `GameManager.cs`.
- Update `TriggerWin()` to fire `OnWin` before state transition.
- Update `TriggerLose()` to fire `OnLose` before state transition.
- Both events require an idempotency guard (`if (CurrentState != GameState.Playing) return;`).

**HUD GDD (design/gdd/hud.md)** — add the following to Section 6 (Dependencies) and
Section 3.9 (HUD Master State):
- `HUDManager` subscribes to `GameManager.OnWin` and `GameManager.OnLose` in `OnEnable`.
- On either event, fade master `CanvasGroup.alpha` to 0 over `hudWinLoseFadeDuration`
  (0.2 s, a new `HUDData` tuning knob, category: Feel).
- Set `CanvasGroup.interactable = false` after fade completes.
- The HUD does NOT restore after Win or Lose — the level session is terminal.

**Level Timer + Stats GDD (not yet written — Vertical Slice scope):**
- Must implement `static event Action<LevelStats> OnStatsReady`.
- Must fire this event synchronously after `GameManager.TriggerWin()` is called
  (i.e., subscribe to `GameManager.OnWin` with higher priority than `WinGameOverController`,
  or fire the event from `LevelExit.OnExitUsed` before `TriggerWin()` is called — this
  sequencing must be confirmed when the Level Timer + Stats GDD is written).
- `LevelStats.TotalTimeSeconds` and `LevelStats.CatchCount` are the minimum required fields.

---

## 7. Tuning Knobs

All values are authored in a `WinGameOverData` ScriptableObject at
`Assets/_Project/Scripts/Data/WinGameOverData.asset`. No values are hardcoded
in `WinGameOverController` or in either panel's MonoBehaviour.

| Knob | Category | Default | Safe Range | Effect of Increase | Effect of Decrease |
|------|----------|---------|------------|-------------------|--------------------|
| `winAppearDelay` | Gate | 0.5 s | 0.1–1.5 s | Win Screen appears later — more time for exit VFX to play before overlay arrives | Win Screen arrives sooner — may collide with exit VFX; exhale moment is compressed |
| `winFadeInDuration` | Feel | 0.4 s | 0.1–1.0 s | Win Screen fades in more slowly — more cinematic, but player waits longer for buttons to become interactive | Win Screen snaps in quickly — more responsive, less ceremonial |
| `gameOverAppearDelay` | Gate | 0.3 s | 0.0–1.0 s | Game Over Screen is delayed more — more breathing room after a loss event | Game Over Screen appears immediately — can feel abrupt if loss event has accompanying audio |
| `gameOverFadeInDuration` | Feel | 0.4 s | 0.1–1.0 s | Game Over Screen fades in slowly | Game Over Screen snaps in |
| `winPanelBackgroundAlpha` | Feel | 0.85 | 0.6–1.0 | Background is more opaque — gameplay world fully obscured | Background is more transparent — scene geometry visible behind panel; can feel unpolished |
| `gameOverPanelBackgroundAlpha` | Feel | 0.92 | 0.7–1.0 | Background is more opaque — heavier visual weight, appropriate for failure state | Lighter background — less visual separation from gameplay; may reduce impact |
| `hudWinLoseFadeDuration` | Feel | 0.2 s | 0.05–0.5 s | HUD takes longer to disappear on win/lose — overlap window with Win Screen increases | HUD disappears faster — less risk of HUD elements visible against incoming Win Screen |
| `winHeadlineFontSize` | Feel | 64 px | 40–96 px | Larger headline — more immediate legibility; may clip on very small screens | Smaller headline — less dominant; may reduce the "immediate comprehension within 0.5 s" requirement |
| `buttonMinWidth` | Feel | 160 px | 120–280 px | Larger buttons — easier to click; more screen real estate used | Smaller buttons — harder to click, especially on touch targets |
| `buttonMinHeight` | Feel | 48 px | 36–72 px | Taller buttons — easier to hit; touch-target best practice (min 44 px recommended by Apple HIG) | Shorter buttons — may fail touch-target accessibility guidelines |

**Accessibility constraint (not a tuning knob):**
`buttonMinHeight` must not be set below 44 px in any production build. 44 px is the
minimum touch target recommended by Apple HIG and Android Material Design guidelines.
Even though UNSEEN is initially PC-only, maintaining this minimum preserves future
mobile compatibility.

---

## 8. Acceptance Criteria

All criteria are testable by a QA tester with access to the Unity Game view
and the Unity Test Runner. "PlayMode" tests require a scene with a configured
`WinGameOverCanvas` prefab, a `GameManager`, and a `PlayerInputHandler`.

| # | Criterion | Test Method | Pass | Fail |
|---|-----------|-------------|------|------|
| AC-WGO-01 | Win Screen appears after `GameManager.TriggerWin()` is called. | PlayMode: call `GameManager.Instance.TriggerWin()` via test harness. Observe canvas. | `WinPanel.CanvasGroup.alpha` reaches 1.0 within `winAppearDelay + winFadeInDuration + 0.1 s` margin (default: 1.0 s). | Panel remains invisible; alpha stays at 0. |
| AC-WGO-02 | Win Screen headline reads "ESCAPED" (not "YOU WIN" or any other string). | PlayMode: observe Win Screen headline TextMeshPro text content after TriggerWin(). | Text content is exactly "ESCAPED". | Any other text content. |
| AC-WGO-03 | Win Screen level name displays the scene or LevelConfig name (non-empty). | PlayMode: trigger Win Screen. Read level name TextMeshPro text. | Non-empty string matching either LevelConfig.DisplayName or SceneManager.GetActiveScene().name. | Empty string or whitespace; or literal "null". |
| AC-WGO-04 | Stats row displays placeholder text "Time: --:--    Caught: --" in MVP (no LevelStats event fired). | PlayMode (MVP): trigger Win Screen without firing `OnStatsReady`. Read stats row text. | Exact text: "Time: --:--    Caught: --". | Any other text; empty string; or a filled stat value. |
| AC-WGO-05 | "Play Again" button reloads the active scene when clicked. | PlayMode: trigger Win Screen; click Play Again; observe scene reload. | Scene reloads (scene name in top bar resets; no persistent state from previous session). | No scene reload; exception logged; button non-interactive. |
| AC-WGO-06 | "Main Menu" button is non-interactive (stub) in MVP. | PlayMode: trigger Win Screen; attempt to click Main Menu button. | Button click produces no scene change. `Button.interactable == false`. | Button navigates to a scene; or logs an unhandled exception. |
| AC-WGO-07 | HUD is fully invisible (alpha = 0) before Win Screen begins fading in. | PlayMode: trigger TriggerWin(), observe HUD CanvasGroup.alpha at t = winAppearDelay (0.5 s). | `HUDManager.masterCanvasGroup.alpha == 0` at t = 0.5 s. | HUD still visible (alpha > 0) when Win Screen fade begins. |
| AC-WGO-08 | Player input is disabled when Win Screen appears (player character cannot move). | PlayMode: trigger Win Screen; attempt Move input. | Character does not move. `PlayerInputHandler.IsEnabled == false`. | Character moves while Win Screen is displayed. |
| AC-WGO-09 | Win Screen buttons are keyboard-navigable (Tab cycles, Enter confirms). | PlayMode: trigger Win Screen; press Tab; press Enter. | Tab moves focus from Play Again to Main Menu (or vice versa). Enter triggers the focused button's onClick. | Tab does not change focus; Enter has no effect. |
| AC-WGO-10 | Game Over Screen does not appear during normal MVP play (no TriggerLose() call). | PlayMode: complete a full level session (get caught multiple times, respawn, then exit). | `GameOverPanel.CanvasGroup.alpha == 0` throughout the session. Win Screen appears on exit. | Game Over Screen visible at any point during normal MVP play. |
| AC-WGO-11 | Game Over Screen appears when `GameManager.TriggerLose()` is called directly via test harness. | PlayMode: call `GameManager.Instance.TriggerLose()` via test harness. | `GameOverPanel.CanvasGroup.alpha` reaches 1.0 within `gameOverAppearDelay + gameOverFadeInDuration + 0.1 s` (default: 0.8 s). No error thrown. | Panel remains invisible; alpha stays at 0; exception logged. |
| AC-WGO-12 | Game Over Screen headline reads "CAPTURED". | PlayMode: trigger TriggerLose(). Read headline text. | Text content is exactly "CAPTURED". | Any other text. |
| AC-WGO-13 | "Try Again" button on Game Over Screen reloads the active scene. | PlayMode: trigger TriggerLose(); click Try Again. | Scene reloads. | No scene reload; exception. |
| AC-WGO-14 | If TriggerWin() and TriggerLose() are both called in the same frame (test harness), only the Win Screen appears. | PlayMode test harness: call TriggerWin() then TriggerLose() in the same Update(). | WinPanel visible; GameOverPanel invisible. No error. | Both panels visible; or only GameOver visible; or error thrown. |
| AC-WGO-15 | Win Screen is not shown during a respawn sequence (Checkpoint system active). | PlayMode: trigger a catch, observe during respawn. | Win Screen does not appear. GameState remains Playing throughout respawn. | Win Screen flashes during respawn. |
| AC-WGO-16 | `WinGameOverController.Start()` catch-up check detects a missed OnWin event and shows the Win Screen if `CurrentState == GameState.Win` at Start time. | PlayMode test harness: set GameManager.CurrentState = Win before WinGameOverController.OnEnable fires (via script execution order manipulation). | Win Screen appears within one frame of Start(). Debug.LogWarning emitted. | Win Screen does not appear; no warning. |
| AC-WGO-17 | Win Screen canvas sortingOrder (20) renders above HUD canvas (sortingOrder 10) and RespawnUI canvas (sortingOrder 15). | PlayMode: inspect Canvas sortingOrder values in Scene hierarchy. | WinGameOverCanvas.sortingOrder == 20. HUDCanvas.sortingOrder < 20. RespawnUI canvas sortingOrder < 20. | Any other canvas with sortingOrder >= 20 visible simultaneously; or Win Screen rendered behind HUD. |
| AC-WGO-18 | Stats row format in Vertical Slice: "Time: 03:07    Caught: 2" for TotalTimeSeconds = 187.4f and CatchCount = 2. | EditMode unit test: call `WinGameOverController.FormatStatsRow(new LevelStats { TotalTimeSeconds = 187.4f, CatchCount = 2 })`. | Returns "Time: 03:07    Caught: 2". | Wrong time format; wrong catch count; wrong separator string. |
| AC-WGO-19 | Stats row displays "Caught: 99+" when CatchCount > 99. | EditMode unit test: call with CatchCount = 150. | Returns "Caught: 99+" in the stats row text. | Displays "Caught: 150" or throws OverflowException. |
| AC-WGO-20 | `WinGameOverController` produces no heap allocation (GC alloc) per frame during steady-state gameplay (before any win/lose event fires). | Profiler session: play 2 minutes of a level with no win/lose event. Record GC alloc for WinGameOverController. | 0 B/frame GC alloc from WinGameOverController during steady state. | Any per-frame alloc from this component during idle. |

---

*End of Win / Game Over Screens GDD.*
