using System;
using UnityEngine;

/// <summary>After buying a face in the shop, runs die pick + face swap without closing the shop window.</summary>
public class ShopFaceSocketFlow : MonoBehaviour
{
    [SerializeField] private DieDisambiguationView dieDisambiguationView;
    [SerializeField] private FaceSwapOverlayView faceSwapOverlayView;
    [Header("UI root (optional)")]
    [Tooltip("Parent that holds both views. Activated when disambiguation or swap is shown; deactivated when swap finishes. Keep inactive in the scene by default.")]
    [SerializeField] private GameObject flowViewsRoot;

    private void Awake()
    {
        if (dieDisambiguationView == null || faceSwapOverlayView == null)
        {
            Debug.LogError("ShopFaceSocketFlow: assign dieDisambiguationView and faceSwapOverlayView.", this);
            return;
        }

        if (!dieDisambiguationView.gameObject.scene.IsValid())
            Debug.LogError(
                "ShopFaceSocketFlow: dieDisambiguationView must be a scene instance (Hierarchy), not a prefab asset from the Project window. Face shop flow will not show UI.",
                this);
        if (!faceSwapOverlayView.gameObject.scene.IsValid())
            Debug.LogError(
                "ShopFaceSocketFlow: faceSwapOverlayView must be a scene instance (Hierarchy), not a prefab asset. Face shop flow will not show UI.",
                this);
    }

    private void SetFlowViewsRootActive(bool active)
    {
        if (flowViewsRoot != null)
            flowViewsRoot.SetActive(active);
    }

    /// <summary>
    /// Activates this transform and any inactive parents up to the scene root (root → leaf order).
    /// Matches <see cref="FaceRewardManager"/> activating its host before showing overlays; otherwise
    /// <see cref="FaceSwapOverlayView.Show"/> runs but the panel stays hidden under an inactive parent.
    /// </summary>
    private static void EnsureHostHierarchyActive(Transform leaf)
    {
        if (leaf == null) return;
        var depth = 0;
        for (var t = leaf; t != null; t = t.parent)
            depth++;
        var chain = new Transform[depth];
        var i = depth;
        for (var t = leaf; t != null; t = t.parent)
            chain[--i] = t;
        for (var j = 0; j < chain.Length; j++)
        {
            if (!chain[j].gameObject.activeSelf)
                chain[j].gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Starts socketing <paramref name="face"/> into the deck. On failure (no matching die), refunds via <paramref name="shopGenerator"/> / <paramref name="shopItem"/>.
    /// </summary>
    public void BeginInstallFace(DieFaceSO face, ShopItem shopItem, ShopGenerator shopGenerator, Action onFinished)
    {
        if (face == null)
        {
            onFinished?.Invoke();
            return;
        }

        var data = PlayerDataContainer.Instance != null ? PlayerDataContainer.Instance.RuntimeData : null;
        if (data == null)
        {
            Debug.LogError("ShopFaceSocketFlow: PlayerDataContainer missing.");
            shopGenerator?.RefundAndRestock(shopItem);
            onFinished?.Invoke();
            return;
        }

        var matching = PlayerInventory.GetDiceMatchingFace(data, face);
        if (matching.Count == 0)
        {
            Debug.LogWarning("ShopFaceSocketFlow: No die matches this face's element; refunding.");
            shopGenerator?.RefundAndRestock(shopItem);
            ShopToastUI.Show("No die matches this face — gold refunded.");
            onFinished?.Invoke();
            return;
        }

        if (matching.Count == 1)
        {
            ShowSwap(matching[0], face, shopItem, shopGenerator, onFinished);
            return;
        }

        SetFlowViewsRootActive(true);
        EnsureHostHierarchyActive(dieDisambiguationView.transform);
        dieDisambiguationView.Show(matching, die =>
        {
            dieDisambiguationView.Hide();
            ShowSwap(die, face, shopItem, shopGenerator, onFinished);
        });
    }

    private void ShowSwap(DieAssetSO die, DieFaceSO face, ShopItem shopItem, ShopGenerator shopGenerator, Action onFinished)
    {
        SetFlowViewsRootActive(true);
        EnsureHostHierarchyActive(faceSwapOverlayView.transform);
        if (faceSwapOverlayView.Show(die, face, () =>
            {
                SetFlowViewsRootActive(false);
                onFinished?.Invoke();
            }))
            return;

        Debug.LogError("ShopFaceSocketFlow: Face swap overlay could not open; refunding.");
        SetFlowViewsRootActive(false);
        shopGenerator?.RefundAndRestock(shopItem);
        ShopToastUI.Show("Could not install face — gold refunded.");
        onFinished?.Invoke();
    }
}
