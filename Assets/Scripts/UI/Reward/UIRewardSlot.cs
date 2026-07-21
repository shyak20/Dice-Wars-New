using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One reward card in the face picker (name, value, rarity, icon).
/// </summary>
public class UIRewardSlot : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private TMP_Text rarityText;
    [Header("Rarity text colors")]
    [SerializeField] private Color commonRarityColor = Color.white;
    [SerializeField] private Color rareRarityColor = new Color(0.2f, 0.6f, 1f);
    [SerializeField] private Color legendaryRarityColor = new Color(1f, 0.474f, 0.052f);
    [SerializeField] private Image iconImage;
    [SerializeField] private Image typeIconImage;
    [Tooltip("Optional. Same effect overlay icon as the 3D die face (DieFaceEffectIconResolver). Hidden when the face has no qualifying action icon.")]
    [SerializeField] private Image faceEffectIconImage;
    [Tooltip("Optional. Behind card content; uses DieFaceSO.uiTooltipBackground.")]
    [SerializeField] private Image tooltipBackgroundImage;
    [SerializeField] private Button button;
    [Header("Optional hover highlight")]
    [Tooltip("Shown while the pointer is over the slot (same raycast target as the Button). Leave empty to disable.")]
    [SerializeField] private GameObject hoverRevealObject;
    [Header("Optional Status Hover Tooltip")]
    [SerializeField] private HoverTooltipTargetUI statusHoverTooltipTarget;

    [Header("Face swap confirmation (die tooltip)")]
    [Tooltip("Shown after the player replaces this face; assign a child Image or set New Face Preview Image.")]
    [SerializeField] private GameObject newFacePickedRevealRoot;
    [SerializeField] private Image newFacePickedPreviewImage;
    [Header("Curse face highlight")]
    [Tooltip("Animator on the face-replace overlay (defaults to Animator on New Face Picked Reveal Root).")]
    [SerializeField] private Animator curseStateAnimator;
    [SerializeField] private string curseAnimatorBoolParameter = "Curse";

    private bool _hoverRevealEnabled = true;
    private bool _standaloneHoverRevealWired;
    private bool _faceTooltipIncludesHeader;
    private DieFaceSO _face;
    private int _curseAnimatorBoolHash;
    public DieFaceSO Face => _face;

    /// <summary>Click target for this Face Action Option card (may be null if misconfigured).</summary>
    public Button OptionButton => button;

    private void Awake()
    {
        if (button == null)
            Debug.LogError($"UIRewardSlot on '{gameObject.name}': assign button.");

        CacheCurseAnimatorBoolHash();
    }

#if UNITY_EDITOR
    private void OnValidate() => CacheCurseAnimatorBoolHash();
#endif

    void CacheCurseAnimatorBoolHash()
    {
        _curseAnimatorBoolHash = string.IsNullOrWhiteSpace(curseAnimatorBoolParameter)
            ? 0
            : Animator.StringToHash(curseAnimatorBoolParameter.Trim());
    }

    public void Bind(DieFaceSO face, System.Action<DieFaceSO> onPicked)
    {
        _face = face;

        if (face == null)
        {
            ApplyFaceEffectIcon(null);
            HideNewFacePickedPreviewWithoutRecurse();
            return;
        }

        if (nameText != null) nameText.text = face.Title;
        if (descriptionText != null) descriptionText.text = face.Description;
        if (valueText != null) valueText.text = face.value.ToString();
        ApplyRarityText(face.rarity);

        var faceSprite = ResolveFaceUiIcon(face);
        var elementSprite = GameIconCatalog.GetElementIcon(face.type);
        if (iconImage != null)
        {
            iconImage.sprite = faceSprite;
            iconImage.enabled = faceSprite != null;
        }

        if (typeIconImage != null)
        {
            typeIconImage.sprite = elementSprite;
            typeIconImage.enabled = elementSprite != null;
        }

        ApplyFaceEffectIcon(face);

        if (tooltipBackgroundImage != null)
            DieTooltipBackgrounds.ApplyFaceTooltip(tooltipBackgroundImage, face);

        SetupStatusHoverTooltip(face);

        _hoverRevealEnabled = true;
        if (hoverRevealObject != null)
            hoverRevealObject.SetActive(false);

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (onPicked != null)
                button.onClick.AddListener(() => onPicked.Invoke(_face));
        }

        // Reveal root + curse appear are only for newly picked faces (ShowNewFacePickedPreview), not Bind.
        HideNewFacePickedPreviewWithoutRecurse();
    }

    /// <summary>
    /// After a successful face replacement, shows <see cref="newFacePickedRevealRoot"/> and the new face art
    /// (uses <see cref="newFacePickedPreviewImage"/> or the first <see cref="Image"/> under the reveal root).
    /// Plays the curse appear animation when the new face is a curse.
    /// </summary>
    public void ShowNewFacePickedPreview(DieFaceSO newFace)
    {
        if (newFacePickedRevealRoot == null || newFace == null)
            return;

        ApplyNewFacePreviewImage(newFace);
        newFacePickedRevealRoot.SetActive(true);
        PlayCurseAppearAnimator(newFace.type == DieType.Curse);
    }

    public void HideNewFacePickedPreview()
    {
        HideNewFacePickedPreviewWithoutRecurse();
    }

    void ApplyNewFacePreviewImage(DieFaceSO face)
    {
        if (face == null || newFacePickedRevealRoot == null)
            return;

        var img = newFacePickedPreviewImage;
        if (img == null)
            img = newFacePickedRevealRoot.GetComponentInChildren<Image>(true);
        if (img == null)
        {
            Debug.LogError($"UIRewardSlot on '{gameObject.name}': no preview Image under newFacePickedRevealRoot.", this);
            return;
        }

        var sprite = ResolveFaceUiIcon(face);
        img.sprite = sprite;
        img.enabled = sprite != null;
        img.gameObject.SetActive(true);
    }

    static Sprite ResolveFaceUiIcon(DieFaceSO face)
    {
        if (face == null)
            return null;

        if (face.uiIcon != null)
            return face.uiIcon;

        return GameIconCatalog.GetElementIcon(face.type);
    }

    /// <summary>
    /// Mirrors <see cref="DieVisualizer"/> face-effect quads: show only when
    /// <see cref="DieFaceEffectIconResolver.TryResolve"/> returns a sprite.
    /// </summary>
    void ApplyFaceEffectIcon(DieFaceSO face)
    {
        if (faceEffectIconImage == null)
            return;

        if (face != null && DieFaceEffectIconResolver.TryResolve(face, out var effectSprite) && effectSprite != null)
        {
            faceEffectIconImage.sprite = effectSprite;
            faceEffectIconImage.enabled = true;
            faceEffectIconImage.gameObject.SetActive(true);
            return;
        }

        faceEffectIconImage.sprite = null;
        faceEffectIconImage.enabled = false;
        faceEffectIconImage.gameObject.SetActive(false);
    }

    void PlayCurseAppearAnimator(bool isCurse)
    {
        var animator = ResolveCurseAnimator();
        if (animator == null || animator.runtimeAnimatorController == null || _curseAnimatorBoolHash == 0)
            return;

        if (!animator.enabled)
            animator.enabled = true;

        animator.Rebind();
        animator.Update(0f);
        animator.SetBool(_curseAnimatorBoolHash, isCurse);
        animator.Update(0f);
    }

    void HideNewFacePickedPreviewWithoutRecurse()
    {
        if (newFacePickedRevealRoot != null)
            newFacePickedRevealRoot.SetActive(false);

        var animator = ResolveCurseAnimator();
        if (animator == null || animator.runtimeAnimatorController == null || _curseAnimatorBoolHash == 0)
            return;

        animator.SetBool(_curseAnimatorBoolHash, false);
    }

    Animator ResolveCurseAnimator()
    {
        if (curseStateAnimator != null)
            return curseStateAnimator;

        if (newFacePickedRevealRoot != null)
            return newFacePickedRevealRoot.GetComponent<Animator>();

        return GetComponentInChildren<Animator>(true);
    }

    /// <summary>For preview-only slots; swap overlay disables the reward button.</summary>
    public void SetInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    /// <summary>
    /// When false, pointer hover no longer shows <see cref="hoverRevealObject"/> (and it is hidden immediately).
    /// </summary>
    public void SetHoverRevealEnabled(bool enabled)
    {
        _hoverRevealEnabled = enabled;
        if (!enabled && hoverRevealObject != null)
            hoverRevealObject.SetActive(false);
    }

    private void ApplyHoverRevealPointerEnter()
    {
        if (!_hoverRevealEnabled || hoverRevealObject == null) return;
        hoverRevealObject.SetActive(true);
    }

    private void ApplyHoverRevealPointerExit()
    {
        if (hoverRevealObject == null) return;
        hoverRevealObject.SetActive(false);
    }

    /// <summary>Used for average-roll hover on the swap overlay (raycast target).</summary>
    public GameObject GetHoverTarget()
    {
        return button != null ? button.gameObject : gameObject;
    }

    /// <summary>
    /// Call from code that builds <see cref="EventTrigger"/> PointerEnter/Exit for face tooltips (before <c>triggers.Add</c>).
    /// Adds show/hide for <see cref="hoverRevealObject"/> on the same entries so a later <c>triggers.Clear()</c> is not used for reveal.
    /// </summary>
    public void AppendHoverRevealListeners(EventTrigger.Entry pointerEnter, EventTrigger.Entry pointerExit)
    {
        if (hoverRevealObject == null) return;
        if (pointerEnter != null)
            pointerEnter.callback.AddListener(_ => ApplyHoverRevealPointerEnter());
        if (pointerExit != null)
            pointerExit.callback.AddListener(_ => ApplyHoverRevealPointerExit());
    }

    /// <summary>
    /// When nothing else wires pointer hover on this slot (reward picker, shop face row, die tooltip grid), registers
    /// self-contained enter/exit. Safe to call on rebind — listeners are only added once per slot.
    /// Do not use together with external <c>EventTrigger.triggers.Clear()</c> on the same button unless you also call <see cref="AppendHoverRevealListeners"/>.
    /// </summary>
    public void EnsureStandaloneHoverReveal()
    {
        if (hoverRevealObject == null || button == null) return;
        hoverRevealObject.SetActive(false);
        if (_standaloneHoverRevealWired) return;
        _standaloneHoverRevealWired = true;
        var go = button.gameObject;
        var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ApplyHoverRevealPointerEnter());
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => ApplyHoverRevealPointerExit());
        et.triggers.Add(enter);
        et.triggers.Add(exit);
    }

    private void ApplyRarityText(FaceRarity rarity)
    {
        if (rarityText == null)
            return;

        rarityText.text = rarity.ToString();
        rarityText.color = GetRarityColor(rarity);
    }

    private Color GetRarityColor(FaceRarity rarity) =>
        rarity switch
        {
            FaceRarity.Rare => rareRarityColor,
            FaceRarity.Legendary => legendaryRarityColor,
            _ => commonRarityColor,
        };

    private void SetupStatusHoverTooltip(DieFaceSO face)
    {
        var hoverGo = GetHoverTarget();
        if (hoverGo == null) return;

        // Always bind the hover target to the actual raycast target (button). A serialized reference on
        // another child object will never receive pointer enter/exit for this slot.
        var target = statusHoverTooltipTarget;
        if (target == null || target.gameObject != hoverGo)
            target = hoverGo.GetComponent<HoverTooltipTargetUI>() ?? hoverGo.AddComponent<HoverTooltipTargetUI>();
        statusHoverTooltipTarget = target;

        // The shared manager resolves face content (main tooltip + stacked status/effect explanations).
        target.SetScriptableSource(face);
        target.SetFaceHeaderTooltipEnabled(_faceTooltipIncludesHeader);
    }

    /// <summary>
    /// When true, hovering this slot's face shows the face name/description tooltip (with the status/effect
    /// explanation stacked under it). Enable where the slot icon does not already show name/description
    /// (Die Tooltip grid, face-replace screen). Face-picker cards leave this false.
    /// </summary>
    public void SetFaceTooltipIncludesHeader(bool includesHeader)
    {
        _faceTooltipIncludesHeader = includesHeader;
        if (statusHoverTooltipTarget != null)
            statusHoverTooltipTarget.SetFaceHeaderTooltipEnabled(includesHeader);
    }
}
