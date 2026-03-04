# Directory Structure

```
/  
├── CLAUDE.md                    \# Master configuration  
├── .claude/                     \# Agent definitions, skills, hooks, rules, docs  
├── Assets/\_Project/Scripts                         \# Game source code (core, gameplay, ai, networking, ui, tools)  
├── design/                      \# Game design documents (gdd, narrative, levels, balance)  
├── docs/                        \# Technical documentation (architecture, api, postmortems)  
│   └── engine-reference/        \# Curated engine API snapshots (version-pinned)  
├── tests/                       \# Test suites (unit, integration, performance, playtest)  
├── tools/                       \# Build and pipeline tools (ci, build, asset-pipeline)  
├── prototypes/                  \# Throwaway prototypes (isolated from src/)  
└── production/                  \# Production management (sprints, milestones, releases)  
 ├── session-state/           \# Ephemeral session state (active.md — gitignored)  
 └── session-logs/            \# Session audit trail (gitignored)
```

## Project Structure

All game code lives under `Assets/\_Project/`. Third-party editor tools are in `Assets/Plugins/` and `Assets/RedBlueGames/`.

```
Assets/\_Project/  
  Animations/        \# Animator controllers and clips  
  Audio/             \# Audio clips and mixers  
  Images/            \# Textures and sprites  
  Input/             \# InputSystem\_Actions.inputactions  
  Materials/  
  Models/  
  Physics Materials/  
  Prefabs/  
  Scenes/              
  Scripts/  
    Utilities/       \# Shared utility layer (see below)  
  Settings/          \# URP pipeline/renderer assets, volume profiles
```
