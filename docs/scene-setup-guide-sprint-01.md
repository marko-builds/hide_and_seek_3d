# Scene Setup Guide — Sprint 01 (Level_1)

**Sprint:** sprint-01
**Goal:** Get Level_1 to a fully playable vertical slice — player can move, hide, be detected, collect an objective token, reach the level exit, and see Win/Lose screens.

---

## Step 0 — ScriptableObject Assets

Create these assets **before** opening the scene. Use `Assets/_Project/Scripts/Data/DataObjects/` as the target folder.

| Asset file | Create menu path | Notes |
|---|---|---|
| `EnemyData.asset` | `HideAndSeek/Data/Enemy Data` | All fields have tuned defaults — leave as-is for first playtest |
| `PlayerData.asset` | `HideAndSeek/Data/Player Data` | |
| `GameRulesData.asset` | `HideAndSeek/Data/Game Rules Data` | Set `roundDuration` (e.g. `180` seconds) |
| `HidingSpotData.asset` | `HideAndSeek/Data/Hiding Spot Data` | Set `exitOffset` to e.g. `(1, 0, 0)` |
| `EscalationProfile.asset` | `HideAndSeek/Data/Escalation Profile` | Already exists in DataObjects |
| `SoundLibrary.asset` | `HideAndSeek/Data/Sound Library` | |
| `ObjectiveData.asset` | `HideAndSeek/Data/Objective Data` | VFX/SFX fields can be null for sprint 1 |
| `LevelExitData.asset` | `HideAndSeek/Data/Level Exit Data` | SFX/VFX fields can be null for sprint 1 |

---

## Step 1 — Recommended Hierarchy

```
Level_1
├── _Managers
│   ├── GameManager
│   ├── AudioManager
│   ├── SeekerRegistry
│   ├── ObjectiveRegistry
│   ├── HidingSpotRegistry
│   ├── LevelPhaseManager
│   └── GameLoop
│       ├── RoundTimer
│       ├── WinConditionEvaluator
│       └── LoseConditionEvaluator
│
├── _Environment
│   ├── Floor
│   └── Walls
│
├── Player
│
├── Enemy
│   └── Waypoints
│       ├── WP_01
│       ├── WP_02
│       └── WP_03
│
├── _Interactables
│   ├── Wardrobe_01
│   │   └── AttachPoint
│   ├── Wardrobe_02
│   │   └── AttachPoint
│   ├── ObjectiveToken_01
│   └── LevelExit
│
├── _UI
│   ├── Canvas
│   │   ├── HUDRoot
│   │   │   ├── SuspicionMeterUI
│   │   │   ├── TimerUI
│   │   │   └── NoiseIndicatorUI
│   │   ├── WinScreen
│   │   └── GameOverScreen
│   └── HUDManager
│
└── Main Camera
```

---

## Step 2 — `_Managers`

### `GameManager` — empty GameObject
- Script: **`GameManager`**
- No Inspector fields. `Singleton<T>` is self-initializing but placing it explicitly avoids race conditions.

### `AudioManager` — empty GameObject
- Script: **`AudioManager`**
- **Sound Library** → `SoundLibrary.asset`
- **Emitter Prefab** → a prefab containing an `AudioSource` + `SoundEmitter` script. Create it: new empty GO → add `AudioSource` + `SoundEmitter` → save as prefab under `Assets/_Project/Prefabs/`.

### `SeekerRegistry` — empty GameObject
- Script: **`SeekerRegistry`**
- No fields. `[DefaultExecutionOrder(-90)]` ensures it initialises before any enemy `Awake`.

### `ObjectiveRegistry` — empty GameObject
- Script: **`ObjectiveRegistry`**
- No fields. `[DefaultExecutionOrder(-100)]` ensures it initialises before any token `Awake`.

### `HidingSpotRegistry` — empty GameObject
- Script: **`HidingSpotRegistry`**
- No fields.

### `LevelPhaseManager` — empty GameObject
- Script: **`LevelPhaseManager`**
- **Escalation Profile** → `EscalationProfile.asset`

### `GameLoop` — empty GameObject (group)

**`RoundTimer`** child:
- Script: **`RoundTimer`**
- **Rules** → `GameRulesData.asset`

**`WinConditionEvaluator`** child:
- Script: **`WinConditionEvaluator`**
- **Round Timer** → drag the `RoundTimer` GameObject

**`LoseConditionEvaluator`** child:
- Script: **`LoseConditionEvaluator`**
- No fields — reads from `SeekerRegistry` at `Start`.

---

## Step 3 — Player

Create an empty GameObject named `Player`. Add **`PlayerController`** first — it carries `[RequireComponent]` for all subsystems.

| Component | How it gets added |
|---|---|
| `PlayerController` | Manually |
| `PlayerInputHandler` | Auto via `[RequireComponent]` |
| `PlayerMovement` | Auto via `[RequireComponent]` |
| `PlayerHiding` | Auto via `[RequireComponent]` |
| `PlayerInteraction` | Auto via `[RequireComponent]` |
| `PlayerNoiseEmitter` | Auto via `[RequireComponent]` |
| `Rigidbody` | Auto via `[RequireComponent]` on `PlayerMovement` |
| **Capsule Collider** | Add manually |

**Capsule Collider settings:**
- Height: `1.8`
- Center Y: `0.9`

**Rigidbody settings:**
- Collision Detection: `Continuous`
- Constraints → Freeze Rotation: X ✓, Y ✓, Z ✓ (player rotates via code)

**`PlayerController` Inspector** — these are `[field: SerializeField]` and require manual assignment:

| Field | Assign |
|---|---|
| Input Handler | `PlayerInputHandler` component on this GO |
| Movement | `PlayerMovement` component on this GO |
| Hiding | `PlayerHiding` component on this GO |
| Interaction | `PlayerInteraction` component on this GO |
| Noise Emitter | `PlayerNoiseEmitter` component on this GO |
| Detection Profile | Inline serialized class — configure sub-fields directly |

> Drag each component from the Inspector's component list into the matching field on `PlayerController`. Unity will resolve them correctly when you drag the same GameObject.

**`PlayerMovement` Inspector:**
| Field | Assign |
|---|---|
| Data | `PlayerData.asset` |
| Camera | Main Camera |

**`PlayerNoiseEmitter` Inspector:**
| Field | Assign |
|---|---|
| Data | `PlayerData.asset` |

**`PlayerInteraction` Inspector:**
| Field | Value |
|---|---|
| Interact Radius | `1.5` |
| Interactable Mask | `Interactable` layer (see Step 11) |

---

## Step 4 — Enemy

Create an empty GameObject named `Enemy`. Add **`EnemyController`** — it carries `[RequireComponent]` for all subsystems.

| Component | How it gets added |
|---|---|
| `EnemyController` | Manually |
| `EnemyDetection` | Auto via `[RequireComponent]` |
| `EnemyNavigation` | Auto via `[RequireComponent]` |
| `NavMeshAgent` | Auto via `[RequireComponent]` on `EnemyNavigation` |
| `SuspicionMeter` | Auto via `[RequireComponent]` on `EnemyDetection` |
| `FieldOfView` | Auto via `[RequireComponent]` on `EnemyDetection` |
| `NoiseListener` | Auto via `[RequireComponent]` on `EnemyDetection` |
| **Capsule Collider** | Add manually |

Add a capsule mesh or primitive so the enemy is visible.

**`EnemyController` Inspector:**
| Field | Assign |
|---|---|
| Data | `EnemyData.asset` |
| Waypoints | Drag in `WP_01`, `WP_02`, `WP_03` Transforms |

**`EnemyDetection` Inspector:**
| Field | Assign |
|---|---|
| Obstruction Mask | Your environment/walls layer — anything that blocks line of sight |
| Player | Drag the `Player` root GameObject (field type is `PlayerController`) |

**`FieldOfView` Inspector:**
| Field | Assign |
|---|---|
| Data | `EnemyData.asset` |

**`SuspicionMeter` Inspector:**
| Field | Assign |
|---|---|
| Data | `EnemyData.asset` (optional — auto-wires from `EnemyController.Data` in `Awake` if left blank) |

**`NoiseListener` Inspector:**
| Field | Value |
|---|---|
| Hearing Radius | Match `EnemyData.hearingRange` (default `10`) |

**`NavMeshAgent` settings:**
- Stopping Distance: `0.3` (matches `EnemyData.waypointArrivalThreshold`)
- Speed: leave at default — `EnemyNavigation.SetSpeed()` overrides it at runtime
- Base Offset: adjust so the agent capsule sits flush on the floor

**Waypoints** — under `Enemy/Waypoints`, create 3+ empty GameObjects (`WP_01`, `WP_02`, `WP_03`). No scripts needed. Place them at standing height around the patrol route.

**NavMesh bake:**
1. Select floor and wall meshes → Inspector → check **Navigation Static**.
2. Assign floor as `Walkable`, walls as `Not Walkable`.
3. Open **Window > AI > Navigation** → **Bake** tab → click **Bake**.

---

## Step 5 — Hiding Spots (Wardrobes)

> `Wardrobe.cs` exists at `Assets/_Project/Scripts/Interaction/Props/Wardrobe.cs` — task 1.6 is complete.

For each wardrobe:

**`Wardrobe_01`** (a box mesh or visible prop):
- Layer → `Interactable`
- Add script: **`HidingSpot`** (`[RequireComponent]` is declared on `Wardrobe`, so adding `Wardrobe` will also add `HidingSpot` automatically)
- Add script: **`Wardrobe`**
- Add **Box Collider** matching the mesh shape

**`HidingSpot` Inspector:**
| Field | Assign |
|---|---|
| Data | `HidingSpotData.asset` |
| Attach Transform | `AttachPoint` child Transform |

**`Wardrobe` Inspector:**
| Field | Assign |
|---|---|
| Animator | Optional — assign an `Animator` with a bool parameter `"IsOpen"` for door animation. Leave blank for instant snap. |
| Door Animation Duration | `0.4` (seconds the script waits for the door open animation before snapping the player) |

**`AttachPoint`** (empty child GameObject):
- Position it **inside** the wardrobe where the player will stand.
- Rotation should face **outward** — the direction the player looks while hiding.

Repeat for `Wardrobe_02`.

**Wardrobe interaction behaviour (for reference):**
- **Enter**: opens door → waits for animation → snaps player to `AttachPoint` → closes door.
- **Exit**: press Interact again while inside → opens door → waits for animation → releases player to `HidingSpotData.exitOffset` → closes door.
- `_isTransitioning` lock prevents double-input during the animation.

---

## Step 6 — Objective Token

**`ObjectiveToken_01`** (a sphere or distinctive mesh):
- Layer → `Interactable`
- Add a **Collider** (Is Trigger: off — `PlayerInteraction` uses `OverlapSphere`, not trigger callbacks)
- Add script: **`ObjectiveToken`**

**`ObjectiveToken` Inspector:**
| Field | Assign |
|---|---|
| Data | `ObjectiveData.asset` (VFX/SFX fields are optional for sprint 1 — null is safe) |

The token auto-registers with `ObjectiveRegistry` in its `Awake`. No manual wiring needed.

---

## Step 7 — Level Exit

**`LevelExit`** (a doorway, arch, or glowing plane):
- Layer → `Interactable`
- Add a **Collider**
- Add script: **`LevelExit`**

**`LevelExit` Inspector:**
| Field | Assign |
|---|---|
| Data | `LevelExitData.asset` (SFX/VFX optional) |

The exit is locked at start. It auto-subscribes to `ObjectiveRegistry.OnAllObjectivesCollected` via `OnEnable` and unlocks when all tokens are collected.

---

## Step 8 — Camera

**Using Cinemachine (recommended):**

1. Add the **Cinemachine** package via Package Manager if not present.
2. **GameObject > Cinemachine > Cinemachine Camera** (Unity 6 name) or Virtual Camera.
3. Set **Follow** → `Player` transform.
4. Set **Look At** → `Player` transform.
5. Choose Body: `3rd Person Follow` for an over-the-shoulder view, or `Framing Transposer` for a top-down follow.
6. The `Main Camera` will have a **`CinemachineBrain`** auto-added.

Ensure `Main Camera` is **tagged `MainCamera`** — `PlayerMovement` reads `_camera.transform.forward` for movement direction and the field is assigned manually (see Step 3).

---

## Step 9 — UI

**`Canvas`** (GameObject > UI > Canvas):
- Render Mode: `Screen Space - Overlay`
- Canvas Scaler: `Scale with Screen Size`, Reference Resolution `1920×1080`

**`HUDRoot`** (empty child of Canvas):
- This GameObject is what `HUDManager` shows during `Playing` and hides otherwise.
- Add placeholder UI elements inside (Text labels for suspicion, timer, etc.) — full HUD wiring is task 2.7.

**`WinScreen`** (empty child of Canvas):
- Add script: **`WinUI`** — `[RequireComponent]` auto-adds `CanvasGroup`.
- Starts invisible (alpha = 0). Becomes visible automatically on `GameManager.OnWin`.
- Add a Text child: "You escaped!" for sprint 1.

**`GameOverScreen`** (empty child of Canvas):
- Add script: **`GameOverUI`** — same pattern.
- Add a Text child: "Caught!" for sprint 1.

**`HUDManager`** (separate empty GameObject, child of `_UI`):
- Add script: **`HUDManager`**

**`HUDManager` Inspector:**
| Field | Assign |
|---|---|
| HUD Root | `HUDRoot` GameObject |

---

## Step 10 — Audio (Task 1.9 — minimum)

`MusicController` is optional for sprint 1. The `AudioManager` Singleton handles all `AudioManager.Instance.Play(SoundID)` calls from `ObjectiveToken`, `LevelExit`, and `LevelPhaseManager` — these are null-safe and will silently skip if the SoundLibrary has no entry for the ID.

For the minimum audio requirement (task 1.9 — footsteps + detection alert sting), this wiring will be specified in a follow-up guide once the audio assets exist.

---

## Step 11 — Layers

Create these layers in **Edit > Project Settings > Tags and Layers** if they don't exist:

| Layer name | Assign to |
|---|---|
| `Interactable` | Wardrobe_01, Wardrobe_02, ObjectiveToken_01, LevelExit |
| `Environment` | Floor, Walls (or use `Default` if you prefer) |
| `Player` | Player (useful for future raycasts) |

Set **`PlayerInteraction.Interactable Mask`** to include `Interactable`.
Set **`EnemyDetection.Obstruction Mask`** to include `Environment` (and any other geometry that blocks LoS). Do **not** include `Interactable` or `Player` in the obstruction mask.

---

## Full Wiring Summary

| Field | Component | Assign |
|---|---|---|
| Data | `PlayerMovement` | `PlayerData.asset` |
| Camera | `PlayerMovement` | Main Camera |
| Data | `PlayerNoiseEmitter` | `PlayerData.asset` |
| Interactable Mask | `PlayerInteraction` | `Interactable` layer |
| InputHandler / Movement / Hiding / Interaction / NoiseEmitter | `PlayerController` | Respective components on Player GO |
| Data | `EnemyController` | `EnemyData.asset` |
| Waypoints | `EnemyController` | WP_01, WP_02, WP_03 |
| Player | `EnemyDetection` | Player root GameObject |
| Obstruction Mask | `EnemyDetection` | `Environment` layer |
| Data | `FieldOfView` | `EnemyData.asset` |
| Data | `SuspicionMeter` | `EnemyData.asset` (optional) |
| Hearing Radius | `NoiseListener` | Match `EnemyData.hearingRange` |
| Rules | `RoundTimer` | `GameRulesData.asset` |
| Round Timer | `WinConditionEvaluator` | `RoundTimer` GameObject |
| Escalation Profile | `LevelPhaseManager` | `EscalationProfile.asset` |
| HUD Root | `HUDManager` | `HUDRoot` GameObject |
| Sound Library | `AudioManager` | `SoundLibrary.asset` |
| Emitter Prefab | `AudioManager` | SoundEmitter prefab |
| Data | `HidingSpot` | `HidingSpotData.asset` |
| Attach Transform | `HidingSpot` | `AttachPoint` child |
| Data | `ObjectiveToken` | `ObjectiveData.asset` |
| Data | `LevelExit` | `LevelExitData.asset` |

---

## Sprint 1 Definition of Done — Checklist

- [ ] Level_1 has: floor, walls, 2 wardrobes, 1 ObjectiveToken, enemy with patrol route, 1 LevelExit, NavMesh baked
- [ ] Player can enter and exit a Wardrobe via Interact input
- [ ] Enemy patrols waypoints, detects player via LoS, transitions to Chase, triggers lose screen
- [ ] Player can collect the ObjectiveToken — triggers Phase 2 escalation (enemy speeds up)
- [ ] LevelExit is locked until token collected, then unlocks and triggers win screen on interact
- [ ] WinUI and GameOverUI both display correctly from gameplay
- [ ] No null reference exceptions in a full play session (collect token → reach exit → Win screen)
