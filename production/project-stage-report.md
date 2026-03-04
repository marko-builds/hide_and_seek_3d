# Project Stage Analysis

**Date:** 2026-03-04
**Stage:** Early Production
**Engine:** Unity 6 (6000.3.10f1) + URP 17.3.0

---

## Summary

The project has a substantial, well-structured codebase covering all major gameplay systems — player, enemy AI, detection, hiding, game loop, UI, and audio — but zero design documentation, zero tests, and no playable end-to-end loop yet. Work to date has been infrastructure-first: the plumbing exists, but it has not been connected into a shippable experience.

---

## Completeness Overview

| Domain | Score | Notes |
|--------|-------|-------|
| Engine & Tooling | 95% | Unity 6 + URP configured, Input System, NavMesh, Test Runner, LevelBuilder suite |
| Source Code | 60% | Core systems coded; interactables not implemented, audio unconnected |
| Design Documentation | 10% | 2 brainstorm docs in `design/`; no formal GDDs, no systems index |
| Architecture | 0% | No ADRs in `docs/architecture/` |
| Tests | 0% | Test Runner installed; no test files written |
| Level Content | 5% | Level_1.unity exists but uncommitted and minimal |
| Production Tracking | 5% | Sprint 01 now created; no milestones |

---

## Implemented Systems

### Player (`Assets/_Project/Scripts/Player/`)
- **PlayerController** — coordinator, wires all player sub-components
- **PlayerInputHandler** — sole consumer of InputSystem_Actions; broadcasts C# events
- **PlayerMovement** — Rigidbody-based locomotion with walk/sprint/crouch
- **PlayerHiding** — enter/exit hiding state; listens for IHideable spots
- **PlayerInteraction** — holds-to-interact via InputSystem Interact action
- **PlayerNoiseEmitter** — emits NoiseEvents based on movement speed and surface

### Enemy AI (`Assets/_Project/Scripts/AI/Enemy/`)
- **EnemyController** — coordinates detection, navigation, state machine
- **EnemyDetection** — wraps FieldOfView + SuspicionMeter; fires OnPlayerDetected
- **EnemyNavigation** — NavMeshAgent wrapper with patrol, investigate, chase modes
- **State Machine**: Idle → Patrol → Investigate → Chase → Search (all 5 states implemented)

### Detection (`Assets/_Project/Scripts/Detection/`)
- **LineOfSightChecker** — static raycast utility, non-allocating
- **FieldOfView** — angle + distance cone check
- **SuspicionMeter** — fills on detection events, drains on timeout
- **DetectionProfile** — `[Serializable]` config owned by PlayerController (visibility, noise modifiers)
- **NoiseEmitter** — static event bus (`Action<NoiseEvent>`)
- **NoiseListener** — filters events by radius; drives EnemyDetection suspicion

### Game Loop (`Assets/_Project/Scripts/GameLoop/`)
- **GameManager** — Singleton; manages round state
- **RoundTimer** — countdown; fires OnTimerExpired
- **WinConditionEvaluator** — evaluates win state (survive timer, collect items)
- **LoseConditionEvaluator** — evaluates lose state (player caught)

### UI (`Assets/_Project/Scripts/UI/`)
- **HUDManager** — coordinates HUD elements
- **SuspicionMeterUI**, **TimerUI**, **NoiseIndicatorUI** — all implemented, event-driven
- **MainMenuUI**, **PauseMenuUI**, **GameOverUI**, **WinUI** — all implemented

### Audio (`Assets/_Project/Scripts/Audio/`)
- **AudioManager** — Singleton + ObjectPool<SoundEmitter>; plays SoundData assets
- **MusicController** — background music transitions
- **SoundEmitter** — pooled; implements IPoolable
- *Note: SoundLibrary and SoundData exist; no audio clips are wired yet*

### Infrastructure (`Assets/_Project/Scripts/Infrastructure/`)
- Interfaces: `IState`, `IHideable`, `IDetectable`, `IInteractable`, `INoiseMaker`, `IPoolable`
- `StateMachine<T>` + `BaseState<T>` — generic, used by EnemyController

### Editor Tooling (`Assets/_Project/Scripts/Editor/LevelBuilder/`)
- Surface drop, bounds snap, bounds align, distribute, scatter, overlap checker, component audit, prefab palette

### Data (ScriptableObjects)
- `PlayerData`, `EnemyData`, `HidingSpotData`, `GameRulesData`, `SoundData`, `SoundLibrary`

---

## Gaps & Status

### Critical Gaps (blocking playable loop)

**1. No concrete IInteractable implementations**
`InteractableBase` (abstract) exists. `Interaction/props/` is empty. The player cannot enter a hiding spot via interaction — PlayerHiding and PlayerInteraction have no concrete target to call. A `Locker` or `Wardrobe` MonoBehaviour implementing both `IInteractable` and `IHideable` is the single highest-priority gap.

**2. Win/Lose not connected to UI**
WinConditionEvaluator and LoseConditionEvaluator are coded but not confirmed to fire WinUI/GameOverUI. No GameManager event wires them. Needs a connection layer.

**3. Audio silent**
AudioManager and SoundEmitter work but SoundLibrary has no clips assigned. The game is completely silent. Minimum viable: footstep sounds + detection sting.

**4. Level_1 uncommitted and minimal**
The scene exists but has uncommitted changes (Packages, TagManager also modified). No confirmed hiding spots placed, NavMesh not baked, patrol path not configured for the enemy.

### Documentation Gaps

**5. No formal GDDs**
Two brainstorm docs exist in `design/`:
- `interactable-items.md` — covers vents, peek mechanics, moveable cover, fuse boxes, noise traps, utility items (Polarized Lens, Magnet, Grappling Hook, Smoke Bombs). Ideas are solid; not in GDD format (missing: Player Fantasy, Formulas, Edge Cases, Acceptance Criteria).
- `puzzles-and-mechanics.md` — covers Noise Radius system (matches existing NoiseEmitter), Light-Based Detection (Exposure Meter), Temperature/Scent Trails, 3 specific puzzle designs (Blind Musician, Mirror Maze, Greedy Guard), Ghost mechanic. Several map directly to existing code.

**Recommended reverse-document targets (code → GDD):**
- `design/gdd/detection-system.md` — SuspicionMeter + FieldOfView + NoiseSystem; formulas already exist in code
- `design/gdd/enemy-ai.md` — state machine with 5 states + transitions
- `design/gdd/game-loop.md` — win/lose conditions, round timer, game states
- `design/gdd/interactable-items.md` — formalize the existing brainstorm doc

**6. No Architecture Decision Records**
Key decisions made without ADRs: Rigidbody vs CharacterController (reversed once already), static NoiseEmitter bus vs instance-based, SceneSingleton vs Singleton for HidingSpotRegistry, NavMesh vs custom pathfinding. These should be captured retroactively.

**7. No tests**
SuspicionMeter, WinConditionEvaluator, LoseConditionEvaluator, and NoiseListener are all logic-heavy and highly testable with EditMode tests. No tests written.

### Configuration Gaps

**8. `technical-preferences.md` not filled in**
All fields still show `[TO BE CONFIGURED]` despite the project being active for weeks. Naming conventions, performance budgets, and forbidden patterns should be filled in to guide future agents.

**9. CLAUDE.md Project Overview outdated**
States "only the utility layer and project scaffolding exist so far; gameplay scripts have not been written yet." This was accurate at project start but is now significantly wrong and will mislead agents in new sessions.

---

## Design Doc Analysis

### `design/interactable-items.md`
**Strength:** Comprehensive brainstorm covering 4 categories (stealth navigation, environmental puzzles, AI manipulation, utility items). Good scope.
**Gap:** Not a formal GDD. Missing: Player Fantasy, Detailed Rules, Formulas, Edge Cases, Tuning Knobs, Acceptance Criteria.
**Alignment with code:** `IInteractable` interface and `InteractableBase` are ready for implementation. `NoiseEmitter` bus already supports remote noise makers and squeaky floor mechanics.

### `design/puzzles-and-mechanics.md`
**Strength:** 3 specific puzzle designs (Blind Musician, Mirror Maze, Greedy Guard) with clear setup/goal/solution structure. Ghost mechanic is high-concept but well-defined.
**Alignment with code:**
- Noise Radius System → maps directly to `NoiseEmitter` + `NoiseListener` (already built)
- Light-Based Detection → not yet implemented; would require a new `ExposureMeter` component
- Scent Trails → not implemented; new AI sensing mode
- Surface noise modifiers → NoiseEvent struct could carry surface type; NoiseEmitter bus is ready for it
**Flag:** The "Greedy Guard" puzzle requires currency/gem pickups. No currency system exists in code. This is a scope decision.

---

## Recommended Next Steps

### Immediate (this session or next)
1. **Fix CLAUDE.md Project Overview** — update to reflect current state ✅ *(Godot→Unity reference fixed)*
2. **Commit Level_1** — stabilize the scene and commit the working state
3. **Implement `Locker` interactable** — first concrete `IInteractable` + `IHideable`; unblocks the core loop

### Sprint 01 (see `production/sprints/sprint-01.md`)
- Connect Win/Lose evaluators to UI
- Wire minimum audio (footsteps + detection alert)
- Reverse-document: detection, enemy AI, game loop GDDs
- Write EditMode tests for SuspicionMeter and win/lose logic

### Sprint 02 (planned)
- Implement DistractableNoiseMaker (throwable)
- `?` / `!` icons on enemy state change (telegraphing, per `puzzles-and-mechanics.md`)
- Formalize `interactable-items.md` into proper GDD
- Light-Based Detection (Exposure Meter) — new system
- Surface-based noise modifiers (carpet/puddle)

### Backlog (future)
- Currency/Gem system (required for Greedy Guard puzzle)
- Moveable cover (crates, trolleys)
- Scent Trail system (new AI sensing mode)
- Ghost mechanic (Phantom Projection)
- Second level

---

## Risk Assessment

| Risk | Severity | Notes |
|------|----------|-------|
| No playable loop yet | HIGH | Core systems coded but not integrated end-to-end |
| Zero tests | MEDIUM | Formula-heavy systems (SuspicionMeter) untested; regressions will be silent |
| Design scope creep risk | MEDIUM | `puzzles-and-mechanics.md` introduces currency, scent, light systems — each is a significant feature |
| Audio completely absent | MEDIUM | Will make playtesting feel broken even if mechanics work |
| CLAUDE.md Project Overview outdated | LOW | Misleads new agent sessions; easy to fix |

---

*Generated by `/project-stage-detect` on 2026-03-04*
