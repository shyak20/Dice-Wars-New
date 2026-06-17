using UnityEngine;

/// <summary>
/// Map scene juice when run HP drops (overflow corruption, unknown events, max-HP costs that clamp current HP).
/// Assign hit flash objects on this component, or leave empty to use "Hit Flash Effect (1)" in the scene.
/// Also registers map canvases on the scene camera shake so overlay / world-space UI moves with hits.
/// </summary>
public sealed class MapRunHitFeedback : DamageHitFeedbackBase
{
    private const string DefaultHitFlashObjectName = "Hit Flash Effect (1)";

    [Header("Map UI shake")]
    [Tooltip("Shake scale for Screen Space Overlay and Screen Space Camera UI groups.")]
    [SerializeField, Min(0f)] private float screenCanvasShakeMagnitudeScale = 120f;
    [Tooltip("Shake scale for world-space canvases (same units as camera shake magnitude).")]
    [SerializeField, Min(0f)] private float worldSpaceCanvasShakeMagnitudeScale = 1f;

    private RunManager _subscribedRun;
    private CameraShake _registeredShake;
    private int _registeredUiTargetCount;

    protected override void Awake()
    {
        if (hitFlashRoots == null || hitFlashRoots.Count == 0)
        {
            var flash = GameObject.Find(DefaultHitFlashObjectName);
            if (flash != null)
                hitFlashRoots.Add(flash);
        }

        base.Awake();
    }

    private void OnEnable()
    {
        TrySubscribe(RunManager.Instance);
        TryRegisterMapUiShakeTargets();
    }

    private void Start()
    {
        TrySubscribe(RunManager.Instance);
        TryRegisterMapUiShakeTargets();
    }

    private void OnDisable()
    {
        if (_subscribedRun != null)
        {
            _subscribedRun.OnRunDamageTaken -= OnRunDamageTaken;
            _subscribedRun = null;
        }
    }

    private void TryRegisterMapUiShakeTargets()
    {
        var shake = CameraShake.Instance;
        if (shake == null)
            return;

        if (_registeredShake == shake && _registeredUiTargetCount > 0)
            return;

        if (_registeredShake != null && _registeredShake != shake)
        {
            _registeredShake.ClearRuntimeAdditionalTargets();
            _registeredUiTargetCount = 0;
        }

        var shakeScene = shake.gameObject.scene;
        if (!shakeScene.IsValid())
            return;

        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var beforeCount = shake.GetRuntimeAdditionalTargetCount();
        for (var i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (canvas == null || canvas.gameObject.scene != shakeScene)
                continue;

            shake.RegisterCanvasForUiShake(canvas, screenCanvasShakeMagnitudeScale, worldSpaceCanvasShakeMagnitudeScale);
        }

        _registeredShake = shake;
        _registeredUiTargetCount = shake.GetRuntimeAdditionalTargetCount() - beforeCount;

        if (_registeredUiTargetCount == 0)
        {
            Debug.LogWarning(
                $"MapRunHitFeedback on '{name}': no UI shake targets registered in scene '{shakeScene.name}'. " +
                "Ensure map canvases exist and this component runs in the map scene.",
                this);
        }
    }

    private void TrySubscribe(RunManager run)
    {
        if (run == null || _subscribedRun == run)
            return;

        if (_subscribedRun != null)
            _subscribedRun.OnRunDamageTaken -= OnRunDamageTaken;

        _subscribedRun = run;
        _subscribedRun.OnRunDamageTaken += OnRunDamageTaken;
    }

    private void OnRunDamageTaken(int grossDamage, int hpLost, int maxHp)
    {
        if (!_subscribedRun.UseMapBasedRun)
            return;

        TryRegisterMapUiShakeTargets();
        PlayHit(grossDamage, hpLost, maxHp);
    }
}
