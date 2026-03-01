# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A 3D hide-and-seek game built in Unity 6 (6000.3.10f1) using the Universal Render Pipeline (URP). The project is in early development — only the utility layer and project scaffolding exist so far; gameplay scripts have not been written yet.

## Unity Version & Render Pipeline

- Unity **6000.3.10f1**
- **URP 17.3.0** with separate PC and Mobile renderer/pipeline assets in `Assets/_Project/Settings/`
- Input handled via **Unity Input System 1.18.0** (`Assets/_Project/Input/InputSystem_Actions.inputactions`)

## Key Packages

| Package | Purpose |
|---|---|
| `com.unity.ai.navigation` 2.0.11 | NavMesh / AI pathfinding |
| `com.unity.inputsystem` 1.18.0 | New Input System |
| `com.gitamend.improvedtimers` | Coroutine-free timer utilities |
| `com.unity.test-framework` 1.6.0 | Unity Test Runner (EditMode/PlayMode) |
| `com.unity.timeline` 1.8.11 | Cutscene / sequencing |

## Project Structure

All game code lives under `Assets/_Project/`. Third-party editor tools are in `Assets/Plugins/` and `Assets/RedBlueGames/`.

```
Assets/_Project/
  Animations/        # Animator controllers and clips
  Audio/             # Audio clips and mixers
  Images/            # Textures and sprites
  Input/             # InputSystem_Actions.inputactions
  Materials/
  Models/
  Physics Materials/
  Prefabs/
  Scenes/            # SampleScene.unity (only scene so far)
  Scripts/
    Utilities/       # Shared utility layer (see below)
  Settings/          # URP pipeline/renderer assets, volume profiles
```

## Utility Layer (`Assets/_Project/Scripts/Utilities/`)

A rich set of utilities and extension methods that all game code should prefer over rolling its own:

- **`Singleton<T>`** — Cross-scene singleton. Auto-creates a GameObject if none exists.
- **`SceneSingleton<T>`** — Scene-bound singleton. Does NOT auto-create; requires the component to be placed in the scene with serialized references.
- **`WaitFor`** — Cached `WaitForSeconds`, `WaitForFixedUpdate`, `WaitForEndOfFrame` to avoid allocations in coroutines.
- **`Helpers`** — `GetWaitForSeconds`, `QuitGame`, `ClearConsole`.
- Many extension methods: `TransformExtensions`, `Vector3Extensions`, `GameObjectExtensions`, `RendererExtensions`, `ListExtensions`, `EnumerableExtensions`, `LayerMaskExtensions`, `StringExtensions`, `MathfExtension`, etc.
- **`TaskExtensions` / `AsyncOperationExtensions`** — async/await helpers for Unity operations.

All utilities are in the `Utilities` namespace.

## Input Actions

Defined action maps in `InputSystem_Actions.inputactions`:

- **Player map**: Move (Vector2), Look (Vector2), Attack, Interact (Hold), Crouch, Jump, Sprint, Previous, Next
- **UI map**: Standard Unity UI actions

## Editor Tools

- **`Tools/Setup/Create Folders`** — Creates the `_Project` folder structure
- **`Tools/Setup/Install Essential Packages`** — Installs packages via UPM
- **`Tools/Setup/Import Essential Assets`** — Imports `.unitypackage` files from the local Asset Store cache
- **`CompileProject`** — Triggers a script recompile from the menu
- **`ForceSaveSceneAndProject`** — Force-saves without confirmation

## Version Control

- Commit work to git regularly with clean, descriptive commit messages.
- Push commits to GitHub after each logical unit of work so progress is never lost.
- Commit messages should be imperative, concise, and describe *what* changed and *why* (e.g. `Add player movement controller with sprint support`).

## Running Tests

Tests use Unity Test Runner (Window > General > Test Runner). No standalone CLI test runner is configured; tests must be run from within the Unity Editor.

## Coding Conventions

- Namespace: `Utilities` for all utility scripts; game-specific scripts can use a project-specific namespace once established.
- Prefer `SceneSingleton<T>` over `Singleton<T>` for managers that need serialized scene references (UI bindings, etc.).
- Use `WaitFor.Seconds(t)` instead of `new WaitForSeconds(t)` in coroutines to avoid GC pressure.
- No Assembly Definitions are set up for game scripts yet — all game code compiles into the default `Assembly-CSharp` assembly.

## SOLID Principles

Apply SOLID principles to all game scripts, especially managers and systems with non-trivial logic.

**Single Responsibility** — Each class does one thing. A `PlayerMovement` component moves the player; it does not also handle input reading or health. Split concerns into separate MonoBehaviours or plain C# classes.

**Open/Closed** — Extend behaviour through composition and interfaces, not by modifying existing classes. Prefer adding a new `IInteractable` implementor over editing a switch-case in an existing manager.

**Liskov Substitution** — Subtypes must be usable wherever their base type is expected without breaking behaviour. Avoid overriding methods in ways that violate the base contract (e.g. throwing where the base does not).

**Interface Segregation** — Prefer small, focused interfaces (`IHideable`, `IDetectable`) over wide ones. Components should only depend on the interface members they actually use.

**Dependency Inversion** — Depend on abstractions (interfaces/abstract classes), not concrete types. Inject dependencies via constructor, method parameter, or `[SerializeField]` inspector reference rather than calling `FindObjectOfType` or `GetComponent` from within a class.

## Design Patterns

Use these patterns where they solve a real problem. Do not apply them speculatively.

**Observer / Event System** — Decouple systems with C# events or `UnityEvent`. Example: `GameEvents.OnPlayerDetected` lets the UI, audio, and AI each react without knowing about each other. Prefer typed `Action<T>` delegates over `UnityEvent` for code-to-code communication.

**State Machine** — Model character and game states explicitly. Each state is a class implementing a common `IState` interface with `Enter`, `Tick`, and `Exit` methods. A `StateMachine` owns the current state and drives transitions. Avoids flag-soup in Update loops.

**Command** — Wrap player actions as command objects to support undo, replay, or deferred execution. Useful for Interact and Attack inputs.

**Object Pool** — Never `Instantiate`/`Destroy` at runtime for frequently spawned objects (projectiles, footstep decals, VFX). Use Unity's built-in `ObjectPool<T>` (`UnityEngine.Pool`).

**Strategy** — Swap algorithms at runtime behind an interface. Example: a seeker AI can switch between `PatrolStrategy`, `SearchStrategy`, and `ChaseStrategy` without changing the AI component.

**Factory Method** — Centralise prefab instantiation (or pool checkout) behind a factory so callers never reference `Resources.Load` or hard-coded prefab paths.

**Decorator** — Layer optional behaviours (e.g. adding a speed buff on top of base movement) without subclassing. Implement via wrapper classes sharing a common interface.

**Service Locator (limited use)** — Acceptable only for truly global services (AudioManager, SceneLoader) where DI is impractical. Prefer injecting interfaces over calling `ServiceLocator.Get<T>()` in gameplay code.

## Performance & GC Rules

- Avoid allocations in `Update`, `FixedUpdate`, and hot coroutine paths. No LINQ, no `string` concatenation, no `new` for value-type wrappers.
- Cache `GetComponent` results in `Awake`/`Start`; never call them per frame.
- Use `TryGetComponent` instead of `GetComponent` when the component may be absent.
- Physics queries (`OverlapSphere`, `Raycast`) should use the non-allocating variants with pre-allocated result arrays.
- Profile before optimising. Use the Unity Profiler and Memory Profiler before concluding something is a bottleneck.
