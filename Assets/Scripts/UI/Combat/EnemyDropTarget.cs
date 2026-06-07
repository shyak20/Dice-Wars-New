using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drop zone over an enemy. When the player drops a <see cref="RolledOutcomeToken"/> here, the token's rolled
/// outcome is assigned to <see cref="Enemy"/> via the <see cref="RollTargetAssignmentController"/>.
/// Place on the enemy's clickable area (e.g. an Image with Raycast Target enabled).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class EnemyDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Optional. Highlighted while a token is hovered over this enemy (drag feedback).")]
    [SerializeField] private GameObject hoverHighlight;

    public EnemyController Enemy { get; private set; }
    public CombatManager Combat { get; private set; }

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
        RestoreHoverHighlightDefault();
    }

    public void OnDrop(PointerEventData eventData)
    {
        SetHighlight(false);
        if (Enemy == null || !Enemy.IsAlive)
            return;

        var token = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponentInParent<RolledOutcomeToken>() : null;
        if (token == null)
            return;

        token.AssignToEnemy(Enemy);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null && eventData.pointerDrag.GetComponentInParent<RolledOutcomeToken>() != null && Enemy != null && Enemy.IsAlive)
            SetHighlight(true);
    }

    public void OnPointerExit(PointerEventData eventData) => SetHighlight(false);

    private void RestoreHoverHighlightDefault()
    {
        if (hoverHighlight == null)
            return;

        // World sprite presentation (e.g. Enemy Image) must stay visible — never hide it as "highlight off".
        if (hoverHighlight.GetComponent<SpriteRenderer>() != null)
            hoverHighlight.SetActive(true);
        else
            hoverHighlight.SetActive(false);
    }

    private void SetHighlight(bool on)
    {
        if (hoverHighlight == null)
            return;

        // Dedicated UI overlays toggle on drag-hover; enemy sprite art stays visible.
        if (hoverHighlight.GetComponent<SpriteRenderer>() != null)
            return;

        hoverHighlight.SetActive(on);
    }
}
