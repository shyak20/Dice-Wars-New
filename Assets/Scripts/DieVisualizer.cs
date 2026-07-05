using UnityEngine;

public class DieVisualizer : MonoBehaviour
{
    public MeshRenderer meshRenderer;
    public DieAssetSO dieData;

    [Tooltip("One MeshRenderer per topology face index (0=Face 1 up, 1=Face 6 down, 2=Face 2 right, 3=Face 5 left, 4=Face 3 forward, 5=Face 4 back). Auto-resolved from Face N CTRL / Face N Icon when empty.")]
    [SerializeField] private MeshRenderer[] faceEffectIconRenderers = new MeshRenderer[DieFaceTopology.FaceCount];

    Material[] _faceIconRuntimeMaterials;

    private void Awake()
    {
        if (meshRenderer == null)
            meshRenderer = ResolveDieMeshRenderer();
    }

    private void OnDestroy()
    {
        DestroyFaceIconRuntimeMaterials();
    }

    public void Initialize(DieAssetSO data)
    {
        if (data == null || data.faces == null || data.faces.Length < 6)
        {
            Debug.LogError(
                $"DieVisualizer: '{(data != null ? data.name : "null")}' needs at least 6 face slots (mesh has 6 sides).",
                this);
            return;
        }

        if (data.faces.Length > 6)
            Debug.LogWarning(
                $"DieVisualizer: '{data.name}' has {data.faces.Length} faces; only the first 6 are used for the 3D die mesh.",
                this);

        dieData = data;

        if (meshRenderer == null)
            meshRenderer = ResolveDieMeshRenderer();
        if (meshRenderer == null)
        {
            Debug.LogError($"DieVisualizer on '{name}': no MeshRenderer found.", this);
            return;
        }

        // Runtime instances so dissolve cutout never mutates shared face material assets.
        var materialsToApply = new Material[6];

        for (int i = 0; i < 6; i++)
        {
            var face = data.faces[i];
            if (face != null && face.faceMaterial != null)
                materialsToApply[i] = new Material(face.faceMaterial);
        }

        meshRenderer.materials = materialsToApply;
        ApplyFaceEffectIcons(data);
    }

    void ApplyFaceEffectIcons(DieAssetSO data)
    {
        EnsureFaceEffectIconRenderersCached();

        DestroyFaceIconRuntimeMaterials();
        _faceIconRuntimeMaterials = new Material[DieFaceTopology.FaceCount];

        for (var faceIndex = 0; faceIndex < DieFaceTopology.FaceCount; faceIndex++)
        {
            var iconRenderer = faceEffectIconRenderers[faceIndex];
            if (iconRenderer == null)
            {
                Debug.LogError(
                    $"DieVisualizer on '{name}': missing face effect icon renderer for topology index {faceIndex} (Face {DieFaceEffectIconResolver.TopologyIndexToFaceLabel(faceIndex)}).",
                    this);
                continue;
            }

            var face = data.faces[faceIndex];
            if (face == null || !DieFaceEffectIconResolver.TryResolve(face, out var iconSprite) || iconSprite == null)
            {
                iconRenderer.gameObject.SetActive(false);
                continue;
            }

            var sourceMaterial = iconRenderer.sharedMaterial;
            if (sourceMaterial == null)
            {
                Debug.LogError(
                    $"DieVisualizer on '{name}': face icon renderer '{iconRenderer.name}' has no material assigned.",
                    iconRenderer);
                iconRenderer.gameObject.SetActive(false);
                continue;
            }

            var runtimeMaterial = new Material(sourceMaterial);
            DieFaceEffectIconMaterialUtility.ApplySprite(runtimeMaterial, iconSprite);
            _faceIconRuntimeMaterials[faceIndex] = runtimeMaterial;
            iconRenderer.sharedMaterial = runtimeMaterial;
            iconRenderer.gameObject.SetActive(true);
        }
    }

    void EnsureFaceEffectIconRenderersCached()
    {
        if (faceEffectIconRenderers == null || faceEffectIconRenderers.Length != DieFaceTopology.FaceCount)
            faceEffectIconRenderers = new MeshRenderer[DieFaceTopology.FaceCount];

        var allAssigned = true;
        for (var i = 0; i < DieFaceTopology.FaceCount; i++)
        {
            if (faceEffectIconRenderers[i] == null)
            {
                allAssigned = false;
                break;
            }
        }

        if (allAssigned)
            return;

        for (var faceIndex = 0; faceIndex < DieFaceTopology.FaceCount; faceIndex++)
        {
            if (faceEffectIconRenderers[faceIndex] != null)
                continue;

            var faceLabel = DieFaceEffectIconResolver.TopologyIndexToFaceLabel(faceIndex);
            faceEffectIconRenderers[faceIndex] = FindFaceIconRenderer(faceLabel);
            if (faceEffectIconRenderers[faceIndex] == null)
            {
                Debug.LogError(
                    $"DieVisualizer on '{name}': could not find a MeshRenderer on child GameObject 'Face {faceLabel} Icon'.",
                    this);
            }
        }
    }

    MeshRenderer FindFaceIconRenderer(int faceLabel)
    {
        var targetName = $"Face {faceLabel} Icon";
        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].gameObject.name == targetName)
                return renderers[i];
        }

        return null;
    }

    MeshRenderer ResolveDieMeshRenderer()
    {
        if (meshRenderer != null && !IsFaceIconRenderer(meshRenderer))
            return meshRenderer;

        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        MeshRenderer fallback = null;
        for (var i = 0; i < renderers.Length; i++)
        {
            var candidate = renderers[i];
            if (IsFaceIconRenderer(candidate))
                continue;

            if (candidate.sharedMaterials != null && candidate.sharedMaterials.Length >= DieFaceTopology.FaceCount)
                return candidate;

            fallback ??= candidate;
        }

        return fallback;
    }

    static bool IsFaceIconRenderer(MeshRenderer renderer) =>
        renderer != null && renderer.gameObject.name.Contains(" Icon");

    public void AppendActiveFaceEffectIconRenderers(System.Collections.Generic.List<Renderer> results)
    {
        if (results == null)
            throw new System.ArgumentNullException(nameof(results));

        if (faceEffectIconRenderers == null)
            return;

        for (var i = 0; i < faceEffectIconRenderers.Length; i++)
        {
            var iconRenderer = faceEffectIconRenderers[i];
            if (iconRenderer != null && iconRenderer.gameObject.activeInHierarchy)
                results.Add(iconRenderer);
        }
    }

    void DestroyFaceIconRuntimeMaterials()
    {
        if (_faceIconRuntimeMaterials == null)
            return;

        for (var i = 0; i < _faceIconRuntimeMaterials.Length; i++)
        {
            if (_faceIconRuntimeMaterials[i] == null)
                continue;

            if (Application.isPlaying)
                Destroy(_faceIconRuntimeMaterials[i]);
            else
                DestroyImmediate(_faceIconRuntimeMaterials[i]);
        }

        _faceIconRuntimeMaterials = null;
    }
}