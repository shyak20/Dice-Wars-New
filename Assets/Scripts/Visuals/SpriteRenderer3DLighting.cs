using UnityEngine;

/// <summary>
/// Applies simple runtime lighting to a SpriteRenderer in 3D space.
/// Useful when you want sprite images to react to world-space light behavior.
/// </summary>
public sealed class SpriteRenderer3DLighting : MonoBehaviour
{
    public enum LightingType
    {
        Directional = 0,
        Point = 1
    }

    [Header("References")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Transform lightTransform;

    [Header("Lighting Type")]
    [SerializeField] private LightingType lightingType = LightingType.Point;

    [Header("Light Values")]
    [SerializeField] private Color lightColor = Color.white;
    [SerializeField, Min(0f)] private float lightIntensity = 1f;
    [SerializeField, Min(0f)] private float lightRange = 5f;
    [SerializeField, Range(0f, 1f)] private float ambient = 0.2f;

    [Header("Normals")]
    [Tooltip("Direction the sprite's front face uses for N.L. For typical sprites this is Transform.forward.")]
    [SerializeField] private Vector3 localNormal = Vector3.forward;

    private Color _baseColor;
    private MaterialPropertyBlock _propertyBlock;
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        if (targetRenderer == null)
            throw new System.InvalidOperationException("SpriteRenderer3DLighting requires targetRenderer.");

        if (lightTransform == null)
            throw new System.InvalidOperationException("SpriteRenderer3DLighting requires lightTransform.");

        _baseColor = targetRenderer.color;
        _propertyBlock = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        Vector3 worldNormal = transform.TransformDirection(localNormal.normalized);
        Vector3 lightDirection;
        float attenuation;

        switch (lightingType)
        {
            case LightingType.Directional:
                lightDirection = (-lightTransform.forward).normalized;
                attenuation = 1f;
                break;

            case LightingType.Point:
            default:
                Vector3 toLight = lightTransform.position - targetRenderer.transform.position;
                float distance = toLight.magnitude;
                lightDirection = distance > 0.0001f ? toLight / distance : worldNormal;
                attenuation = lightRange <= 0f ? 0f : Mathf.Clamp01(1f - (distance / lightRange));
                break;
        }

        float ndotl = Mathf.Max(0f, Vector3.Dot(worldNormal, lightDirection));
        float litStrength = ambient + (ndotl * attenuation * lightIntensity);

        Color litColor = _baseColor * lightColor * litStrength;
        litColor.a = _baseColor.a;

        targetRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(ColorId, litColor);
        targetRenderer.SetPropertyBlock(_propertyBlock);
    }

    public void SetLightingType(LightingType type)
    {
        lightingType = type;
    }
}
