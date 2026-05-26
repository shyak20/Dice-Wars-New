using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Debug/readout UI: lists reward faces that are loot-eligible vs trial-gated and still locked.
/// Wire to two <see cref="TMP_Text"/> fields and assign <see cref="facesLootTable"/> (Faces Loot Table).
/// </summary>
[DefaultExecutionOrder(50)]
public sealed class ProgressionFaceAvailabilityDebugUI : MonoBehaviour
{
    [SerializeField] private DiceSelectSceneController diceSelectSceneController;
    [SerializeField] private FaceLootTableSO facesLootTable;
    [SerializeField] private TMP_Text availableFacesText;
    [SerializeField] private TMP_Text lockedFacesText;

    void Awake()
    {
        if (diceSelectSceneController == null)
            diceSelectSceneController = FindObjectOfType<DiceSelectSceneController>(true);

        if (facesLootTable == null)
            Debug.LogError("ProgressionFaceAvailabilityDebugUI: assign facesLootTable (Faces Loot Table asset).", this);
        if (availableFacesText == null || lockedFacesText == null)
            Debug.LogError("ProgressionFaceAvailabilityDebugUI: assign availableFacesText and lockedFacesText.", this);
    }

    void OnEnable()
    {
        if (diceSelectSceneController != null)
            diceSelectSceneController.CharacterPreviewChanged += OnCharacterPreviewChanged;

        ProgressionManager.OnCharacterProgressionChanged += OnProgressionChanged;
        DiceSelectProgressionDisplayGate.DeferredRefreshRequested += OnDeferredProgressionRefreshRequested;
    }

    void Start() => TryRefresh();

    void OnDisable()
    {
        if (diceSelectSceneController != null)
            diceSelectSceneController.CharacterPreviewChanged -= OnCharacterPreviewChanged;

        ProgressionManager.OnCharacterProgressionChanged -= OnProgressionChanged;
        DiceSelectProgressionDisplayGate.DeferredRefreshRequested -= OnDeferredProgressionRefreshRequested;
    }

    void OnCharacterPreviewChanged(PlayerDataSO _) => TryRefresh();

    void OnProgressionChanged(PlayerDataSO _) => TryRefresh();

    void OnDeferredProgressionRefreshRequested() => Refresh();

    void TryRefresh()
    {
        if (!DiceSelectProgressionDisplayGate.ShouldRefreshProgressionDisplays())
            return;

        Refresh();
    }

    public void Refresh()
    {
        if (availableFacesText == null || lockedFacesText == null)
            return;

        EnsureProgressionForSelectedCharacter();

        var progression = ProgressionManager.TryGetRuntime();
        var catalog = progression != null ? progression.Catalog : null;

        ClassifyFaces(
            GetRewardFacePool(),
            catalog,
            progression,
            out var available,
            out var locked);

        availableFacesText.text = FormatList("Available", available);
        lockedFacesText.text = FormatList("Locked", locked);
    }

    void EnsureProgressionForSelectedCharacter()
    {
        if (!TryGetSelectedCharacter(out var character))
            return;

        var progression = ProgressionManager.TryGetRuntime();
        if (progression == null && character.progressionCatalog != null)
            progression = ProgressionManager.EnsureRuntime(character.progressionCatalog);

        if (progression != null && !progression.IsInitializedFor(character))
            progression.InitializeForCharacter(character);
    }

    bool TryGetSelectedCharacter(out PlayerDataSO character)
    {
        character = null;
        if (diceSelectSceneController != null && diceSelectSceneController.TryGetPreviewCharacter(out character))
            return character != null;

        var container = PlayerDataContainer.Instance;
        if (container?.ActiveCharacterTemplate != null)
        {
            character = container.ActiveCharacterTemplate;
            return true;
        }

        return false;
    }

    List<DieFaceSO> GetRewardFacePool()
    {
        var pool = new List<DieFaceSO>();
        if (facesLootTable?.allPossibleFaces == null)
            return pool;

        for (var i = 0; i < facesLootTable.allPossibleFaces.Count; i++)
        {
            var face = facesLootTable.allPossibleFaces[i];
            if (face != null)
                pool.Add(face);
        }

        pool.Sort((a, b) => string.CompareOrdinal(GetDisplayName(a), GetDisplayName(b)));
        return pool;
    }

    static void ClassifyFaces(
        IReadOnlyList<DieFaceSO> pool,
        ProgressionCatalogSO catalog,
        ProgressionManager progression,
        out List<string> available,
        out List<string> locked)
    {
        available = new List<string>();
        locked = new List<string>();

        for (var i = 0; i < pool.Count; i++)
        {
            var face = pool[i];
            if (face == null)
                continue;

            var label = GetDisplayName(face);
            if (IsFaceLocked(face, catalog, progression))
                locked.Add(label);
            else
                available.Add(label);
        }
    }

    static bool IsFaceLocked(DieFaceSO face, ProgressionCatalogSO catalog, ProgressionManager progression)
    {
        var contentId = ProgressionContentIds.ForFace(face);
        if (catalog == null || progression == null)
            return false;

        if (!ProgressionGatedContent.IsGated(catalog, contentId))
            return false;

        return !progression.IsContentUnlocked(contentId);
    }

    static string GetDisplayName(DieFaceSO face) =>
        face != null ? face.Title : string.Empty;

    static string FormatList(string heading, List<string> names)
    {
        if (names == null || names.Count == 0)
            return $"{heading} (0)\n—";

        var sb = new StringBuilder();
        sb.Append(heading).Append(" (").Append(names.Count).Append(")\n");
        for (var i = 0; i < names.Count; i++)
        {
            if (i > 0)
                sb.Append('\n');
            sb.Append(names[i]);
        }

        return sb.ToString();
    }
}
