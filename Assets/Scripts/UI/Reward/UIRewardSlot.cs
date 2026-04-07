using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One reward card in the face picker (name, value, rarity, icon).
/// </summary>
public class UIRewardSlot : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image typeIconImage;
    [SerializeField] private Button button;

    [Header("Type Icon Sprites")]
    [SerializeField] private Sprite damageTypeIcon;
    [SerializeField] private Sprite armorTypeIcon;
    [SerializeField] private Sprite fireTypeIcon;
    [SerializeField] private Sprite iceTypeIcon;
    [SerializeField] private Sprite natureTypeIcon;

    private DieFaceSO _face;

    private void Awake()
    {
        if (button == null)
            Debug.LogError($"UIRewardSlot on '{gameObject.name}': assign button.");
    }

    public void Bind(DieFaceSO face, System.Action<DieFaceSO> onPicked)
    {
        _face = face;

        if (face == null) return;

        if (nameText != null) nameText.text = face.Title;
        if (valueText != null) valueText.text = face.value.ToString();
        if (rarityText != null) rarityText.text = face.rarity.ToString();
        if (iconImage != null)
        {
            iconImage.sprite = face.faceIcon;
            iconImage.enabled = face.faceIcon != null;
        }

        if (typeIconImage != null)
        {
            var typeSprite = GetTypeIcon(face.type);
            typeIconImage.sprite = typeSprite;
            typeIconImage.enabled = typeSprite != null;
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (onPicked != null)
                button.onClick.AddListener(() => onPicked.Invoke(_face));
        }
    }

    /// <summary>For preview-only slots; swap overlay disables the reward button.</summary>
    public void SetInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    /// <summary>Used for average-roll hover on the swap overlay (raycast target).</summary>
    public GameObject GetHoverTarget()
    {
        return button != null ? button.gameObject : gameObject;
    }

    private Sprite GetTypeIcon(DieType type)
    {
        switch (type)
        {
            case DieType.Damage:
                return damageTypeIcon;
            case DieType.Armor:
                return armorTypeIcon;
            case DieType.Fire:
                return fireTypeIcon;
            case DieType.Ice:
                return iceTypeIcon;
            case DieType.Nature:
                return natureTypeIcon;
            default:
                return null;
        }
    }
}
