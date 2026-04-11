using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One row in the win-stage rewards list: gold amount + collect button.</summary>
public class WinStageGoldRewardRow : MonoBehaviour
{
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private Button collectButton;

    private int _amount;
    private Action _onCollected;

    private void Awake()
    {
        if (collectButton == null)
            Debug.LogError($"WinStageGoldRewardRow on '{name}': assign collectButton.");
    }

    public void Setup(int amount, Action onCollected)
    {
        _amount = Mathf.Max(0, amount);
        _onCollected = onCollected;

        if (amountText != null)
            amountText.text = _amount.ToString();

        if (collectButton == null)
        {
            Debug.LogError($"WinStageGoldRewardRow on '{name}': assign Collect Button on the prefab — clicks won't grant gold.");
            return;
        }

        collectButton.onClick.RemoveAllListeners();
        collectButton.onClick.AddListener(OnCollectClicked);
    }

    private void OnCollectClicked()
    {
        if (_amount > 0)
            RunEconomyManager.TryGetRuntime()?.GrantGold(_amount, null);

        _onCollected?.Invoke();
        Destroy(gameObject);
    }
}
