# Systems Index: UNSEEN

> **Status**: Approved **Created**: 2026-03-04 **Last Updated**: 2026-03-04 **Source Concept**: design/gdd/game-concept.md


## Overview

UNSEEN is a single-player 3D stealth puzzle game built around two interacting design axes: hand-crafted levels (each room has a discoverable logic) and systemic depth (light, sound, and AI behavior are simulation-quality). The mechanical scope is therefore dominated by detection infrastructure — everything from the Sound Propagation Model up through the Seeker AI depends on a stable, legible detection core.

The core loop (Observe → Plan → Move → React) requires eight MVP systems to function: a detection simulation, a player noise emitter, a seeker AI, a hiding spot mechanic, an objective to collect, a level exit to reach, a checkpoint to respawn at, and enough UI to keep the player informed. All other systems — gadgets, puzzle gates, adaptive audio, progression — layer on top once that core is proven fun.

The four pillars (The Room Has Rules / Silence Is a Tool / Legible Jeopardy / Two-Beat Tension) constrain every design decision. Detection legibility is load-bearing for Pillar 3, making the Detection System the single highest-risk bottleneck in the entire project.


## Systems Enumeration

### Core

| \# | System Name | Category | Priority | Status | Design Doc | Depends On |
| - | - | - | - | - | - | - |
| 1 | Sound Propagation Model | Core | MVP | Approved | design/gdd/sound-propagation-model.md | — |
| 2 | Light Source System | Core | MVP | Approved | design/gdd/light-source-system.md | — |
| 3 | Detection System | Core | MVP | Approved | design/gdd/detection-system.md | Sound Propagation Model, Light Source System |
| 4 | Player Interaction System | Core | MVP | Approved | design/gdd/player-interaction-system.md | — |
| 5 | Checkpoint System | Core | MVP | Not Started | — | — |


### Gameplay

| \# | System Name | Category | Priority | Status | Design Doc | Depends On |
| - | - | - | - | - | - | - |
| 6 | Player Noise Emitter | Gameplay | MVP | Approved | design/gdd/player-noise-emitter.md | Sound Propagation Model, Player Movement (exists) |
| 7 | Throwable Object | Gameplay | MVP | Approved | design/gdd/throwable-object.md | Sound Propagation Model, Player Interaction System |
| 8 | Seeker AI | Gameplay | MVP | Approved | design/gdd/seeker-ai.md | Detection System |
| 9 | Hiding Spot System | Gameplay | MVP | Approved | design/gdd/hiding-spot-system.md | Player Interaction System, Detection System |
| 10 | Objective System | Gameplay | MVP | Approved | design/gdd/objective-system.md | Player Interaction System |
| 11 | Level Exit System | Gameplay | MVP | Approved | design/gdd/level-exit-system.md | Objective System, Seeker AI |
| 12 | Two-Phase Level Structure (inferred) | Gameplay | MVP | Not Started | — | Objective System, Level Exit System, Seeker AI |
| 13 | Environmental Interaction | Gameplay | Vertical Slice | Not Started | — | Player Interaction System, Light Source System, Sound Propagation Model |
| 14 | Stealth Toolkit / Gadgets | Gameplay | Vertical Slice | Not Started | — | Player Interaction System, Gadget Inventory |
| 15 | Puzzle Gate System | Gameplay | Alpha | Not Started | — | Environmental Interaction, Player Interaction System |


### Progression

| \# | System Name | Category | Priority | Status | Design Doc | Depends On |
| - | - | - | - | - | - | - |
| 16 | Gadget Inventory (inferred) | Progression | Vertical Slice | Not Started | — | — |
| 17 | Level Timer + Stats (inferred) | Progression | Vertical Slice | Not Started | — | Detection System, Level Exit System |
| 18 | Level Progression / Unlock (inferred) | Progression | Alpha | Not Started | — | Level Timer + Stats, Save/Load System |


### Persistence

| \# | System Name | Category | Priority | Status | Design Doc | Depends On |
| - | - | - | - | - | - | - |
| 19 | Save/Load System (inferred) | Persistence | Alpha | Not Started | — | Level Timer + Stats |


### UI

| \# | System Name | Category | Priority | Status | Design Doc | Depends On |
| - | - | - | - | - | - | - |
| 20 | HUD (inferred) | UI | MVP | Not Started | — | Detection System, Player Noise Emitter, Gadget Inventory |
| 21 | Win / Game Over Screens | UI | MVP | Not Started | — | Two-Phase Level Structure |
| 22 | Detection Event Feedback UI (inferred) | UI | Vertical Slice | Not Started | — | Detection System, Seeker AI |
| 23 | Level Complete Screen (inferred) | UI | Vertical Slice | Not Started | — | Level Timer + Stats |
| 24 | Main Menu + Pause Menu | UI | Full Vision | Not Started | — | — |


### Audio

| \# | System Name | Category | Priority | Status | Design Doc | Depends On |
| - | - | - | - | - | - | - |
| 25 | Footstep Audio (inferred) | Audio | MVP | Not Started | — | Player Noise Emitter, Sound Propagation Model |
| 26 | Seeker Audio (inferred) | Audio | Vertical Slice | Not Started | — | Seeker AI |
| 27 | Adaptive Music (inferred) | Audio | Vertical Slice | Not Started | — | Detection System |



## Categories

| Category | Description | Systems in This Project |
| - | - | - |
| **Core** | Foundation systems everything else depends on | Sound Propagation Model, Light Source System, Detection System, Player Interaction System, Checkpoint System |
| **Gameplay** | The systems that make the game fun | Player Noise Emitter, Throwable Object, Seeker AI, Hiding Spot, Objective, Level Exit, Two-Phase Structure, Environmental Interaction, Gadgets, Puzzle Gates |
| **Progression** | How the player and session state grow | Gadget Inventory, Level Timer + Stats, Level Progression/Unlock |
| **Persistence** | Save state and continuity | Save/Load System |
| **UI** | Player-facing information displays | HUD, Win/Lose, Detection Feedback, Level Complete, Menus |
| **Audio** | Sound and music systems | Footstep Audio, Seeker Audio, Adaptive Music |



## Priority Tiers

| Tier | Definition | Target Milestone | Design Urgency |
| - | - | - | - |
| **MVP** | Required for the core loop to function | Sprint 1 (2 weeks) | Design FIRST |
| **Vertical Slice** | Required for 3 complete polished levels | Sprint 3–4 | Design SECOND |
| **Alpha** | Full mechanical scope, all levels rough | Month 4–6 | Design THIRD |
| **Full Vision** | Polish, meta, and content-complete | 12–18 months | Design as needed |



## Dependency Map

### Foundation Layer (no dependencies)

1. **Sound Propagation Model** — defines surface types and sound radius rules; everything touching sound reads from this

2. **Light Source System** — defines ambient light level per zone and manages breakable/carriable light sources

3. **Player Interaction System** — the "press E" framework; all interactable objects register with this

4. **Gadget Inventory** — pure data container defining gadget types and slot counts

5. **Checkpoint System** — defines respawn point placement and respawn behavior; no game state dependencies

### Core Layer (depends on foundation)

1. **Detection System** — depends on: Sound Propagation Model, Light Source System

2. **Player Noise Emitter** — depends on: Sound Propagation Model, Player Movement (exists)

3. **Objective System** — depends on: Player Interaction System

### Feature Layer (depends on core)

1. **Throwable Object** — depends on: Sound Propagation Model, Player Interaction System. Emits a `NoiseEvent` at impact world position when thrown; the SPM propagates it identically to player-body sounds. MVP requires one distraction item; Stealth Toolkit / Gadgets encompasses broader gadget inventory in Vertical Slice.

2. **Seeker AI** — depends on: Detection System

3. **Hiding Spot System** — depends on: Player Interaction System, Detection System

4. **Environmental Interaction** — depends on: Player Interaction System, Light Source System, Sound Propagation Model

5. **Stealth Toolkit / Gadgets** — depends on: Player Interaction System, Gadget Inventory

6. **Level Exit System** — depends on: Objective System, Seeker AI

### Level Layer (depends on features)

1. **Two-Phase Level Structure** — depends on: Objective System, Level Exit System, Seeker AI

2. **Puzzle Gate System** — depends on: Environmental Interaction, Player Interaction System

3. **Level Timer + Stats** — depends on: Detection System, Level Exit System

### Presentation Layer (depends on gameplay)

1. **HUD** — depends on: Detection System, Player Noise Emitter, Gadget Inventory

2. **Win / Game Over Screens** — depends on: Two-Phase Level Structure

3. **Detection Event Feedback UI** — depends on: Detection System, Seeker AI

4. **Level Complete Screen** — depends on: Level Timer + Stats

5. **Footstep Audio** — depends on: Player Noise Emitter, Sound Propagation Model

6. **Seeker Audio** — depends on: Seeker AI

7. **Adaptive Music** — depends on: Detection System

### Polish Layer (depends on everything)

1. **Level Progression / Unlock** — depends on: Level Timer + Stats, Save/Load System

2. **Save/Load System** — depends on: Level Timer + Stats

3. **Main Menu + Pause Menu** — standalone; wraps all other systems


## Recommended Design Order

| Order | System | Priority | Layer | Agent(s) | Est. Effort |
| - | - | - | - | - | - |
| 1 | Detection System | MVP | Core | game-designer + systems-designer | L |
| 2 | Sound Propagation Model | MVP | Foundation | game-designer + systems-designer | S |
| 3 | Light Source System | MVP | Foundation | game-designer | S |
| 4 | Seeker AI | MVP | Feature | game-designer + ai-programmer | M |
| 5 | Player Noise Emitter | MVP | Core | game-designer + systems-designer | S |
| 6 | Player Interaction System | MVP | Foundation | game-designer | S |
| 7 | Throwable Object | MVP | Feature | game-designer + systems-designer | S |
| 8 | Hiding Spot System | MVP | Feature | game-designer | S |
| 9 | Objective System | MVP | Core | game-designer | S |
| 10 | Level Exit System | MVP | Feature | game-designer | S |
| 11 | Two-Phase Level Structure | MVP | Level | game-designer | S |
| 12 | Checkpoint System | MVP | Foundation | game-designer | S |
| 13 | HUD | MVP | Presentation | game-designer + ux-designer | M |
| 14 | Win / Game Over Screens | MVP | Presentation | game-designer + ux-designer | S |
| 15 | Footstep Audio | MVP | Presentation | audio-director + sound-designer | M |
| 16 | Environmental Interaction | Vertical Slice | Feature | game-designer | M |
| 17 | Gadget Inventory | Vertical Slice | Foundation | game-designer | S |
| 18 | Stealth Toolkit / Gadgets | Vertical Slice | Feature | game-designer + systems-designer | M |
| 19 | Level Timer + Stats | Vertical Slice | Level | game-designer | S |
| 20 | Level Complete Screen | Vertical Slice | Presentation | game-designer + ux-designer | S |
| 21 | Detection Event Feedback UI | Vertical Slice | Presentation | game-designer + ux-designer | S |
| 22 | Seeker Audio | Vertical Slice | Presentation | audio-director + sound-designer | M |
| 23 | Adaptive Music | Vertical Slice | Presentation | audio-director | M |
| 24 | Puzzle Gate System | Alpha | Feature | game-designer + systems-designer | L |
| 25 | Level Progression / Unlock | Alpha | Polish | game-designer | S |
| 26 | Save/Load System | Alpha | Persistence | game-designer | M |
| 27 | Main Menu + Pause Menu | Full Vision | Polish | game-designer + ux-designer | M |


*Effort: S = 1 session, M = 2–3 sessions, L = 4+ sessions.*


## Circular Dependencies

- **Detection System ↔ Seeker AI**: The Detection System requires seeker position, orientation, and detection parameters as inputs. The Seeker AI reacts to detection outputs. This is NOT a true circular dependency — it's resolved by designing the Detection System to accept a `DetectionQuery` interface (seeker properties as input data), and having the Seeker AI implement that interface. The Detection System does not depend on the Seeker AI class; the Seeker AI depends on Detection System output. **Resolution**: Design Detection System first. Define its input/output contract. Seeker AI GDD is written to match that contract.


## High-Risk Systems

| System | Risk Type | Risk Description | Mitigation |
| - | - | - | - |
| **Detection System** | Design | 3D LoS legibility is fundamentally harder than 2D. A system that feels opaque breaks Pillar 3 (Legible Jeopardy) and destroys player trust. | Prototype first in Sprint 1. Define visual feedback (vision cone mesh, audio cue) before finalising the math. |
| **Detection System** | Technical | Full model (LoS + light + sound + suspicion + seeker modifiers) is complex; bugs are invisible and erode trust silently. | GDD must define explicit thresholds. Unit test all detection transitions against spec. |
| **Sound Propagation Model** | Design | True 3D sound occlusion is expensive. A simplified model that *feels* accurate is harder to design than a physically accurate one. | Define "believable simplification" rules in the GDD. Player should never feel cheated, not hear physically accurate results. |
| **Seeker AI** | Design | Seeker variant differentiation: what makes each type *meaningfully* different vs. just numerically different? Shallow variants feel gimmicky. | Prototype 2 variants before committing to the variant count. Differentiation axis must come from behavior, not just stats. |
| **Two-Phase Level Structure** | Scope | Hand-crafting escape escalation for 8–12 levels solo is the highest sustained design burden in the project. | Define a minimum viable escalation template (e.g., "seeker patrol speed +30%, all seekers alerted simultaneously") that can be applied consistently, not invented per level. |



## Progress Tracker

| Metric | Count |
| - | - |
| Total systems identified | 27 |
| Design docs started | 9 |
| Design docs reviewed | 10 |
| Design docs approved | 10 |
| MVP systems designed | 10 / 15 |
| Vertical Slice systems designed | 0 / 8 |



## Next Steps

- [ ] Design the Detection System GDD first (highest-risk, most dependents)

- [ ] Run `/design-review design/gdd/detection-system.md` after each GDD is written

- [ ] Run `/gate-check pre-production` when all 14 MVP systems are designed

- [ ] Prototype the Detection System in Sprint 1 before other systems are locked in

- [ ] Use `/design-systems \[system-name\]` to jump to any system, or `/design-systems next` to continue in order

