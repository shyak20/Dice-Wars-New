using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row in <see cref="DieDisambiguationView"/>: die name + die icon from <see cref="DieAssetSO.uiIcon"/> + click to select.
/// </summary>
public class DieDisambiguationRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text dieNameText;
    [SerializeField] private Image dieIconImage;
    [SerializeField] private Button selectButton;

    private void Awake()
    {
        if (selectButton == null)
            Debug.LogError($"DieDisambiguationRowUI on '{gameObject.name}': assign selectButton.");
    }

    public void Bind(DieAssetSO die, Action<DieAssetSO> onSelected)
    {
        if (die == null) return;
        if (dieNameText != null) dieNameText.text = die.dieName;

        if (dieIconImage != null)
        {
            dieIconImage.sprite = die.uiIcon;
            dieIconImage.enabled = die.uiIcon != null;
        }

        if (die.uiIcon == null)
            Debug.LogWarning($"DieDisambiguationRowUI: Die '{die.dieName}' has no uiIcon on its DieAssetSO; row will show no icon.");

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelected?.Invoke(die));
        }
    }
}
