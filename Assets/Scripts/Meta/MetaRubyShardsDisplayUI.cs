using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Shows <see cref="MetaProgressionManager.CurrentRubyShards"/> on a TextMeshPro label.
/// </summary>
[DisallowMultipleComponent]
public sealed class MetaRubyShardsDisplayUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private string format = "{0}";
    [SerializeField, Min(0f)] private float countDurationSeconds = 0.35f;

    Coroutine _activeCountRoutine;
    int _displayedAmount;

    void Awake()
    {
        if (label == null)
            label = GetComponent<TMP_Text>();
        if (label == null)
            throw new System.InvalidOperationException(
                $"MetaRubyShardsDisplayUI on '{name}': assign a TMP_Text or put this on the same object as the text.");
    }

    void OnEnable()
    {
        MetaProgressionManager.OnRubyShardsChanged += OnRubyShardsChanged;
        var manager = MetaProgressionManager.Instance;
        _displayedAmount = manager != null ? manager.CurrentRubyShards : 0;
        ApplyText(_displayedAmount);
    }

    void OnDisable()
    {
        MetaProgressionManager.OnRubyShardsChanged -= OnRubyShardsChanged;
        if (_activeCountRoutine != null)
        {
            StopCoroutine(_activeCountRoutine);
            _activeCountRoutine = null;
        }
    }

    void OnRubyShardsChanged(int newTotal)
    {
        if (_activeCountRoutine != null)
            StopCoroutine(_activeCountRoutine);

        if (countDurationSeconds <= 0f)
        {
            _displayedAmount = newTotal;
            ApplyText(_displayedAmount);
            return;
        }

        _activeCountRoutine = StartCoroutine(AnimateCount(_displayedAmount, newTotal, countDurationSeconds));
    }

    void ApplyText(int amount)
    {
        label.text = string.Format(format, amount);
    }

    IEnumerator AnimateCount(int fromAmount, int toAmount, float durationSeconds)
    {
        var elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / durationSeconds);
            _displayedAmount = Mathf.RoundToInt(Mathf.Lerp(fromAmount, toAmount, t));
            ApplyText(_displayedAmount);
            yield return null;
        }

        _displayedAmount = toAmount;
        ApplyText(_displayedAmount);
        _activeCountRoutine = null;
    }
}
