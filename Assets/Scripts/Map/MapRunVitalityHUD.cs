using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Map scene: shows <see cref="RunManager"/> run HP and refreshes when vitality changes (overflow damage, shrine heal, etc.).
/// </summary>
public sealed class MapRunVitalityHUD : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text healthText;

    private void Awake()
    {
        if (healthSlider == null && healthText == null)
            Debug.LogError("MapRunVitalityHUD: assign at least healthSlider or healthText.", this);
    }

    private void OnEnable()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnRunVitalityChanged += Refresh;
    }

    private void OnDisable()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnRunVitalityChanged -= Refresh;
    }

    private void Start() => Refresh();

    private void Refresh()
    {
        if (RunManager.Instance == null)
            return;

        var hp = RunManager.Instance.RunCurrentHp;
        var maxHp = RunManager.Instance.RunMaxHp;

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHp;
            healthSlider.value = hp;
        }

        if (healthText != null)
            healthText.text = $"{hp} / {maxHp}";
    }
}
