using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optional overlay on <see cref="MapUnknownEventPanel"/> — shows what an unknown event option did
/// (relic gained, curse face added to a die, etc.) before returning to the map.
/// </summary>
public sealed class MapUnknownEventOutcomeResultView : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [Tooltip("Unknown event art from UnknownMapEventSO.eventArt (same as the main event panel).")]
    [SerializeField] private Image eventArtImage;

    [Header("Result copy (inspector)")]
    [SerializeField] private string relicResultTitle = "Artifact found!";
    [TextArea(2, 6)]
    [SerializeField] private string relicResultDescription = string.Empty;
    [SerializeField] private string curseResultTitle = "Curse inflicted!";
    [TextArea(2, 6)]
    [SerializeField] private string curseResultDescription = string.Empty;

    [Header("Result visuals")]
    [Tooltip("Parent for the instantiated die or relic display prefab.")]
    [SerializeField] private Transform contentRoot;
    [Tooltip("Die tray button prefab (root with DiceTrayButtonView). Used for curse outcomes.")]
    [SerializeField] private GameObject dieDisplayPrefab;
    [Tooltip("Relic slot prefab (root with RunRelicSlotView). Used for relic outcomes.")]
    [SerializeField] private GameObject relicDisplayPrefab;
    [Tooltip("Same overlay as the unknown-event die picker — always shown for curse outcomes.")]
    [SerializeField] private DieTooltipOverlayUI dieTooltipOverlay;
    [Tooltip("Uniform scale applied to spawned die and relic display prefabs.")]
    [SerializeField, Min(0.01f)] private float displayScaleFactor = 1f;

    [SerializeField] private Button continueButton;

    GameObject _spawnedContent;

    void Awake()
    {
        if (panel == null)
            panel = gameObject;
        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);
        HideImmediate();
    }

    void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(OnContinueClicked);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (contentRoot == null)
            Debug.LogWarning($"MapUnknownEventOutcomeResultView on '{name}': assign contentRoot for die/relic prefabs.", this);
        if (dieDisplayPrefab == null)
            Debug.LogWarning($"MapUnknownEventOutcomeResultView on '{name}': assign dieDisplayPrefab.", this);
        if (relicDisplayPrefab == null)
            Debug.LogWarning($"MapUnknownEventOutcomeResultView on '{name}': assign relicDisplayPrefab.", this);
        if (dieTooltipOverlay == null)
            Debug.LogWarning($"MapUnknownEventOutcomeResultView on '{name}': assign dieTooltipOverlay for curse outcomes.", this);
    }
#endif

    public void Show(UnknownMapEventOutcomeResult result, Sprite eventArt, Action onContinue)
    {
        if (result == null)
        {
            onContinue?.Invoke();
            return;
        }

        _onContinue = onContinue;
        ActivatePanel();
        dieTooltipOverlay?.Hide();

        ClearSpawnedContent();

        switch (result.Kind)
        {
            case UnknownMapEventOutcomeResultKind.RelicGranted:
                ApplyCopy(relicResultTitle, relicResultDescription);
                SpawnRelicDisplay(result.Relic);
                break;
            case UnknownMapEventOutcomeResultKind.CurseFaceAdded:
                ApplyCopy(curseResultTitle, curseResultDescription);
                SpawnDieDisplay(result);
                break;
            default:
                ApplyCopy(string.Empty, string.Empty);
                break;
        }

        ApplyIcon(eventArtImage, eventArt, eventArt != null);
        RebuildContentLayout();
    }

    Action _onContinue;

    void ActivatePanel()
    {
        if (panel != null)
            panel.SetActive(true);
        gameObject.SetActive(true);
    }

    void ApplyCopy(string title, string body)
    {
        if (titleText != null)
            titleText.text = title ?? string.Empty;
        if (bodyText != null)
            bodyText.text = body ?? string.Empty;
    }

    void SpawnDieDisplay(UnknownMapEventOutcomeResult result)
    {
        if (dieDisplayPrefab == null)
        {
            Debug.LogError($"MapUnknownEventOutcomeResultView on '{name}': dieDisplayPrefab is not assigned.", this);
            return;
        }

        if (contentRoot == null)
        {
            Debug.LogError($"MapUnknownEventOutcomeResultView on '{name}': contentRoot is not assigned.", this);
            return;
        }

        var die = ResolveAffectedDie(result);
        if (die == null)
        {
            Debug.LogError(
                $"MapUnknownEventOutcomeResultView on '{name}': curse result has no affected die (deck index {result.AffectedDieDeckIndex}).",
                this);
            return;
        }

        _spawnedContent = Instantiate(dieDisplayPrefab, contentRoot, false);
        _spawnedContent.SetActive(true);
        ApplySpawnedRectLayout(_spawnedContent.transform as RectTransform);

        foreach (var breath in _spawnedContent.GetComponentsInChildren<BreathAnimationScript>(true))
            breath.enabled = false;

        var label = _spawnedContent.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.text = string.IsNullOrWhiteSpace(die.dieName) ? die.name : die.dieName;

        var trayView = _spawnedContent.GetComponent<DiceTrayButtonView>();
        if (trayView != null)
        {
            trayView.SetIcon(die.uiIcon != null ? die.uiIcon : result.PrimaryIcon);
            trayView.SetSelected(false);
            trayView.SetSelectedIconShakeEnabled(false);
        }
        else
        {
            Debug.LogError(
                $"MapUnknownEventOutcomeResultView on '{name}': dieDisplayPrefab needs DiceTrayButtonView.",
                dieDisplayPrefab);
        }

        DisableDieButtonClicks(_spawnedContent);
        RebuildContentLayout();
        ShowDieTooltip(die);
    }

    void ShowDieTooltip(DieAssetSO die)
    {
        if (die == null)
            return;

        if (dieTooltipOverlay == null)
        {
            Debug.LogError($"MapUnknownEventOutcomeResultView on '{name}': dieTooltipOverlay is not assigned.", this);
            return;
        }

        dieTooltipOverlay.ShowDie(die, facesInteractable: false);
    }

    static DieAssetSO ResolveAffectedDie(UnknownMapEventOutcomeResult result)
    {
        if (result == null)
            return null;

        if (result.AffectedDie != null)
            return result.AffectedDie;

        if (result.AffectedDieDeckIndex < 0)
            return null;

        var deck = PlayerDataContainer.Instance?.RuntimeData?.currentDeck;
        if (deck == null || result.AffectedDieDeckIndex >= deck.Count)
            return null;

        return deck[result.AffectedDieDeckIndex];
    }

    void SpawnRelicDisplay(RelicSO relic)
    {
        if (relic == null || relicDisplayPrefab == null || contentRoot == null)
            return;

        _spawnedContent = Instantiate(relicDisplayPrefab, contentRoot, false);
        _spawnedContent.SetActive(true);
        ApplySpawnedRectLayout(_spawnedContent.transform as RectTransform);

        var slotView = _spawnedContent.GetComponent<RunRelicSlotView>();
        if (slotView != null)
            slotView.Bind(relic);
        else
        {
            Debug.LogError(
                $"MapUnknownEventOutcomeResultView on '{name}': relicDisplayPrefab needs RunRelicSlotView.",
                relicDisplayPrefab);
        }

        DisableDieButtonClicks(_spawnedContent);
        RebuildContentLayout();
    }

    void ApplySpawnedRectLayout(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        var scale = Mathf.Max(0.01f, displayScaleFactor);
        rect.localScale = new Vector3(scale, scale, scale);
    }

    void RebuildContentLayout()
    {
        if (contentRoot is not RectTransform contentRect)
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        Canvas.ForceUpdateCanvases();
    }

    static void DisableDieButtonClicks(GameObject root)
    {
        if (root == null)
            return;

        var buttons = root.GetComponentsInChildren<Button>(true);
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].onClick.RemoveAllListeners();
            buttons[i].enabled = false;
        }
    }

    void ClearSpawnedContent()
    {
        dieTooltipOverlay?.Hide();

        if (_spawnedContent != null)
        {
            Destroy(_spawnedContent);
            _spawnedContent = null;
        }

        if (contentRoot == null)
            return;

        for (var i = contentRoot.childCount - 1; i >= 0; i--)
        {
            var child = contentRoot.GetChild(i);
            if (child != null)
                Destroy(child.gameObject);
        }
    }

    static void ApplyIcon(Image image, Sprite sprite, bool showSlot)
    {
        if (image == null)
            return;

        if (!showSlot)
        {
            image.enabled = false;
            return;
        }

        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    void OnContinueClicked()
    {
        var cb = _onContinue;
        _onContinue = null;
        HideImmediate();
        cb?.Invoke();
    }

    public void HideImmediate()
    {
        dieTooltipOverlay?.Hide();
        ClearSpawnedContent();
        if (panel != null)
            panel.SetActive(false);
    }
}
