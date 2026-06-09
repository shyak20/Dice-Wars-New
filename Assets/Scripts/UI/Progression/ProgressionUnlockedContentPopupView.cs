using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shown after trial completion when that trial unlocked new faces or relics.
/// Runs before the rank-up celebration in <see cref="DiceSelectProgressionCelebrationController"/>.
/// </summary>
public sealed class ProgressionUnlockedContentPopupView : ProgressionCelebrationPopupViewBase
{
    [Header("Section headers")]
    [SerializeField] private GameObject showFaces;
    [SerializeField] private GameObject showRelics;

    [Header("Content")]
    [SerializeField] private RectTransform itemsLayout;
    [SerializeField] private UIRewardSlot faceSlotPrefab;
    [SerializeField] private RankTrialRewardDisplay relicSlotPrefab;
    [SerializeField] private Button continueButton;

    readonly List<GameObject> _spawnedItems = new List<GameObject>();
    Action _onContinueClicked;

    void Awake()
    {
        ResolvePanelRootInAwake();
        if (panelRoot == null)
            Debug.LogError($"ProgressionUnlockedContentPopupView on '{name}': assign panelRoot.", this);
        if (itemsLayout == null)
            Debug.LogError($"ProgressionUnlockedContentPopupView on '{name}': assign itemsLayout.", this);
        if (faceSlotPrefab == null)
            Debug.LogError($"ProgressionUnlockedContentPopupView on '{name}': assign faceSlotPrefab.", this);
        if (relicSlotPrefab == null)
            Debug.LogError($"ProgressionUnlockedContentPopupView on '{name}': assign relicSlotPrefab.", this);
        if (continueButton == null)
            Debug.LogError($"ProgressionUnlockedContentPopupView on '{name}': assign continueButton.", this);
        if (showFaces == null)
            Debug.LogError($"ProgressionUnlockedContentPopupView on '{name}': assign showFaces.", this);
        if (showRelics == null)
            Debug.LogError($"ProgressionUnlockedContentPopupView on '{name}': assign showRelics.", this);

        if (continueButton != null)
            continueButton.onClick.AddListener(HandleContinueClicked);

        HideImmediate();
    }

    void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(HandleContinueClicked);
    }

    public void Show(IReadOnlyList<ProgressionUnlockedContentItem> items, Action onContinueClicked)
    {
        _onContinueClicked = onContinueClicked;
        ClearSpawnedItems();

        if (items == null || items.Count == 0)
        {
            Debug.LogError("ProgressionUnlockedContentPopupView.Show: no unlock content.", this);
            HandleContinueClicked();
            return;
        }

        var hasFaces = false;
        var hasRelics = false;
        for (var i = 0; i < items.Count; i++)
        {
            if (!items[i].IsValid)
                continue;

            if (items[i].Kind == ProgressionUnlockedContentKind.Face)
                hasFaces = true;
            else if (items[i].Kind == ProgressionUnlockedContentKind.Relic)
                hasRelics = true;
        }

        showFaces.SetActive(hasFaces);
        showRelics.SetActive(hasRelics);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!item.IsValid)
                continue;

            if (item.Kind == ProgressionUnlockedContentKind.Face)
                SpawnFaceSlot(item.Face);
            else if (item.Kind == ProgressionUnlockedContentKind.Relic)
                SpawnRelicSlot(item.Relic);
        }

        if (_spawnedItems.Count == 0)
        {
            Debug.LogError("ProgressionUnlockedContentPopupView.Show: no valid unlock items.", this);
            HandleContinueClicked();
            return;
        }

        ShowPanel();
    }

    public void Hide()
    {
        _onContinueClicked = null;
        ClearSpawnedItems();
        HideImmediate();
    }

    void HideImmediate() => HidePanelImmediate();

    void HandleContinueClicked()
    {
        var callback = _onContinueClicked;
        Hide();
        callback?.Invoke();
    }

    void SpawnFaceSlot(DieFaceSO face)
    {
        var slot = Instantiate(faceSlotPrefab, itemsLayout);
        slot.Bind(face, onPicked: null);
        slot.SetInteractable(false);
        _spawnedItems.Add(slot.gameObject);
    }

    void SpawnRelicSlot(RelicSO relic)
    {
        var row = Instantiate(relicSlotPrefab, itemsLayout);
        row.BindRelic(relic, string.Empty);
        _spawnedItems.Add(row.gameObject);
    }

    void ClearSpawnedItems()
    {
        for (var i = 0; i < _spawnedItems.Count; i++)
        {
            if (_spawnedItems[i] != null)
                Destroy(_spawnedItems[i]);
        }

        _spawnedItems.Clear();
    }

    void OnDisable() => ClearSpawnedItems();
}
