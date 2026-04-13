using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Map treasure tile: shows a rolled <see cref="MapTreasurePackSO"/> and grants rewards on collect.</summary>
public sealed class MapTreasurePanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Button collectButton;
    [SerializeField] private Button closeButton;

    private MapTreasurePackSO _currentPack;

    private void Awake()
    {
        if (root == null)
            root = gameObject;
        root.SetActive(false);
        if (collectButton != null)
            collectButton.onClick.AddListener(OnCollectClicked);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    public bool TryOpen(MapTreasurePackSO pack)
    {
        if (RunManager.Instance == null)
        {
            Debug.LogError("MapTreasurePanel: RunManager missing.", this);
            return false;
        }

        if (!RunManager.Instance.UseMapBasedRun)
        {
            Debug.LogError("MapTreasurePanel: not in map-based run.", this);
            return false;
        }

        _currentPack = pack;
        if (root == null)
            root = gameObject;

        ActivateSelfAndAncestors(root.transform);
        root.SetActive(true);

        if (titleText != null)
            titleText.text = pack != null ? pack.packTitle : "Treasure";

        if (bodyText != null)
            bodyText.text = BuildBody(pack);

        return true;
    }

    static string BuildBody(MapTreasurePackSO pack)
    {
        if (pack == null)
            return "No treasure packs are configured for this act. Nothing to collect.";

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(pack.packDescription))
        {
            sb.AppendLine(pack.packDescription);
            sb.AppendLine();
        }

        if (pack.rewards == null || pack.rewards.Count == 0)
        {
            sb.Append("This chest is empty.");
            return sb.ToString();
        }

        sb.AppendLine("Contents:");
        foreach (var e in pack.rewards)
        {
            switch (e.kind)
            {
                case TreasureRewardKind.Gold:
                {
                    var lo = Mathf.Min(e.goldMin, e.goldMax);
                    var hi = Mathf.Max(e.goldMin, e.goldMax);
                    sb.AppendLine(lo == hi ? $"• {lo} gold" : $"• {lo}–{hi} gold (random)");
                    break;
                }
                case TreasureRewardKind.Die:
                    sb.AppendLine(e.dieLootTable != null ? $"• Random die ({e.dieLootTable.name})" : "• (assign DieLootTableSO)");
                    break;
                case TreasureRewardKind.Relic:
                    sb.AppendLine(e.relicLootTable != null ? $"• Random relic ({e.relicLootTable.name})" : "• (assign RelicLootTableSO)");
                    break;
            }
        }

        return sb.ToString();
    }

    private void OnCollectClicked()
    {
        if (_currentPack != null)
            ApplyPack(_currentPack);
        Close();
    }

    private static void ApplyPack(MapTreasurePackSO pack)
    {
        if (pack.rewards == null) return;

        foreach (var e in pack.rewards)
        {
            switch (e.kind)
            {
                case TreasureRewardKind.Gold:
                {
                    var lo = Mathf.Min(e.goldMin, e.goldMax);
                    var hi = Mathf.Max(e.goldMin, e.goldMax);
                    var amount = Random.Range(lo, hi + 1);
                    if (amount > 0 && RunEconomyManager.Instance != null)
                        RunEconomyManager.Instance.GrantGold(amount, null);
                    break;
                }
                case TreasureRewardKind.Die:
                {
                    var die = e.dieLootTable != null ? e.dieLootTable.GetRandomDie() : null;
                    if (die != null && PlayerDataContainer.Instance != null)
                        PlayerDataContainer.Instance.AddDieToDeck(die);
                    break;
                }
                case TreasureRewardKind.Relic:
                {
                    if (e.relicLootTable == null || RunManager.Instance == null) break;
                    var rolled = e.relicLootTable.GetRandomRelics(1);
                    if (rolled.Count > 0 && rolled[0] != null)
                        RunManager.Instance.AddRunRelic(rolled[0]);
                    break;
                }
            }
        }
    }

    private void Close()
    {
        _currentPack = null;
        if (root != null)
            root.SetActive(false);
    }

    private static void ActivateSelfAndAncestors(Transform t)
    {
        if (t == null) return;
        if (t.parent != null)
            ActivateSelfAndAncestors(t.parent);
        t.gameObject.SetActive(true);
    }
}
