# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Dice Wars is a turn-based deck-building combat game built in Unity 2022.3.5f1 LTS. Players select dice from their deck, roll them using 3D physics, and accumulate attack/defense points to fight enemies. Features a bust mechanic (power overflow) and sequential/random enemy action cycles.

## Build & Run

- **Unity Version**: 2022.3.5f1 (open via Unity Hub)
- **Main Scene**: `Assets/Scenes/SampleScene.unity`
- No test suite or CI/CD pipeline exists
- No command-line build scripts — use Unity Editor to build

## Architecture

### Core Pattern: Event-Driven State Machine

The combat loop is driven by `CombatManager` cycling through `CombatState` enum states (`WaitingForRoll → Rolling → BustCheck → TurnEnd → EnemyTurn`). Cross-system communication happens through static events in `CombatEvents` — UI, enemies, and dice all subscribe to these events rather than holding direct references.

### Data Layer: ScriptableObjects

All game data (dice definitions, face values, enemy types, enemy actions) lives in ScriptableObjects under `Assets/Data/`. Runtime code reads from these — they are the single source of truth for game configuration.

- `DieAssetSO` → 6 faces, attack/defense type
- `DieFaceSO` → value, type, material, optional `IGameAction` (see [GameAction system design](Assets/Scripts/Effects/DESIGN.md))
- `EnemyTypeSO` → HP, action list, sequential/random cycle
- `EnemyActionSO` → damage, armor, attack count
- `PlayerDataSO` → player's dice deck

### Dice Pipeline

1. `CombatUIController` — player picks dice from deck (buttons generated dynamically from `PlayerDataSO`)
2. `DiceSpawner` — instantiates dice prefabs, applies random force/torque
3. `DiceRoller` — monitors Rigidbody velocity to detect settlement, raycasts to determine top face
4. `DieVisualizer` — assigns face materials to the 3D die mesh

### Enemy System

`EnemyController` manages individual enemy instances. Each enemy has an `EnemyTypeSO` defining its action queue (list of `EnemyActionSO`). Actions execute sequentially or randomly per enemy type config. Enemies include visual feedback (sprite flash, camera shake via `CameraShake` singleton, hit particles).

### Key Enums

`CombatEnums.cs` defines `DieType` (Attack/Defense) and `FaceRarity` — use these, not string comparisons.

## All Scripts Location

Scripts are in `Assets/Scripts/` with subfolders:
- `Assets/Scripts/Effects/` — GameAction system (abilities on dice faces)
- `Assets/Scripts/UI/` — Standalone UI panels (e.g., PrecisionPanel)

## Adding Abilities

See [Effects DESIGN.md](Assets/Scripts/Effects/DESIGN.md) for the full system design, all ability patterns (immediate, turn-end, player-prompt), the CombatManager API available to actions, and step-by-step instructions for adding new abilities.

## Agent Behavior

- Before writing code, outline: what will change, why, risks/assumptions
- Prefer small, reversible changes
- **No direct scene interaction** — create scripts and provide instructions for engine setup

## Conventions

- ScriptableObjects for all data configuration — no hardcoded values
- Static event bus (`CombatEvents`) for decoupled communication
- Coroutines for async game flow (enemy turns, animations, timers)
- Both 2D (sprites, UI) and 3D (dice physics) coexist — dice use Rigidbody3D
- `SimulationSpeedController` controls `Time.timeScale` (0.2x–4x range)
- Don't create `.meta` files — let Unity handle them
- Split code into files, methods, and classes logically; use folders as the project grows
- Encapsulate logic in the leaf class that owns it (e.g., call `PauseRunning()` instead of saving speed and setting it to zero from the caller)
- Propose using libraries/Asset Store packages before reinventing the wheel

## Code Quality

- No null checks that silently skip logic when the component should never be null
- Every added config or component must validate its setup and throw meaningful errors if misconfigured
- Never fix real issues with patching or workarounds — find and fix the actual root cause
- Avoid `GetComponent`/`GetComponentInChildren` — prefer explicit references
- Avoid static variables — Unity's "Enter Play Mode Options" (no domain reload) means static state persists between play sessions, causing subtle bugs


- When needed (you may ask), use Odin for better inspector in complicated serialized classes.