using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns one <see cref="RollChargeIndicatorView"/> per total roll into a Layout Group and
/// toggles On/Off from remaining vs max (<see cref="CombatEvents.OnRollsRemainingChanged"/>).
/// </summary>
public sealed class RollChargesDisplay : MonoBehaviour
{
    [Tooltip("Parent with a Layout Group. Indicator instances are created as children here.")]
    [SerializeField] private Transform layoutContainer;
    [SerializeField] private RollChargeIndicatorView indicatorPrefab;

    private readonly List<RollChargeIndicatorView> _indicators = new List<RollChargeIndicatorView>();
    private int _lastRemaining = -1;
    private int _lastMax = -1;

    void Awake()
    {
        if (layoutContainer == null)
            layoutContainer = transform;
        if (indicatorPrefab == null)
            Debug.LogError($"{nameof(RollChargesDisplay)} on '{name}': assign indicatorPrefab (RollChargeIndicatorView).", this);
    }

    void OnEnable()
    {
        // Parent UI often disables during Rolling; restore pips when shown again.
        if (_lastMax >= 0)
            SetRolls(_lastRemaining, _lastMax);
    }

    void OnDestroy() => ClearIndicators();

    /// <summary>
    /// Syncs indicator count to <paramref name="max"/> (or remaining when bonus rolls exceed max)
    /// and lights the first <paramref name="remaining"/> pips; the rest are Off.
    /// </summary>
    public void SetRolls(int remaining, int max)
    {
        if (layoutContainer == null)
            layoutContainer = transform;
        if (indicatorPrefab == null)
            return;

        remaining = Mathf.Max(0, remaining);
        max = Mathf.Max(0, max);
        _lastRemaining = remaining;
        _lastMax = max;

        // Always reserve one slot per total roll; grow if remaining temporarily exceeds max.
        var count = Mathf.Max(max, remaining);
        EnsureIndicatorCount(count);

        for (var i = 0; i < _indicators.Count; i++)
        {
            var indicator = _indicators[i];
            if (indicator == null)
                continue;
            if (!indicator.gameObject.activeSelf)
                indicator.gameObject.SetActive(true);
            // Spend from the end: last remaining pips stay On (e.g. 1/4 → Off Off Off On).
            indicator.SetCharged(i >= count - remaining);
        }
    }

    void EnsureIndicatorCount(int count)
    {
        while (_indicators.Count > count)
        {
            var last = _indicators[_indicators.Count - 1];
            _indicators.RemoveAt(_indicators.Count - 1);
            if (last != null)
                Destroy(last.gameObject);
        }

        while (_indicators.Count < count)
        {
            var inst = Instantiate(indicatorPrefab, layoutContainer);
            inst.gameObject.SetActive(true);
            inst.name = $"RollCharge_{_indicators.Count}";
            _indicators.Add(inst);
        }
    }

    void ClearIndicators()
    {
        for (var i = 0; i < _indicators.Count; i++)
        {
            if (_indicators[i] != null)
                Destroy(_indicators[i].gameObject);
        }

        _indicators.Clear();
    }
}
