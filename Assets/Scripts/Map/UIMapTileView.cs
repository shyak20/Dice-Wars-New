using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>Reachability / selection state for map tile UI (drives colors and optional available pulse).</summary>
public enum MapTileUIViewState
{
    /// <summary>Not a valid one-step move from the player.</summary>
    Idle = 0,
    /// <summary>Orthogonal neighbor of the player with a directed exit from the player’s tile toward it.</summary>
    Available = 1,
    /// <summary>Tile the player is on, or the click target while the pawn is moving.</summary>
    Selected = 2
}

/// <summary>One map cell: background color, optional node label, event icon, exit arrows, click to move.</summary>
public class UIMapTileView : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button clickButton;
    [Header("Exit arrows (one GameObject per direction; orient sprites in the prefab)")]
    [FormerlySerializedAs("arrowTop")]
    [SerializeField] private GameObject exitArrowTop;
    [FormerlySerializedAs("arrowRight")]
    [SerializeField] private GameObject exitArrowRight;
    [FormerlySerializedAs("arrowBottom")]
    [SerializeField] private GameObject exitArrowBottom;
    [FormerlySerializedAs("arrowLeft")]
    [SerializeField] private GameObject exitArrowLeft;
    [Header("Incoming arrows (one GameObject per direction; optional)")]
    [SerializeField] private GameObject incomingArrowTop;
    [SerializeField] private GameObject incomingArrowRight;
    [SerializeField] private GameObject incomingArrowBottom;
    [SerializeField] private GameObject incomingArrowLeft;
    [Header("Arrow colors")]
    [SerializeField] private Color arrowColorCurrentTile = new Color(1f, 0.95f, 0.4f, 1f);
    [SerializeField] private Color arrowColorOtherTiles = new Color(0.55f, 0.55f, 0.6f, 0.9f);
    [Header("Tile background (standing only)")]
    [Tooltip("Background tint on the tile the pawn is standing on; other tiles use the default background color.")]
    [SerializeField] private Color playerCurrentTileBackgroundColor = new Color(0.85f, 0.85f, 0.4f, 1f);
    [Header("Event icon (reachability)")]
    [SerializeField] private Color iconColorAvailable = new Color(0.35f, 0.95f, 0.45f, 1f);
    [SerializeField] private Color iconColorNotAvailable = Color.white;
    [Header("One-way exit arrow sizing")]
    [Tooltip("Used when this tile has an exit in a direction and the adjacent tile does not have a return exit back.")]
    [SerializeField, Min(1f)] private float oneWayExitArrowWidth = 60f;
    [Header("Optional")]
    [SerializeField] private TMP_Text nodeTypeLabel;
    [SerializeField] private Image eventIconImage;
    [Header("Button hover")]
    [Tooltip("Shown while the pointer is over this tile’s Click Button. Requires an EventSystem in the scene.")]
    [SerializeField] private GameObject buttonHoverHighlightObject;
    [Tooltip("Optional. If unset, uses a CanvasGroup on the hover object or adds one to its root for alpha fades.")]
    [SerializeField] private CanvasGroup buttonHoverHighlightCanvasGroup;
    [Tooltip("Fade-out after pointer exit: X = normalized time 0→1 over the duration below; Y = alpha (1 = opaque, 0 = transparent).")]
    [SerializeField] private AnimationCurve buttonHoverFadeOutCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [SerializeField, Min(0.01f)] private float buttonHoverFadeOutDurationSeconds = 0.25f;
    [Header("Visited tiles")]
    [Tooltip("Scale multiplier applied to background image when tile.eventConsumed is true, or when this is the start cell and the player has moved elsewhere.")]
    [SerializeField, Min(0.01f)] private float visitedBackgroundScale = 0.9f;
    [Header("Boss tile")]
    [Tooltip("Uniform scale multiplier for the event icon on the boss / end cell only.")]
    [SerializeField, Min(0.01f)] private float bossTileEventIconScale = 1.2f;
    [Header("Available tile pulse (optional)")]
    [Tooltip("Stays active; scale loops from the curve only while this tile is Available (otherwise scale resets to base).")]
    [SerializeField] private GameObject availablePulseTarget;
    [SerializeField] private AnimationCurve availablePulseScaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    [SerializeField, Min(0.01f)] private float availablePulseLoopDurationSeconds = 1.2f;
    [Header("Land on tile (one-shot scale)")]
    [Tooltip("If unset, uses eventIconImage transform. Curve X = normalized time 0→1 over the duration; Y = uniform scale multiplier.")]
    [SerializeField] private Transform landedScaleTarget;
    [SerializeField] private AnimationCurve landedScaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.85f);
    [SerializeField, Min(0.01f)] private float landedScaleDurationSeconds = 0.35f;

    private Vector3 _eventIconBaseLocalScale = Vector3.one;
    private Vector3 _backgroundBaseLocalScale = Vector3.one;
    private Vector3 _hoverHighlightBaseLocalScale = Vector3.one;
    private Color _baseBackgroundColor = Color.white;
    private float _exitArrowTopBaseWidth;
    private float _exitArrowRightBaseWidth;
    private float _exitArrowBottomBaseWidth;
    private float _exitArrowLeftBaseWidth;
    private Vector2Int _cell;
    private MapMovementManager _manager;
    private int _lastExitMask;
    private int _lastIncomingMask;
    private int _lastOneWayExitMask;
    private bool _playerOnThisTile;
    /// <summary>Tile has an event icon sprite; hidden while <see cref="_playerOnThisTile"/>.</summary>
    private bool _eventIconActiveForTile;
    private bool _tileEventConsumed;
    private bool _isStartCell;
    private MapTileUIViewState _reachabilityState = MapTileUIViewState.Idle;
    private Transform _pulseTransform;
    private Vector3 _pulseBaseScale = Vector3.one;
    private float _pulsePhase;
    private Transform _landedScaleAnimTransform;
    private Vector3 _landedScaleBaseAtStart = Vector3.one;
    private float _landedScaleAnimElapsed = -1f;
    private bool _buttonHoverHandlersRegistered;
    private CanvasGroup _resolvedHoverCanvasGroup;
    private Coroutine _buttonHoverFadeOutRoutine;

    private float _standingVisitedTransElapsed = -1f;
    private float _standingVisitedTransDuration;
    private Color _standingVisitedColorFrom;
    private Color _standingVisitedColorTo;
    private Vector3 _standingVisitedBgScaleFrom;
    private Vector3 _standingVisitedBgScaleTo;
    private Vector3 _standingVisitedHoverScaleFrom;
    private Vector3 _standingVisitedHoverScaleTo;
    private bool _standingVisitedTransIncludesHover;

    private void Awake()
    {
        if (backgroundImage == null)
            Debug.LogError("UIMapTileView: assign backgroundImage.", this);
        else
            _baseBackgroundColor = backgroundImage.color;
        if (clickButton == null)
            Debug.LogError("UIMapTileView: assign clickButton.", this);
        if (eventIconImage != null)
            _eventIconBaseLocalScale = eventIconImage.transform.localScale;
        if (backgroundImage != null)
            _backgroundBaseLocalScale = backgroundImage.transform.localScale;
        _exitArrowTopBaseWidth = ReadArrowWidth(exitArrowTop);
        _exitArrowRightBaseWidth = ReadArrowWidth(exitArrowRight);
        _exitArrowBottomBaseWidth = ReadArrowWidth(exitArrowBottom);
        _exitArrowLeftBaseWidth = ReadArrowWidth(exitArrowLeft);

        if (availablePulseTarget != null)
        {
            _pulseTransform = availablePulseTarget.transform;
            _pulseBaseScale = _pulseTransform.localScale;
            availablePulseTarget.SetActive(true);
        }

        if (buttonHoverHighlightObject != null)
            _hoverHighlightBaseLocalScale = buttonHoverHighlightObject.transform.localScale;

        RegisterButtonHoverHighlightHandlers();
    }

    private void OnDisable()
    {
        StopButtonHoverFadeOutCoroutine();
        if (buttonHoverHighlightObject != null)
            buttonHoverHighlightObject.SetActive(false);
        var cg = buttonHoverHighlightCanvasGroup != null ? buttonHoverHighlightCanvasGroup : _resolvedHoverCanvasGroup;
        if (cg != null)
            cg.alpha = 1f;
    }

    private void RegisterButtonHoverHighlightHandlers()
    {
        if (clickButton == null || buttonHoverHighlightObject == null || _buttonHoverHandlersRegistered)
            return;

        buttonHoverHighlightObject.SetActive(false);
        var cgInit = ResolveHoverHighlightCanvasGroup();
        if (cgInit != null)
            cgInit.alpha = 1f;

        var trigger = clickButton.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = clickButton.gameObject.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => OnButtonHoverPointerEnter());
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => OnButtonHoverPointerExit());
        trigger.triggers.Add(exit);

        _buttonHoverHandlersRegistered = true;
    }

    private CanvasGroup ResolveHoverHighlightCanvasGroup()
    {
        if (buttonHoverHighlightObject == null)
            return null;
        if (buttonHoverHighlightCanvasGroup != null)
            return buttonHoverHighlightCanvasGroup;
        if (_resolvedHoverCanvasGroup == null)
        {
            _resolvedHoverCanvasGroup = buttonHoverHighlightObject.GetComponent<CanvasGroup>();
            if (_resolvedHoverCanvasGroup == null)
                _resolvedHoverCanvasGroup = buttonHoverHighlightObject.GetComponentInChildren<CanvasGroup>(true);
            if (_resolvedHoverCanvasGroup == null)
            {
                _resolvedHoverCanvasGroup = buttonHoverHighlightObject.AddComponent<CanvasGroup>();
                _resolvedHoverCanvasGroup.blocksRaycasts = false;
                _resolvedHoverCanvasGroup.interactable = false;
            }
        }

        return _resolvedHoverCanvasGroup;
    }

    private void StopButtonHoverFadeOutCoroutine()
    {
        if (_buttonHoverFadeOutRoutine == null)
            return;
        StopCoroutine(_buttonHoverFadeOutRoutine);
        _buttonHoverFadeOutRoutine = null;
    }

    private void OnButtonHoverPointerEnter()
    {
        StopButtonHoverFadeOutCoroutine();
        if (buttonHoverHighlightObject == null)
            return;
        var cg = ResolveHoverHighlightCanvasGroup();
        if (cg != null)
            cg.alpha = 1f;
        buttonHoverHighlightObject.SetActive(true);
    }

    private void OnButtonHoverPointerExit()
    {
        if (buttonHoverHighlightObject == null || !buttonHoverHighlightObject.activeSelf)
            return;
        StopButtonHoverFadeOutCoroutine();
        _buttonHoverFadeOutRoutine = StartCoroutine(CoButtonHoverFadeOut());
    }

    private IEnumerator CoButtonHoverFadeOut()
    {
        var cg = ResolveHoverHighlightCanvasGroup();
        if (cg == null)
        {
            buttonHoverHighlightObject.SetActive(false);
            _buttonHoverFadeOutRoutine = null;
            yield break;
        }

        var duration = buttonHoverFadeOutDurationSeconds;
        var curve = buttonHoverFadeOutCurve != null && buttonHoverFadeOutCurve.length > 0
            ? buttonHoverFadeOutCurve
            : AnimationCurve.Linear(0f, 1f, 1f, 0f);

        if (duration <= 0f)
        {
            cg.alpha = Mathf.Clamp01(curve.Evaluate(1f));
            buttonHoverHighlightObject.SetActive(false);
            cg.alpha = 1f;
            _buttonHoverFadeOutRoutine = null;
            yield break;
        }

        var t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            var u = Mathf.Clamp01(t / duration);
            cg.alpha = Mathf.Clamp01(curve.Evaluate(u));
            yield return null;
        }

        cg.alpha = Mathf.Clamp01(curve.Evaluate(1f));
        buttonHoverHighlightObject.SetActive(false);
        cg.alpha = 1f;
        _buttonHoverFadeOutRoutine = null;
    }

    private void Update()
    {
        UpdateStandingVisitedBackgroundTransition();
        UpdateLandedScaleAnimation();
        UpdateAvailablePulseAnimation();
    }

    private void CancelStandingVisitedBackgroundTransition()
    {
        _standingVisitedTransElapsed = -1f;
    }

    /// <summary>
    /// Linearly interpolates standing background tint and visited/standing background + hover scales toward the state
    /// as if the pawn were already on <paramref name="standingHereEnd"/>. Call when the marker move starts (with current visuals already applied for the move-start snapshot).
    /// </summary>
    public void BeginStandingVisitedBackgroundTransition(bool standingHereEnd, float durationSeconds)
    {
        if (backgroundImage == null || durationSeconds <= 0f)
            return;

        CancelStandingVisitedBackgroundTransition();

        _standingVisitedColorFrom = backgroundImage.color;
        _standingVisitedBgScaleFrom = backgroundImage.transform.localScale;
        _standingVisitedColorTo = GetTargetStandingBackgroundColor(standingHereEnd);
        _standingVisitedBgScaleTo = GetTargetBackgroundScaleForVisited(standingHereEnd);

        _standingVisitedTransIncludesHover = buttonHoverHighlightObject != null;
        if (_standingVisitedTransIncludesHover)
        {
            _standingVisitedHoverScaleFrom = buttonHoverHighlightObject.transform.localScale;
            _standingVisitedHoverScaleTo = GetTargetHoverHighlightScaleForVisited(standingHereEnd);
        }

        _standingVisitedTransDuration = durationSeconds;
        _standingVisitedTransElapsed = 0f;
    }

    private void UpdateStandingVisitedBackgroundTransition()
    {
        if (_standingVisitedTransElapsed < 0f || backgroundImage == null)
            return;

        _standingVisitedTransElapsed += Time.unscaledDeltaTime;
        var u = _standingVisitedTransDuration > 0f
            ? Mathf.Clamp01(_standingVisitedTransElapsed / _standingVisitedTransDuration)
            : 1f;

        backgroundImage.color = Color.LerpUnclamped(_standingVisitedColorFrom, _standingVisitedColorTo, u);
        backgroundImage.transform.localScale = Vector3.LerpUnclamped(_standingVisitedBgScaleFrom, _standingVisitedBgScaleTo, u);
        if (_standingVisitedTransIncludesHover && buttonHoverHighlightObject != null)
            buttonHoverHighlightObject.transform.localScale =
                Vector3.LerpUnclamped(_standingVisitedHoverScaleFrom, _standingVisitedHoverScaleTo, u);

        if (u >= 1f)
            CancelStandingVisitedBackgroundTransition();
    }

    private Color GetTargetStandingBackgroundColor(bool standingHere)
    {
        return standingHere ? playerCurrentTileBackgroundColor : _baseBackgroundColor;
    }

    private Vector3 GetTargetBackgroundScaleForVisited(bool playerOnThisTileForScale)
    {
        var leftStartVisuallyVisited = _isStartCell && !playerOnThisTileForScale;
        var useVisitedScale = _tileEventConsumed || leftStartVisuallyVisited;
        return playerOnThisTileForScale
            ? Vector3.one
            : useVisitedScale
                ? _backgroundBaseLocalScale * visitedBackgroundScale
                : _backgroundBaseLocalScale;
    }

    private Vector3 GetTargetHoverHighlightScaleForVisited(bool playerOnThisTileForScale)
    {
        var leftStartVisuallyVisited = _isStartCell && !playerOnThisTileForScale;
        var useVisitedScale = _tileEventConsumed || leftStartVisuallyVisited;
        return playerOnThisTileForScale
            ? Vector3.one
            : useVisitedScale
                ? _hoverHighlightBaseLocalScale * visitedBackgroundScale
                : _hoverHighlightBaseLocalScale;
    }

    /// <summary>Starts the landing scale-down on this tile (call when the player finishes moving here).</summary>
    public void PlayLandingScaleDown()
    {
        CancelLandedScaleAnimation(restoreScale: true);
        var t = ResolveLandedScaleTransform();
        if (t == null)
            return;
        if (landedScaleDurationSeconds <= 0f)
            return;
        _landedScaleAnimTransform = t;
        _landedScaleBaseAtStart = t.localScale;
        _landedScaleAnimElapsed = 0f;
        ApplyEventIconReachabilityTint();
    }

    /// <summary>Applies standing (pawn) highlight, reachability colors, and arrow tint in one step.</summary>
    public void ApplyPlayerStandingAndReachabilityVisual(bool standingHere, MapTileUIViewState reachabilityState)
    {
        CancelStandingVisitedBackgroundTransition();
        _playerOnThisTile = standingHere;
        _reachabilityState = reachabilityState;
        ApplyBackgroundForStandingOnly();
        ApplyBackgroundScaleForVisited();
        ApplyEventIconVisibilityForStanding();
        ApplyEventIconReachabilityTint();
        ApplyArrowMasks(_lastExitMask, _lastIncomingMask, _lastOneWayExitMask);
        SyncAvailablePulseObjectActive();
    }

    public void Setup(Vector2Int cell, MapTile tile, bool isStart, bool isBoss, MapMovementManager manager,
        MapPresentationSO presentation)
    {
        CancelStandingVisitedBackgroundTransition();
        CancelLandedScaleAnimation(restoreScale: true);
        _cell = cell;
        _manager = manager;
        _tileEventConsumed = tile.eventConsumed;
        _isStartCell = isStart;

        if (clickButton != null)
        {
            clickButton.onClick.RemoveAllListeners();
            clickButton.onClick.AddListener(OnClicked);
        }

        if (nodeTypeLabel != null)
        {
            if (tile.eventConsumed)
                nodeTypeLabel.text = "";
            else if (isStart)
                nodeTypeLabel.text = "Start";
            else if (isBoss)
                nodeTypeLabel.text = "Boss";
            else
                nodeTypeLabel.text = tile.eventType.ToString();
        }

        _eventIconActiveForTile = false;
        if (eventIconImage != null)
        {
            if (tile.eventConsumed)
            {
                _eventIconActiveForTile = false;
            }
            else if (presentation != null)
            {
                var sp = presentation.GetEventIcon(tile.eventType, isStart, isBoss);
                if (sp != null)
                {
                    eventIconImage.sprite = sp;
                    _eventIconActiveForTile = true;
                }
            }
        }

        if (eventIconImage != null)
        {
            if (_eventIconActiveForTile)
                eventIconImage.transform.localScale = _eventIconBaseLocalScale * (isBoss ? bossTileEventIconScale : 1f);
            else
                eventIconImage.transform.localScale = _eventIconBaseLocalScale;
        }

        _playerOnThisTile = manager != null && cell == manager.PlayerGridPosition;
        if (manager != null && manager.Grid != null && manager.Grid.Contains(cell))
        {
            var p = manager.PlayerGridPosition;
            if (cell == p)
                _reachabilityState = MapTileUIViewState.Selected;
            else if (manager.IsValidOneStepMoveTarget(cell))
                _reachabilityState = MapTileUIViewState.Available;
            else
                _reachabilityState = MapTileUIViewState.Idle;
        }
        else
            _reachabilityState = MapTileUIViewState.Idle;

        ApplyBackgroundForStandingOnly();
        ApplyBackgroundScaleForVisited();
        ApplyEventIconVisibilityForStanding();
        ApplyEventIconReachabilityTint();
        var incomingMask = 0;
        var oneWayExitMask = 0;
        var currentGrid = manager != null ? manager.Grid : null;
        if (currentGrid != null && currentGrid.Contains(_cell))
        {
            incomingMask = BuildIncomingMask(currentGrid);
            oneWayExitMask = BuildOneWayExitMask(currentGrid, tile.exitMask);
        }
        ApplyArrowMasks(tile.exitMask, incomingMask, oneWayExitMask);
        SyncAvailablePulseObjectActive();
    }

    /// <summary>Updates arrow tint for “standing here” vs other tiles (call when the player moves).</summary>
    public void SetPlayerStandingHere(bool standingHere)
    {
        CancelStandingVisitedBackgroundTransition();
        _playerOnThisTile = standingHere;
        ApplyBackgroundForStandingOnly();
        ApplyBackgroundScaleForVisited();
        ApplyEventIconVisibilityForStanding();
        ApplyEventIconReachabilityTint();
        ApplyArrowMasks(_lastExitMask, _lastIncomingMask, _lastOneWayExitMask);
        SyncAvailablePulseObjectActive();
    }

    private void ApplyBackgroundForStandingOnly()
    {
        if (backgroundImage == null)
            return;
        backgroundImage.color = _playerOnThisTile ? playerCurrentTileBackgroundColor : _baseBackgroundColor;
    }

    private void ApplyEventIconVisibilityForStanding()
    {
        if (eventIconImage == null)
            return;
        if (!_eventIconActiveForTile)
        {
            eventIconImage.enabled = false;
            return;
        }

        eventIconImage.enabled = true;
    }

    private void ApplyEventIconReachabilityTint()
    {
        if (eventIconImage == null || !_eventIconActiveForTile || !eventIconImage.enabled)
            return;
        var useAvailableTint = _reachabilityState == MapTileUIViewState.Available || IsLandingScaleAnimating();
        eventIconImage.color = useAvailableTint ? iconColorAvailable : iconColorNotAvailable;
    }

    private bool IsLandingScaleAnimating() => _landedScaleAnimElapsed >= 0f;

    private Transform ResolveLandedScaleTransform()
    {
        if (landedScaleTarget != null)
            return landedScaleTarget;
        return eventIconImage != null ? eventIconImage.transform : null;
    }

    private void CancelLandedScaleAnimation(bool restoreScale)
    {
        if (!IsLandingScaleAnimating())
        {
            _landedScaleAnimTransform = null;
            _landedScaleAnimElapsed = -1f;
            return;
        }

        if (restoreScale && _landedScaleAnimTransform != null)
            _landedScaleAnimTransform.localScale = _landedScaleBaseAtStart;
        _landedScaleAnimTransform = null;
        _landedScaleAnimElapsed = -1f;
    }

    private void UpdateLandedScaleAnimation()
    {
        if (!IsLandingScaleAnimating() || _landedScaleAnimTransform == null)
            return;

        var duration = landedScaleDurationSeconds;
        if (duration <= 0f)
        {
            CancelLandedScaleAnimation(restoreScale: false);
            ApplyEventIconReachabilityTint();
            return;
        }

        _landedScaleAnimElapsed += Time.unscaledDeltaTime;
        var u = Mathf.Clamp01(_landedScaleAnimElapsed / duration);
        var curve = landedScaleCurve != null && landedScaleCurve.length > 0
            ? landedScaleCurve
            : AnimationCurve.Linear(0f, 1f, 1f, 0.85f);
        var mult = curve.Evaluate(u);
        _landedScaleAnimTransform.localScale = _landedScaleBaseAtStart * mult;
        if (u >= 1f)
        {
            CancelLandedScaleAnimation(restoreScale: false);
            ApplyEventIconReachabilityTint();
        }
    }

    public void RefreshExits(MapGrid grid)
    {
        if (grid == null || !grid.Contains(_cell))
            return;
        var exitMask = grid.Get(_cell.x, _cell.y).exitMask;
        var incomingMask = BuildIncomingMask(grid);
        var oneWayExitMask = BuildOneWayExitMask(grid, exitMask);
        ApplyArrowMasks(exitMask, incomingMask, oneWayExitMask);
    }

    private int BuildIncomingMask(MapGrid grid)
    {
        var mask = 0;
        for (var i = 0; i < 4; i++)
        {
            var direction = (MapCardinalDirection)i;
            var neighbor = _cell + direction.ToDelta();
            if (grid.Contains(neighbor) && grid.HasExit(neighbor.x, neighbor.y, direction.Opposite()))
                mask = mask.With(direction);
        }

        return mask;
    }

    private int BuildOneWayExitMask(MapGrid grid, int exitMask)
    {
        var mask = 0;
        for (var i = 0; i < 4; i++)
        {
            var direction = (MapCardinalDirection)i;
            if (!exitMask.Contains(direction))
                continue;

            var neighbor = _cell + direction.ToDelta();
            if (!grid.Contains(neighbor))
                continue;

            if (!grid.HasExit(neighbor.x, neighbor.y, direction.Opposite()))
                mask = mask.With(direction);
        }

        return mask;
    }

    private void ApplyArrowMasks(int exitMask, int incomingMask, int oneWayExitMask)
    {
        _lastExitMask = exitMask;
        _lastIncomingMask = incomingMask;
        _lastOneWayExitMask = oneWayExitMask;

        SetArrowVisual(exitArrowTop, MapCardinalDirection.Top, _lastExitMask, _lastOneWayExitMask, _exitArrowTopBaseWidth, true);
        SetArrowVisual(exitArrowRight, MapCardinalDirection.Right, _lastExitMask, _lastOneWayExitMask, _exitArrowRightBaseWidth, true);
        SetArrowVisual(exitArrowBottom, MapCardinalDirection.Bottom, _lastExitMask, _lastOneWayExitMask, _exitArrowBottomBaseWidth, true);
        SetArrowVisual(exitArrowLeft, MapCardinalDirection.Left, _lastExitMask, _lastOneWayExitMask, _exitArrowLeftBaseWidth, true);

        SetArrowVisual(incomingArrowTop, MapCardinalDirection.Top, _lastIncomingMask, 0, 0f, false);
        SetArrowVisual(incomingArrowRight, MapCardinalDirection.Right, _lastIncomingMask, 0, 0f, false);
        SetArrowVisual(incomingArrowBottom, MapCardinalDirection.Bottom, _lastIncomingMask, 0, 0f, false);
        SetArrowVisual(incomingArrowLeft, MapCardinalDirection.Left, _lastIncomingMask, 0, 0f, false);
    }

    private void SetArrowVisual(
        GameObject arrowGo,
        MapCardinalDirection direction,
        int arrowMask,
        int oneWayMask,
        float baseWidth,
        bool applyWidthOverride)
    {
        if (arrowGo == null) return;
        var show = arrowMask.Contains(direction);
        arrowGo.SetActive(show);
        if (applyWidthOverride)
            ApplyArrowWidth(arrowGo, show && oneWayMask.Contains(direction), baseWidth);
        if (!show) return;

        var graphic = arrowGo.GetComponent<Graphic>() ?? arrowGo.GetComponentInChildren<Graphic>(true);
        if (graphic != null)
            graphic.color = _playerOnThisTile ? arrowColorCurrentTile : arrowColorOtherTiles;
    }

    private float ReadArrowWidth(GameObject arrowGo)
    {
        var rt = arrowGo != null ? arrowGo.transform as RectTransform : null;
        return rt != null ? rt.sizeDelta.x : 0f;
    }

    private void ApplyArrowWidth(GameObject arrowGo, bool useOneWayWidth, float baseWidth)
    {
        var rt = arrowGo != null ? arrowGo.transform as RectTransform : null;
        if (rt == null)
            return;

        var size = rt.sizeDelta;
        size.x = useOneWayWidth ? oneWayExitArrowWidth : baseWidth;
        rt.sizeDelta = size;
    }

    private void ApplyBackgroundScaleForVisited()
    {
        if (backgroundImage != null)
        {
            var leftStartVisuallyVisited = _isStartCell && !_playerOnThisTile;
            var useVisitedScale = _tileEventConsumed || leftStartVisuallyVisited;
            backgroundImage.transform.localScale = _playerOnThisTile
                ? Vector3.one
                : useVisitedScale
                ? _backgroundBaseLocalScale * visitedBackgroundScale
                : _backgroundBaseLocalScale;
        }

        ApplyHoverHighlightScaleForVisited();
    }

    private void ApplyHoverHighlightScaleForVisited()
    {
        if (buttonHoverHighlightObject == null)
            return;
        var leftStartVisuallyVisited = _isStartCell && !_playerOnThisTile;
        var useVisitedScale = _tileEventConsumed || leftStartVisuallyVisited;
        buttonHoverHighlightObject.transform.localScale = _playerOnThisTile
            ? Vector3.one
            : useVisitedScale
            ? _hoverHighlightBaseLocalScale * visitedBackgroundScale
            : _hoverHighlightBaseLocalScale;
    }

    private void SyncAvailablePulseObjectActive()
    {
        if (availablePulseTarget == null || _pulseTransform == null)
            return;
        availablePulseTarget.SetActive(true);
        var pulsing = !_playerOnThisTile && _reachabilityState == MapTileUIViewState.Available;
        if (!pulsing)
        {
            _pulseTransform.localScale = _pulseBaseScale;
            _pulsePhase = 0f;
        }
    }

    private void UpdateAvailablePulseAnimation()
    {
        if (availablePulseTarget == null || _pulseTransform == null)
            return;
        if (_playerOnThisTile || _reachabilityState != MapTileUIViewState.Available)
            return;

        var period = availablePulseLoopDurationSeconds;
        _pulsePhase += Time.unscaledDeltaTime / period;
        if (_pulsePhase > 1f)
            _pulsePhase -= Mathf.Floor(_pulsePhase);

        var curve = availablePulseScaleCurve != null && availablePulseScaleCurve.length > 0
            ? availablePulseScaleCurve
            : AnimationCurve.Constant(0f, 1f, 1f);
        var mult = curve.Evaluate(_pulsePhase);
        _pulseTransform.localScale = _pulseBaseScale * mult;
    }

    private void OnClicked()
    {
        if (_manager != null)
            _manager.TryMoveTo(_cell);
    }
}
