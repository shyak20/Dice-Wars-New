using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Shows hover text via <see cref="HoverTooltipManager"/> with <see cref="UIMapMoveCounterUI"/> description formatted using live map overflow values.
/// </summary>
public sealed class UIMapMovesHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    RectTransform _anchorRect;
    private MapMovementManager _manager;
    private string _title;
    private string _descriptionFormat;
    private Vector2 _screenOffset;

    public void Initialize(
        RectTransform anchor,
        MapMovementManager manager,
        string title,
        string descriptionFormat,
        Vector2 screenOffset)
    {
        _anchorRect = anchor;
        _manager = manager;
        _title = title ?? string.Empty;
        _descriptionFormat = descriptionFormat ?? string.Empty;
        _screenOffset = screenOffset;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_anchorRect == null)
            return;
        if (string.IsNullOrWhiteSpace(_title) && string.IsNullOrWhiteSpace(_descriptionFormat))
            return;

        var mgr = HoverTooltipManager.Instance;
        if (mgr == null || !mgr.HasValidPrefab)
            return;

        var nextDamage = _manager != null ? _manager.GetCorruptionDamageForNextStep() : 0;
        var increase = _manager != null ? _manager.OverflowDamageIncreasePerMove : 0;
        var description = string.Format(_descriptionFormat, nextDamage, increase);
        mgr.Show(_anchorRect, _screenOffset, _title, description);
    }

    public void OnPointerExit(PointerEventData eventData) => HoverTooltipManager.Instance?.Hide();

    private void OnDisable() => HoverTooltipManager.Instance?.Hide();
}
