using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows icons for <see cref="GameActionWithIcon"/> when a face resolves (actions with <see cref="IGameAction.ActivateImmediately"/> true).
/// Wire next to <see cref="StatusEffectBarUI"/> on the player HUD.
/// </summary>
public class ImmediateGameActionBarUI : MonoBehaviour
{
    [SerializeField] private Transform iconContainer;
    [SerializeField] private GameObject iconPrefab;

    private void Awake()
    {
        if (iconContainer == null)
            Debug.LogError($"ImmediateGameActionBarUI on '{name}': assign iconContainer.");
        if (iconPrefab == null)
            Debug.LogError($"ImmediateGameActionBarUI on '{name}': assign iconPrefab (Image root).");
    }

    private void OnEnable()
    {
        CombatEvents.OnImmediateGameActionIconsShown += OnIconsShown;
        CombatEvents.OnImmediateGameActionBarClear += Clear;
    }

    private void OnDisable()
    {
        CombatEvents.OnImmediateGameActionIconsShown -= OnIconsShown;
        CombatEvents.OnImmediateGameActionBarClear -= Clear;
    }

    private void OnIconsShown(IReadOnlyList<Sprite> sprites)
    {
        if (iconContainer == null || iconPrefab == null || sprites == null || sprites.Count == 0)
            return;

        foreach (var sprite in sprites)
        {
            if (sprite == null) continue;
            var go = Instantiate(iconPrefab, iconContainer);
            var img = go.GetComponent<Image>();
            if (img == null)
                img = go.GetComponentInChildren<Image>();
            if (img != null)
            {
                img.sprite = sprite;
                img.enabled = true;
            }
        }
    }

    private void Clear()
    {
        if (iconContainer == null) return;
        foreach (Transform c in iconContainer)
            Destroy(c.gameObject);
    }
}
