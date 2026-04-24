using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Single reward row visual in the map treasure panel.</summary>
public sealed class MapTreasureRewardItemView : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [Tooltip("Optional. Shows amount text for stackable rewards (e.g. gold).")]
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private Button collectButton;
    [Tooltip("Optional; usually on the icon Image. When set, relic rows show RelicTooltipUI on hover.")]
    [SerializeField] private RelicTooltipTrigger relicTooltipTrigger;

    public void Setup(Sprite icon, RelicSO relicForTooltip, int amount, Action onCollect)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.raycastTarget = icon != null || relicForTooltip != null;
        }

        if (amountText != null)
        {
            var hasAmount = amount > 0;
            amountText.gameObject.SetActive(true);
            amountText.text = hasAmount ? amount.ToString() : string.Empty;
        }

        if (relicTooltipTrigger != null)
            relicTooltipTrigger.SetRelic(relicForTooltip);

        if (collectButton == null)
        {
            Debug.LogError("MapTreasureRewardItemView: assign collectButton.", this);
            return;
        }

        collectButton.onClick.RemoveAllListeners();
        collectButton.onClick.AddListener(() =>
        {
            onCollect?.Invoke();
            Destroy(gameObject);
        });
    }
}
