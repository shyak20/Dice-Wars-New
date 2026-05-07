using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Map treasure tile: shows a rolled <see cref="MapTreasurePackSO"/> and grants rewards on collect.</summary>
public sealed class MapTreasurePanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [Header("Rewards list")]
    [SerializeField] private Transform rewardsLayout;
    [SerializeField] private GameObject rewardItemPrefab;
    [Header("Reward icons")]
    [SerializeField] private Sprite goldRewardIcon;
    [SerializeField] private Button closeButton;

    private MapTreasurePackSO _currentPack;
    private readonly List<RolledReward> _rolledRewards = new List<RolledReward>();

    private enum RolledRewardKind
    {
        Gold,
        Die,
        Relic
    }

    private sealed class RolledReward
    {
        public RolledRewardKind Kind;
        public int GoldAmount;
        public DieAssetSO Die;
        public RelicSO Relic;
    }

    private void Awake()
    {
        if (root == null)
            root = gameObject;
        root.SetActive(false);
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

        RollRewards(pack, _rolledRewards);
        RebuildRewardsLayout(_rolledRewards);

        return true;
    }

    private void RebuildRewardsLayout(List<RolledReward> rewards)
    {
        if (rewardsLayout == null)
        {
            Debug.LogError("MapTreasurePanel: assign rewardsLayout.", this);
            return;
        }

        for (var i = rewardsLayout.childCount - 1; i >= 0; i--)
            Destroy(rewardsLayout.GetChild(i).gameObject);

        if (rewards == null || rewards.Count == 0)
            return;

        foreach (var reward in rewards)
        {
            if (rewardItemPrefab == null)
            {
                Debug.LogError("MapTreasurePanel: assign rewardItemPrefab.", this);
                return;
            }

            var go = Instantiate(rewardItemPrefab, rewardsLayout);
            var item = go.GetComponent<MapTreasureRewardItemView>();
            if (item == null)
            {
                Debug.LogError("MapTreasurePanel: rewardItemPrefab must have MapTreasureRewardItemView.", go);
                Destroy(go);
                continue;
            }

            item.Setup(
                IconForReward(reward),
                reward.Kind == RolledRewardKind.Relic ? reward.Relic : null,
                reward.Kind == RolledRewardKind.Gold ? reward.GoldAmount : 0,
                () => CollectReward(reward));
        }
    }

    private Sprite IconForReward(RolledReward reward)
    {
        return reward.Kind switch
        {
            RolledRewardKind.Gold => goldRewardIcon,
            RolledRewardKind.Die => reward.Die != null ? reward.Die.uiIcon : null,
            RolledRewardKind.Relic => reward.Relic != null ? reward.Relic.icon : null,
            _ => null
        };
    }

    private static void RollRewards(MapTreasurePackSO pack, List<RolledReward> destination)
    {
        destination.Clear();
        if (pack == null)
            return;

        if (pack.rewards != null)
        {
            foreach (var e in pack.rewards)
            {
                switch (e.kind)
                {
                    case TreasureRewardKind.Gold:
                    {
                        var lo = Mathf.Min(e.goldMin, e.goldMax);
                        var hi = Mathf.Max(e.goldMin, e.goldMax);
                        var amount = Random.Range(lo, hi + 1);
                        if (amount > 0)
                        {
                            destination.Add(new RolledReward
                            {
                                Kind = RolledRewardKind.Gold,
                                GoldAmount = amount
                            });
                        }
                        break;
                    }
                }
            }
        }

        if (pack.dieDropLootTable != null && Random.value <= pack.dieDropChance)
        {
            var die = pack.dieDropLootTable.GetRandomDie();
            if (die != null)
            {
                destination.Add(new RolledReward
                {
                    Kind = RolledRewardKind.Die,
                    Die = die
                });
            }
        }

        if (pack.relicDropLootTable != null && Random.value <= pack.relicDropChance)
        {
            var rolled = pack.relicDropLootTable.GetRandomRelics(1);
            if (rolled.Count > 0 && rolled[0] != null)
            {
                destination.Add(new RolledReward
                {
                    Kind = RolledRewardKind.Relic,
                    Relic = rolled[0]
                });
            }
        }

    }

    private void CollectReward(RolledReward reward)
    {
        switch (reward.Kind)
        {
            case RolledRewardKind.Gold:
                if (reward.GoldAmount > 0 && RunEconomyManager.Instance != null)
                    RunEconomyManager.Instance.GrantGold(reward.GoldAmount, null);
                break;
            case RolledRewardKind.Die:
                if (reward.Die != null && PlayerDataContainer.Instance != null)
                    PlayerDataContainer.Instance.AddDieToDeck(reward.Die);
                break;
            case RolledRewardKind.Relic:
                if (reward.Relic != null && RunManager.Instance != null)
                    RunManager.Instance.AddRunRelic(reward.Relic);
                break;
        }

        _rolledRewards.Remove(reward);
        if (_rolledRewards.Count == 0)
            Close();
    }

    private void Close() => Hide();

    /// <summary>Closes the panel and clears pending rewards UI (e.g. map regenerated).</summary>
    public void Hide()
    {
        _currentPack = null;
        _rolledRewards.Clear();
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
