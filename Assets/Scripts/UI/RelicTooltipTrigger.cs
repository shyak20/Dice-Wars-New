using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hover target for a single <see cref="RelicSO"/>. Requires a <see cref="Graphic"/> with Raycast Target enabled.
/// Calls <see cref="RelicTooltipUI"/> (optional override or <see cref="RelicTooltipUI.Instance"/>).
/// </summary>
[RequireComponent(typeof(Graphic))]
public sealed class RelicTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RelicTooltipUI tooltipOverride;
    [SerializeField] private bool showTooltipAboveReference;
    [SerializeField] private Vector2 tooltipScreenOffset;

    private RelicSO _relic;
    private Graphic _graphic;

    private void Awake()
    {
        _graphic = GetComponent<Graphic>();
        if (_graphic == null)
        {
            Debug.LogError("RelicTooltipTrigger: missing Graphic (RequireComponent should prevent this).", this);
            return;
        }

        if (!_graphic.raycastTarget)
            Debug.LogError($"RelicTooltipTrigger on '{name}': Graphic raycastTarget must be enabled for hover.", this);
    }

    public void SetRelic(RelicSO relic) => _relic = relic;
    public void ConfigurePositioning(bool showAboveReference, Vector2 screenOffset)
    {
        showTooltipAboveReference = showAboveReference;
        tooltipScreenOffset = screenOffset;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_relic == null) return;
        var t = Resolve();
        if (t == null)
        {
            Debug.LogError("RelicTooltipTrigger: no RelicTooltipUI in scene. Add RelicTooltipUI under your Canvas.", this);
            return;
        }

        t.Show(_relic, _graphic.rectTransform, showTooltipAboveReference, tooltipScreenOffset);
    }

    public void OnPointerExit(PointerEventData eventData) => Resolve()?.Hide();

    private RelicTooltipUI Resolve()
    {
        if (tooltipOverride != null)
            return tooltipOverride;
        if (RelicTooltipUI.Instance != null)
            return RelicTooltipUI.Instance;

        // Fallback for cases where singleton was not initialized yet (inactive UI root, scene load order).
        var found = FindObjectOfType<RelicTooltipUI>(true);
        if (found != null)
            tooltipOverride = found;
        return found;
    }
}
