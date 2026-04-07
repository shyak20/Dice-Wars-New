using TMPro;
using UnityEngine;
using System.Collections;

/// <summary>
/// Shows <see cref="RunEconomyManager.CurrentGold"/> on a TextMeshPro label. Add to the same GameObject as the text or assign the reference.
/// Works in any scene once <see cref="RunEconomyManager"/> exists (typically DontDestroyOnLoad).
/// </summary>
[DisallowMultipleComponent]
public class RunGoldDisplayUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private string format = "Gold: {0}";
    [SerializeField, Min(0f)] private float countDurationSeconds = 0.35f;

    private Coroutine activeCountRoutine;
    private int displayedAmount;

    private void Awake()
    {
        if (label == null)
            label = GetComponent<TMP_Text>();
        if (label == null)
            Debug.LogError($"RunGoldDisplayUI on '{name}': assign a TMP_Text or put this on the same object as the text.", this);
    }

    private void OnEnable()
    {
        RunEconomyManager.OnGoldChanged += OnGoldChanged;
        if (RunEconomyManager.Instance != null)
        {
            displayedAmount = RunEconomyManager.Instance.CurrentGold;
            ApplyText(displayedAmount);
        }
        else
        {
            displayedAmount = 0;
            ApplyText(displayedAmount);
        }
    }

    private void OnDisable()
    {
        RunEconomyManager.OnGoldChanged -= OnGoldChanged;
        if (activeCountRoutine != null)
        {
            StopCoroutine(activeCountRoutine);
            activeCountRoutine = null;
        }
    }

    private void OnGoldChanged(int newTotal)
    {
        if (label == null)
            return;

        if (activeCountRoutine != null)
            StopCoroutine(activeCountRoutine);

        if (countDurationSeconds <= 0f)
        {
            displayedAmount = newTotal;
            ApplyText(displayedAmount);
            return;
        }

        activeCountRoutine = StartCoroutine(AnimateGoldCount(displayedAmount, newTotal, countDurationSeconds));
    }

    private void ApplyText(int amount)
    {
        if (label == null) return;
        label.text = string.Format(format, amount);
    }

    private IEnumerator AnimateGoldCount(int fromAmount, int toAmount, float durationSeconds)
    {
        float elapsed = 0f;

        while (elapsed < durationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / durationSeconds);
            displayedAmount = Mathf.RoundToInt(Mathf.Lerp(fromAmount, toAmount, t));
            ApplyText(displayedAmount);
            yield return null;
        }

        displayedAmount = toAmount;
        ApplyText(displayedAmount);
        activeCountRoutine = null;
    }
}
