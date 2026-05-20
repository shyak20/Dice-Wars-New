using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// After victory: waits, hides the enemy root, shows win popup with rewards (per-kind row prefabs + optional face offer).
/// </summary>
public class WinStageFlowController : MonoBehaviour, IRewardOfferFlowHost
{
    [SerializeField, Min(0f)] private float delayAfterVictorySeconds = 2f;
    [Header("UI")]
    [SerializeField] private GameObject winStagePanel;
    [SerializeField] private Transform rewardsLayout;
    [Header("Reward row prefabs")]
    [FormerlySerializedAs("rewardRowPrefab")]
    [Tooltip("Row prefab for gold; tune presentation on RunRewardOfferRow on that prefab.")]
    [SerializeField] private GameObject goldRewardRowPrefab;
    [Tooltip("Row prefab for Ruby Shards (meta currency). Falls back to gold row prefab when unset.")]
    [SerializeField] private GameObject rubyShardRewardRowPrefab;
    [SerializeField] private GameObject gemRewardRowPrefab;
    [SerializeField] private GameObject relicRewardRowPrefab;
    [SerializeField] private GameObject faceRewardRowPrefab;
    [SerializeField] private GameObject dieRewardRowPrefab;
    [SerializeField] private Button continueButton;
    [FormerlySerializedAs("enemyHealthBarRoot")]
    [Tooltip("Root GameObject for the enemy in the fight (sprite, HP bar, intent UI, etc.). Hidden when the win stage appears.")]
    [SerializeField] private GameObject enemyRoot;
    [Header("Victory — hide in scene")]
    [Tooltip("Set inactive after Delay After Victory Seconds (with enemy root / win panel), not on the victory event itself.")]
    [SerializeField] private List<GameObject> disableOnVictoryScreen = new List<GameObject>();
    [Header("Flow")]
    [SerializeField] private FaceRewardManager faceRewardManager;
    [Header("Unclaimed rewards")]
    [Tooltip("Optional. When empty, uses ConfirmationDialog in the active scene.")]
    [SerializeField] private ConfirmationDialog confirmationDialog;
    [SerializeField] private string unclaimedRewardsMessage =
        "You still have unclaimed rewards.\nContinue without collecting them?";

    private int _uncollectedGold;
    private int _uncollectedRubyShards;
    private int _uncollectedGemRewards;
    private int _uncollectedRelicRewards;
    private int _uncollectedDieRewards;
    private readonly List<GemSO> _pendingGemRewards = new List<GemSO>();
    private readonly List<RelicSO> _pendingRelicRewards = new List<RelicSO>();
    private readonly List<DieAssetSO> _pendingDieRewards = new List<DieAssetSO>();
    private bool _faceFlowComplete = true;
    /// <summary>True while the optional face-offer row is still in the layout (not consumed).</summary>
    private bool _faceRewardRowPending;
    private Coroutine _flowRoutine;
    /// <summary>True after the post-victory intro (delay + first layout) has run for this fight.</summary>
    private bool _victoryIntroCompletedForCurrentFight;
    public bool IsWinStageVisible => winStagePanel != null && winStagePanel.activeInHierarchy;

    private void OnDisable()
    {
        if (_flowRoutine != null)
        {
            StopCoroutine(_flowRoutine);
            _flowRoutine = null;
        }
    }

    private void Awake()
    {
        if (winStagePanel != null) winStagePanel.SetActive(false);
        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);
    }

    /// <summary>Called from <see cref="WinLoseUIController"/> on victory (after <see cref="VictoryRewardBuffer"/> was set).</summary>
    public void BeginVictoryFlow()
    {
        if (_flowRoutine != null)
            return;

        // Spurious second victory notification: bring win UI back without replaying delay or rebuilding rewards.
        if (_victoryIntroCompletedForCurrentFight)
        {
            if (winStagePanel != null)
                winStagePanel.SetActive(true);
            UpdateContinueInteractable();
            return;
        }

        _uncollectedGold = VictoryRewardBuffer.PendingGold;
        _uncollectedRubyShards = VictoryRewardBuffer.PendingRubyShards;
        _pendingGemRewards.Clear();
        _pendingGemRewards.AddRange(VictoryRewardBuffer.PendingGems);
        _pendingRelicRewards.Clear();
        _pendingRelicRewards.AddRange(VictoryRewardBuffer.PendingRelics);
        _pendingDieRewards.Clear();
        _pendingDieRewards.AddRange(VictoryRewardBuffer.PendingDice);
        _uncollectedGemRewards = 0;
        _uncollectedRelicRewards = 0;
        _uncollectedDieRewards = 0;
        VictoryRewardBuffer.Clear();
        _faceFlowComplete = true;

        _flowRoutine = StartCoroutine(VictorySequence());
    }

    private IEnumerator VictorySequence()
    {
        if (delayAfterVictorySeconds > 0f)
            yield return new WaitForSeconds(delayAfterVictorySeconds);

        DisableObjectsForVictoryScreen();

        if (enemyRoot != null)
            enemyRoot.SetActive(false);

        if (winStagePanel != null)
            winStagePanel.SetActive(true);

        RebuildRewardsLayout();
        _victoryIntroCompletedForCurrentFight = true;
        UpdateContinueInteractable();
        _flowRoutine = null;
    }

    private void RebuildRewardsLayout()
    {
        if (rewardsLayout == null) return;

        if (faceRewardManager != null)
            faceRewardManager.OnWinStageRewardsLayoutRebuilt();

        foreach (Transform c in rewardsLayout)
            Destroy(c.gameObject);

        _faceRewardRowPending = false;

        if (_uncollectedGold > 0)
            InstantiateGoldRow();

        if (_uncollectedRubyShards > 0)
            InstantiateRubyShardRow();

        if (_pendingGemRewards.Count > 0 && faceRewardManager != null)
        {
            for (var i = 0; i < _pendingGemRewards.Count; i++)
            {
                var gem = _pendingGemRewards[i];
                if (gem == null) continue;
                _uncollectedGemRewards++;

                if (!TryInstantiateRow(gemRewardRowPrefab, "gem", out var row))
                {
                    _uncollectedGemRewards = Mathf.Max(0, _uncollectedGemRewards - 1);
                    continue;
                }

                row.SetupGem(gem, this, faceRewardManager, () =>
                {
                    _uncollectedGemRewards = Mathf.Max(0, _uncollectedGemRewards - 1);
                    UpdateContinueInteractable();
                });
            }
        }

        for (var i = 0; i < _pendingRelicRewards.Count; i++)
        {
            var relic = _pendingRelicRewards[i];
            if (relic == null) continue;
            _uncollectedRelicRewards++;

            if (!TryInstantiateRow(relicRewardRowPrefab, "relic", out var row))
            {
                _uncollectedRelicRewards = Mathf.Max(0, _uncollectedRelicRewards - 1);
                continue;
            }

            row.SetupRelic(relic, () =>
            {
                _uncollectedRelicRewards = Mathf.Max(0, _uncollectedRelicRewards - 1);
                UpdateContinueInteractable();
            });
        }

        if (faceRewardManager != null)
        {
            if (TryInstantiateRow(faceRewardRowPrefab, "face", out var faceRow))
            {
                faceRow.SetupFace(this, faceRewardManager);
                _faceRewardRowPending = true;
            }
        }

        for (var i = 0; i < _pendingDieRewards.Count; i++)
        {
            var die = _pendingDieRewards[i];
            if (die == null) continue;
            _uncollectedDieRewards++;

            if (!TryInstantiateRow(dieRewardRowPrefab, "die", out var row))
            {
                _uncollectedDieRewards = Mathf.Max(0, _uncollectedDieRewards - 1);
                continue;
            }

            row.SetupDie(die, () =>
            {
                _uncollectedDieRewards = Mathf.Max(0, _uncollectedDieRewards - 1);
                UpdateContinueInteractable();
            });
        }
    }

    private void InstantiateRubyShardRow()
    {
        var prefab = rubyShardRewardRowPrefab != null ? rubyShardRewardRowPrefab : goldRewardRowPrefab;
        if (!TryInstantiateRow(prefab, "ruby shard", out var row))
        {
            MetaProgressionManager.TryGetRuntime()?.GrantRubyShards(_uncollectedRubyShards);
            _uncollectedRubyShards = 0;
            return;
        }

        row.SetupRubyShards(_uncollectedRubyShards, () =>
        {
            _uncollectedRubyShards = 0;
            UpdateContinueInteractable();
        });
    }

    private void InstantiateGoldRow()
    {
        if (!TryInstantiateRow(goldRewardRowPrefab, "gold", out var row))
            return;

        row.SetupGold(_uncollectedGold, () =>
        {
            _uncollectedGold = 0;
            UpdateContinueInteractable();
        });
    }

    private bool TryInstantiateRow(GameObject prefab, string rewardKindLabel, out RunRewardOfferRow row)
    {
        row = null;
        if (prefab == null)
        {
            Debug.LogError($"WinStageFlowController: assign {rewardKindLabel} reward row prefab.");
            return false;
        }

        var go = Instantiate(prefab, rewardsLayout);
        row = go.GetComponent<RunRewardOfferRow>();
        if (row == null)
        {
            Debug.LogError($"WinStageFlowController: {rewardKindLabel} reward prefab needs RunRewardOfferRow.");
            Destroy(go);
            return false;
        }

        return true;
    }

    /// <summary>Called when the player opens the face picker from the reward row.</summary>
    public void NotifyFacePickerOpening()
    {
        _faceFlowComplete = false;
        UpdateContinueInteractable();
    }

    /// <summary>Called when the player uses Back on the face picker (reward not consumed; row stays).</summary>
    public void NotifyFacePickerBackedOut()
    {
        _faceFlowComplete = true;
        if (winStagePanel != null)
            winStagePanel.SetActive(true);
        UpdateContinueInteractable();
    }

    /// <summary>Called when the face reward row is consumed (Skip, swap finished, or no-match close).</summary>
    public void NotifyFaceRewardRowRemoved()
    {
        _faceRewardRowPending = false;
        _faceFlowComplete = true;
        if (winStagePanel != null)
            winStagePanel.SetActive(true);
        UpdateContinueInteractable();
    }

    private void UpdateContinueInteractable()
    {
        if (continueButton == null) return;
        continueButton.interactable = true;
        TryAutoCompleteVictoryIfReady();
    }

    private void TryAutoCompleteVictoryIfReady()
    {
        if (!_victoryIntroCompletedForCurrentFight)
            return;
        if (!IsWinStageVisible)
            return;
        if (_uncollectedGold > 0 || _uncollectedRubyShards > 0 || _uncollectedGemRewards > 0 || _uncollectedRelicRewards > 0 || _uncollectedDieRewards > 0)
            return;
        if (_faceRewardRowPending)
            return;

        AdvanceVictoryAfterWinStage();
    }

    private void OnContinueClicked()
    {
        if (!HasUncollectedRewards())
        {
            AdvanceVictoryAfterWinStage();
            return;
        }

        if (!ConfirmationDialog.TryShow(
                unclaimedRewardsMessage,
                AdvanceVictoryAfterWinStage,
                dialog: ResolveConfirmationDialog()))
        {
            Debug.LogError(
                "WinStageFlowController: assign Confirmation Dialog on this component or add one to the fight scene.",
                this);
        }
    }

    private ConfirmationDialog ResolveConfirmationDialog() =>
        confirmationDialog != null ? confirmationDialog : ConfirmationDialog.Instance;

    private bool HasUncollectedRewards() =>
        _uncollectedGold > 0
        || _uncollectedRubyShards > 0
        || _uncollectedGemRewards > 0
        || _uncollectedRelicRewards > 0
        || _uncollectedDieRewards > 0
        || _faceRewardRowPending
        || !_faceFlowComplete;

    private void AdvanceVictoryAfterWinStage()
    {
        _victoryIntroCompletedForCurrentFight = false;

        if (winStagePanel != null)
            winStagePanel.SetActive(false);

        if (RunManager.Instance == null)
        {
            Debug.LogError("WinStageFlowController: RunManager missing — cannot advance.");
            return;
        }

        var player = FindObjectOfType<PlayerStatus>();
        if (player != null)
            RunManager.Instance.CaptureRunVitalityFromPlayer(player);

        RunManager.Instance.HandleVictoryContinueFromCombat();
    }

    private void DisableObjectsForVictoryScreen()
    {
        if (disableOnVictoryScreen == null)
            return;
        for (var i = 0; i < disableOnVictoryScreen.Count; i++)
        {
            var go = disableOnVictoryScreen[i];
            if (go != null)
                go.SetActive(false);
        }
    }

    /// <summary>Same list as post-delay victory hide; used e.g. on defeat when combat UI should drop immediately.</summary>
    public void ApplyVictoryHideListImmediately()
    {
        DisableObjectsForVictoryScreen();
    }
}
