using System;
using UnityEngine;

/// <summary>After buying a face in the shop, runs die pick + face swap without closing the shop window.</summary>
public class ShopFaceSocketFlow : MonoBehaviour
{
    [SerializeField] private DieDisambiguationView dieDisambiguationView;
    [SerializeField] private FaceSwapOverlayView faceSwapOverlayView;

    private void Awake()
    {
        if (dieDisambiguationView == null || faceSwapOverlayView == null)
            Debug.LogError("ShopFaceSocketFlow: assign dieDisambiguationView and faceSwapOverlayView.");
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
            ShowSwap(matching[0], face, onFinished);
            return;
        }

        dieDisambiguationView.Show(matching, die =>
        {
            dieDisambiguationView.Hide();
            ShowSwap(die, face, onFinished);
        });
    }

    private void ShowSwap(DieAssetSO die, DieFaceSO face, Action onFinished)
    {
        faceSwapOverlayView.Show(die, face, () => {
            onFinished?.Invoke();
        });
    }
}
