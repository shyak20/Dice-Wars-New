# Status Effects System — Design Notes

## Overview

Persistent buffs/debuffs that last beyond a single face landing. GameActions can apply/remove status effects.

## Two Persistence Levels

- **Turn effects** — cleared at turn end (e.g., "This turn, gain +1 Armor for every die roll")
- **Battle effects** — persist until removed or battle ends (e.g., poison stacks, bleed)

## Core Types

### IStatusEffect (interface)
- `OnApply(context, stacks)` — when first applied or stacks added
- `OnTick(context, stacks)` — called at turn boundaries
- `OnRemove(context)` — cleanup when removed

### StatusEffectInstance (runtime wrapper)
- `IStatusEffect Effect`
- `int Stacks`
- `int TurnsRemaining` (-1 for permanent until removed)
- `StatusDuration Duration` (Turn or Battle)

### StatusEffectManager (MonoBehaviour)
- Holds two lists: turnEffects, battleEffects
- `ApplyStatus(effect, stacks)` — add or stack
- `RemoveStatus<T>()` — remove by type
- `GetStacks<T>()` — query stack count
- `TickTurnStart(context)` / `TickTurnEnd(context)`
- `ClearTurnEffects()`

## Integration Points

- CombatManager calls `TickTurnStart` at turn begin, `TickTurnEnd` at turn end
- GameActions call `StatusEffectManager.ApplyStatus()` via context
- GameActionContext needs a `StatusEffectManager` reference added

## Example Status Effects

- **Poison**: Battle duration, stackable. OnTick deals stacks damage, reduces 1 stack per turn.
- **BustProtection**: Turn duration. Modifies bust check to not cancel anything.
- **ArmorPerRoll**: Turn duration. Listens to roll events, adds armor per roll.
