using System;
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

    private int _characterIndex = -1;
    private Action<int> _onSelected;

    public void Bind(int characterIndex, PlayerDataSO character, Action<int> onSelected)
    {
        if (character == null)
            throw new ArgumentNullException(nameof(character));

        _characterIndex = characterIndex;
        _onSelected = onSelected;

        if (portraitImage != null)
        {
            var sprite = character.SmallPortrait;
            portraitImage.sprite = sprite;
            portraitImage.enabled = sprite != null;
        }

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
    }

    public void SetSelected(bool selected)
    {
        if (selectedVisual != null)
            selectedVisual.SetActive(selected);
    }

    void HandleClicked()
    {
        if (_characterIndex < 0)
            return;
        _onSelected?.Invoke(_characterIndex);
    }
}
