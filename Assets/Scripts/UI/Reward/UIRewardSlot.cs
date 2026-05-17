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

    private bool _hoverRevealEnabled = true;
    private DieFaceSO _face;
    public DieFaceSO Face => _face;

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
        if (descriptionText != null) descriptionText.text = face.Description;
        if (valueText != null) valueText.text = face.value.ToString();
        ApplyRarityText(face.rarity);

        var faceSprite = face.uiIcon;
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

        if (tooltipBackgroundImage != null)
            DieTooltipBackgrounds.ApplyFaceTooltip(tooltipBackgroundImage, face);

        SetupStatusHoverTooltip(face);

        _hoverRevealEnabled = true;
        if (hoverRevealObject != null)
            hoverRevealObject.SetActive(false);
        HideNewFacePickedPreview();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (onPicked != null)
                button.onClick.AddListener(() => onPicked.Invoke(_face));
        }
    }

    /// <summary>
    /// After a successful face replacement, shows <see cref="newFacePickedRevealRoot"/> and the new face art
    /// (uses <see cref="newFacePickedPreviewImage"/> or the first <see cref="Image"/> under the reveal root).
    /// </summary>
    public void ShowNewFacePickedPreview(DieFaceSO newFace)
    {
        if (newFacePickedRevealRoot == null || newFace == null)
            return;

        var img = newFacePickedPreviewImage;
        if (img == null)
            img = newFacePickedRevealRoot.GetComponentInChildren<Image>(true);
        if (img != null)
        {
            var s = newFace.uiIcon;
            img.sprite = s;
            img.enabled = s != null;
        }

        newFacePickedRevealRoot.SetActive(true);

        var animator = newFacePickedRevealRoot.GetComponent<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }
    }

    public void HideNewFacePickedPreview()
    {
        if (newFacePickedRevealRoot != null)
            newFacePickedRevealRoot.SetActive(false);
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

    /// <summary>
    /// <see cref="DieTooltipOverlayUI"/> drives status copy via its own panel; disable the standalone
    /// <see cref="HoverTooltipTargetUI"/> so it does not compete with overlay <see cref="EventTrigger"/> hovers.
    /// </summary>
    public void SetExternalStatusHoverTooltipEnabled(bool enabled)
    {
        var hoverGo = GetHoverTarget();
        if (hoverGo == null) return;
        var t = statusHoverTooltipTarget;
        if (t == null || t.gameObject != hoverGo)
            t = hoverGo.GetComponent<HoverTooltipTargetUI>();
        if (t != null)
            t.enabled = enabled;
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
    /// When nothing else wires pointer hover on this slot (reward picker, shop face row), registers self-contained enter/exit.
    /// Do not use together with external <c>EventTrigger.triggers.Clear()</c> on the same button unless you also call <see cref="AppendHoverRevealListeners"/>.
    /// </summary>
    public void EnsureStandaloneHoverReveal()
    {
        if (hoverRevealObject == null || button == null) return;
        hoverRevealObject.SetActive(false);
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

        // Always bind status hover to the actual raycast target (button). A serialized reference on
        // another child object will never receive pointer enter/exit for this slot.
        var target = statusHoverTooltipTarget;
        if (target == null || target.gameObject != hoverGo)
            target = hoverGo.GetComponent<HoverTooltipTargetUI>() ?? hoverGo.AddComponent<HoverTooltipTargetUI>();
        statusHoverTooltipTarget = target;

        BuildEffectTooltip(face, out var title, out var description);
        target.SetContent(title, description);
    }

    /// <summary>Shared by reward slots and <see cref="DieTooltipOverlayUI"/> status line (ApplyStatus, Heal, etc.).</summary>
    public static void BuildEffectTooltip(DieFaceSO face, out string title, out string description)
    {
        if (!DieFaceGameIconOnlyTooltipText.TryBuild(face, out title, out description))
        {
            title = string.Empty;
            description = string.Empty;
        }
    }
}
