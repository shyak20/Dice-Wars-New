using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatusEffectIconUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text stackText;

    private void Awake()
    {
        if (iconImage == null)
            Debug.LogError($"StatusEffectIconUI on '{gameObject.name}': iconImage is not assigned!");
        if (stackText == null)
            Debug.LogError($"StatusEffectIconUI on '{gameObject.name}': stackText is not assigned!");
    }

    public void Setup(StatusEffectInstance effect)
    {
        iconImage.sprite = effect.Definition.icon;
        UpdateStacks(effect.Stacks);
    }

    public void UpdateStacks(int stacks)
    {
        stackText.text = stacks > 0 ? stacks.ToString() : "";
    }
}
