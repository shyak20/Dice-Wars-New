using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One selectable character entry on the dice-select screen. Bound at runtime from <see cref="DiceSelectSceneController"/>.
/// </summary>
public sealed class DiceSelectCharacterButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text nameLabel;
    [Tooltip("Shown while this character is the active preview (e.g. selected frame).")]
    [SerializeField] private GameObject selectedVisual;

    [Header("Portrait selection")]
    [Tooltip("Portrait tint when this button is not the active character.")]
    [SerializeField] private Color deselectedPortraitColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [Tooltip("Uniform local scale multiplier on the portrait when not selected (relative to its scale at Bind).")]
    [SerializeField, Min(0f)] private float deselectedPortraitScale = 0.92f;
    [SerializeField, Min(0f)] private float selectionTransitionDuration = 0.25f;
    [SerializeField] private AnimationCurve selectionTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool useUnscaledTime = true;

    private int _characterIndex = -1;
    private Action<int> _onSelected;
    private Color _defaultPortraitColor;
    private Vector3 _defaultPortraitScale = Vector3.one;
    private RectTransform _portraitRect;
    private Coroutine _selectionTransitionRoutine;
    private bool _portraitDefaultsCaptured;

    public void Bind(int characterIndex, PlayerDataSO character, Action<int> onSelected)
    {
        if (character == null)
            throw new ArgumentNullException(nameof(character));

        _characterIndex = characterIndex;
        _onSelected = onSelected;

        RefreshPortrait(character);

        if (nameLabel != null)
            nameLabel.text = character.DisplayName;

        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError($"DiceSelectCharacterButton on '{name}': assign a Button.", this);
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(HandleClicked);

        CapturePortraitDefaultsOnce();
    }

    public void RefreshPortrait(PlayerDataSO character)
    {
        if (character == null)
            throw new ArgumentNullException(nameof(character));

        if (portraitImage == null)
        {
            Debug.LogError($"DiceSelectCharacterButton on '{name}': assign portraitImage.", this);
            return;
        }

        var sprite = ProgressionRankPortraitUtility.GetPortrait(character, useSmallPortrait: true);
        portraitImage.sprite = sprite;
        portraitImage.enabled = sprite != null;
    }

    public void SetSelected(bool selected)
    {
        if (selectedVisual != null)
            selectedVisual.SetActive(selected);

        if (portraitImage == null)
            return;

        var targetColor = selected ? _defaultPortraitColor : deselectedPortraitColor;
        var targetScale = selected ? _defaultPortraitScale : GetDeselectedPortraitScale();
        BeginPortraitTransition(targetColor, targetScale);
    }

    public void SetInteractable(bool interactable)
    {
        if (button == null)
            button = GetComponent<Button>();
        if (button != null)
            button.interactable = interactable;
    }

    void OnDisable() => StopPortraitTransition();

    void CapturePortraitDefaultsOnce()
    {
        if (_portraitDefaultsCaptured || portraitImage == null)
            return;

        _portraitRect = portraitImage.rectTransform;
        _defaultPortraitColor = portraitImage.color;
        _defaultPortraitScale = _portraitRect.localScale;
        _portraitDefaultsCaptured = true;
    }

    Vector3 GetDeselectedPortraitScale() => _defaultPortraitScale * deselectedPortraitScale;

    void BeginPortraitTransition(Color targetColor, Vector3 targetScale)
    {
        CapturePortraitDefaultsOnce();
        StopPortraitTransition();

        if (selectionTransitionDuration <= 0f)
        {
            ApplyPortraitVisual(targetColor, targetScale);
            return;
        }

        _selectionTransitionRoutine = StartCoroutine(CoPortraitTransition(targetColor, targetScale));
    }

    IEnumerator CoPortraitTransition(Color targetColor, Vector3 targetScale)
    {
        var startColor = portraitImage.color;
        var startScale = _portraitRect != null ? _portraitRect.localScale : targetScale;
        var elapsed = 0f;

        while (elapsed < selectionTransitionDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / selectionTransitionDuration);
            var eased = selectionTransitionCurve != null
                ? selectionTransitionCurve.Evaluate(t)
                : t;

            ApplyPortraitVisual(
                Color.Lerp(startColor, targetColor, eased),
                Vector3.Lerp(startScale, targetScale, eased));

            yield return null;
        }

        ApplyPortraitVisual(targetColor, targetScale);
        _selectionTransitionRoutine = null;
    }

    void ApplyPortraitVisual(Color color, Vector3 scale)
    {
        portraitImage.color = color;
        if (_portraitRect != null)
            _portraitRect.localScale = scale;
    }

    void StopPortraitTransition()
    {
        if (_selectionTransitionRoutine == null)
            return;

        StopCoroutine(_selectionTransitionRoutine);
        _selectionTransitionRoutine = null;
    }

    void HandleClicked()
    {
        if (_characterIndex < 0)
            return;
        _onSelected?.Invoke(_characterIndex);
    }
}
