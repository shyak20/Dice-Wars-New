using TMPro;
using UnityEngine;

/// <summary>
/// Fight scene: enforces a frame-rate cap and shows a live FPS readout (unscaled time).
/// Add to a GameObject with a <see cref="TMP_Text"/> assigned (e.g. corner of a Screen Space canvas).
/// </summary>
public sealed class FightSceneFrameRateHud : MonoBehaviour
{
    [Header("Frame cap")]
    [SerializeField, Min(-1)] private int targetFrameRate = 60;
    [Tooltip("When on, vSync is turned off so Application.targetFrameRate is honored on this platform.")]
    [SerializeField] private bool disableVerticalSyncForTargetRate = true;

    [Header("Display")]
    [SerializeField] private TMP_Text fpsLabel;
    [SerializeField, Min(0.1f)] private float refreshIntervalSeconds = 0.5f;

    private float _accumulatedUnscaledTime;
    private int _framesInWindow;

    private void Awake()
    {
        if (fpsLabel == null)
            throw new System.InvalidOperationException($"{nameof(FightSceneFrameRateHud)} on '{name}': assign fpsLabel (TMP_Text).");

        if (targetFrameRate > 0)
        {
            if (disableVerticalSyncForTargetRate)
                QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
        }
    }

    private void Update()
    {
        _accumulatedUnscaledTime += Time.unscaledDeltaTime;
        _framesInWindow++;

        if (_accumulatedUnscaledTime < refreshIntervalSeconds)
            return;

        float fps = _framesInWindow / _accumulatedUnscaledTime;
        fpsLabel.text = $"{fps:0} FPS";

        _accumulatedUnscaledTime = 0f;
        _framesInWindow = 0;
    }
}
