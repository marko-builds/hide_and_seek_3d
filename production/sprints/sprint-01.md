# Sprint 01 — Playable Vertical Slice

**Goal:** Get to a single playable loop in Level_1 — player can move, hide, be detected, win, or lose.
**Start:** 2026-03-04
**Target:** 2026-03-18 (2 weeks)
**Day:** 3 of 14 — Design phase complete. Now: implementation.

---

## Design Status

All 15 MVP system GDDs are **approved** as of 2026-03-05. Sprint has transitioned
from design+implement to pure implementation. Refer to each GDD for exact acceptance
criteria before marking any implementation task done.

---

## Sprint Goal

A seeker AI patrols Level_1. The player can move, crouch, sprint, collect an
objective token, hide in at least one interactable spot, reach the level exit
after collection, and trigger a win or lose state that displays the correct UI
screen. The loop should feel like the game.

---

## Resolved Design Decisions

| Decision | Resolution | Source |
|----------|------------|--------|
| Peek mechanic while hiding | ✅ Resolved — peeking raises detection floor modifier; peek input defined in hiding-spot-system.md §3 | design/gdd/hiding-spot-system.md |
| Surface-based noise modifiers | ✅ Resolved — surface multipliers defined; implement in Sprint 01 (P2) | design/gdd/player-noise-emitter.md, design/gdd/sound-propagation-model.md |
| Currency / Gems | ✅ Deferred — not in MVP. Objective System uses relic tokens only; no loot/economy loop | design/gdd/objective-system.md |

---

## Backlog

### P1 — Blockers (must ship this sprint)

Ordered by implementation dependency. Complete in this sequence.

| # | Task | Owner | Status | GDD Reference |
|---|------|-------|--------|---------------|
| 1.1 | Implement SuspicionMeter and basic LineOfSight detection (LoS only — no light/sound modifier for first pass) | | ✅ done | design/gdd/detection-system.md |
| 1.2 | Implement EnemyController: NavMesh patrol → Chase state machine; EnemyDetection drives SuspicionMeter | | ✅ done | design/gdd/seeker-ai.md |
| 1.3 | Implement ObjectiveToken + ObjectiveRegistry; place one token in Level_1 | | ✅ done (scripts only — scene placement in 1.8) | design/gdd/objective-system.md |
| 1.4 | Implement LevelPhaseManager + EscalationProfile; wire ObjectiveRegistry.OnAllObjectivesCollected → Phase 2 | | ✅ done | design/gdd/two-phase-level-structure.md |
| 1.5 | Implement LevelExitSystem; place exit trigger in Level_1; wire to OnAllObjectivesCollected unlock | | ✅ done (scripts only — scene placement in 1.8) | design/gdd/level-exit-system.md |
| 1.6 | Implement Wardrobe — the concrete IInteractable HidingSpot; place at least two in Level_1 | | pending | design/gdd/hiding-spot-system.md |
| 1.7 | Connect WinConditionEvaluator → WinUI and LoseConditionEvaluator → GameOverUI | | ✅ done | design/gdd/win-game-over-screens.md |
| 1.8 | Commit and stabilize Level_1.unity: floor, walls, 2 hiding spots, 1 objective token, 1 enemy patrol route, 1 exit trigger, NavMesh baked | | pending | — |
| 1.9 | Wire minimum audio: footsteps (walk/crouch/sprint surfaces), detection alert sting | | pending | design/gdd/footstep-audio.md |

### P2 — Quality (target this sprint, not blocking)

| # | Task | Owner | Status | Notes |
|---|------|-------|--------|-------|
| 2.1 | ~~Reverse-document Detection System GDD~~ | | ✅ done | design/gdd/detection-system.md approved 2026-03-05 |
| 2.2 | ~~Reverse-document Enemy AI GDD~~ | | ✅ done | design/gdd/seeker-ai.md approved 2026-03-05 |
| 2.3 | ~~Reverse-document Game Loop GDD~~ | | ✅ done | Covered by two-phase-level-structure.md + checkpoint-system.md |
| 2.4 | EditMode tests: SuspicionMeter threshold transitions (Unaware → Alert → Chase) | | pending | Run after 1.1 complete |
| 2.5 | EditMode tests: WinConditionEvaluator and LoseConditionEvaluator logic | | pending | Run after 1.7 complete |
| 2.6 | Implement NoiseEmitter surface multipliers in PlayerNoiseEmitter | | pending | design/gdd/player-noise-emitter.md |
| 2.7 | HUD: wire SuspicionMeterUI and objective counter (CollectedCount / TotalCount) | | pending | design/gdd/hud.md |

### P3 — Stretch (next sprint if not reached)

| # | Task | Owner | Status | Notes |
|---|------|-------|--------|-------|
| 3.1 | Implement ThrowableObject (DistractableNoiseMaker) — throw-to-distract interactable | | pending | GDD approved; design/gdd/throwable-object.md |
| 3.2 | Add `?` / `!` icons above enemy head on suspicion state changes | | pending | design/gdd/seeker-ai.md §visual-feedback |
| 3.3 | ~~Formalize interactable-items.md into a proper GDD~~ | | ✅ done | Superseded by throwable-object.md |
| 3.4 | Implement LightSourceSystem (LightZone exposure tracking) | | pending | GDD approved; design/gdd/light-source-system.md |
| 3.5 | Implement CheckpointSystem respawn (LoseConditionEvaluator respawns at last checkpoint) | | pending | design/gdd/checkpoint-system.md |

---

## Definition of Done (Sprint 01)

- [ ] Level_1 is committed and has: floor, walls, at least 2 hiding spots, at least 1 ObjectiveToken, 1 enemy patrol route, 1 LevelExit trigger, NavMesh baked
- [ ] Player can enter/exit at least one Wardrobe hiding spot via Interact (hold) input
- [ ] Enemy AI patrols, detects the player via LoS, transitions to Chase, and triggers LoseConditionEvaluator
- [ ] Player can collect the ObjectiveToken (tap interact), triggering Phase 2 escalation and seeker speed increase
- [ ] Level Exit becomes interactable only after all tokens collected; reaching it triggers WinConditionEvaluator
- [ ] WinUI and GameOverUI both display correctly from gameplay
- [ ] SuspicionMeter has at least one EditMode test covering Unaware → Alert → Chase transitions
- [ ] No null reference exceptions in a full play session (collect token → reach exit → Win screen)
