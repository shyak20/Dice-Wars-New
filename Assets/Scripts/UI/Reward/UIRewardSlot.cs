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

        var elementSprite = GameIconCatalog.GetElementIcon(face.type);
        if (iconImage != null)
        {
            iconImage.sprite = elementSprite;
            iconImage.enabled = elementSprite != null;
        }

        if (typeIconImage != null)
        {
            typeIconImage.sprite = elementSprite;
            typeIconImage.enabled = elementSprite != null;
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
}
