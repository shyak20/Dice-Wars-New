using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Single reward row: icon, title, optional coin amount line, action button, optional hover tooltip.
/// One prefab per reward kind. Gem, relic, and die titles come from ScriptableObjects at runtime.
/// Assign a <see cref="tooltipPanelPrefab"/> (asset or scene instance); prefab assets are instantiated once under this row's Canvas.
/// </summary>
public class WinStageRewardRow : MonoBehaviour
{
    [Header("Row definition")]
    [Tooltip("When set, runtime icons (gem / relic / die) are applied to this Image instead of Icon Image. Leave empty to use Icon Image.")]
    [SerializeField] private Image iconOverride;

    [Tooltip("Gold row only: string.Format(numberFormat, coinAmount). Empty hides the number line.")]
    [SerializeField] private string numberFormat = "";

    [Header("Tooltip")]
    [Tooltip("HoverTooltipPanelUI prefab asset or scene instance. Prefab assets are instantiated under the nearest Canvas.")]
    [SerializeField] private HoverTooltipPanelUI tooltipPanelPrefab;

    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleTextField;
    [SerializeField] private TMP_Text numberTextField;
    [SerializeField] private Button actionButton;
    [Tooltip("Hover target for tooltips. If unset, tries icon then this GameObject.")]
    [SerializeField] private HoverTooltipTargetUI tooltipTarget;
    [SerializeField] private GameObject tooltipHoverArea;
    [SerializeField] private Vector2 tooltipScreenOffset;

    private WinStageFlowController _host;
    private FaceRewardManager _faceRewards;
    private HoverTooltipPanelUI _runtimeTooltipPanel;
    private bool _ownsRuntimeTooltipPanel;

    private int _goldAmount;
    private GemSO _gem;
    private RelicSO _relic;
    private DieAssetSO _die;
    private Action _onCollected;
    private bool _faceFlowSubscribed;

    private void Awake()
    {
        if (actionButton == null)
            Debug.LogError($"{nameof(WinStageRewardRow)} on '{name}': assign action button.", this);
        EnsureTooltipTarget();
    }

    private void OnDestroy()
    {
        if (_faceFlowSubscribed)
            FaceRewardEvents.OnFaceRewardCompleted -= OnFaceRewardFlowCompletedOnce;
        if (_ownsRuntimeTooltipPanel && _runtimeTooltipPanel != null)
            Destroy(_runtimeTooltipPanel.gameObject);
    }

    public void SetupGold(int amount, Action onCollected)
    {
        ClearRowState();
        _goldAmount = Mathf.Max(0, amount);
        _onCollected = onCollected;

        ApplyGoldAmountDisplay(_goldAmount);
        DisableTooltipTarget();
        WireButtonGold();
    }

    public void SetupGem(
        GemSO gem,
        WinStageFlowController host,
        FaceRewardManager faceRewardManager,
        Action onCollected)
    {
        ClearRowState();
        _gem = gem;
        _host = host;
        _faceRewards = faceRewardManager;
        _onCollected = onCollected;

        ApplyCommonVisuals(gem != null ? gem.icon : null);

        if (titleTextField != null)
            titleTextField.text = gem != null ? gem.DisplayLabel : "Gem";

        HideNumberLine();
        ApplyTooltipGem(gem);
        WireButtonGem();
    }

    public void SetupRelic(RelicSO relic, Action onCollected)
    {
        ClearRowState();
        _relic = relic;
        _onCollected = onCollected;

        ApplyCommonVisuals(relic != null ? relic.icon : null);

        if (titleTextField != null)
            titleTextField.text = relic != null
                ? (string.IsNullOrEmpty(relic.title) ? relic.name : relic.title)
                : "Relic";

        HideNumberLine();
        ApplyTooltipRelic(relic);
        WireButtonRelic();
    }

    public void SetupDie(DieAssetSO die, Action onCollected)
    {
        ClearRowState();
        _die = die;
        _onCollected = onCollected;

        ApplyCommonVisuals(die != null ? die.uiIcon : null);

        if (titleTextField != null)
            titleTextField.text = die != null
                ? (string.IsNullOrEmpty(die.dieName) ? die.name : die.dieName)
                : "Die";

        HideNumberLine();
        ApplyTooltipDie(die);
        WireButtonDie();
    }

    public void SetupFace(WinStageFlowController host, FaceRewardManager faceRewardManager)
    {
        ClearRowState();
        _host = host;
        _faceRewards = faceRewardManager;

        HideNumberLine();
        DisableTooltipTarget();
        WireButtonFace();
    }

    private void ClearRowState()
    {
        if (_faceFlowSubscribed)
            FaceRewardEvents.OnFaceRewardCompleted -= OnFaceRewardFlowCompletedOnce;
        _faceFlowSubscribed = false;

        _host = null;
        _faceRewards = null;
        _goldAmount = 0;
        _gem = null;
        _relic = null;
        _die = null;
        _onCollected = null;
    }

    private Image GetIconImage() => iconOverride != null ? iconOverride : iconImage;

    private void ApplyCommonVisuals(Sprite runtimeIcon)
    {
        var img = GetIconImage();
        if (img == null)
            return;
        img.sprite = runtimeIcon;
        img.enabled = runtimeIcon != null;
    }

    private void ApplyGoldAmountDisplay(int amount)
    {
        if (numberTextField == null)
            return;

        if (string.IsNullOrEmpty(numberFormat))
        {
            numberTextField.gameObject.SetActive(false);
            return;
        }

        numberTextField.gameObject.SetActive(true);
        try
        {
            numberTextField.text = string.Format(numberFormat, amount);
        }
        catch (FormatException)
        {
            numberTextField.text = amount.ToString();
        }
    }

    private void HideNumberLine()
    {
        if (numberTextField == null)
            return;
        numberTextField.gameObject.SetActive(false);
    }

    private HoverTooltipPanelUI ResolveTooltipPanel()
    {
        if (_runtimeTooltipPanel != null)
            return _runtimeTooltipPanel;

        if (tooltipPanelPrefab == null)
            return null;

        if (tooltipPanelPrefab.gameObject.scene.IsValid())
        {
            _runtimeTooltipPanel = tooltipPanelPrefab;
            return _runtimeTooltipPanel;
        }

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError(
                $"{nameof(WinStageRewardRow)} on '{name}': Tooltip Panel Prefab is an asset but no Canvas was found in parents — cannot instantiate.",
                this);
            return null;
        }

        _runtimeTooltipPanel = Instantiate(tooltipPanelPrefab, canvas.transform);
        _runtimeTooltipPanel.name = tooltipPanelPrefab.name;
        _ownsRuntimeTooltipPanel = true;
        return _runtimeTooltipPanel;
    }

    private void DisableTooltipTarget()
    {
        var t = ResolveTooltipTarget();
        if (t == null)
            return;
        t.enabled = false;
        t.SetContent("", "");
    }

    private void ApplyTooltipGem(GemSO gem)
    {
        var t = ResolveTooltipTarget();
        if (t == null)
            return;

        if (gem == null)
        {
            t.enabled = false;
            t.SetContent("", "");
            return;
        }

        var panel = ResolveTooltipPanel();
        if (panel == null)
        {
            Debug.LogError(
                $"{nameof(WinStageRewardRow)} on '{name}': assign Tooltip Panel Prefab to show gem tooltip.",
                this);
            t.enabled = false;
            t.SetContent("", "");
            return;
        }

        t.enabled = true;
        var ttl = gem.DisplayLabel;
        var body = gem.description ?? "";
        t.Configure(panel, ttl, body);
        if (tooltipScreenOffset.sqrMagnitude > 0.0001f)
            t.SetTooltipScreenOffset(tooltipScreenOffset);
    }

    private void ApplyTooltipRelic(RelicSO relic)
    {
        var t = ResolveTooltipTarget();
        if (t == null)
            return;

        if (relic == null)
        {
            t.enabled = false;
            t.SetContent("", "");
            return;
        }

        var panel = ResolveTooltipPanel();
        if (panel == null)
        {
            Debug.LogError(
                $"{nameof(WinStageRewardRow)} on '{name}': assign Tooltip Panel Prefab to show relic tooltip.",
                this);
            t.enabled = false;
            t.SetContent("", "");
            return;
        }

        t.enabled = true;
        var ttl = string.IsNullOrEmpty(relic.title) ? relic.name : relic.title;
        var body = relic.description ?? "";
        t.Configure(panel, ttl, body);
        if (tooltipScreenOffset.sqrMagnitude > 0.0001f)
            t.SetTooltipScreenOffset(tooltipScreenOffset);
    }

    private void ApplyTooltipDie(DieAssetSO die)
    {
        var t = ResolveTooltipTarget();
        if (t == null)
            return;

        if (die == null)
        {
            t.enabled = false;
            t.SetContent("", "");
            return;
        }

        var panel = ResolveTooltipPanel();
        if (panel == null)
        {
            Debug.LogError(
                $"{nameof(WinStageRewardRow)} on '{name}': assign Tooltip Panel Prefab to show die tooltip.",
                this);
            t.enabled = false;
            t.SetContent("", "");
            return;
        }

        t.enabled = true;
        var ttl = string.IsNullOrEmpty(die.dieName) ? die.name : die.dieName;
        var body = "";
        t.Configure(panel, ttl, body);
        if (tooltipScreenOffset.sqrMagnitude > 0.0001f)
            t.SetTooltipScreenOffset(tooltipScreenOffset);
    }

    private HoverTooltipTargetUI ResolveTooltipTarget()
    {
        if (tooltipTarget != null)
            return tooltipTarget;
        var icon = GetIconImage();
        var area = tooltipHoverArea != null ? tooltipHoverArea : (icon != null ? icon.gameObject : gameObject);
        tooltipTarget = area.GetComponent<HoverTooltipTargetUI>() ?? area.AddComponent<HoverTooltipTargetUI>();
        return tooltipTarget;
    }

    private void EnsureTooltipTarget()
    {
        if (tooltipTarget == null && tooltipHoverArea == null && GetIconImage() == null)
            return;
        ResolveTooltipTarget();
    }

    private void WireButtonGold()
    {
        if (actionButton == null)
            return;
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(OnGoldClicked);
    }

    private void WireButtonGem()
    {
        if (actionButton == null)
            return;
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(OnGemClicked);
    }

    private void WireButtonRelic()
    {
        if (actionButton == null)
            return;
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(OnRelicClicked);
    }

    private void WireButtonDie()
    {
        if (actionButton == null)
            return;
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(OnDieClicked);
    }

    private void WireButtonFace()
    {
        if (actionButton == null)
            return;
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(OnFaceClicked);
    }

    private void OnGoldClicked()
    {
        if (_goldAmount > 0)
            RunEconomyManager.TryGetRuntime()?.GrantGold(_goldAmount, null);
        _onCollected?.Invoke();
        Destroy(gameObject);
    }

    private void OnGemClicked()
    {
        if (_host == null || _faceRewards == null || _gem == null)
        {
            Debug.LogError($"{nameof(WinStageRewardRow)}: gem row not configured.");
            return;
        }

        actionButton.interactable = false;
        _faceRewards.StartGemRewardFromWinStage(
            _gem,
            () =>
            {
                _onCollected?.Invoke();
                Destroy(gameObject);
            },
            () => { actionButton.interactable = true; });
    }

    private void OnRelicClicked()
    {
        if (_relic == null)
        {
            Debug.LogError($"{nameof(WinStageRewardRow)}: relic row has no relic.");
            return;
        }

        if (RunManager.Instance == null)
        {
            Debug.LogError($"{nameof(WinStageRewardRow)}: RunManager missing — cannot grant relic.");
            return;
        }

        RunManager.Instance.AddRunRelic(_relic);
        _onCollected?.Invoke();
        Destroy(gameObject);
    }

    private void OnDieClicked()
    {
        if (_die == null)
        {
            Debug.LogError($"{nameof(WinStageRewardRow)}: die row has no die.");
            return;
        }

        if (PlayerDataContainer.Instance == null)
        {
            Debug.LogError($"{nameof(WinStageRewardRow)}: PlayerDataContainer missing — cannot grant die.");
            return;
        }

        PlayerDataContainer.Instance.AddDieToDeck(_die);
        _onCollected?.Invoke();
        Destroy(gameObject);
    }

    private void OnFaceClicked()
    {
        if (_host == null || _faceRewards == null)
        {
            Debug.LogError($"{nameof(WinStageRewardRow)}: face row not configured.");
            return;
        }

        FaceRewardEvents.OnFaceRewardCompleted -= OnFaceRewardFlowCompletedOnce;
        FaceRewardEvents.OnFaceRewardCompleted += OnFaceRewardFlowCompletedOnce;
        _faceFlowSubscribed = true;

        SimulationSpeedController.ApplyRealtimeGlobally();
        _host.NotifyFacePickerOpening();
        _faceRewards.StartFaceRewardFromWinStage(OnFacePickerBack);
    }

    private void OnFacePickerBack()
    {
        UnsubscribeFaceCompleted();
        _host.NotifyFacePickerBackedOut();
    }

    private void OnFaceRewardFlowCompletedOnce(DieFaceSO _)
    {
        UnsubscribeFaceCompleted();
        _host.NotifyFaceRewardRowRemoved();
        Destroy(gameObject);
    }

    private void UnsubscribeFaceCompleted()
    {
        if (!_faceFlowSubscribed)
            return;
        FaceRewardEvents.OnFaceRewardCompleted -= OnFaceRewardFlowCompletedOnce;
        _faceFlowSubscribed = false;
    }
}
