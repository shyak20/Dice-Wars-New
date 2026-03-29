# GameAction & Effects System

## Overview

Modular action system for dice face effects, buffs, and debuffs. GameActions are source-agnostic — triggered by dice faces now, but designed for reuse by potions, relics, or any future system.

## How to Add a New Ability

1. Create a `[Serializable]` class implementing `IGameAction` in `Assets/Scripts/Effects/GameActions/`
2. Add `[SerializeField]` fields for configuration (amount, etc.)
3. Implement `Execute(GameActionContext context)` — pick the right pattern below
4. Add a debug log gated by `GameActionDebug.Enabled`
5. Assign it to a `DieFaceSO` via the Odin `[SerializeReference]` dropdown in the inspector

### Ability Patterns

**Immediate** — acts directly in `Execute`. The simplest pattern.
```csharp
[Serializable]
public class MyAction : IGameAction
{
    [SerializeField] private int amount = 1;

    public void Execute(GameActionContext context)
    {
        // Act directly on context
        context.CombatManager.SomeMethod(amount);
        if (GameActionDebug.Enabled)
            Debug.Log($"[MyAction] Did something: {amount}");
    }
}
```
Examples: `OverchargeAction`, `SafetyNetAction`, `EchoAction`

**Turn-end** — queues work for turn end via `QueueTurnEndAction`. The callback receives a `GameActionContext` so it can read final turn state. **Must multiply values by `GetAppliedMultiplier()`** to scale with Perfect Strike + Overcharge.
```csharp
public void Execute(GameActionContext context)
{
    var baseAmount = amount;
    context.CombatManager.QueueTurnEndAction(ctx =>
    {
        var final = baseAmount * ctx.CombatManager.GetAppliedMultiplier();
        // Apply final value
    });
}
```
Examples: `HealAction`

**Player-prompt** — requires player input after dice settle but before bust check. Uses CombatManager's precision queue. CombatManager calls `PrecisionPanel.Show(amount, callback)` directly — no events.
```csharp
public void Execute(GameActionContext context)
{
    context.CombatManager.QueuePrecisionChoice(amount);
}
```
The queue is processed after all dice settle, before `CheckBustStatus`. Each prompt pauses the flow, shows a UI panel, and continues the queue on response.
Examples: `PrecisionAction`

## Key Classes

### IGameAction (`Effects/IGameAction.cs`)
```csharp
public interface IGameAction { void Execute(GameActionContext context); }
```
No metadata on the interface — name, description, timing are implementation details.

### GameActionDebug (`Effects/IGameAction.cs`)
```csharp
public static class GameActionDebug { public const bool Enabled = true; }
```
Set to `false` to silence all action debug logs.

### GameActionContext (`Effects/GameActionContext.cs`)
Passed to every action's `Execute`. Holds:
- `CombatManager` — queue actions, read multiplier, refund power, etc.
- `Player` (PlayerStatus) — heal, damage, armor
- `Enemy` (EnemyController) — deal damage, read state
- `ChanneledFaces` (List\<FaceResult\>) — all face results this turn
- `TriggeringFace` (FaceResult) — the face that triggered this action (null if not face-triggered)

### FaceResult (`Effects/FaceResult.cs`)
Individual dice face landing result:
- `Face` (DieFaceSO), `Value` (int), `Type` (DieType), `Action` (IGameAction, nullable)

### PrecisionPanel (`UI/PrecisionPanel.cs`)
MonoBehaviour for player-prompt popups. `Show(int amount, Action<bool> onResult)` — displays panel, calls back with player's choice. Referenced directly by CombatManager (no events).

## CombatManager API for Actions

Methods actions can call via `context.CombatManager`:

| Method | Pattern | Description |
|--------|---------|-------------|
| `QueueTurnEndAction(Action<GameActionContext>)` | Turn-end | Queue work for turn end |
| `GetAppliedMultiplier()` | Turn-end | Get Perfect Strike multiplier (1 normally, 2+ on strike) |
| `AddOvercharge(int)` | Immediate | Add to Perfect Strike multiplier |
| `SetBustProtected()` | Immediate | Prevent bust consequences this turn |
| `SetImmune()` | Immediate | Cap all enemy damage to 1 this turn |
| `AddThorns(int)` | Immediate | Add thorns — enemy takes N damage per attack this turn (stacks) |
| `ActivateKineticShield()` | Immediate | +1 bonus armor per subsequent die settled this turn |
| `RefundPower(int)` | Immediate | Subtract from current power |
| `QueuePrecisionChoice(int)` | Player-prompt | Queue a power-add prompt |

## Turn Flow

1. **Die settles** → `CombatManager.ResolveRollResult(face)`
2. Create `FaceResult`, add to `channeledFaces`
3. `currentPower += face.value`
4. If face has action → build `GameActionContext`, call `action.Execute(context)`
5. Fire UI events with derived totals
6. All dice settled → `ProcessPrecisionQueue` (prompt player for each queued choice)
7. All prompts resolved → `CheckBustStatus`
   - Perfect Strike (power == max): multiply all values by `appliedMultiplier`
   - Bust (power > max): if `bustProtected` → submit normally, else player chooses pool to nullify
   - Under: continue rolling or auto-submit if no rolls remain
8. `SubmitTurn` → process turn-end action queue → derive final totals → apply damage/armor
9. `ResetTurn` → clear all per-turn state

## Channeled Faces

Totals are derived from the list, not accumulated:
```
GetPendingAttack() = channeledFaces.Where(Attack).Sum(Value)
GetPendingDefense() = channeledFaces.Where(Defense).Sum(Value)
```

## Existing Abilities

| Action | Pattern | Description |
|--------|---------|-------------|
| `HealAction` | Turn-end | Heals player HP × appliedMultiplier |
| `OverchargeAction` | Immediate | Adds to Perfect Strike multiplier |
| `SafetyNetAction` | Immediate | Bust has no consequences this turn |
| `EchoAction` | Immediate | Refunds this face's power cost |
| `PrecisionAction` | Player-prompt | Player chooses to add X power |
| `ImmuneAction` | Immediate | Enemy attacks deal max 1 damage this turn |
| `ThornsAction` | Immediate | Enemy takes 1 damage per attack this turn (stacks) |
| `KineticShieldAction` | Immediate | +1 bonus armor for every die that settles after this one, this turn |

## File Structure

```
Assets/Scripts/Effects/
├── IGameAction.cs              — interface + GameActionDebug
├── GameActionContext.cs         — context object
├── FaceResult.cs                — individual face result
├── GameActions/                 — concrete action implementations
│   ├── HealAction.cs
│   ├── OverchargeAction.cs
│   ├── SafetyNetAction.cs
│   ├── EchoAction.cs
│   ├── ImmuneAction.cs
│   ├── ThornsAction.cs
│   ├── PrecisionAction.cs
│   ├── KineticShieldAction.cs       — +1 armor per subsequent die this turn
│   ├── ApplyStatusEffectAction.cs  — applies any status effect (configurable)
│   └── CleanseAction.cs            — removes all debuffs from player
├── StatusEffects/               — status effect system
│   ├── StatusEffectSO.cs        — abstract base ScriptableObject
│   ├── StatusEffectInstance.cs  — runtime stack wrapper
│   ├── StatusEffectContext.cs   — context for status hooks
│   ├── StatusEffectManager.cs   — MonoBehaviour managing effect instances
│   ├── StatusEffectEnums.cs     — StatusEffectType, StatusEffectTarget
│   └── Definitions/
│       ├── ShadowEffectSO.cs
│       ├── PoisonEffectSO.cs
│       ├── BleedEffectSO.cs
│       ├── ChillEffectSO.cs
│       ├── FrozenEffectSO.cs
│       ├── ShatteredEffectSO.cs
│       └── ConfusionEffectSO.cs
└── DESIGN.md                    — this file

Assets/Scripts/UI/
└── PrecisionPanel.cs            — player-prompt popup

Assets/Data/StatusEffects/       — SO assets for each effect
```

---

## Status Effect System

Persistent cross-turn buffs/debuffs with stacking. Per-turn flags (immune, thorns, bustProtected) are NOT status effects — those stay on CombatManager.

### Architecture

**`StatusEffectSO`** (abstract ScriptableObject) — base for all effect definitions.
- Fields: `effectName`, `icon`, `description`, `type` (Buff/Debuff), `target` (Player/Enemy), `stackDecayPerTurn`
- Virtual hooks: `OnApply`, `OnTurnStart`, `OnBeforeEnemyTurn`, `ModifyEnemyHitDamage`, `ShouldRedirectAttackToSelf`, `OnAfterEnemyTurn`, `OnPerfectStrike`, `ModifyDamageToOwner`, `GetBonusAttack`, `OnRemove`

**`StatusEffectInstance`** — runtime wrapper: `Definition` + `Stacks`. Auto-removed when stacks reach 0.

**`StatusEffectManager`** (MonoBehaviour) — lives on both PlayerStatus and EnemyController GameObjects. Manages a list of instances, provides `ApplyStatus`, `RemoveStatus`, `GetStacks`, `ClearDebuffs`, and tick methods.

### Integration with CombatManager

| Timing | Hook | Example |
|--------|------|---------|
| Turn start (in ResetTurn) | `TickTurnStart` — applies `stackDecayPerTurn`, removes expired | Chill loses 5 stacks |
| SubmitTurn — attack calc | `GetTotalBonusAttack` + `ApplyDamageModifiers` | Shadow +1/stack, Frozen +20%/stack |
| Perfect Strike | `TickPerfectStrike` | Bleed deals stacks as damage |
| Before enemy attacks | `TickBeforeEnemyTurn` | Poison deals 1×stacks |
| Per enemy hit | `ModifyEnemyHitDamage` | Chill reduces by stacks |
| Per enemy hit | `CheckRedirectAttackToSelf` | Confusion redirects to self |
| After enemy attacks | `TickAfterEnemyTurn` | Bleed deals 2×stacks |

### Applying Status Effects from Dice

Use `ApplyStatusEffectAction` (a generic `IGameAction`) — assign a `StatusEffectSO` reference and stack count. Target is read from the SO's `target` field.

### Existing Status Effects

| Effect | Type | Target | Decay | Behavior |
|--------|------|--------|-------|----------|
| Shadow | Buff | Player | 0 | +1 attack per stack at submission |
| Poison | Debuff | Enemy | 0 | 1×stacks damage before enemy turn (ignores armor) |
| Bleed | Debuff | Enemy | 1/turn | 2×stacks after enemy turn; stacks as damage on perfect strike |
| Chill | Debuff | Enemy | 5/turn | -1 damage per hit per stack; adds Frozen at threshold |
| Frozen | Debuff | Enemy | 0 | +20% damage to owner per stack |
| Shattered | Debuff | Enemy | 1/turn | -X% enemy attack damage; stacks = duration, not intensity |
| Confusion | Debuff | Enemy | 0 | X% per stack chance enemy hits itself instead of player |
