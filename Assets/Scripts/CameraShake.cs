using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shakes a camera's local position. Multiple instances may exist (map + preloaded fight);
/// <see cref="Instance"/> and <see cref="ShakeActive"/> resolve the one in the active scene that is enabled.
/// </summary>
public class CameraShake : MonoBehaviour
{
    static readonly List<CameraShake> Registry = new List<CameraShake>();

    /// <summary>Active, enabled shake in <see cref="SceneManager.GetActiveScene"/> when possible.</summary>
    public static CameraShake Instance => ResolveForActiveScene();

    private Vector3 _originalLocalPosition;
    private Coroutine _shakeRoutine;

    private void OnEnable()
    {
        Register();
        CaptureOriginalLocalPosition();
    }

    private void OnDisable()
    {
        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
        }

        transform.localPosition = _originalLocalPosition;
        Unregister();
    }

    private void OnDestroy() => Unregister();

    /// <summary>Shakes the active scene's camera when one is available.</summary>
    public static void ShakeActive(float duration, float magnitude) => Instance?.Shake(duration, magnitude);

    public void Shake(float duration, float magnitude)
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        CaptureOriginalLocalPosition();

        if (_shakeRoutine != null)
            StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeSequence(duration, magnitude));
    }

    private IEnumerator ShakeSequence(float duration, float magnitude)
    {
        var elapsed = 0f;

        while (elapsed < duration)
        {
            var randomPoint = Random.insideUnitCircle * magnitude;
            transform.localPosition = new Vector3(
                _originalLocalPosition.x + randomPoint.x,
                _originalLocalPosition.y + randomPoint.y,
                _originalLocalPosition.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = _originalLocalPosition;
        _shakeRoutine = null;
    }

    private void CaptureOriginalLocalPosition()
    {
        if (_shakeRoutine != null)
            return;
        _originalLocalPosition = transform.localPosition;
    }

    private void Register()
    {
        if (!Registry.Contains(this))
            Registry.Add(this);
    }

    private void Unregister() => Registry.Remove(this);

    static void CleanupDestroyedEntries()
    {
        for (var i = Registry.Count - 1; i >= 0; i--)
        {
            if (Registry[i] == null)
                Registry.RemoveAt(i);
        }
    }

    static CameraShake ResolveForActiveScene()
    {
        CleanupDestroyedEntries();
        if (Registry.Count == 0)
            return null;

        var activeScene = SceneManager.GetActiveScene();
        CameraShake fallback = null;
        for (var i = 0; i < Registry.Count; i++)
        {
            var shake = Registry[i];
            if (shake == null || !shake.isActiveAndEnabled || !shake.gameObject.activeInHierarchy)
                continue;

            if (fallback == null)
                fallback = shake;
            if (activeScene.IsValid() && shake.gameObject.scene == activeScene)
                return shake;
        }

        return fallback;
    }
}
