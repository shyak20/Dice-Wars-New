using UnityEngine;

/// <summary>
/// One clickable starting-die choice: assigns <see cref="DieAssetSO"/>, toggles <see cref="selectionIndicator"/> and optional <see cref="tooltipRoot"/>.
/// Tooltip: click pins this die's tooltip; hovering another die switches the visible tooltip; leaving hover restores the pinned die's tooltip (<see cref="DiceSelectSceneController"/>).
/// Requires a <see cref="Collider"/> on this object (or a child) so <see cref="OnMouseDown"/> / hover works when a camera is present.
/// Or wire <see cref="ToggleSelection"/> to a UI Button.
/// </summary>
public sealed class DiceSelectDieSlot : MonoBehaviour
{
    [SerializeField] private DieAssetSO startingDie;
    [SerializeField] private GameObject selectionIndicator;
    [Header("Tooltip")]
    [Tooltip("Unique tooltip root per die; visibility is driven by DiceSelectSceneController (click to pin, hover another to switch).")]
    [SerializeField] private GameObject tooltipRoot;

    [Header("Optional")]
    [Tooltip("When assigned (or found on child), animation pauses while this die is selected (speed 0) and resumes when deselected.")]
    [SerializeField] private Animator dieAnimator;
    /// <summary>Captured at startup so deselect restores your prefab's animation speed.</summary>
    private float _animatorBaseSpeed = 1f;

    private DiceSelectSceneController _owner;
    private bool _selected;

    public DieAssetSO StartingDie => startingDie;
    public bool IsSelected => _selected;

    public void Initialize(DiceSelectSceneController owner)
    {
        _owner = owner;
        ApplyIndicatorVisual();
    }

    private void Awake()
    {
        if (dieAnimator == null)
            dieAnimator = GetComponentInChildren<Animator>(true);
        if (dieAnimator != null)
            _animatorBaseSpeed = dieAnimator.speed > 0f ? dieAnimator.speed : 1f;

        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);

        ApplyIndicatorVisual();
    }

    private void OnValidate()
    {
        if (selectionIndicator != null && !Application.isPlaying)
            selectionIndicator.SetActive(false);
        if (tooltipRoot != null && !Application.isPlaying)
            tooltipRoot.SetActive(false);
    }

    private void OnDisable()
    {
        if (_owner != null)
            _owner.NotifyTooltipSlotDisabled(this);
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
    }

    private void OnMouseEnter()
    {
        if (_owner == null)
            return;
        _owner.NotifyTooltipHoverEnter(this);
    }

    private void OnMouseExit()
    {
        if (_owner == null)
            return;
        _owner.NotifyTooltipHoverExit(this);
    }

    internal void ApplyTooltipVisibility(bool visible)
    {
        if (tooltipRoot != null)
            tooltipRoot.SetActive(visible);
    }

    /// <summary>For UI Buttons or context menu; same as clicking the collider.</summary>
    public void ToggleSelection()
    {
        if (startingDie == null || _owner == null)
            return;

        _selected = !_selected;
        ApplyIndicatorVisual();
        _owner.RefreshSelectionUi();
        _owner.NotifyTooltipPinned(this);
    }

    private void OnMouseDown()
    {
        ToggleSelection();
    }

    private void ApplyIndicatorVisual()
    {
        if (selectionIndicator != null)
            selectionIndicator.SetActive(_selected);
        ApplyAnimatorForSelection();
    }

    private void ApplyAnimatorForSelection()
    {
        if (dieAnimator == null)
            return;
        dieAnimator.speed = _selected ? 0f : _animatorBaseSpeed;
    }

    internal void ResetSelectionSilent(bool selected)
    {
        _selected = selected;
        ApplyIndicatorVisual();
    }
}
