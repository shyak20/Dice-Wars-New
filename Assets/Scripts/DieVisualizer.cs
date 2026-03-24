using UnityEngine;

public class DieVisualizer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the child object containing the MeshRenderer here.")]
    public MeshRenderer targetRenderer;

    // This is the missing variable the DiceRoller is looking for!
    [HideInInspector]
    public DieAssetSO currentData;

    public void Initialize(DieAssetSO data)
    {
        currentData = data; // Store the data reference

        // Fallback: If you forgot to drag it in, try to find it in children
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<MeshRenderer>();
        }

        if (targetRenderer == null || data == null)
        {
            UnityEngine.Debug.LogError($"DieVisualizer on {gameObject.name}: Missing Renderer or Data!");
            return;
        }

        // Apply materials to the sub-meshes
        Material[] newMaterials = new Material[6];

        for (int i = 0; i < 6; i++)
        {
            if (data.faces[i] != null)
            {
                newMaterials[i] = data.faces[i].faceMaterial;
            }
            else
            {
                UnityEngine.Debug.LogWarning($"DieAsset {data.dieName} is missing a face at index {i}!");
            }
        }

        targetRenderer.materials = newMaterials;
    }
}