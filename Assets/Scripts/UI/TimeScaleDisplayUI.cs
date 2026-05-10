using TMPro;
using UnityEngine;

/// <summary>Shows <see cref="Time.timeScale"/> on a TMP label (updates every frame).
/// </summary>
public sealed class TimeScaleDisplayUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [Tooltip("Format string for string.Format; {0} = time scale.")]
    [SerializeField] private string format = "Time scale: {0:0.00}×";

    private void Awake()
    {
        if (label == null)
            Debug.LogError($"TimeScaleDisplayUI on '{name}': assign label (TMP_Text).", this);
    }

    private void Update()
    {
        if (label == null)
            return;
        label.text = string.Format(format, Time.timeScale);
    }
}
