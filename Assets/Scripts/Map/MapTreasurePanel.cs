using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Map treasure tile: shows a rolled <see cref="MapTreasurePackSO"/> and grants rewards on collect (gold / die / relic rows only — no faces or gems).</summary>
public sealed class MapTreasurePanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [Header("Rewards list")]
    [SerializeField] private Transform rewardsLayout;
    [Header("Reward row prefabs (each root must have RunRewardOfferRow)")]
    [SerializeField] private GameObject goldRewardRowPrefab;
    [Tooltip("Optional. Falls back to gold row prefab when unset.")]
    [SerializeField] private GameObject rubyShardRewardRowPrefab;
    [SerializeField] private GameObject relicRewardRowPrefab;
    [SerializeField] private GameObject dieRewardRowPrefab;
    [Header("UI")]
    [SerializeField] private Button closeButton;
    [Header("Open chest (optional)")]
    [Tooltip("When assigned, a click applies the two lists below, then the button is disabled until the panel closes.")]
    [SerializeField] private Button openButton;
    [Tooltip("Set active true when Open is pressed.")]
    [SerializeField] private List<GameObject> activateWhenOpenPressed = new List<GameObject>();
    [Tooltip("Set active false when Open is pressed.")]
    [SerializeField] private List<GameObject> deactivateWhenOpenPressed = new List<GameObject>();

    [Header("Chest open shake")]
    [Tooltip("When a CameraShake component exists on the map/combat camera, its transform is offset for this long (world units magnitude).")]
    [SerializeField, Min(0f)] private float openCameraShakeDuration = 0.28f;
    [SerializeField, Min(0f)] private float openCameraShakeMagnitude = 0.14f;
    [Tooltip("Optional UI root to shake in anchored pixels (use for Screen Space Overlay, or in addition to camera). If unset, uses Root when it is a RectTransform.")]
    [SerializeField] private RectTransform canvasShakeTarget;
    [SerializeField, Min(0.05f)] private float canvasShakeDuration = 0.38f;
    [SerializeField, Min(0f)] private float canvasShakeMaxOffset = 22f;
    [SerializeField, Min(1f)] private float canvasShakeWaves = 12f;

    [Header("Close")]
    [Tooltip("After the last reward row is collected, wait this long before hiding the panel. Close button still closes immediately.")]
    [SerializeField, Min(0f)] private float delayBeforeCloseSeconds = 1f;
    [Header("Unclaimed rewards")]
    [Tooltip("Optional. When empty, uses ConfirmationDialog in the active scene.")]
    [SerializeField] private ConfirmationDialog confirmationDialog;
    [SerializeField] private string unclaimedRewardsMessage =
        "You still have unclaimed rewards.\nLeave without collecting them?";

    private MapTreasurePackSO _currentPack;
    private readonly List<RolledReward> _rolledRewards = new List<RolledReward>();
    private int _pendingRowCompletions;

    private enum RolledRewardKind
    {
        Gold,
        RubyShards,
        Die,
        Relic
    }

    private sealed class RolledReward
    {
        public RolledRewardKind Kind;
        public int GoldAmount;
        public int RubyShardAmount;
        public DieAssetSO Die;
        public RelicSO Relic;
    }

    private Coroutine _canvasShakeRoutine;
    private Coroutine _delayedCloseRoutine;
    private Vector2 _canvasShakeBaseAnchoredPosition;

    private void Awake()
    {
        if (root == null)
            root = gameObject;
        root.SetActive(false);
        if (canvasShakeTarget == null && root != null)
            canvasShakeTarget = root.transform as RectTransform;
        if (canvasShakeTarget != null)
            _canvasShakeBaseAnchoredPosition = canvasShakeTarget.anchoredPosition;
        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseButtonClicked);
        if (openButton != null)
            openButton.onClick.AddListener(OnOpenButtonPressed);
    }

    private void OnDisable()
    {
        StopCanvasShake(resetPosition: true);
        CancelDelayedClose();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        if (openButton != null)
            openButton.onClick.RemoveListener(OnOpenButtonPressed);
    }

    private void OnOpenButtonPressed()
    {
        PlayChestOpenShake();
        ApplyOpenPressedVisuals();
        if (openButton != null)
            openButton.interactable = false;
    }

    void PlayChestOpenShake()
    {
        if (openCameraShakeDuration > 0f && openCameraShakeMagnitude > 0f)
            CameraShake.ShakeActive(openCameraShakeDuration, openCameraShakeMagnitude);

        if (canvasShakeTarget != null && canvasShakeDuration > 0f && canvasShakeMaxOffset > 0f)
        {
            StopCanvasShake(resetPosition: true);
            _canvasShakeRoutine = StartCoroutine(CoShakeCanvasOpen());
        }
    }

    void StopCanvasShake(bool resetPosition)
    {
        if (_canvasShakeRoutine != null)
        {
            StopCoroutine(_canvasShakeRoutine);
            _canvasShakeRoutine = null;
        }

        if (resetPosition && canvasShakeTarget != null)
            canvasShakeTarget.anchoredPosition = _canvasShakeBaseAnchoredPosition;
    }

    IEnumerator CoShakeCanvasOpen()
    {
        var rt = canvasShakeTarget;
        if (rt == null)
        {
            _canvasShakeRoutine = null;
            yield break;
        }

        _canvasShakeBaseAnchoredPosition = rt.anchoredPosition;
        var duration = canvasShakeDuration;
        var maxOff = canvasShakeMaxOffset;
        var waves = canvasShakeWaves;
        var t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            var u = Mathf.Clamp01(t / duration);
            var decay = 1f - u;
            var angle = u * Mathf.PI * waves;
            var ox = Mathf.Sin(angle) * maxOff * decay;
            var oy = Mathf.Sin(angle * 1.11f) * maxOff * 0.65f * decay;
            rt.anchoredPosition = _canvasShakeBaseAnchoredPosition + new Vector2(ox, oy);
            yield return null;
        }

        rt.anchoredPosition = _canvasShakeBaseAnchoredPosition;
        _canvasShakeRoutine = null;
    }

    private void ApplyOpenPressedVisuals()
    {
        for (var i = 0; i < activateWhenOpenPressed.Count; i++)
        {
            var go = activateWhenOpenPressed[i];
            if (go != null)
                go.SetActive(true);
        }

        for (var i = 0; i < deactivateWhenOpenPressed.Count; i++)
        {
            var go = deactivateWhenOpenPressed[i];
            if (go != null)
                go.SetActive(false);
        }
    }

    private void ResetOpenButtonState()
    {
        if (openButton != null)
            openButton.interactable = true;
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
        ResetOpenButtonState();

        RollRewards(pack, _rolledRewards);
        RebuildRewardsLayout(_rolledRewards);

        if (canvasShakeTarget != null)
            _canvasShakeBaseAnchoredPosition = canvasShakeTarget.anchoredPosition;

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

        _pendingRowCompletions = 0;

        if (rewards == null || rewards.Count == 0)
            return;

        foreach (var reward in rewards)
        {
            switch (reward.Kind)
            {
                case RolledRewardKind.Gold:
                {
                    if (!TryInstantiateRow(goldRewardRowPrefab, "gold", out var row))
                        return;
                    row.SetupGold(reward.GoldAmount, OnOneRewardRowDone);
                    _pendingRowCompletions++;
                    break;
                }
                case RolledRewardKind.RubyShards:
                {
                    var prefab = rubyShardRewardRowPrefab != null ? rubyShardRewardRowPrefab : goldRewardRowPrefab;
                    if (!TryInstantiateRow(prefab, "ruby shard", out var row))
                        return;
                    row.SetupRubyShards(reward.RubyShardAmount, OnOneRewardRowDone);
                    _pendingRowCompletions++;
                    break;
                }
                case RolledRewardKind.Relic:
                {
                    if (!TryInstantiateRow(relicRewardRowPrefab, "relic", out var row))
                        return;
                    row.SetupRelic(reward.Relic, OnOneRewardRowDone);
                    _pendingRowCompletions++;
                    break;
                }
                case RolledRewardKind.Die:
                {
                    if (!TryInstantiateRow(dieRewardRowPrefab, "die", out var row))
                        return;
                    row.SetupDie(reward.Die, OnOneRewardRowDone);
                    _pendingRowCompletions++;
                    break;
                }
            }
        }
    }

    private bool TryInstantiateRow(GameObject prefab, string rewardKindLabel, out RunRewardOfferRow row)
    {
        row = null;
        if (prefab == null)
        {
            Debug.LogError($"MapTreasurePanel: assign {rewardKindLabel} reward row prefab.", this);
            return false;
        }

        var go = Instantiate(prefab, rewardsLayout);
        row = go.GetComponent<RunRewardOfferRow>();
        if (row == null)
        {
            Debug.LogError($"MapTreasurePanel: {rewardKindLabel} prefab must have RunRewardOfferRow.", go);
            Destroy(go);
            return false;
        }

        return true;
    }

    private void OnOneRewardRowDone()
    {
        _pendingRowCompletions = Mathf.Max(0, _pendingRowCompletions - 1);
        if (_pendingRowCompletions == 0)
            ScheduleCloseAfterRewardsCollected();
    }

    void ScheduleCloseAfterRewardsCollected()
    {
        CancelDelayedClose();
        if (delayBeforeCloseSeconds <= 0f)
        {
            Hide();
            return;
        }

        _delayedCloseRoutine = StartCoroutine(CoCloseAfterDelay());
    }

    IEnumerator CoCloseAfterDelay()
    {
        yield return new WaitForSecondsRealtime(delayBeforeCloseSeconds);
        _delayedCloseRoutine = null;
        Hide();
    }

    void CancelDelayedClose()
    {
        if (_delayedCloseRoutine == null)
            return;

        StopCoroutine(_delayedCloseRoutine);
        _delayedCloseRoutine = null;
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
                    case TreasureRewardKind.RubyShards:
                    {
                        var lo = Mathf.Min(e.rubyShardMin, e.rubyShardMax);
                        var hi = Mathf.Max(e.rubyShardMin, e.rubyShardMax);
                        var amount = Random.Range(lo, hi + 1);
                        if (amount > 0)
                        {
                            destination.Add(new RolledReward
                            {
                                Kind = RolledRewardKind.RubyShards,
                                RubyShardAmount = amount
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

        if (pack.rubyShardBonusDropChance > 0f && Random.value <= pack.rubyShardBonusDropChance)
        {
            var lo = Mathf.Min(pack.rubyShardBonusMin, pack.rubyShardBonusMax);
            var hi = Mathf.Max(pack.rubyShardBonusMin, pack.rubyShardBonusMax);
            var amount = Random.Range(lo, hi + 1);
            if (amount > 0)
            {
                destination.Add(new RolledReward
                {
                    Kind = RolledRewardKind.RubyShards,
                    RubyShardAmount = amount
                });
            }
        }
    }

    private void OnCloseButtonClicked()
    {
        if (!HasUncollectedRewards())
        {
            Hide();
            return;
        }

        if (!ConfirmationDialog.TryShow(
                unclaimedRewardsMessage,
                Hide,
                dialog: ResolveConfirmationDialog()))
        {
            Debug.LogError(
                "MapTreasurePanel: assign Confirmation Dialog on this component or add one to the map scene.",
                this);
        }
    }

    private ConfirmationDialog ResolveConfirmationDialog() =>
        confirmationDialog != null ? confirmationDialog : ConfirmationDialog.Instance;

    private bool HasUncollectedRewards() => _pendingRowCompletions > 0;

    /// <summary>Closes the panel and clears pending rewards UI (e.g. map regenerated).</summary>
    public void Hide()
    {
        CancelDelayedClose();
        StopCanvasShake(resetPosition: true);
        _currentPack = null;
        _rolledRewards.Clear();
        _pendingRowCompletions = 0;
        ResetOpenButtonState();
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
