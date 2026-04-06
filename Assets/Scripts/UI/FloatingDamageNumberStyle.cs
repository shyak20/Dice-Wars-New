using UnityEngine;

[System.Serializable]
public class FloatingDamageNumberStyle
{
    [Tooltip("Prefab root must have RectTransform + FloatingDamageNumberInstance.")]
    public GameObject prefab;
    public Color textColor = Color.white;
    public float duration = 0.85f;
    [Range(0f, 1f)] public float fadeStartNormalized = 0.6f;
    [Tooltip("Random horizontal drift magnitude (min/max, pixels in parent rect space).")]
    public Vector2 horizontalAmplitudeMinMax = new Vector2(-72f, 72f);
    [Tooltip("Random vertical fall distance (min/max, pixels; applied downward).")]
    public Vector2 verticalFallMinMax = new Vector2(48f, 120f);
    [Tooltip("Multiplies horizontal amplitude over normalized time (0–1).")]
    public AnimationCurve horizontalMotionGraph = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("Multiplies vertical fall over normalized time (0–1). Typically rises to 1 so the number moves down over the lifetime.")]
    public AnimationCurve verticalMotionGraph = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
}
