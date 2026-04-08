using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StatusEffectBarUI : MonoBehaviour
{
    [SerializeField] private Transform iconContainer;
    [SerializeField] private GameObject statusEffectIconPrefab;

    private readonly Dictionary<StatusEffectSO, StatusEffectIconUI> activeIcons = new();

    private StatusEffectManager trackedManager;

    private void Awake()
    {
        if (iconContainer == null)
            Debug.LogError($"StatusEffectBarUI on '{gameObject.name}': iconContainer is not assigned!");
        if (statusEffectIconPrefab == null)
            Debug.LogError($"StatusEffectBarUI on '{gameObject.name}': statusEffectIconPrefab is not assigned!");
    }

    public void Bind(StatusEffectManager manager)
    {
        if (trackedManager != null)
            trackedManager.OnEffectsChanged -= Refresh;

        trackedManager = manager;

        if (trackedManager == null)
        {
            Debug.LogError("StatusEffectBarUI: Bound to null StatusEffectManager!");
            return;
        }

        trackedManager.OnEffectsChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (trackedManager != null)
            trackedManager.OnEffectsChanged -= Refresh;
    }

    private void Refresh()
    {
        if (trackedManager == null) return;

        // Clean up entries whose icon objects were destroyed externally.
        var nullIconKeys = activeIcons
            .Where(kvp => kvp.Value == null)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var key in nullIconKeys)
            activeIcons.Remove(key);

        var currentEffects = new HashSet<StatusEffectSO>();

        foreach (var effect in trackedManager.Effects)
        {
            var definition = effect.Definition;
            currentEffects.Add(definition);

            if (activeIcons.TryGetValue(definition, out var existingIcon) && existingIcon != null)
            {
                if (!existingIcon.gameObject.activeSelf)
                    existingIcon.gameObject.SetActive(true);
                existingIcon.RefreshVisual(effect);
            }
            else
            {
                var iconObj = Instantiate(statusEffectIconPrefab, iconContainer);
                var iconUI = iconObj.GetComponent<StatusEffectIconUI>();

                if (iconUI == null)
                {
                    Debug.LogError("StatusEffectBarUI: Prefab missing StatusEffectIconUI component!");
                    Destroy(iconObj);
                    return;
                }

                iconUI.Setup(effect);
                activeIcons[definition] = iconUI;
            }
        }

        // Keep created icons; hide when effect is currently inactive.
        var staleKeys = activeIcons.Keys.Where(k => !currentEffects.Contains(k)).ToList();
        foreach (var key in staleKeys)
        {
            if (activeIcons.TryGetValue(key, out var iconUi) && iconUi != null)
                iconUi.gameObject.SetActive(false);
        }
    }
}
