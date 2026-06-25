using System.Collections;
using UnityEngine;

/// <summary>
/// Root component on a die-to-die projectile prefab. Holds arc settings and optional child effect hooks.
/// </summary>
public sealed class DieToDieProjectileFlight : MonoBehaviour
{
    [SerializeField] private ArcFlightSettings flightSettings;

    [Header("Child effect hooks")]
    [Tooltip("Activated when the projectile launches.")]
    [SerializeField] private GameObject[] activateOnLaunch;

    [Tooltip("Activated when the projectile arrives at its target.")]
    [SerializeField] private GameObject[] activateOnArrival;

    [Tooltip("Seconds to wait after arrival before this projectile root is destroyed (lets hit VFX play).")]
    [SerializeField, Min(0f)] private float destroyDelaySeconds = 0.35f;

    public ArcFlightSettings FlightSettings => flightSettings;

    public float DestroyDelaySeconds => destroyDelaySeconds;

    private void Awake()
    {
        if (flightSettings.flyDuration <= 0f)
            flightSettings = ArcFlightSettings.CreateDefault();

        flightSettings.Validate($"{nameof(DieToDieProjectileFlight)} on '{name}'");
    }

    private void Reset()
    {
        flightSettings = ArcFlightSettings.CreateDefault();
    }

    public void PlayLaunch()
    {
        SetActiveArray(activateOnLaunch, true);
    }

    public void PlayArrival()
    {
        SetActiveArray(activateOnArrival, true);
    }

    public IEnumerator CoFlyToTarget(Transform target, System.Action onArrived = null)
    {
        if (target == null)
            throw new System.InvalidOperationException($"{nameof(DieToDieProjectileFlight)} on '{name}': target is null.");

        PlayLaunch();

        var startWorld = transform.position;
        yield return ArcFlightMotion.CoFly(transform, startWorld, target, flightSettings, () =>
        {
            PlayArrival();
            onArrived?.Invoke();
        });
    }

    static void SetActiveArray(GameObject[] objects, bool active)
    {
        if (objects == null)
            return;

        for (var i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                objects[i].SetActive(active);
        }
    }
}
