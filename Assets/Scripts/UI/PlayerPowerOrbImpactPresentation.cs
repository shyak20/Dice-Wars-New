using System.Collections;
using UnityEngine;

/// <summary>
/// Optional: flashes an object when the power orb flies to the player support anchor (no enemy damage line this turn).
/// Place on the player / HUD rig; wire an inactive indicator.
/// </summary>
public sealed class PlayerPowerOrbImpactPresentation : MonoBehaviour
{
    [SerializeField] private GameObject orbImpactIndicator;
    [SerializeField, Min(0.02f)] private float displaySeconds = 0.12f;

    private void OnEnable() => CombatEvents.OnPowerOrbImpact += OnOrbImpact;
    private void OnDisable() => CombatEvents.OnPowerOrbImpact -= OnOrbImpact;

    private void OnOrbImpact(PowerOrbImpactPayload payload)
    {
        if (payload.Target != PowerOrbImpactTarget.PlayerSupport) return;
        StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        if (orbImpactIndicator == null) yield break;
        orbImpactIndicator.SetActive(true);
        yield return new WaitForSecondsRealtime(displaySeconds);
        orbImpactIndicator.SetActive(false);
    }
}
