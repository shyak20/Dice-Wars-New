using System;
using UnityEngine;

/// <summary>Inspector-tunable arc flight parameters for world-space projectiles.</summary>
[Serializable]
public struct ArcFlightSettings
{
    public enum ArcAxisMode
    {
        WorldUp,
        PerpendicularHorizontal,
        /// <summary>Legacy power-orb offset along world +X (used by <see cref="PowerReactiveEffectController"/>).</summary>
        WorldX,
    }

    [Tooltip("Seconds to reach the target (scaled time via Time.deltaTime).")]
    [Min(0.01f)]
    public float flyDuration;

    [Tooltip("X = normalized flight time (0–1). Y = blend along the path from start to target (0 = start, 1 = hit).")]
    public AnimationCurve flyCurve;

    [Tooltip("World-units arc magnitude. Set 0 to disable.")]
    public float arcHeight;

    [Tooltip("X = normalized flight time (0–1). Y = arc multiplier.")]
    public AnimationCurve arcCurve;

    public ArcAxisMode arcAxis;

    [Tooltip("Offset applied to the source anchor in world space.")]
    public Vector3 sourceWorldOffset;

    [Tooltip("Offset applied to the target anchor in world space.")]
    public Vector3 targetWorldOffset;

    [Tooltip("When enabled, rotates the flying transform to face movement each frame.")]
    public bool faceTowardTarget;

    [Tooltip("When > 0, arrival triggers when within this world distance of the target (after hitProximityMinNormalizedTime).")]
    [Min(0f)]
    public float hitCloseDistanceWorld;

    [Tooltip("Proximity hit only after this fraction of flyDuration has elapsed (0–1).")]
    [Range(0f, 1f)]
    public float hitProximityMinNormalizedTime;

    public static ArcFlightSettings CreateDefault()
    {
        return new ArcFlightSettings
        {
            flyDuration = 0.45f,
            flyCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
            arcHeight = 0.35f,
            arcCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 0f)),
            arcAxis = ArcAxisMode.WorldUp,
            sourceWorldOffset = new Vector3(0f, 0.12f, 0f),
            targetWorldOffset = new Vector3(0f, 0.12f, 0f),
            faceTowardTarget = true,
            hitCloseDistanceWorld = 0.12f,
            hitProximityMinNormalizedTime = 0.82f,
        };
    }

    public void Validate(string contextLabel)
    {
        if (flyDuration <= 0f)
            throw new InvalidOperationException($"{contextLabel}: flyDuration must be > 0.");

        if (flyCurve == null || flyCurve.length == 0)
            throw new InvalidOperationException($"{contextLabel}: flyCurve must have at least one key.");

        if (arcHeight != 0f && (arcCurve == null || arcCurve.length == 0))
            throw new InvalidOperationException($"{contextLabel}: arcCurve must have at least one key when arcHeight is non-zero.");
    }
}
