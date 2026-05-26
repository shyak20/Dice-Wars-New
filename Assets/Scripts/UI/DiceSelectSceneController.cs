using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Character select screen: pick a <see cref="PlayerCharacterRosterSO"/> profile via character buttons,
/// preview that character's <see cref="PlayerDataSO.currentDeck"/>, then continue with the full profile.
/// </summary>
public sealed class DiceSelectSceneController : MonoBehaviour
{
    [SerializeField] private PlayerCharacterRosterSO characterRoster;
    [SerializeField] private DiceSelectStartingDiceLayout startingDiceLayout;
    [Header("Character UI")]
    [SerializeField] private Transform characterButtonContainer;
    [SerializeField] private DiceSelectCharacterButton characterButtonPrefab;
    [SerializeField] private Image characterPortraitImage;
    [SerializeField] private TMP_Text characterNameLabel;
    [SerializeField] private TMP_Text characterDescriptionLabel;
    [Header("Flow")]
    [SerializeField] private Button continueButton;
    [Tooltip("Optional blocker visual shown while Continue is locked.")]
    [SerializeField] private GameObject continueLockedOverlay;

    private readonly List<PlayerDataSO> _selectableCharacters = new List<PlayerDataSO>();
    private readonly List<DiceSelectCharacterButton> _characterButtons = new List<DiceSelectCharacterButton>();
    private int _selectedCharacterIndex = -1;

    public event Action<PlayerDataSO> CharacterPreviewChanged;

    private void Awake()
    {
        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);
    }

    void OnEnable() => DiceSelectProgressionDisplayGate.DeferredRefreshRequested += OnDeferredProgressionRefreshRequested;

    void OnDisable() => DiceSelectProgressionDisplayGate.DeferredRefreshRequested -= OnDeferredProgressionRefreshRequested;

    void OnDeferredProgressionRefreshRequested() => RefreshCharacterDisplay(replayPortraitReveal: false);

    private void Start()
    {
        if (characterRoster == null)
        {
            Debug.LogError("DiceSelectSceneController: assign characterRoster.", this);
            RefreshFlowUi();
            return;
        }

        BuildSelectableCharacterList();
        if (_selectableCharacters.Count == 0)
        {
            Debug.LogError("DiceSelectSceneController: characterRoster has no valid PlayerDataSO entries.", this);
            RefreshFlowUi();
            return;
        }

        if (startingDiceLayout == null)
            startingDiceLayout = GetComponentInChildren<DiceSelectStartingDiceLayout>(true);

        if (startingDiceLayout == null)
        {
            Debug.LogError("DiceSelectSceneController: assign startingDiceLayout.", this);
            RefreshFlowUi();
            return;
        }

        BuildCharacterButtons();
        SelectCharacter(0);
        RefreshFlowUi();
    }

    private void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(OnContinueClicked);
    }

    public void SelectCharacter(int characterIndex)
    {
        if (characterIndex < 0 || characterIndex >= _selectableCharacters.Count)
            return;

        var replayPortraitReveal = _selectedCharacterIndex >= 0 && _selectedCharacterIndex != characterIndex;
        _selectedCharacterIndex = characterIndex;
        RefreshCharacterDisplay(replayPortraitReveal);
        RefreshCharacterButtonSelection();
        RefreshFlowUi();
    }

    void BuildSelectableCharacterList()
    {
        _selectableCharacters.Clear();
        if (characterRoster?.characters == null)
            return;

        for (var i = 0; i < characterRoster.characters.Count; i++)
        {
            var character = characterRoster.characters[i];
            if (character != null)
                _selectableCharacters.Add(character);
        }
    }

    void BuildCharacterButtons()
    {
        ClearCharacterButtons();

        if (characterButtonContainer == null)
        {
            Debug.LogError("DiceSelectSceneController: assign characterButtonContainer.", this);
            return;
        }

        if (characterButtonPrefab == null)
        {
            Debug.LogError("DiceSelectSceneController: assign characterButtonPrefab.", this);
            return;
        }

        for (var i = 0; i < _selectableCharacters.Count; i++)
        {
            var character = _selectableCharacters[i];
            var buttonView = Instantiate(characterButtonPrefab, characterButtonContainer);
            buttonView.Bind(i, character, SelectCharacter);
            _characterButtons.Add(buttonView);
        }
    }

    void ClearCharacterButtons()
    {
        for (var i = 0; i < _characterButtons.Count; i++)
        {
            if (_characterButtons[i] != null)
                Destroy(_characterButtons[i].gameObject);
        }

        _characterButtons.Clear();
    }

    void RefreshCharacterButtonSelection()
    {
        for (var i = 0; i < _characterButtons.Count; i++)
        {
            var button = _characterButtons[i];
            if (button != null)
                button.SetSelected(i == _selectedCharacterIndex);
        }
    }

    /// <summary>Re-runs preview bind (e.g. after rank-up celebration). Called by <see cref="DiceSelectProgressionCelebrationController"/>.</summary>
    public void RefreshCharacterDisplayPublic() => RefreshCharacterDisplay(replayPortraitReveal: false);

    public void SetInteractionBlocked(bool blocked)
    {
        var hasCharacter = TryGetSelectedCharacter(out _);

        if (continueButton != null)
            continueButton.interactable = !blocked && hasCharacter;
        if (continueLockedOverlay != null)
            continueLockedOverlay.SetActive(!continueButton.interactable);

        for (var i = 0; i < _characterButtons.Count; i++)
        {
            var button = _characterButtons[i];
            if (button != null)
                button.SetInteractable(!blocked);
        }
    }

    void RefreshCharacterDisplay(bool replayPortraitReveal = false)
    {
        if (!TryGetSelectedCharacter(out var character))
            return;

        if (DiceSelectProgressionDisplayGate.HasPendingCelebrationsFor(character))
            DiceSelectProgressionDisplayGate.SetDeferred(true);

        if (DiceSelectProgressionDisplayGate.IsDeferred)
        {
            RefreshCharacterPresentationOnly(character, replayPortraitReveal);
            return;
        }

        RefreshCharacterProgressionAndDisplays(character, replayPortraitReveal);
    }

    void RefreshCharacterPresentationOnly(PlayerDataSO character, bool replayPortraitReveal)
    {
        if (characterNameLabel != null)
            characterNameLabel.text = character.DisplayName;

        if (characterDescriptionLabel != null)
            characterDescriptionLabel.text = character.Description;

        if (characterPortraitImage != null)
        {
            var portrait = character.Portrait;
            characterPortraitImage.sprite = portrait;

            if (replayPortraitReveal && portrait != null)
            {
                var portraitRoot = characterPortraitImage.gameObject;
                portraitRoot.SetActive(false);
                portraitRoot.SetActive(true);
                characterPortraitImage.enabled = true;
            }
            else
                characterPortraitImage.enabled = portrait != null;
        }
    }

    void RefreshCharacterProgressionAndDisplays(PlayerDataSO character, bool replayPortraitReveal)
    {
        if (character.progressionCatalog != null)
            ProgressionManager.EnsureRuntime(character.progressionCatalog);
        ProgressionManager.TryGetRuntime()?.RefreshCharacterProgression(character);

        RefreshCharacterPresentationOnly(character, replayPortraitReveal);

        if (startingDiceLayout != null)
            startingDiceLayout.RefreshDeck(GetEffectiveStartingDeckPreview(character));

        CharacterPreviewChanged?.Invoke(character);
    }

    public bool TryGetPreviewCharacter(out PlayerDataSO character) => TryGetSelectedCharacter(out character);

    IReadOnlyList<DieAssetSO> GetEffectiveStartingDeckPreview(PlayerDataSO character)
    {
        var progression = ProgressionManager.TryGetRuntime();
        if (progression != null)
            return progression.BuildEffectiveStartingDeckTemplates(character);

        return character.currentDeck;
    }

    void RefreshFlowUi()
    {
        var hasCharacter = TryGetSelectedCharacter(out _);

        if (continueButton != null)
            continueButton.interactable = hasCharacter;
        if (continueLockedOverlay != null)
            continueLockedOverlay.SetActive(!hasCharacter);
    }

    bool TryGetSelectedCharacter(out PlayerDataSO character)
    {
        character = null;
        if (_selectedCharacterIndex < 0 || _selectedCharacterIndex >= _selectableCharacters.Count)
            return false;

        character = _selectableCharacters[_selectedCharacterIndex];
        return character != null;
    }

    void OnContinueClicked()
    {
        if (!TryGetSelectedCharacter(out var character))
            return;

        var progression = ProgressionManager.TryGetRuntime();
        var effectiveDeck = progression != null
            ? progression.BuildEffectiveStartingDeckTemplates(character)
            : character.currentDeck;

        if (effectiveDeck == null || effectiveDeck.Count == 0)
        {
            Debug.LogError($"DiceSelectSceneController: '{character.DisplayName}' has an empty currentDeck.", this);
            return;
        }

        if (PlayerDataContainer.Instance == null)
        {
            Debug.LogError("DiceSelectSceneController: PlayerDataContainer missing.", this);
            return;
        }

        PlayerDataContainer.Instance.ApplyCharacterProfile(character);

        if (RunManager.Instance == null)
        {
            Debug.LogError("DiceSelectSceneController: RunManager missing.", this);
            return;
        }

        RunManager.Instance.LoadMapAfterDiceSelect();
    }
}
