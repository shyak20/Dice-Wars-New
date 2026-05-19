using UnityEngine;

public class DieVisualizer : MonoBehaviour
{
    public MeshRenderer meshRenderer;
    public DieAssetSO dieData;

    private void Awake()
    {
        if (meshRenderer == null) meshRenderer = GetComponentInChildren<MeshRenderer>();
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
            meshRenderer = GetComponentInChildren<MeshRenderer>();
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
    }
}