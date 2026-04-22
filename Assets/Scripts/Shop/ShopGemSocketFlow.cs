using System;
using UnityEngine;

/// <summary>After buying a gem, pick a die with a free socket and socket the gem permanently.</summary>
public class ShopGemSocketFlow : MonoBehaviour
{
    [SerializeField] private DieDisambiguationView dieDisambiguationView;
    [Header("UI root (optional)")]
    [SerializeField] private GameObject flowViewsRoot;

    private void Awake()
    {
        if (dieDisambiguationView == null)
            Debug.LogError("ShopGemSocketFlow: assign dieDisambiguationView.", this);
    }

    private void SetFlowViewsRootActive(bool active)
    {
        if (flowViewsRoot != null)
            flowViewsRoot.SetActive(active);
    }

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

    public void BeginSocketGem(GemSO gem, ShopItem shopItem, ShopGenerator shopGenerator, Action onFinished)
    {
        if (gem == null)
        {
            onFinished?.Invoke();
            return;
        }

        var data = PlayerDataContainer.Instance != null ? PlayerDataContainer.Instance.RuntimeData : null;
        if (data == null)
        {
            Debug.LogError("ShopGemSocketFlow: PlayerDataContainer missing.");
            shopGenerator?.RefundAndRestock(shopItem);
            onFinished?.Invoke();
            return;
        }

        var candidates = PlayerInventory.GetDiceWithEmptyGemSocket(data);
        if (candidates.Count == 0)
        {
            Debug.LogWarning("ShopGemSocketFlow: No die with an empty gem socket; refunding.");
            shopGenerator?.RefundAndRestock(shopItem);
            ShopToastUI.Show("No free gem sockets — gold refunded.");
            onFinished?.Invoke();
            return;
        }

        if (candidates.Count == 1)
        {
            TrySocketOrRefund(candidates[0], gem, shopItem, shopGenerator, onFinished);
            return;
        }

        if (dieDisambiguationView == null)
        {
            Debug.LogError("ShopGemSocketFlow: assign dieDisambiguationView for multiple candidates.");
            shopGenerator?.RefundAndRestock(shopItem);
            onFinished?.Invoke();
            return;
        }

        SetFlowViewsRootActive(true);
        EnsureHostHierarchyActive(dieDisambiguationView.transform);
        dieDisambiguationView.Show(candidates, die =>
        {
            dieDisambiguationView.Hide();
            SetFlowViewsRootActive(false);
            TrySocketOrRefund(die, gem, shopItem, shopGenerator, onFinished);
        });
    }

    private static void TrySocketOrRefund(
        DieAssetSO die,
        GemSO gem,
        ShopItem shopItem,
        ShopGenerator shopGenerator,
        Action onFinished)
    {
        if (die == null || !die.TrySocketGem(gem))
        {
            shopGenerator?.RefundAndRestock(shopItem);
            ShopToastUI.Show("Could not socket gem — gold refunded.");
            onFinished?.Invoke();
            return;
        }

        ShopToastUI.Show($"Gem: {gem.DisplayLabel}");
        onFinished?.Invoke();
    }
}
