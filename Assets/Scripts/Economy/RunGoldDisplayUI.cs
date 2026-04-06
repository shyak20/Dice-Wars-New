using TMPro;
using UnityEngine;

/// <summary>
/// Shows <see cref="RunEconomyManager.CurrentGold"/> on a TextMeshPro label. Add to the same GameObject as the text or assign the reference.
/// Works in any scene once <see cref="RunEconomyManager"/> exists (typically DontDestroyOnLoad).
/// </summary>
[DisallowMultipleComponent]
public class RunGoldDisplayUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private string format = "Gold: {0}";

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
            OnGoldChanged(RunEconomyManager.Instance.CurrentGold);
        else
            ApplyText(0);
    }

    private void OnDisable()
    {
        RunEconomyManager.OnGoldChanged -= OnGoldChanged;
    }

    private void OnGoldChanged(int newTotal)
    {
        ApplyText(newTotal);
    }

    private void ApplyText(int amount)
    {
        if (label == null) return;
        label.text = string.Format(format, amount);
    }
}
