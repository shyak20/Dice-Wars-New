using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RewardButtonUI : MonoBehaviour
{
    [Header("UI References")]
    public UnityEngine.UI.Image valueIcon; // Replaced TMP_Text with Image
    public TMP_Text typeText;
    public TMP_Text rarityText;
    public UnityEngine.UI.Image backgroundImage;

    private DieFaceSO currentFace;
    private System.Action<DieFaceSO> onSelected;

    public void Setup(DieFaceSO face, System.Action<DieFaceSO> callback)
    {
        currentFace = face;
        onSelected = callback;

        if (valueIcon != null)
        {
            var spr = GameIconCatalog.GetElementIcon(face.type);
            valueIcon.sprite = spr;
            valueIcon.enabled = spr != null;
        }

        if (typeText != null) typeText.text = face.type.ToString();
        if (rarityText != null) rarityText.text = face.rarity.ToString();

        if (rarityText != null)
        {
            rarityText.color = GetRarityColor(face.rarity);
        }

        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onSelected?.Invoke(currentFace));
        }
    }

    private Color GetRarityColor(FaceRarity rarity)
    {
        return rarity switch
        {
            FaceRarity.Common => Color.white,
            FaceRarity.Uncommon => new Color(0.65f, 0.95f, 0.65f),
            FaceRarity.Rare => new Color(0.2f, 0.6f, 1f),
            FaceRarity.Legendary => new Color(1f, 0.8f, 0f),
            FaceRarity.Epic => new Color(0.75f, 0.45f, 1f),
            _ => Color.white
        };
    }
}