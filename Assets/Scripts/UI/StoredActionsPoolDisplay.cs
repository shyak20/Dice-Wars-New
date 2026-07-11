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

    [Tooltip("Per-enemy element layout: when on, this display ignores ALL global combat pool events and is driven only by drag-assignment deposits (ApplyPoolDelta / ClearAllRows / MultiplyAllDisplayed).")]
    [SerializeField] private bool standalonePerEnemyPool;

    public bool IsStandalonePerEnemyPool => standalonePerEnemyPool;

    [Header("Layout")]
    [Tooltip("Parent for instantiated icons. Use a horizontal/vertical layout group here.")]
    [SerializeField] private RectTransform iconContainer;

    [FormerlySerializedAs("poolIconPrefab")]
    [SerializeField] private StoredActionsPoolIcon rowIconPrefab;

    private Dictionary<PoolRowKey, StoredActionsPoolIcon> iconMap;
    private Dictionary<PoolRowKey, int> displayedPools;
    private readonly Dictionary<PoolRowKey, Sprite> runtimeRowIcons = new Dictionary<PoolRowKey, Sprite>();
    private readonly Dictionary<PoolRowKey, Sprite> runtimeRowBackgrounds = new Dictionary<PoolRowKey, Sprite>();

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
        // Per-enemy pools are driven only by drag-assignment deposits; they must not react to global combat pool events.
        if (standalonePerEnemyPool)
            return;

        CombatEvents.OnStoredActionsPoolIconsFullResync += ApplyFullPoolSync;
        CombatEvents.OnStoredActionsPoolRuntimeIconsClear += ClearRuntimeRowIcons;
        CombatEvents.OnRuntimePoolIconForRow += OnRuntimePoolIconForRow;
        CombatEvents.OnRuntimePoolRowBackgroundForRow += OnRuntimePoolRowBackgroundForRow;
        if (!incrementPoolIconsWithFlyouts)
            CombatEvents.OnStoredActionsPoolUpdated += ApplyFullPoolSync;
    }

    private void OnDisable()
    {
        if (standalonePerEnemyPool)
            return;

        CombatEvents.OnStoredActionsPoolIconsFullResync -= ApplyFullPoolSync;
        CombatEvents.OnStoredActionsPoolRuntimeIconsClear -= ClearRuntimeRowIcons;
        CombatEvents.OnRuntimePoolIconForRow -= OnRuntimePoolIconForRow;
        CombatEvents.OnRuntimePoolRowBackgroundForRow -= OnRuntimePoolRowBackgroundForRow;
        if (!incrementPoolIconsWithFlyouts)
            CombatEvents.OnStoredActionsPoolUpdated -= ApplyFullPoolSync;
    }

    private void OnRuntimePoolIconForRow(PoolRowKey key, Sprite sprite)
    {
        if (sprite == null) return;
        runtimeRowIcons[key] = sprite;
        RefreshIcon(key);
    }

    private void OnRuntimePoolRowBackgroundForRow(PoolRowKey key, Sprite sprite)
    {
        if (sprite == null) return;
        runtimeRowBackgrounds[key] = sprite;
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

    /// <summary>
    /// Fly target for player-only pool rows (self-damage, heal, etc.). Uses the row icon when already visible;
    /// otherwise the icon container so the first increment-mode flyout still has a valid destination.
    /// </summary>
    public RectTransform GetPlayerElementPoolFlyTarget(PoolRowKey key)
    {
        var row = GetFlyTargetRect(key);
        if (row != null && row.gameObject.activeInHierarchy)
            return row;

        return GetIconContainerRect();
    }

    public Sprite GetPoolRowSprite(PoolRowKey key)
    {
        if (runtimeRowIcons.TryGetValue(key, out var rt) && rt != null)
            return rt;
        if (PoolRowKey.TryGetDieType(key, out var dt))
            return GameIconCatalog.GetElementIcon(dt);
        return null;
    }

    public Sprite GetPoolRowBackground(PoolRowKey key)
    {
        if (runtimeRowBackgrounds.TryGetValue(key, out var runtimeBg) && runtimeBg != null)
            return runtimeBg;
        return GameIconCatalog.TryGetPoolRowBackground(key);
    }

    /// <summary>True when at least one row icon is visible in this layout.</summary>
    public bool HasVisibleRows() => GetVisiblePoolIconsTopToBottom().Count > 0;

    /// <summary>Removes one row from the layout when it begins flying to the player status bar.</summary>
    public void ConsumeDisplayedRow(PoolRowKey key)
    {
        if (displayedPools == null)
            return;

        displayedPools[key] = 0;
        RefreshRow(key, 0);
        ReorderPoolIcons();
    }

    public void ApplyPoolDelta(PoolRowKey key, int delta, Sprite lineIconOverride = null, Sprite lineRowBackgroundOverride = null)
    {
        if (delta == 0) return;
        displayedPools.TryGetValue(key, out var cur);
        displayedPools[key] = cur + delta;
        if (lineIconOverride != null)
            runtimeRowIcons[key] = lineIconOverride;
        if (lineRowBackgroundOverride != null)
            runtimeRowBackgrounds[key] = lineRowBackgroundOverride;
        RefreshRow(key, displayedPools[key]);
        ReorderPoolIcons();
    }

    public void ClearRuntimeRowIcons()
    {
        runtimeRowIcons.Clear();
        runtimeRowBackgrounds.Clear();
        foreach (var k in iconMap.Keys.ToList())
            RefreshIcon(k);
    }

    /// <summary>Removes all displayed rows (used when a per-enemy pool's owner leaves the roster).</summary>
    public void ClearAllRows()
    {
        if (displayedPools == null) return;
        displayedPools.Clear();
        runtimeRowIcons.Clear();
        runtimeRowBackgrounds.Clear();
        if (iconMap != null)
        {
            foreach (var kvp in iconMap)
            {
                if (kvp.Value != null)
                    kvp.Value.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>Current displayed amount for a row (per-enemy pools read this to know what to resolve).</summary>
    public int GetDisplayedAmount(PoolRowKey key) =>
        displayedPools != null && displayedPools.TryGetValue(key, out var v) ? v : 0;

    /// <summary>
    /// Resolves the iconMap key and best-known amount for a row icon. Prefer this over
    /// <see cref="StoredActionsPoolIcon.RowKey"/> alone — Configure can drift from the map key.
    /// Returns true when the icon belongs to this display (amount may still be 0).
    /// </summary>
    public bool TryGetRowAmount(StoredActionsPoolIcon icon, out PoolRowKey key, out int amount)
    {
        key = default;
        amount = 0;
        if (icon == null || iconMap == null)
            return false;

        foreach (var kvp in iconMap)
        {
            if (kvp.Value != icon)
                continue;

            key = kvp.Key;
            if (displayedPools != null && displayedPools.TryGetValue(key, out var stored) && stored > 0)
                amount = stored;
            else if (icon.TryGetVisibleAmountText(out var shown) && shown > 0)
                amount = shown;
            return true;
        }

        key = icon.RowKey;
        if (displayedPools != null && displayedPools.TryGetValue(key, out var byRowKey) && byRowKey > 0)
        {
            amount = byRowKey;
            return true;
        }

        if (icon.TryGetVisibleAmountText(out amount) && amount > 0)
            return true;

        return false;
    }

    /// <summary>Shallow copy of current displayed totals (pre-PrepareJackpot flyout state, etc.).</summary>
    public Dictionary<PoolRowKey, int> CopyDisplayedPools()
    {
        var copy = new Dictionary<PoolRowKey, int>();
        if (displayedPools == null)
            return copy;
        foreach (var kvp in displayedPools)
            copy[kvp.Key] = kvp.Value;
        return copy;
    }

    /// <summary>
    /// Repaints every row's amount text from the stored totals. Used after Perfect Cast, where
    /// <see cref="MultiplyAllDisplayed"/> scales the totals with <c>refreshIcons:false</c> and the jackpot reveal
    /// animates the text — this guarantees the final value is shown even if a per-icon reveal was skipped.
    /// </summary>
    public void RefreshDisplayedRows()
    {
        if (displayedPools == null) return;
        foreach (var key in displayedPools.Keys.ToList())
            RefreshRow(key, displayedPools[key]);
        ReorderPoolIcons();
    }

    /// <summary>True when at least one pool row is visible (amount ≥ 1). Used to decide multi-enemy power-orb duplicate flights.</summary>
    public bool HasAnyDisplayedElements()
    {
        if (displayedPools == null)
            return false;

        foreach (var kvp in displayedPools)
        {
            if (kvp.Value >= 1)
                return true;
        }

        return false;
    }

    /// <summary>Perfect Cast: scale every displayed row by the multiplier (per-enemy pools mirror the multiplied board).</summary>
    /// <param name="refreshIcons">When false, only internal totals change; jackpot presentation updates amount text on a delay.</param>
    public void MultiplyAllDisplayed(int multiplier, bool refreshIcons = true)
    {
        if (multiplier <= 1 || displayedPools == null) return;
        foreach (var key in displayedPools.Keys.ToList())
        {
            var scaled = displayedPools[key] * multiplier;
            displayedPools[key] = scaled;
            if (refreshIcons)
                RefreshRow(key, scaled);
        }

        if (refreshIcons)
            ReorderPoolIcons();
    }

    private void ApplyFullPoolSync(Dictionary<PoolRowKey, int> pools) =>
        ApplyFullPoolSync(pools, force: false);

    /// <param name="force">
    /// When true, apply even while <see cref="CombatEvents.DeferStoredActionsPoolIconFullResync"/> is set.
    /// Required for <see cref="FinishJackpotPresentation"/> — that call is the intentional end-of-jackpot write
    /// of post-multiply totals, and must not be swallowed by the defer gate meant only for mid-sequence events.
    /// </param>
    private void ApplyFullPoolSync(Dictionary<PoolRowKey, int> pools, bool force)
    {
        if (!force && !standalonePerEnemyPool && CombatEvents.DeferStoredActionsPoolIconFullResync)
            return;

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
        poolIcon.ResetToIdleVisualState();
        poolIcon.SetPoolSprite(GetPoolRowSprite(key));
        poolIcon.SetRowBackground(GetPoolRowBackground(key));
        poolIcon.SetValue(v);
    }

    private void ReorderPoolIcons()
    {
        var visible = new List<(PoolRowKey key, StoredActionsPoolIcon icon)>();
        foreach (var kvp in iconMap)
        {
            if (kvp.Value == null || !kvp.Value.gameObject.activeInHierarchy) continue;
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

        // Keep any flyout-only row amounts that are not in the combat snapshot (VisualFlyoutOnly deposits, etc.).
        // Snapshot keys overwrite so the visible pre-multiply totals match BuildStoredActionsPool.
        foreach (var kvp in valuesBefore)
            displayedPools[kvp.Key] = kvp.Value;

        foreach (var kvp in iconMap)
        {
            // Clear any prior Perfect Cast reveal flags so this sequence can schedule value updates again.
            if (kvp.Value != null)
                kvp.Value.CancelJackpotValueReveal();
            RefreshIcon(kvp.Key);
        }

        ReorderPoolIcons();
    }

    /// <summary>
    /// Writes a row's stored total without refreshing icon text. Used when the jackpot value reveal owns the
    /// visible amount update so FinishJackpotPresentation can keep internals aligned without a late SetValue.
    /// </summary>
    public void SetDisplayedAmountWithoutRefresh(PoolRowKey key, int amount)
    {
        if (displayedPools == null) return;
        if (amount < 1)
        {
            displayedPools.Remove(key);
            return;
        }

        displayedPools[key] = amount;
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
            if (icon == null || !icon.gameObject.activeInHierarchy) continue;
            list.Add(icon);
        }

        list.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        return list;
    }

    /// <summary>Visible rows in layout order from left to right (or top to bottom for vertical stacks).</summary>
    public List<StoredActionsPoolIcon> GetVisiblePoolIconsInLayoutOrder() => GetVisiblePoolIconsTopToBottom();

    public void HideAllBustDestroyVisuals()
    {
        StoredActionsPoolIcon.HideAllBustDestroyVisualsInScene();
    }

    public void RestoreAllIconDefaultChildStates()
    {
        StoredActionsPoolIcon.RestoreAllDefaultChildVisualStatesInScene();
    }

    public void FinishJackpotPresentation(Dictionary<PoolRowKey, int> valuesAfter)
    {
        StoredActionsPoolIcon.HideAllJackpotPresentationsInScene();

        if (valuesAfter == null)
            return;

        // Keep internal totals on the post-multiply amounts. Do not RefreshRow here — that SetValue would
        // visually update amounts when the container returns home. Visible updates belong to
        // ArmJackpotPostMultiplyValueReveal (scale-up on each Element Value).
        displayedPools.Clear();
        foreach (var kvp in valuesAfter)
            displayedPools[kvp.Key] = kvp.Value;

        var keys = new HashSet<PoolRowKey>(iconMap.Keys);
        foreach (var k in displayedPools.Keys)
            keys.Add(k);

        foreach (var key in keys)
        {
            var amount = displayedPools.TryGetValue(key, out var v) ? v : 0;
            var icon = GetOrCreateIcon(key);
            if (icon == null)
                continue;

            if (amount < 1)
            {
                runtimeRowIcons.Remove(key);
                displayedPools.Remove(key);
                icon.gameObject.SetActive(false);
                continue;
            }

            icon.gameObject.SetActive(true);
            // Fallback only if a mid-sequence reveal never armed / wrote (should be rare after pool-vs-token fix).
            if (!icon.JackpotPostMultiplyValueTextApplied)
                icon.SetValue(amount);
        }

        ReorderPoolIcons();
    }
}
