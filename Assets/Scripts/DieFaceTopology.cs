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
        TryFindTopFaceIndex(dieTransform, out var faceIndex, out _);
        return faceIndex;
    }

    /// <summary>
    /// Face index whose outward normal best aligns with world up, plus alignment score
    /// (1 = flat face up, ~0.71 = edge, ~0.58 = vertex on a cube).
    /// </summary>
    public static bool TryFindTopFaceIndex(Transform dieTransform, out int faceIndex, out float upDot)
    {
        faceIndex = 0;
        upDot = -1f;
        if (dieTransform == null)
            return false;

        for (var i = 0; i < FaceCount; i++)
        {
            var worldFaceDir = dieTransform.TransformDirection(LocalFaceDirections[i]);
            var dot = Vector3.Dot(worldFaceDir, Vector3.up);
            if (dot > upDot)
            {
                upDot = dot;
                faceIndex = i;
            }
        }

        return true;
    }

    /// <summary>Rotation that aligns <paramref name="faceIndex"/>'s outward normal with world up.</summary>
    public static bool TryGetRotationSnapToFaceUp(Transform dieTransform, int faceIndex, out Quaternion snappedRotation)
    {
        snappedRotation = dieTransform != null ? dieTransform.rotation : Quaternion.identity;
        if (dieTransform == null || faceIndex < 0 || faceIndex >= FaceCount)
            return false;

        var worldFaceDir = dieTransform.TransformDirection(LocalFaceDirections[faceIndex]);
        if (worldFaceDir.sqrMagnitude < 1e-8f)
            return false;

        var align = Quaternion.FromToRotation(worldFaceDir, Vector3.up);
        snappedRotation = align * dieTransform.rotation;
        return true;
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
