using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Clears leftover combat popups/indicators when a new fight starts (e.g. same scene reactivated after map).</summary>
public static class FightScenePresentationCleanup
{
    public static void Apply(Scene fightScene)
    {
        if (!fightScene.IsValid())
            return;

        FloatingDamageNumberSpawner.DestroyAllInstancesInScene(fightScene);

        var goldFloaters = Object.FindObjectsOfType<GoldPopupFloater>(true);
        for (var i = 0; i < goldFloaters.Length; i++)
        {
            var g = goldFloaters[i];
            if (g != null && g.gameObject.scene == fightScene)
                Object.Destroy(g.gameObject);
        }

        var enemyPresentations = Object.FindObjectsOfType<EnemyCombatPresentationController>(true);
        for (var i = 0; i < enemyPresentations.Length; i++)
        {
            var p = enemyPresentations[i];
            if (p != null && p.gameObject.scene == fightScene)
                p.ResetTransientDamagePresentation();
        }

        var playerOrb = Object.FindObjectsOfType<PlayerPowerOrbImpactPresentation>(true);
        for (var i = 0; i < playerOrb.Length; i++)
        {
            var p = playerOrb[i];
            if (p != null && p.gameObject.scene == fightScene)
                p.ResetTransientOrbPresentation();
        }
    }
}
