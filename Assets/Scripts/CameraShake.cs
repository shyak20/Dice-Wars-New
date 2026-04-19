using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    // A singleton makes it easy to call from anywhere
    public static CameraShake Instance { get; private set; }

    private Vector3 originalPosition;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Must not destroy the whole GameObject: other cameras (e.g. URP overlay) also get this by accident.
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        // Store the camera's resting position
        originalPosition = transform.localPosition;
    }

    /// <summary>
    /// Call this to trigger a shake.
    /// duration: how long it shakes (seconds)
    /// magnitude: how intense the shake is (meters)
    /// </summary>
    public void Shake(float duration, float magnitude)
    {
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeSequence(duration, magnitude));
    }

    private IEnumerator ShakeSequence(float duration, float magnitude)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Calculate a random offset in a sphere
            Vector2 randomPoint = UnityEngine.Random.insideUnitCircle * magnitude;

            // Apply the offset, keeping original Z depth
            transform.localPosition = new Vector3(originalPosition.x + randomPoint.x, originalPosition.y + randomPoint.y, originalPosition.z);

            elapsed += Time.deltaTime;
            yield return null; // Wait for next frame
        }

        // Return camera to original rest point
        transform.localPosition = originalPosition;
        shakeRoutine = null;
    }
}