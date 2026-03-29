# GameAction & Effects System Design

## Overview

Modular action system for dice face effects, buffs, and debuffs. GameActions are source-agnostic — triggered by dice faces now, but designed for reuse by potions, relics, or any future system.

## Core Concepts

### IGameAction (Interface)

Minimal interface: `void Execute(GameActionContext context)`. No metadata on the interface — name, description, timing are implementation details of each class or the ScriptableObject that references it.

Concrete actions are `[Serializable]` classes. Odin's `[SerializeReference]` on `DieFaceSO` provides an inspector dropdown to assign them.

### GameActionContext

A context object passed to every action's `Execute`. Holds references actions might need:

- `CombatManager` — queue turn-end actions, read/modify bonus values
- `Player` (PlayerStatus) — heal, damage, armor
- `Enemy` (EnemyController) — deal damage, read state
- `ChanneledFaces` (List\<FaceResult\>) — all face results this turn
- `TriggeringFace` (FaceResult) — the face that triggered this action (null if not face-triggered)

### FaceResult

Stores an individual dice face landing result:

- `Face` (DieFaceSO) — the source SO
- `Value` (int) — the face's numeric value (can be modified, e.g., doubled on Perfect Strike)
- `Type` (DieType) — Attack or Defense
- `Action` (IGameAction) — the action from the face, nullable

### Channeled Faces

The turn's face results are stored as `List<FaceResult>` instead of accumulated totals. `pendingAttack` and `pendingDefense` are derived:

```
GetPendingAttack() = channeledFaces.Where(Attack).Sum(Value) + bonusAttack
GetPendingDefense() = channeledFaces.Where(Defense).Sum(Value) + bonusDefense
```

`bonusAttack`/`bonusDefense` are for action-granted bonuses (e.g., AddAttackAction adds to bonusAttack).

## Execution Timing

Actions decide their own timing inside `Execute()`:

- **Immediate (on land)**: Act directly in `Execute`. Example: `AddAttackAction` adds to `bonusAttack` immediately.
- **Turn end**: Queue via `context.CombatManager.QueueTurnEndAction(ctx => ...)`. The callback receives a `GameActionContext` so it can read turn-end state (e.g., `appliedMultiplier`). The queue is processed in `SubmitTurn` before damage/armor are applied. Example: `HealAction` queues healing for turn end.

There is no timing enum — each action knows when it should run.

## Turn Flow Integration

1. **Die settles** → `CombatManager.ResolveRollResult(face)`
2. Create `FaceResult`, add to `channeledFaces`
3. `currentPower += face.value` (power cost always applies)
4. If face has action → build `GameActionContext`, call `action.Execute(context)`
5. Fire UI events with derived totals
6. All dice settled → `CheckBustStatus` (Perfect Strike multiplies individual `FaceResult.Value` entries by `appliedMultiplier`)
7. `SubmitTurn` → build context, pass to turn-end action queue → derive final totals → apply damage/armor
8. `ResetTurn` → clear channeled faces, turn-end queue, bonus values, overcharge, appliedMultiplier

## Overcharge & Applied Multiplier

Perfect Strike base multiplier is 2. `OverchargeAction` adds to this via `CombatManager.AddOvercharge(amount)`.

When Perfect Strike triggers: `appliedMultiplier = 2 + overchargeBonus`. This multiplier is applied to:

- All `FaceResult.Value` entries in the channeled list (attack, defense)
- `bonusAttack` and `bonusDefense`
- Turn-end action values — callbacks receive a `GameActionContext` and should multiply their values by `ctx.CombatManager.GetAppliedMultiplier()`

Without Perfect Strike, `appliedMultiplier` stays 1 — turn-end actions apply their base values.

Both `overchargeBonus` and `appliedMultiplier` reset each turn.

**When writing a new turn-end action**, always multiply the action's value by `ctx.CombatManager.GetAppliedMultiplier()` so it scales with Perfect Strike + Overcharge.

## Player-Prompt Actions (Precision Pattern)

Some actions require player input after dice settle but before bust/turn resolution. These use a queue-and-prompt pattern:

1. Action's `Execute` queues a choice via `CombatManager.QueuePrecisionChoice(amount)`
2. After all dice settle, `ProcessPrecisionQueue` fires before `CheckBustStatus`
3. If queue has entries, fires `CombatEvents.OnPrecisionPrompt` → UI shows popup
4. Player responds → `CombatEvents.OnPrecisionResolved` → applies choice, processes next in queue
5. When queue is empty → `CheckBustStatus` proceeds normally

This pattern can be extended for future prompt-based actions by adding new queues and events.

## Bust Handling

On bust, the player chooses to nullify Attack or Defense. This removes all `FaceResult` entries of that type from `channeledFaces` and zeroes the corresponding bonus.

## File Structure

```
Assets/Scripts/Effects/
├── IGameAction.cs              — interface
├── GameActionContext.cs         — context object
├── FaceResult.cs                — individual face result
├── GameActions/                 — concrete action implementations
│   ├── HealAction.cs            — turn-end: heals player HP (scaled by appliedMultiplier)
│   ├── OverchargeAction.cs      — immediate: adds to Perfect Strike multiplier
│   ├── SafetyNetAction.cs       — immediate: bust this turn has no consequences
│   ├── EchoAction.cs            — immediate: refunds this face's power cost
│   └── PrecisionAction.cs       — queued prompt: player chooses to add power
└── DESIGN.md                    — this file
```

## Adding a New GameAction

1. Create a `[Serializable]` class implementing `IGameAction`
2. Add serialized fields for configuration (amount, etc.)
3. Implement `Execute(GameActionContext context)`:
   - For immediate: act directly on context
   - For turn-end: call `context.CombatManager.QueueTurnEndAction(ctx => ...)` — multiply values by `ctx.CombatManager.GetAppliedMultiplier()`
4. Assign it to a `DieFaceSO` via the Odin interface dropdown in the inspector

## Future: Status Effects

Persistent buffs/debuffs (poison stacks, bleed, etc.) that survive across turns are designed but not yet implemented. See `Assets/Scripts/TODO_StatusEffects.md` for the design. GameActions will be able to apply/remove status effects via the context once that system is built.
