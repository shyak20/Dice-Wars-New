using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One row in the win-stage rewards list: gem icon/name + collect button (opens gem die-selection flow).</summary>
public class WinStageGemRewardRow : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button collectButton;

    private WinStageFlowController _host;
    private FaceRewardManager _faceRewards;
    private GemSO _gem;
    private Action _onCollected;

    private void Awake()
    {
        if (collectButton == null)
            Debug.LogError($"WinStageGemRewardRow on '{name}': assign collectButton.");
    }

    public void Setup(WinStageFlowController host, FaceRewardManager faceRewardManager, GemSO gem, Action onCollected)
    {
        _host = host;
        _faceRewards = faceRewardManager;
        _gem = gem;
        _onCollected = onCollected;

        if (labelText != null)
            labelText.text = gem != null ? gem.DisplayLabel : "Gem";

        if (iconImage != null)
        {
            var icon = gem != null ? gem.icon : null;
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (collectButton == null)
            return;

        collectButton.onClick.RemoveAllListeners();
        collectButton.onClick.AddListener(OnCollectClicked);
    }

    private void OnCollectClicked()
    {
        if (_host == null || _faceRewards == null || _gem == null)
        {
            Debug.LogError("WinStageGemRewardRow: not configured — call Setup from WinStageFlowController.");
            return;
        }

        collectButton.interactable = false;
        _faceRewards.StartGemRewardFromWinStage(
            _gem,
            () =>
            {
                _onCollected?.Invoke();
                Destroy(gameObject);
            },
            () =>
            {
                collectButton.interactable = true;
            });
    }
}
