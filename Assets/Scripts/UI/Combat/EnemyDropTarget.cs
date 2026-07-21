using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drop zone over an enemy. When the player drops a <see cref="RolledOutcomeToken"/> here, or clicks while a token is
/// selected, the token's rolled outcome is assigned to <see cref="Enemy"/> via the <see cref="RollTargetAssignmentController"/>.
/// Place on the enemy's clickable area (e.g. an Image with Raycast Target enabled).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class EnemyDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Optional. Non-sprite UI overlay toggled while a token is hovered (e.g. a frame Image). Enemy sprite outline is handled by EnemyCombatPresentationController.")]
    [SerializeField] private GameObject hoverHighlight;

    public EnemyController Enemy { get; private set; }
    public CombatManager Combat { get; private set; }

    private EnemyCombatPresentationController _presentation;
    private RectTransform _rect;
    public RectTransform Rect => _rect != null ? _rect : (_rect = (RectTransform)transform);

    private void Awake()
    {
        _rect = (RectTransform)transform;
    }

    /// <summary>Wires this drop target to its enemy + combat manager (called by <see cref="EnemyController.ActivateInRoster"/>).</summary>
    public void Bind(EnemyController enemy, CombatManager combat)
    {
        Enemy = enemy;
        Combat = combat;
        _presentation = enemy != null ? enemy.CombatPresentation : null;
        SetDragHoverOutline(false);
        RestoreHoverHighlightDefault();
    }

    public void OnDrop(PointerEventData eventData)
    {
        SetDragHoverOutline(false);
        SetUiHoverHighlight(false);
        if (Enemy == null || !Enemy.IsAlive)
            return;

        var token = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponentInParent<RolledOutcomeToken>() : null;
        if (token == null)
            return;

        token.AssignToEnemy(Enemy);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!ShouldShowAssignHover(eventData))
            return;
        if (Enemy == null || !Enemy.IsAlive)
            return;

        SetDragHoverOutline(true);
        SetUiHoverHighlight(true);
    }

    public void OnPointerExit(PointerEventData eventData) => ClearHoverFeedback();

    bool ShouldShowAssignHover(PointerEventData eventData)
    {
        if (IsRolledOutcomeTokenDrag(eventData))
            return true;

        var assignment = ResolveAssignment();
        if (assignment == null || !assignment.IsWaitingForPlayerAssignment)
            return false;

        var selected = assignment.SelectedToken;
        return selected != null && selected.IsDragEnabled && !selected.IsDragging;
    }

    RollTargetAssignmentController ResolveAssignment()
    {
        return Combat != null ? Combat.TargetAssignment : null;
    }

    private static bool IsRolledOutcomeTokenDrag(PointerEventData eventData)
    {
        return eventData.pointerDrag != null &&
               eventData.pointerDrag.GetComponentInParent<RolledOutcomeToken>() != null;
    }

    private void ClearHoverFeedback()
    {
        SetDragHoverOutline(false);
        SetUiHoverHighlight(false);
    }

    private void SetDragHoverOutline(bool on)
    {
        _presentation?.SetDragAssignHoverOutline(on);
    }

    private void RestoreHoverHighlightDefault()
    {
        SetUiHoverHighlight(false);
    }

    private void SetUiHoverHighlight(bool on)
    {
        if (hoverHighlight == null)
            return;

        if (hoverHighlight.GetComponent<SpriteRenderer>() != null)
            return;

        hoverHighlight.SetActive(on);
    }
}
