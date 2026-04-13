using UnityEngine;

/// <summary>Shared world → canvas spawn for <see cref="PlayerFloatingDamageNumberController"/> and <see cref="EnemyFloatingDamageNumberController"/>.</summary>
public static class FloatingDamageNumberSpawner
{
    /// <param name="spawnAtSpawnParentCenter">
    /// When true, ignores <paramref name="worldPosition"/> and places the instance at the spawn parent's local center
    /// (<c>anchoredPosition</c> 0,0). Use for HUD regions where world projection does not match the layout.
    /// </param>
    public static void Spawn(
        Component context,
        int amount,
        Vector3 worldPosition,
        Canvas canvas,
        RectTransform spawnParent,
        Camera worldCamera,
        FloatingDamageNumberStyle style,
        bool spawnAtSpawnParentCenter = false)
    {
        if (amount <= 0 || style.prefab == null || canvas == null || spawnParent == null) return;

        Camera eventCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera != null ? canvas.worldCamera : worldCamera;

        Vector2 anchoredInParent;
        if (spawnAtSpawnParentCenter)
        {
            anchoredInParent = Vector2.zero;
        }
        else
        {
            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null)
            {
                Debug.LogError($"{(context != null ? context.name : "FloatingDamageNumber")}: Assign worldCamera (or tag MainCamera).");
                return;
            }

            Vector3 screen = cam.WorldToScreenPoint(worldPosition);
            if (screen.z <= 0f)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(spawnParent, screen, eventCam, out anchoredInParent))
                return;
        }

        GameObject go = Object.Instantiate(style.prefab, spawnParent);
        var instance = go.GetComponent<FloatingDamageNumberInstance>();
        if (instance == null)
        {
            Debug.LogError("Floating damage prefab needs FloatingDamageNumberInstance on the root.");
            Object.Destroy(go);
            return;
        }

        var rt = go.GetComponent<RectTransform>();
        if (rt != null && spawnAtSpawnParentCenter)
        {
            rt.localPosition = Vector3.zero;
            rt.localRotation = Quaternion.identity;
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
