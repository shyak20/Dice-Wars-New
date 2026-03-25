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
        if (data == null || data.faces == null || data.faces.Length != 6)
        {
            Debug.LogError("DieVisualizer: DieAssetSO must have exactly 6 faces assigned!");
            return;
        }

        dieData = data;

        // Build the material array based on the Face SOs
        Material[] materialsToApply = new Material[6];

        for (int i = 0; i < 6; i++)
        {
            if (data.faces[i] != null)
            {
                materialsToApply[i] = data.faces[i].faceMaterial;
            }
        }

        meshRenderer.materials = materialsToApply;
    }
}