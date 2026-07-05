using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Clears leftover map hit flash and floating damage when the map is bootstrapped or restored after a sub-scene.</summary>
public static class MapScenePresentationCleanup
{
    public static void Apply(Scene mapScene)
    {
        if (!mapScene.IsValid())
            return;

        FloatingDamageNumberSpawner.DestroyAllInstancesInScene(mapScene);

        var hitFeedback = Object.FindObjectsOfType<MapRunHitFeedback>(true);
        for (var i = 0; i < hitFeedback.Length; i++)
        {
            var feedback = hitFeedback[i];
            if (feedback != null && feedback.gameObject.scene == mapScene)
                feedback.ResetTransientPresentation();
        }
    }
}
