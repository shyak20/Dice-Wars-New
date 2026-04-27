using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One row in the win-stage rewards list: gem icon/name + collect button (sockets into a random die with a free socket).</summary>
public class WinStageGemRewardRow : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button collectButton;

    private GemSO _gem;
    private Action _onCollected;

    private void Awake()
    {
        if (collectButton == null)
            Debug.LogError($"WinStageGemRewardRow on '{name}': assign collectButton.");
    }

    public void Setup(GemSO gem, Action onCollected)
    {
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
        TrySocketGemToRandomDie(_gem);
        _onCollected?.Invoke();
        Destroy(gameObject);
    }

    private static void TrySocketGemToRandomDie(GemSO gem)
    {
        if (gem == null) return;
        var data = PlayerDataContainer.Instance != null ? PlayerDataContainer.Instance.RuntimeData : null;
        var candidates = PlayerInventory.GetDiceWithEmptyGemSocket(data);
        if (candidates == null || candidates.Count == 0)
        {
            Debug.LogWarning($"WinStageGemRewardRow: could not collect gem '{gem.name}' — no dice have free sockets.");
            return;
        }

        var die = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        if (die != null && die.TrySocketGem(gem))
            Debug.Log($"Win-stage reward: socketed gem '{gem.name}' into die '{die.dieName}'.");
    }
}
