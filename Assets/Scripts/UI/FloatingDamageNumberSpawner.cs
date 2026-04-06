using UnityEngine;

/// <summary>Shared world → canvas spawn for <see cref="PlayerFloatingDamageNumberController"/> and <see cref="EnemyFloatingDamageNumberController"/>.</summary>
public static class FloatingDamageNumberSpawner
{
    public static void Spawn(
        Component context,
        int amount,
        Vector3 worldPosition,
        Canvas canvas,
        RectTransform spawnParent,
        Camera worldCamera,
        FloatingDamageNumberStyle style)
    {
        if (amount <= 0 || style.prefab == null || canvas == null || spawnParent == null) return;

        Camera eventCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera != null ? canvas.worldCamera : worldCamera;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogError($"{(context != null ? context.name : "FloatingDamageNumber")}: Assign worldCamera (or tag MainCamera).");
            return;
        }

        Vector3 screen = cam.WorldToScreenPoint(worldPosition);
        if (screen.z <= 0f)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(spawnParent, screen, eventCam, out Vector2 anchoredInParent))
            return;

        GameObject go = Object.Instantiate(style.prefab, spawnParent);
        var instance = go.GetComponent<FloatingDamageNumberInstance>();
        if (instance == null)
        {
            Debug.LogError("Floating damage prefab needs FloatingDamageNumberInstance on the root.");
            Object.Destroy(go);
            return;
        }

        instance.Begin(
            amount,
            anchoredInParent,
            style.textColor,
            style.duration,
            style.fadeStartNormalized,
            style.horizontalAmplitudeMinMax,
            style.verticalFallMinMax,
            style.horizontalMotionGraph,
            style.verticalMotionGraph);
    }
}
