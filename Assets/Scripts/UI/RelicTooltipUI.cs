using TMPro;
using UnityEngine;

/// <summary>
/// Scene UI singleton: shows <see cref="RelicSO.title"/> and <see cref="RelicSO.description"/> near the pointer.
/// Place one instance under your Canvas (e.g. Fight / Map / Shop). Wire panel root, title, and description.
/// </summary>
public sealed class RelicTooltipUI : MonoBehaviour
{
    public static RelicTooltipUI Instance { get; private set; }

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Vector2 screenOffset = new Vector2(16f, -16f);

    private RectTransform _rectTransform;
    private RectTransform _parentRect;
    private Canvas _canvas;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"RelicTooltipUI: duplicate on '{name}' — remove the extra. Only one per scene is supported.", this);
            enabled = false;
            return;
        }

        _rectTransform = transform as RectTransform;
        if (_rectTransform == null)
        {
            Debug.LogError("RelicTooltipUI: must be on a RectTransform.", this);
            enabled = false;
            return;
        }

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null)
        {
            Debug.LogError("RelicTooltipUI: must be a child of a Canvas.", this);
            enabled = false;
            return;
        }

        _parentRect = _rectTransform.parent as RectTransform;
        if (_parentRect == null)
        {
            Debug.LogError("RelicTooltipUI: parent must be a RectTransform (e.g. Canvas or panel).", this);
            enabled = false;
            return;
        }

        if (panelRoot == null || titleText == null || descriptionText == null)
        {
            Debug.LogError("RelicTooltipUI: assign panelRoot, titleText, and descriptionText.", this);
            enabled = false;
            return;
        }

        Instance = this;
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnDisable() => Hide();

    public void Show(RelicSO relic, Vector2 screenPosition)
    {
        if (relic == null || panelRoot == null || titleText == null || descriptionText == null)
            return;

        titleText.text = relic.title ?? "";
        descriptionText.text = relic.description ?? "";
        panelRoot.SetActive(true);
        MoveTo(screenPosition);
    }

    public void MoveTo(Vector2 screenPosition)
    {
        if (panelRoot == null || !panelRoot.activeSelf || _rectTransform == null || _parentRect == null || _canvas == null)
            return;

        var cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        var point = screenPosition + screenOffset;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, point, cam, out var local))
            return;

        _rectTransform.anchoredPosition = local;
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}
