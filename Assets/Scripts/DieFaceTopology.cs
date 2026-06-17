using UnityEngine;

/// <summary>
/// Submesh / face-index mapping for the imported die mesh (must match <see cref="DiceRoller"/>).
/// </summary>
public static class DieFaceTopology
{
    public const int FaceCount = 6;

    static readonly Vector3[] LocalFaceDirections =
    {
        Vector3.up,      // 0: Face 1
        Vector3.down,    // 1: Face 6
        Vector3.right,   // 2: Face 2
        Vector3.left,    // 3: Face 5
        Vector3.forward, // 4: Face 3
        Vector3.back     // 5: Face 4
    };

    public static Vector3 GetLocalFaceDirection(int faceIndex)
    {
        if (faceIndex < 0 || faceIndex >= FaceCount)
            throw new System.ArgumentOutOfRangeException(nameof(faceIndex), faceIndex, $"Face index must be 0..{FaceCount - 1}.");
        return LocalFaceDirections[faceIndex];
    }

    /// <summary>Face index whose outward normal best aligns with world up (settled top face).</summary>
    public static int FindTopFaceIndex(Transform dieTransform)
    {
        var bestDot = -1f;
        var closestIndex = 0;
        for (var i = 0; i < FaceCount; i++)
        {
            var worldFaceDir = dieTransform.TransformDirection(LocalFaceDirections[i]);
            var dot = Vector3.Dot(worldFaceDir, Vector3.up);
            if (dot > bestDot)
            {
                bestDot = dot;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    public static Vector3 GetFaceWorldNormal(Transform dieTransform, int faceIndex) =>
        dieTransform.TransformDirection(GetLocalFaceDirection(faceIndex).normalized);

    /// <summary>Approximate center of the face on the die mesh (world space).</summary>
    public static Vector3 GetFaceWorldPosition(Transform dieTransform, MeshRenderer meshRenderer, int faceIndex)
    {
        var worldDir = GetFaceWorldNormal(dieTransform, faceIndex);
        if (meshRenderer != null)
        {
            var bounds = meshRenderer.bounds;
            var e = bounds.extents;
            var absDir = new Vector3(Mathf.Abs(worldDir.x), Mathf.Abs(worldDir.y), Mathf.Abs(worldDir.z));
            var offset = absDir.x * e.x + absDir.y * e.y + absDir.z * e.z;
            return bounds.center + worldDir * offset;
        }

        return dieTransform.position + worldDir * 0.3f;
    }
}
