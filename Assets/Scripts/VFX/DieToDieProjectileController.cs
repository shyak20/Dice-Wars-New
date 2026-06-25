using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns die-to-die projectile prefabs in FightScene and flies them in parallel to target dice.
/// </summary>
/// <remarks>
/// FightScene setup:
/// 1. Create empty GameObject "Die To Die Projectiles" and add this component.
/// 2. On CombatManager, assign this controller and a default projectile prefab.
/// 3. Create a projectile prefab (e.g. under Assets/Prefabs/VFX/) with
///    <see cref="DieToDieProjectileFlight"/> on the root plus optional child VFX
///    wired to activateOnLaunch / activateOnArrival.
/// 4. Optionally assign per-action prefabs on RerollDieAction, RerollOtherDiceAfterAllSettledAction,
///    or gem effect rows (RandomBatchRerollOtherDiceNoPower).
/// </remarks>
public sealed class DieToDieProjectileController : MonoBehaviour
{
    public IEnumerator LaunchAndWait(
        Transform sourceDie,
        IReadOnlyList<Transform> targetDice,
        GameObject projectilePrefab,
        Action<int> onTargetArrived = null)
    {
        if (sourceDie == null)
            throw new InvalidOperationException($"{nameof(DieToDieProjectileController)}: sourceDie is null.");
        if (targetDice == null || targetDice.Count == 0)
            yield break;
        if (projectilePrefab == null)
            throw new InvalidOperationException($"{nameof(DieToDieProjectileController)}: projectilePrefab is null.");

        var templateFlight = projectilePrefab.GetComponent<DieToDieProjectileFlight>();
        if (templateFlight == null)
            throw new InvalidOperationException(
                $"{nameof(DieToDieProjectileController)}: prefab '{projectilePrefab.name}' is missing {nameof(DieToDieProjectileFlight)}.");

        var sourceAnchor = sourceDie.position + templateFlight.FlightSettings.sourceWorldOffset;
        var remaining = 0;

        for (var i = 0; i < targetDice.Count; i++)
        {
            var target = targetDice[i];
            if (target == null)
                throw new InvalidOperationException($"{nameof(DieToDieProjectileController)}: target at index {i} is null.");

            remaining++;
            var targetIndex = i;
            StartCoroutine(CoFlySingleProjectile(sourceAnchor, target, projectilePrefab, () =>
            {
                onTargetArrived?.Invoke(targetIndex);
                remaining--;
            }));
        }

        yield return new WaitUntil(() => remaining <= 0);
    }

    IEnumerator CoFlySingleProjectile(
        Vector3 sourceAnchor,
        Transform target,
        GameObject projectilePrefab,
        Action onComplete)
    {
        var instance = Instantiate(projectilePrefab, sourceAnchor, Quaternion.identity, transform);
        var flight = instance.GetComponent<DieToDieProjectileFlight>();
        if (flight == null)
        {
            Destroy(instance);
            throw new InvalidOperationException(
                $"{nameof(DieToDieProjectileController)}: spawned instance of '{projectilePrefab.name}' is missing {nameof(DieToDieProjectileFlight)}.");
        }

        yield return flight.CoFlyToTarget(target, onComplete);

        if (flight.DestroyDelaySeconds > 0f)
            yield return new WaitForSeconds(flight.DestroyDelaySeconds);

        Destroy(instance);
    }
}
