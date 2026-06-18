# Hide & Seek *(working title)*

![Status](https://img.shields.io/badge/status-in%20development-yellow)

A 3D stealth game built in Unity where a player must hide from AI-controlled seekers patrolling a dungeon environment. The seeker reacts to sound, line of sight, and investigates suspicious activity using a multi-state behaviour system.

---

## Tech Stack

| Area | Technology |
|---|---|
| Engine | Unity 6.3 LTS (6000.3.10f1) |
| Language | C# |
| Rendering | Universal Render Pipeline (URP 17.3.0) |
| AI / Pathfinding | Unity NavMesh (`com.unity.ai.navigation` 2.0.11) |
| Camera | Cinemachine |
| Input | Unity Input System 1.18.0 |
| Version Control | Git |

---

## Features

### Gameplay
- Player hides from AI seekers in a 3D dungeon environment
- Noise and line-of-sight detection systems
- Suspicion meter that escalates enemy alertness
- Interactable hiding spots (wardrobes, etc.)
- Round timer with win/lose conditions

### Player
- Rigidbody-based character movement (walk, sprint, crouch)
- Input handled via Unity Input System action maps
- Interact, hide, and noise-emission systems decoupled into separate components

### Enemy AI
- State machine architecture using an `IEnemyState` interface with `NavMeshAgent`
- Five states: **Idle → Patrol → Investigate → Search → Chase**
- Line-of-sight and field-of-view checks per frame
- Noise event bus (`NoiseEmitter`) — enemies react to player-generated sounds

### Editor Tooling
- **LevelBuilder suite** — custom Unity Editor windows for level construction:
  - Surface drop tool (snap objects to geometry)
  - Bounds snap, align, and distribute tools
  - Scatter tool with overlap avoidance
  - Prefab palette for click-to-place placement
  - Component audit tool (tag → required-component validation)

---

## Project Structure

```
Assets/_Project/
  Animations/
  Audio/
  Images/
  Input/           # InputSystem_Actions.inputactions
  Materials/
  Models/
  Prefabs/
  Scenes/
  Scripts/
    AI/            # Enemy controller, detection, states
    Audio/         # AudioManager, MusicController, SoundEmitter
    Data/          # ScriptableObjects (PlayerData, EnemyData, etc.)
    Detection/     # LineOfSight, FieldOfView, SuspicionMeter, NoiseSystem
    Editor/        # LevelBuilder editor tools
    GameLoop/      # GameManager, RoundTimer, WinCondition, LoseCondition
    HidingSpots/   # HidingSpot, HidingSpotRegistry
    Infrastructure/# Interfaces, StateMachine, BaseState
    Interaction/   # InteractableBase, props
    Player/        # PlayerController, Movement, Input, Hiding, Interaction
    UI/            # HUD, Menus
    Utilities/     # Singleton, SceneSingleton, WaitFor, extensions
  Settings/        # URP pipeline and renderer assets
```

---

## Getting Started

**Requirements**
- Unity 6.3 LTS (6000.3.10f1)
- Git LFS

**Steps**

1. Clone the repository:
   ```bash
   git clone https://github.com/marko-builds/hide_and_seek_3d.git
   ```
2. Open Unity Hub → **Add project from disk** → select the cloned folder.
3. Unity will import assets and compile scripts on first open (this may take a few minutes).
4. Open `Assets/_Project/Scenes/Level_1.unity`.
5. Press **Play**.

> The project targets PC (Windows/Linux/macOS). Mobile renderer assets are included but the game is not optimised for mobile.

---

## Author

**Marko Stankovic**
- Website: [markostankovic.org](https://markostankovic.org)
- Email: [contact@markostankovic.org](mailto:contact@markostankovic.org)
