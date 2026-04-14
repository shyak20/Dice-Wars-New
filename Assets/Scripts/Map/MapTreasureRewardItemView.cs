using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Single reward row visual in the map treasure panel.</summary>
public sealed class MapTreasureRewardItemView : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Button collectButton;

    public void Setup(Sprite icon, Action onCollect)
    {
        if (iconImage != null)
            iconImage.sprite = icon;

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
