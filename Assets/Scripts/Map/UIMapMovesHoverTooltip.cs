using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Shows <see cref="HoverTooltipPanelUI"/> on hover with <see cref="UIMapMoveCounterUI"/> description formatted using live map overflow values.</summary>
public sealed class UIMapMovesHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private HoverTooltipPanelUI _panel;
    private MapMovementManager _manager;
    private string _title;
    private string _descriptionFormat;

    public void Initialize(
        HoverTooltipPanelUI panel,
        MapMovementManager manager,
        string title,
        string descriptionFormat)
    {
        _panel = panel;
        _manager = manager;
        _title = title ?? string.Empty;
        _descriptionFormat = descriptionFormat ?? string.Empty;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_panel == null)
            return;
        if (string.IsNullOrWhiteSpace(_title) && string.IsNullOrWhiteSpace(_descriptionFormat))
            return;

        var nextDamage = _manager != null ? _manager.GetCorruptionDamageForNextStep() : 0;
        var increase = _manager != null ? _manager.OverflowDamageIncreasePerMove : 0;
        var description = string.Format(_descriptionFormat, nextDamage, increase);
        _panel.Show(_title, description);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_panel != null)
            _panel.Hide();
    }

    private void OnDisable()
    {
        if (_panel != null)
            _panel.Hide();
    }
}
