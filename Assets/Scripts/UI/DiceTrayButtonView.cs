using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Place on the dice deck button prefab. Toggles which child <see cref="Image"/> is visible for regular vs selected.
/// </summary>
public class DiceTrayButtonView : MonoBehaviour
{
    [Header("Background State")]
    [SerializeField] private Image selectedImage;
    [Header("Icon")]
    [SerializeField] private Image iconImage;

    [Header("Selected Icon Shake")]
    [SerializeField] private bool shakeWhenSelected = true;
    [SerializeField, Min(0f)] private float shakeRotationDegrees = 8f;
    [SerializeField, Min(0f)] private float shakeSpeed = 12f;
    [SerializeField] private bool randomizeShakePhase = true;

    private bool isSelected;
    private Quaternion iconBaseRotation;
    private float shakePhase;

    private void Awake()
    {
        if (selectedImage == null)
            Debug.LogError($"DiceTrayButtonView on '{gameObject.name}': selectedImage is not assigned.");
        if (iconImage == null)
            Debug.LogError($"DiceTrayButtonView on '{gameObject.name}': iconImage is not assigned.");

        iconBaseRotation = iconImage != null ? iconImage.rectTransform.localRotation : Quaternion.identity;
        if (randomizeShakePhase)
            shakePhase = Random.Range(0f, 1000f);

        SetSelected(false);
    }

    private void Update()
    {
        if (!isSelected || !shakeWhenSelected || iconImage == null)
            return;

        var angle = Mathf.Sin((Time.unscaledTime + shakePhase) * shakeSpeed) * shakeRotationDegrees;
        iconImage.rectTransform.localRotation = iconBaseRotation * Quaternion.Euler(0f, 0f, angle);
    }

    public void SetIcon(Sprite icon)
    {
        if (iconImage == null) return;
        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selectedImage != null)
            selectedImage.gameObject.SetActive(selected);

        if (!selected && iconImage != null)
            iconImage.rectTransform.localRotation = iconBaseRotation;
    }
}
