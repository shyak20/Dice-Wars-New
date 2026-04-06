using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Lives on the floating damage prefab root (RectTransform). Animates anchored position using horizontal/vertical curves and random amplitudes, then destroys itself.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class FloatingDamageNumberInstance : MonoBehaviour
{
    [SerializeField] private TMP_Text damageText;

    public void Begin(
        int damage,
        Vector2 startAnchoredPosition,
        Color textColor,
        float duration,
        float fadeStartNormalized,
        Vector2 horizontalAmplitudeMinMax,
        Vector2 verticalFallMinMax,
        AnimationCurve horizontalGraph,
        AnimationCurve verticalGraph)
    {
        if (damageText == null)
        {
            Debug.LogError($"FloatingDamageNumberInstance on '{gameObject.name}': damageText is not assigned.");
            Destroy(gameObject);
            return;
        }

        damageText.text = damage.ToString();
        var rt = (RectTransform)transform;
        rt.anchoredPosition = startAnchoredPosition;
        var c = textColor;
        c.a = 1f;
        damageText.color = c;

        float hAmp = Random.Range(
            Mathf.Min(horizontalAmplitudeMinMax.x, horizontalAmplitudeMinMax.y),
            Mathf.Max(horizontalAmplitudeMinMax.x, horizontalAmplitudeMinMax.y));
        float vAmp = Random.Range(
            Mathf.Min(verticalFallMinMax.x, verticalFallMinMax.y),
            Mathf.Max(verticalFallMinMax.x, verticalFallMinMax.y));

        StartCoroutine(AnimateRoutine(rt, c, duration, fadeStartNormalized, hAmp, vAmp, horizontalGraph, verticalGraph));
    }

    private IEnumerator AnimateRoutine(
        RectTransform rt,
        Color baseColor,
        float duration,
        float fadeStartNormalized,
        float horizontalAmplitude,
        float verticalFallAmplitude,
        AnimationCurve horizontalGraph,
        AnimationCurve verticalGraph)
    {
        Vector2 start = rt.anchoredPosition;
        float dur = Mathf.Max(0.05f, duration);
        float fadeStart = Mathf.Clamp01(fadeStartNormalized);
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);

            float hx = horizontalGraph != null ? horizontalGraph.Evaluate(u) : u;
            float vy = verticalGraph != null ? verticalGraph.Evaluate(u) : u;
            float xOff = horizontalAmplitude * hx;
            float yOff = -Mathf.Abs(verticalFallAmplitude) * Mathf.Abs(vy);
            rt.anchoredPosition = start + new Vector2(xOff, yOff);

            if (u >= fadeStart && fadeStart < 1f - 0.001f)
            {
                float fadeT = Mathf.InverseLerp(fadeStart, 1f, u);
                baseColor.a = 1f - fadeT;
                damageText.color = baseColor;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private void Awake()
    {
        if (damageText == null)
            Debug.LogError($"FloatingDamageNumberInstance on '{gameObject.name}': assign damageText (TMP).");
    }
}
