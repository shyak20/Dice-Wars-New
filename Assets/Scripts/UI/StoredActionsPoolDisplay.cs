using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Shows one icon per deferred <see cref="IGameAction"/> row (<see cref="FaceResult.ActionPoolContributions"/>).
/// Amounts match what will execute (× Perfect Strike where applicable). Not raw face damage/armor unless added as pool rows by an action.
/// </summary>
public class StoredActionsPoolDisplay : MonoBehaviour
{
    [Tooltip("When enabled, row icons only change on flyout landing + full resync. Assign DiceRollOutcomeFlyoutController for per-die flyouts.")]
    [SerializeField] private bool incrementPoolIconsWithFlyouts;

    [Header("Layout")]
    [Tooltip("Parent for instantiated icons. Use a horizontal/vertical layout group here.")]
    [SerializeField] private RectTransform iconContainer;

    [FormerlySerializedAs("poolIconPrefab")]
    [SerializeField] private StoredActionsPoolIcon rowIconPrefab;

    private Dictionary<PoolRowKey, StoredActionsPoolIcon> iconMap;
    private Dictionary<PoolRowKey, int> displayedPools;
    private readonly Dictionary<PoolRowKey, Sprite> runtimeRowIcons = new Dictionary<PoolRowKey, Sprite>();

    private void Awake()
    {
        if (rowIconPrefab == null)
        {
            Debug.LogError($"StoredActionsPoolDisplay on '{name}': assign rowIconPrefab (StoredActionsPoolIcon).");
            return;
        }

        iconMap = new Dictionary<PoolRowKey, StoredActionsPoolIcon>();
        displayedPools = new Dictionary<PoolRowKey, int>();
    }

    private void OnEnable()
    {
        CombatEvents.OnStoredActionsPoolIconsFullResync += ApplyFullPoolSync;
        CombatEvents.OnStoredActionsPoolRuntimeIconsClear += ClearRuntimeRowIcons;
        CombatEvents.OnRuntimePoolIconForRow += OnRuntimePoolIconForRow;
        if (!incrementPoolIconsWithFlyouts)
            CombatEvents.OnStoredActionsPoolUpdated += ApplyFullPoolSync;
    }

    private void OnDisable()
    {
        CombatEvents.OnStoredActionsPoolIconsFullResync -= ApplyFullPoolSync;
        CombatEvents.OnStoredActionsPoolRuntimeIconsClear -= ClearRuntimeRowIcons;
        CombatEvents.OnRuntimePoolIconForRow -= OnRuntimePoolIconForRow;
        if (!incrementPoolIconsWithFlyouts)
            CombatEvents.OnStoredActionsPoolUpdated -= ApplyFullPoolSync;
    }

    private void OnRuntimePoolIconForRow(PoolRowKey key, Sprite sprite)
    {
        if (sprite == null) return;
        runtimeRowIcons[key] = sprite;
        RefreshIcon(key);
    }

    public bool UsesFlyoutIncrementMode => incrementPoolIconsWithFlyouts;

    StoredActionsPoolIcon GetOrCreateIcon(PoolRowKey key)
    {
        if (iconMap.TryGetValue(key, out var existing) && existing != null)
            return existing;

        if (rowIconPrefab == null) return null;

        var parent = iconContainer != null ? iconContainer : (RectTransform)transform;
        var inst = Instantiate(rowIconPrefab, parent);
        inst.name = $"StoredAction_{key.StableId}";
        var rt = inst.transform as RectTransform;
        if (rt != null)
            rt.localScale = Vector3.one;

        var comp = inst.GetComponent<StoredActionsPoolIcon>();
        if (comp == null)
        {
            Debug.LogError("StoredActionsPoolDisplay: rowIconPrefab must have a StoredActionsPoolIcon component.");
            Destroy(inst.gameObject);
            return null;
        }

        comp.Configure(key);
        iconMap[key] = comp;
        comp.gameObject.SetActive(false);
        return comp;
    }

    public RectTransform GetFlyTargetRect(PoolRowKey key)
    {
        var icon = GetOrCreateIcon(key);
        return icon != null ? icon.FlyTargetRect : null;
    }

    public Sprite GetPoolRowSprite(PoolRowKey key)
    {
        if (runtimeRowIcons.TryGetValue(key, out var rt) && rt != null)
            return rt;
        if (PoolRowKey.TryGetDieType(key, out var dt))
            return GameIconCatalog.GetElementIcon(dt);
        return null;
    }

    public Sprite GetPoolRowBackground(PoolRowKey key) => GameIconCatalog.TryGetPoolRowBackground(key);

    public void ApplyPoolDelta(PoolRowKey key, int delta, Sprite lineIconOverride = null)
    {
        if (delta == 0) return;
        displayedPools.TryGetValue(key, out var cur);
        displayedPools[key] = cur + delta;
        if (lineIconOverride != null)
            runtimeRowIcons[key] = lineIconOverride;
        RefreshRow(key, displayedPools[key]);
        ReorderPoolIcons();
    }

    public void ClearRuntimeRowIcons()
    {
        runtimeRowIcons.Clear();
        foreach (var k in iconMap.Keys.ToList())
            RefreshIcon(k);
    }

    private void ApplyFullPoolSync(Dictionary<PoolRowKey, int> pools)
    {
        displayedPools.Clear();
        if (pools != null)
        {
            foreach (var kvp in pools)
                displayedPools[kvp.Key] = kvp.Value;
        }

        var keys = new HashSet<PoolRowKey>(iconMap.Keys);
        foreach (var k in displayedPools.Keys)
            keys.Add(k);

        foreach (var k in keys)
        {
            var v = displayedPools.TryGetValue(k, out var val) ? val : 0;
            RefreshRow(k, v);
        }

        ReorderPoolIcons();
    }

    void RefreshIcon(PoolRowKey key) =>
        RefreshRow(key, displayedPools.TryGetValue(key, out var v) ? v : 0);

    void RefreshRow(PoolRowKey key, int v)
    {
        var poolIcon = GetOrCreateIcon(key);
        if (poolIcon == null) return;

        if (v < 1)
        {
            runtimeRowIcons.Remove(key);
            displayedPools.Remove(key);
            poolIcon.gameObject.SetActive(false);
            return;
        }

        displayedPools[key] = v;
        poolIcon.gameObject.SetActive(true);
        poolIcon.SetPoolSprite(GetPoolRowSprite(key));
        poolIcon.SetRowBackground(GetPoolRowBackground(key));
        poolIcon.SetValue(v);
    }

    private void ReorderPoolIcons()
    {
        var visible = new List<(PoolRowKey key, StoredActionsPoolIcon icon)>();
        foreach (var kvp in iconMap)
        {
            if (kvp.Value == null || !kvp.Value.gameObject.activeSelf) continue;
            visible.Add((kvp.Key, kvp.Value));
        }

        visible.Sort((a, b) => PoolRowKey.Compare(a.key, b.key));
        for (var i = 0; i < visible.Count; i++)
            visible[i].icon.transform.SetSiblingIndex(i);
    }

    /// <summary>Sync pool values and row order for perfect-strike presentation. Does not show jackpot UI per row — caller staggers that.</summary>
    public void PrepareJackpotPresentation(Dictionary<PoolRowKey, int> valuesBefore)
    {
        if (valuesBefore == null || iconMap == null) return;

        foreach (var kvp in valuesBefore)
            displayedPools[kvp.Key] = kvp.Value;

        foreach (var kvp in iconMap)
            RefreshIcon(kvp.Key);

        ReorderPoolIcons();
    }

    public RectTransform GetIconContainerRect() => iconContainer;

    /// <summary>Visible rows in layout order (lowest sibling index first — top of a typical vertical stack).</summary>
    public List<StoredActionsPoolIcon> GetVisiblePoolIconsTopToBottom()
    {
        var list = new List<StoredActionsPoolIcon>();
        if (iconMap == null) return list;

        foreach (var kvp in iconMap)
        {
            var icon = kvp.Value;
            if (icon == null || !icon.gameObject.activeSelf) continue;
            list.Add(icon);
        }

        list.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        return list;
    }

    public void HideAllBustDestroyVisuals()
    {
        if (iconMap == null) return;
        foreach (var kvp in iconMap)
        {
            if (kvp.Value == null) continue;
            kvp.Value.ShowBustDestroyVisual(false);
        }
    }

    public void FinishJackpotPresentation(Dictionary<PoolRowKey, int> valuesAfter)
    {
        if (iconMap == null) return;
        foreach (var kvp in iconMap)
            kvp.Value?.HideJackpotMultiplierBadge();

        if (valuesAfter != null)
            ApplyFullPoolSync(valuesAfter);
    }
}
