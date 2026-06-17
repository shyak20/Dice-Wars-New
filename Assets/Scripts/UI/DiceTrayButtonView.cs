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

    [Header("Appear Animation")]
    [SerializeField] private Animator appearAnimator;
    [SerializeField] private string appearStateName = "Appear Anim";

    /// <summary>
    /// When false, selected-icon shake is off (e.g. shop / other UI). Combat enables this after spawning tray buttons.
    /// </summary>
    private bool _selectedIconShakeEnabled;

    private bool isSelected;
    private Quaternion iconBaseRotation;
    private float shakePhase;
    private int _appearStateHash;

    /// <param name="enabled">Pass true only in the combat scene when using the dice hand for rolls.</param>
    public void SetSelectedIconShakeEnabled(bool enabled) => _selectedIconShakeEnabled = enabled;

    /// <summary>Die artwork rect; use to align tooltips (e.g. face picker) to the icon center.</summary>
    public RectTransform IconRectTransform => iconImage != null ? iconImage.rectTransform : null;

    private void Awake()
    {
        if (selectedImage == null)
            Debug.LogError($"DiceTrayButtonView on '{gameObject.name}': selectedImage is not assigned.");
        if (iconImage == null)
            Debug.LogError($"DiceTrayButtonView on '{gameObject.name}': iconImage is not assigned.");

        iconBaseRotation = iconImage != null ? iconImage.rectTransform.localRotation : Quaternion.identity;
        if (randomizeShakePhase)
            shakePhase = Random.Range(0f, 1000f);

        if (appearAnimator == null)
            appearAnimator = GetComponentInChildren<Animator>(true);
        _appearStateHash = string.IsNullOrWhiteSpace(appearStateName)
            ? 0
            : Animator.StringToHash(appearStateName.Trim());
        if (appearAnimator != null)
            appearAnimator.enabled = false;

        SetSelected(false);
    }

    private void Update()
    {
        if (!isSelected || !_selectedIconShakeEnabled || !shakeWhenSelected || iconImage == null)
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

    /// <summary>Plays the tray appear animation (shop purchase, unknown-event duplicate, etc.). Other tray buttons stay idle.</summary>
    public void PlayAppearAnimation()
    {
        if (appearAnimator == null || appearAnimator.runtimeAnimatorController == null || _appearStateHash == 0)
            return;

        appearAnimator.enabled = true;
        appearAnimator.Rebind();
        appearAnimator.Update(0f);
        appearAnimator.Play(_appearStateHash, 0, 0f);
    }
}
