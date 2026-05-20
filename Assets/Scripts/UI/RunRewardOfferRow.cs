using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Single reward row: icon, title, optional coin amount line, action button, optional hover tooltip.
/// One prefab per reward kind (win screen, map treasure, etc.). Gem, relic, and die titles come from ScriptableObjects at runtime.
/// Hover text uses <see cref="HoverTooltipManager"/> via <see cref="HoverTooltipTargetUI"/> (assign <see cref="tooltipScreenOffset"/> as needed).
/// </summary>
public class RunRewardOfferRow : MonoBehaviour
{
    [Header("Row definition")]
    [Tooltip("When set, runtime icons (gem / relic / die) are applied to this Image instead of Icon Image. Leave empty to use Icon Image.")]
    [SerializeField] private Image iconOverride;

    [Tooltip("Gold row only: string.Format(numberFormat, coinAmount). Empty hides the number line.")]
    [SerializeField] private string numberFormat = "";

    [Header("Tooltip")]
    [Tooltip("Hover target for tooltips. If unset, tries icon then this GameObject.")]
    [SerializeField] private HoverTooltipTargetUI tooltipTarget;
    [SerializeField] private GameObject tooltipHoverArea;
    [SerializeField] private Vector2 tooltipScreenOffset;

    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleTextField;
    [SerializeField] private TMP_Text numberTextField;
    [SerializeField] private Button actionButton;

    private IRewardOfferFlowHost _host;
    private FaceRewardManager _faceRewards;

    private int _goldAmount;
    private int _rubyShardAmount;
    private GemSO _gem;
    private RelicSO _relic;
    private DieAssetSO _die;
    private Action _onCollected;
    private bool _faceFlowSubscribed;

    private void Awake()
    {
        if (actionButton == null)
            Debug.LogError($"{nameof(RunRewardOfferRow)} on '{name}': assign action button.", this);
        EnsureTooltipTarget();
    }

    private void OnDestroy()
    {
        if (_faceFlowSubscribed)
            FaceRewardEvents.OnFaceRewardCompleted -= OnFaceRewardFlowCompletedOnce;
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

    public void SetupRubyShards(int amount, Action onCollected)
    {
        ClearRowState();
        _rubyShardAmount = Mathf.Max(0, amount);
        _onCollected = onCollected;

        ApplyRubyShardAmountDisplay(_rubyShardAmount);
        DisableTooltipTarget();
        WireButtonRubyShards();
    }

    public void SetupGem(
        GemSO gem,
        IRewardOfferFlowHost host,
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

    public void SetupFace(IRewardOfferFlowHost host, FaceRewardManager faceRewardManager)
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
        _rubyShardAmount = 0;
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

    private void ApplyRubyShardAmountDisplay(int amount)
    {
        ApplyGoldAmountDisplay(amount);
    }

    private void HideNumberLine()
    {
        if (numberTextField == null)
            return;
        numberTextField.gameObject.SetActive(false);
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

        t.enabled = true;
        var ttl = gem.DisplayLabel;
        var body = gem.description ?? "";
        t.SetTooltipScreenOffset(tooltipScreenOffset);
        t.SetContent(ttl, body);
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

        t.enabled = true;
        var ttl = string.IsNullOrEmpty(relic.title) ? relic.name : relic.title;
        var body = relic.description ?? "";
        t.SetTooltipScreenOffset(tooltipScreenOffset);
        t.SetContent(ttl, body);
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

        t.enabled = true;
        var ttl = string.IsNullOrEmpty(die.dieName) ? die.name : die.dieName;
        var body = "";
        t.SetTooltipScreenOffset(tooltipScreenOffset);
        t.SetContent(ttl, body);
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

    private void WireButtonRubyShards()
    {
        if (actionButton == null)
            return;
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(OnRubyShardsClicked);
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

    private void OnRubyShardsClicked()
    {
        if (_rubyShardAmount > 0)
            MetaProgressionManager.TryGetRuntime()?.GrantRubyShards(_rubyShardAmount);

        _onCollected?.Invoke();
        Destroy(gameObject);
    }

    private void OnGoldClicked()
    {
        if (_goldAmount > 0)
        {
            DisplayCoins.TryShowFromReward(_goldAmount, transform);
            RunEconomyManager.TryGetRuntime()?.GrantGold(_goldAmount, null);
        }
        _onCollected?.Invoke();
        Destroy(gameObject);
    }

    private void OnGemClicked()
    {
        if (_host == null || _faceRewards == null || _gem == null)
        {
            Debug.LogError($"{nameof(RunRewardOfferRow)}: gem row not configured.");
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
            Debug.LogError($"{nameof(RunRewardOfferRow)}: relic row has no relic.");
            return;
        }

        if (RunManager.Instance == null)
        {
            Debug.LogError($"{nameof(RunRewardOfferRow)}: RunManager missing — cannot grant relic.");
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
            Debug.LogError($"{nameof(RunRewardOfferRow)}: die row has no die.");
            return;
        }

        if (PlayerDataContainer.Instance == null)
        {
            Debug.LogError($"{nameof(RunRewardOfferRow)}: PlayerDataContainer missing — cannot grant die.");
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
            Debug.LogError($"{nameof(RunRewardOfferRow)}: face row not configured.");
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
