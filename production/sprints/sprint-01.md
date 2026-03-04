# Sprint 01 — Playable Vertical Slice

**Goal:** Get to a single playable loop in Level_1 — player can move, hide, be detected, win, or lose.
**Start:** 2026-03-04
**Target:** 2026-03-18 (2 weeks)

---

## Sprint Goal

A seeker AI patrols Level_1. The player can move, crouch, sprint, hide in at least one interactable spot, and trigger a win or lose state that displays the correct UI screen. The loop should feel like the game.

---

## Backlog

### P1 — Blockers (must ship this sprint)

| # | Task | Owner | Status |
|---|------|-------|--------|
| 1.1 | Commit and stabilize Level_1.unity (place hiding spots, bake NavMesh) | | pending |
| 1.2 | Implement at least one concrete `IInteractable` — a `Locker` or `Wardrobe` that the player can enter/exit | | pending |
| 1.3 | Connect WinConditionEvaluator and LoseConditionEvaluator to WinUI / GameOverUI | | pending |
| 1.4 | Wire minimum audio: footsteps (walk/crouch/sprint), detection alert sting | | pending |

### P2 — Quality (target this sprint, not blocking)

| # | Task | Owner | Status |
|---|------|-------|--------|
| 2.1 | Reverse-document: Detection System GDD (`design/gdd/detection-system.md`) | | pending |
| 2.2 | Reverse-document: Enemy AI GDD (`design/gdd/enemy-ai.md`) | | pending |
| 2.3 | Reverse-document: Game Loop GDD (`design/gdd/game-loop.md`) | | pending |
| 2.4 | EditMode tests: SuspicionMeter thresholds and WinConditionEvaluator logic | | pending |

### P3 — Stretch (next sprint if not reached)

| # | Task | Owner | Status |
|---|------|-------|--------|
| 3.1 | Implement `DistractableNoiseMaker` interactable (throwable that creates a noise event) | | pending |
| 3.2 | Add `?` / `!` icons above enemy head on suspicion state changes | | pending |
| 3.3 | Formalize `interactable-items.md` into a proper GDD with 8 required sections | | pending |
| 3.4 | Implement Light-Based Detection (Exposure Meter) from `puzzles-and-mechanics.md` | | pending |

---

## Key Design Decisions Pending

- **Currency/Gems**: `interactable-items.md` mentions currency. Is this in scope for Sprint 01 or deferred? Enemy seeker is currently AI-only — no loot pickup loop exists.
- **Peek mechanic**: While hiding, can player camera peek? Needs decision before Locker implementation.
- **Surface-based noise**: `puzzles-and-mechanics.md` specifies carpet vs. puddle noise variation. NoiseEmitter event bus exists — does Sprint 01 implement surface material modifiers?

---

## Definition of Done (Sprint 01)

- [ ] Level_1 is committed and has: floor, walls, at least 2 hiding spots, at least 1 enemy patrol route, NavMesh baked
- [ ] Player can enter/exit at least one hiding spot via Interact input
- [ ] Enemy AI detects the player, transitions to Chase, and triggers LoseConditionEvaluator
- [ ] Win condition is reachable (e.g., survive until timer expires)
- [ ] WinUI and GameOverUI both display correctly from gameplay
- [ ] No null ref exceptions in a full play session
