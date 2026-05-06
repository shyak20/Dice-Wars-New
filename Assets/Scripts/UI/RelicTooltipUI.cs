using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene UI singleton: shows <see cref="RelicSO.title"/> and <see cref="RelicSO.description"/>.
/// Horizontal position matches the hover target’s center (same rule as <see cref="HoverTooltipPanelUI.AlignPivotWorldXToRect"/>).
/// </summary>
public sealed class RelicTooltipUI : MonoBehaviour
{
    public static RelicTooltipUI Instance { get; private set; }

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [Tooltip("Padding from screen edges in pixels when clamping tooltip inside the viewport.")]
    [SerializeField, Min(0f)] private float screenEdgePadding = 16f;

    private Canvas _parentCanvas;
    private RectTransform _panelRect;
    private Vector3 _defaultPanelLocalPosition;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"RelicTooltipUI: duplicate on '{name}' — remove the extra. Only one per scene is supported.", this);
            enabled = false;
            return;
        }

        if (transform as RectTransform == null)
        {
            Debug.LogError("RelicTooltipUI: must be on a RectTransform.", this);
            enabled = false;
            return;
        }

        if (GetComponentInParent<Canvas>() == null)
        {
            Debug.LogError("RelicTooltipUI: must be a child of a Canvas.", this);
            enabled = false;
            return;
        }
        _parentCanvas = GetComponentInParent<Canvas>();

        if (panelRoot == null || titleText == null || descriptionText == null)
        {
            Debug.LogError("RelicTooltipUI: assign panelRoot, titleText, and descriptionText.", this);
            enabled = false;
            return;
        }
        _panelRect = panelRoot.transform as RectTransform;
        if (_panelRect == null)
        {
            Debug.LogError("RelicTooltipUI: panelRoot must be a RectTransform.", this);
            enabled = false;
            return;
        }
        _defaultPanelLocalPosition = _panelRect.localPosition;
        EnsureTooltipPanelDoesNotBlockRaycasts();

        Instance = this;
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnDisable() => Hide();

    /// <param name="alignTo">Graphic that was hovered; panel pivot world X matches this rect’s horizontal center.</param>
    public void Show(RelicSO relic, RectTransform alignTo)
    {
        Show(relic, alignTo, false, Vector2.zero);
    }

    /// <summary>
    /// Shows tooltip aligned to the hovered rect. When <paramref name="showAboveReference"/> is true,
    /// anchors above the hovered rect center, then applies <paramref name="screenOffset"/>.
    /// </summary>
    public void Show(RelicSO relic, RectTransform alignTo, bool showAboveReference, Vector2 screenOffset)
    {
        if (relic == null || panelRoot == null || titleText == null || descriptionText == null)
            return;

        titleText.text = relic.title ?? "";
        descriptionText.text = relic.description ?? "";
        panelRoot.SetActive(true);
        ResetPanelToDefaultPosition();
        AlignToReference(alignTo, showAboveReference, screenOffset);
        ClampPanelInsideScreen();
    }

    /// <summary>Aligns <see cref="panelRoot"/> pivot world X to <paramref name="reference"/> center (preserves Y/Z).</summary>
    public void AlignPivotWorldXToReference(RectTransform reference)
    {
        if (reference == null || panelRoot == null) return;
        var panelRect = panelRoot.transform as RectTransform;
        if (panelRect == null) return;

        var corners = new Vector3[4];
        reference.GetWorldCorners(corners);
        var centerWorldX = (corners[0].x + corners[2].x) * 0.5f;
        var pos = panelRect.position;
        pos.x = centerWorldX;
        panelRect.position = pos;
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void ClampPanelInsideScreen()
    {
        if (_panelRect == null) return;

        // Ensure text/layout size is up to date before reading corners.
        Canvas.ForceUpdateCanvases();

        var corners = new Vector3[4];
        _panelRect.GetWorldCorners(corners);

        var minX = corners[0].x;
        var minY = corners[0].y;
        var maxX = corners[2].x;
        var maxY = corners[2].y;

        var shiftX = 0f;
        var shiftY = 0f;
        var leftLimit = screenEdgePadding;
        var rightLimit = Screen.width - screenEdgePadding;
        var bottomLimit = screenEdgePadding;
        var topLimit = Screen.height - screenEdgePadding;

        if (minX < leftLimit) shiftX = leftLimit - minX;
        else if (maxX > rightLimit) shiftX = rightLimit - maxX;

        if (minY < bottomLimit) shiftY = bottomLimit - minY;
        else if (maxY > topLimit) shiftY = topLimit - maxY;

        if (Mathf.Abs(shiftX) < 0.01f && Mathf.Abs(shiftY) < 0.01f)
            return;

        var cameraForCanvas = _parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _parentCanvas.worldCamera
            : null;
        var panelScreenPos = RectTransformUtility.WorldToScreenPoint(cameraForCanvas, _panelRect.position);
        panelScreenPos += new Vector2(shiftX, shiftY);

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _panelRect.parent as RectTransform,
                panelScreenPos,
                cameraForCanvas,
                out var clampedWorldPos))
        {
            _panelRect.position = clampedWorldPos;
        }
    }

    private void ResetPanelToDefaultPosition()
    {
        if (_panelRect == null) return;
        _panelRect.localPosition = _defaultPanelLocalPosition;
    }

    private void EnsureTooltipPanelDoesNotBlockRaycasts()
    {
        if (panelRoot == null) return;
        var graphics = panelRoot.GetComponentsInChildren<Graphic>(true);
        for (var i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
    }

    private void AlignToReference(RectTransform reference, bool showAboveReference, Vector2 screenOffset)
    {
        if (reference == null || _panelRect == null) return;

        var corners = new Vector3[4];
        reference.GetWorldCorners(corners);
        var center = (corners[0] + corners[2]) * 0.5f;
        var targetWorld = center;
        if (showAboveReference)
            targetWorld.y = corners[1].y;

        var cameraForCanvas = _parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _parentCanvas.worldCamera
            : null;
        var targetScreen = RectTransformUtility.WorldToScreenPoint(cameraForCanvas, targetWorld) + screenOffset;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _panelRect.parent as RectTransform,
                targetScreen,
                cameraForCanvas,
                out var worldPos))
        {
            _panelRect.position = worldPos;
        }
    }
}
