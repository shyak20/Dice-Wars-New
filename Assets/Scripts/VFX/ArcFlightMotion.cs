using System;
using System.Collections;
using UnityEngine;

/// <summary>Shared coroutine helper for world-space arc flight along a moving target.</summary>
public static class ArcFlightMotion
{
    public static IEnumerator CoFly(
        Transform flyingTransform,
        Vector3 flightStartWorld,
        Transform target,
        ArcFlightSettings settings,
        Action onArrived = null)
    {
        if (flyingTransform == null)
            throw new ArgumentNullException(nameof(flyingTransform));
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        settings.Validate(nameof(ArcFlightMotion));

        var duration = settings.flyDuration;
        var elapsed = 0f;
        var hitSqr = settings.hitCloseDistanceWorld > 0f
            ? settings.hitCloseDistanceWorld * settings.hitCloseDistanceWorld
            : -1f;
        var previousPos = flightStartWorld;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var u = Mathf.Clamp01(elapsed / duration);
            var eased = Mathf.Clamp01(settings.flyCurve.Evaluate(u));
            var anchorPos = target.position + settings.targetWorldOffset;
            var pos = Vector3.LerpUnclamped(flightStartWorld, anchorPos, eased);
            pos += EvaluateArcOffset(flightStartWorld, anchorPos, settings, u);

            flyingTransform.position = pos;

            if (settings.faceTowardTarget)
            {
                var delta = pos - previousPos;
                if (delta.sqrMagnitude > 0.000001f)
                    flyingTransform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            }

            previousPos = pos;

            var curveComplete = eased >= 1f;
            var proximityHit = hitSqr > 0f
                && u >= settings.hitProximityMinNormalizedTime
                && (pos - anchorPos).sqrMagnitude <= hitSqr;

            if (curveComplete || proximityHit)
                break;

            yield return null;
        }

        flyingTransform.position = target.position + settings.targetWorldOffset;
        onArrived?.Invoke();
    }

    public static Vector3 EvaluateArcOffset(
        Vector3 startWorld,
        Vector3 endWorld,
        ArcFlightSettings settings,
        float normalizedTime)
    {
        if (settings.arcHeight == 0f || settings.arcCurve == null || settings.arcCurve.length == 0)
            return Vector3.zero;

        var arcMult = Mathf.Clamp01(settings.arcCurve.Evaluate(normalizedTime));
        var magnitude = arcMult * settings.arcHeight;

        return settings.arcAxis switch
        {
            ArcFlightSettings.ArcAxisMode.WorldUp => Vector3.up * magnitude,
            ArcFlightSettings.ArcAxisMode.PerpendicularHorizontal => ResolvePerpendicularHorizontal(startWorld, endWorld) * magnitude,
            ArcFlightSettings.ArcAxisMode.WorldX => Vector3.right * magnitude,
            _ => Vector3.up * magnitude,
        };
    }

    static Vector3 ResolvePerpendicularHorizontal(Vector3 startWorld, Vector3 endWorld)
    {
        var path = endWorld - startWorld;
        path.y = 0f;
        if (path.sqrMagnitude < 0.000001f)
            return Vector3.right;

        return Vector3.Cross(Vector3.up, path.normalized).normalized;
    }
}
