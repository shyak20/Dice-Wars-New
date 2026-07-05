using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A draggable chip representing exactly one rolled outcome <b>piece</b> — either a face's direct damage, or a single
/// enemy-targeted action (Burn, Vulnerable, ...). The player drags it onto an <see cref="EnemyDropTarget"/> to choose which
/// enemy it hits, so the damage and the debuff of the same die can be sent to different enemies.
/// Visuals (icon, value, jackpot ×N reveal, bust destroy) are delegated to a <see cref="StoredActionsPoolIcon"/> on this prefab.
/// Created and owned by <see cref="RollTargetAssignmentController"/>.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class RolledOutcomeToken : MonoBehaviour
{
    private const float DragHitAlpha = 0.004f;

    private static Sprite _dragHitSprite;

    [Tooltip("Visual row component on this prefab; the token drives its icon/value and jackpot/bust visuals.")]
    [SerializeField] private StoredActionsPoolIcon poolIcon;

    [Header("Drag")]
    [Tooltip("Graphic that receives pointer hits for dragging (usually a full-size transparent Image). Auto-created on top of the token when left empty.")]
    [SerializeField] private Graphic dragRaycastTarget;

    private RectTransform _rect;
    private CanvasGroup _canvasGroup;
    private Canvas _canvas;
    private RollTargetAssignmentController _owner;
    private RollOutcomeVisualLine _line;
    private Vector2 _dragPointerOffset;
    private bool _dragEnabled;
    private bool _isDragging;

    public RectTransform RectTransform => _rect != null ? _rect : (_rect = (RectTransform)transform);
    public bool IsDragEnabled => _dragEnabled;
    public bool IsDragging => _isDragging;

    /// <summary>Fired when the player begins dragging this token.</summary>
    public event System.Action DragStarted;

    /// <summary>Fired when the player releases this token (whether or not it was dropped on an enemy).</summary>
    public event System.Action DragEnded;

    /// <summary>Fired when this token is dropped on an enemy and assignment begins.</summary>
    public event System.Action AssignedToEnemy;

    /// <summary>Fired when drag input is enabled or disabled (e.g. after spawn presentation completes).</summary>
    public event System.Action<bool> DragEnabledChanged;

    /// <summary>The rolled face this token's piece belongs to.</summary>
    public FaceResult Face { get; private set; }

    /// <summary>The enemy-targeted action this token represents; null when this is the face's direct-damage piece.</summary>
    public ApplyStatusEffectAction SourceAction { get; private set; }

    /// <summary>The single pool row deposited into the chosen enemy's element layout when assigned (non-immediate).</summary>
    public RollOutcomeVisualLine Line => _line;

    /// <summary>True when this piece resolves the instant it is dropped (Trigger Immediately).</summary>
    public bool ResolvesImmediatelyOnDrop { get; private set; }

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _canvasGroup = GetComponent<CanvasGroup>();
        if (poolIcon == null)
            Debug.LogError($"RolledOutcomeToken on '{name}': assign the StoredActionsPoolIcon used for visuals.", this);
    }

    public void Configure(RollTargetAssignmentController owner, Canvas canvas, FaceResult face,
        RollOutcomeVisualLine line, ApplyStatusEffectAction sourceAction)
    {
        _owner = owner;
        _canvas = canvas != null ? canvas : GetComponentInParent<Canvas>();
        Face = face;
        _line = line;
        SourceAction = sourceAction;
        ResolvesImmediatelyOnDrop = line.ResolvesImmediatelyOnDrop;

        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;

        if (poolIcon != null)
        {
            poolIcon.Configure(line.RowKey);
            poolIcon.SetPoolSprite(ResolveIcon(line));
            poolIcon.SetRowBackground(ResolveBackground(line));
            poolIcon.SetValue(line.Amount);
            poolIcon.SetPointerRaycastsEnabled(false);
        }

        EnsureDragSurface();
        SetDragEnabled(false);
    }

    /// <summary>When false, the spawn presentation is running and pointer drag is ignored.</summary>
    public void SetDragEnabled(bool enabled)
    {
        if (_dragEnabled == enabled)
            return;

        _dragEnabled = enabled;
        _canvasGroup.interactable = enabled;
        if (dragRaycastTarget != null)
            dragRaycastTarget.raycastTarget = enabled;
        DragEnabledChanged?.Invoke(enabled);
    }

    /// <summary>
    /// Converts a screen-space pointer position to a parent-local point (relative to the parent's pivot), suitable for
    /// assigning to <see cref="Transform.localPosition"/>. Resolves the event camera from the parent's own canvas so it
    /// stays correct at any resolution and for any canvas type (Screen Space Overlay/Camera, World Space).
    /// </summary>
    private bool ScreenPointToParentLocalPoint(Vector2 screenPos, out Vector2 localPoint)
    {
        localPoint = default;
        if (_rect == null) return false;
        var parentRT = _rect.parent as RectTransform;
        if (parentRT == null) return false;

        var parentCanvas = parentRT.GetComponentInParent<Canvas>();
        Camera uiCam = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? parentCanvas.worldCamera
            : null;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, screenPos, uiCam, out localPoint);
    }

    private static Sprite ResolveIcon(RollOutcomeVisualLine line)
    {
        if (line.IconOverride != null) return line.IconOverride;
        return PoolRowKey.TryGetDieType(line.RowKey, out var dt) ? GameIconCatalog.GetElementIcon(dt) : null;
    }

    private static Sprite ResolveBackground(RollOutcomeVisualLine line)
    {
        return line.BackgroundOverride != null ? line.BackgroundOverride : GameIconCatalog.TryGetPoolRowBackground(line.RowKey);
    }

    /// <summary>Perfect Cast: scale this piece's amount only — jackpot visuals run during <see cref="JackpotPresentationController"/>.</summary>
    public void MultiplyAmountOnly(int multiplier)
    {
        if (multiplier <= 1) return;
        _line.Amount *= multiplier;
    }

    /// <summary>Perfect Cast: scale this piece's amount and play the ×N reveal on the icon.</summary>
    public void ApplyPerfectStrikeMultiply(int multiplier, float valueRevealDelay)
    {
        if (multiplier <= 1) return;
        _line.Amount *= multiplier;
        if (poolIcon != null)
        {
            poolIcon.ShowJackpotMultiplierBadge(multiplier);
            poolIcon.ScheduleJackpotPostMultiplyValueReveal(_line.Amount, valueRevealDelay);
        }
    }

    /// <summary>Cast Overload (bust): play the destroy visual before the controller removes this token.</summary>
    public void PlayBustDestroyVisual()
    {
        if (poolIcon != null)
            poolIcon.ShowBustDestroyVisual(true);
    }

    /// <summary>Increase Other Elements: bump this piece's displayed amount (keeps token <see cref="Line"/> in sync for assignment).</summary>
    public void ApplyAmountBonus(int bonusDelta)
    {
        if (bonusDelta <= 0)
            return;

        _line.Amount += bonusDelta;
        if (poolIcon != null)
            poolIcon.SetAmountWithPulse(_line.Amount);
    }

    /// <summary>Sets the displayed amount (e.g. after a per-hit damage buff on a split-attack face).</summary>
    public void SetDisplayAmount(int value)
    {
        _line.Amount = value;
        if (poolIcon != null)
            poolIcon.SetAmountWithPulse(value);
    }

    public void SetAnchoredPosition(Vector2 anchored)
    {
        if (_rect == null) _rect = (RectTransform)transform;
        _rect.localPosition = new Vector3(anchored.x, anchored.y, 0f);
    }

    /// <summary>Called by <see cref="RolledOutcomeTokenDragRelay"/> on the drag raycast surface.</summary>
    public void HandleBeginDrag(PointerEventData eventData)
    {
        if (!_dragEnabled || _rect == null)
            return;

        if (ScreenPointToParentLocalPoint(eventData.position, out var local))
            _dragPointerOffset = (Vector2)_rect.localPosition - local;

        _canvasGroup.blocksRaycasts = false;
        transform.SetAsLastSibling();
        _isDragging = true;
        DragStarted?.Invoke();
    }

    /// <summary>Called by <see cref="RolledOutcomeTokenDragRelay"/> on the drag raycast surface.</summary>
    public void HandleDrag(PointerEventData eventData)
    {
        if (!_dragEnabled || _rect == null)
            return;

        if (ScreenPointToParentLocalPoint(eventData.position, out var local))
        {
            var p = local + _dragPointerOffset;
            _rect.localPosition = new Vector3(p.x, p.y, 0f);
        }
    }

    /// <summary>Called by <see cref="RolledOutcomeTokenDragRelay"/> on the drag raycast surface.</summary>
    public void HandleEndDrag(PointerEventData eventData)
    {
        _canvasGroup.blocksRaycasts = true;
        EnemyCombatPresentationController.ClearAllDragAssignHoverOutlines();
        _isDragging = false;
        DragEnded?.Invoke();
        // If not consumed by an EnemyDropTarget.OnDrop, the token stays pending where it was released.
        if (_owner != null)
            _owner.NotifyTokenDragEnded(this);
    }

    private void EnsureDragSurface()
    {
        if (dragRaycastTarget == null)
            dragRaycastTarget = CreateDragCatcherOverlay();

        ConfigureDragGraphic(dragRaycastTarget);

        // Must sit above icon/value children so it receives pointer hits first.
        dragRaycastTarget.transform.SetAsLastSibling();
        BindDragRelay(dragRaycastTarget);
    }

    private Graphic CreateDragCatcherOverlay()
    {
        var catcherGo = new GameObject("Drag Catcher", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RolledOutcomeTokenDragRelay));
        catcherGo.transform.SetParent(transform, false);
        catcherGo.transform.SetAsLastSibling();

        var rt = (RectTransform)catcherGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var image = catcherGo.GetComponent<Image>();
        image.sprite = GetDragHitSprite();
        image.type = Image.Type.Simple;
        // Alpha must be > 0 or Unity culls the mesh and the GraphicRaycaster never hits this target.
        image.color = new Color(1f, 1f, 1f, DragHitAlpha);
        image.raycastTarget = true;

        var renderer = catcherGo.GetComponent<CanvasRenderer>();
        renderer.cullTransparentMesh = false;

        return image;
    }

    /// <summary>1×1 white sprite so the drag Image is raycastable without relying on Unity built-in UI assets.</summary>
    private static Sprite GetDragHitSprite()
    {
        if (_dragHitSprite != null)
            return _dragHitSprite;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false, true);

        _dragHitSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        _dragHitSprite.name = "RolledOutcomeTokenDragHit";
        return _dragHitSprite;
    }

    private void ConfigureDragGraphic(Graphic graphic)
    {
        if (graphic is Image image)
        {
            if (image.sprite == null)
                image.sprite = GetDragHitSprite();
            if (image.color.a <= 0f)
                image.color = new Color(image.color.r, image.color.g, image.color.b, DragHitAlpha);
            var renderer = image.canvasRenderer;
            if (renderer != null)
                renderer.cullTransparentMesh = false;
        }

        graphic.raycastTarget = true;
    }

    private void BindDragRelay(Graphic target)
    {
        var relay = target.GetComponent<RolledOutcomeTokenDragRelay>();
        if (relay == null)
            relay = target.gameObject.AddComponent<RolledOutcomeTokenDragRelay>();
        relay.Bind(this);
    }

    /// <summary>Called by <see cref="EnemyDropTarget"/> when this token is dropped on an enemy.</summary>
    public void AssignToEnemy(EnemyController enemy)
    {
        AssignedToEnemy?.Invoke();
        if (_owner != null)
            _owner.AssignTokenToEnemy(this, enemy);
    }
}
