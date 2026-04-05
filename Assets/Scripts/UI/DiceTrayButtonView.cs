using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Place on the dice deck button prefab. Toggles which child <see cref="Image"/> is visible for regular vs selected.
/// </summary>
public class DiceTrayButtonView : MonoBehaviour
{
    [SerializeField] private Image regularImage;
    [SerializeField] private Image selectedImage;

    private void Awake()
    {
        if (regularImage == null && selectedImage == null)
            Debug.LogError($"DiceTrayButtonView on '{gameObject.name}': assign at least one of regularImage / selectedImage.");
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (regularImage != null)
            regularImage.gameObject.SetActive(!selected);
        if (selectedImage != null)
            selectedImage.gameObject.SetActive(selected);
    }
}
