using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// After victory: waits, hides the enemy root, shows win popup with rewards (gold collect + optional face reward row).
/// </summary>
public class WinStageFlowController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float delayAfterVictorySeconds = 2f;
    [Header("UI")]
    [SerializeField] private GameObject winStagePanel;
    [SerializeField] private Transform rewardsLayout;
    [SerializeField] private GameObject goldRewardRowPrefab;
    [Tooltip("Reward row with WinStageFaceRewardRow + Select button; opens FaceRewardManager when pressed.")]
    [SerializeField] private GameObject faceRewardRowPrefab;
    [SerializeField] private string faceRewardRowLabel = "New face";
    [SerializeField] private Button continueButton;
    [FormerlySerializedAs("enemyHealthBarRoot")]
    [Tooltip("Root GameObject for the enemy in the fight (sprite, HP bar, intent UI, etc.). Hidden when the win stage appears.")]
    [SerializeField] private GameObject enemyRoot;
    [Header("Flow")]
    [SerializeField] private FaceRewardManager faceRewardManager;

    private int _uncollectedGold;
    private bool _faceFlowComplete = true;
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
        VictoryRewardBuffer.Clear();
        _faceFlowComplete = true;

        _flowRoutine = StartCoroutine(VictorySequence());
    }

    private IEnumerator VictorySequence()
    {
        if (delayAfterVictorySeconds > 0f)
            yield return new WaitForSeconds(delayAfterVictorySeconds);

        if (enemyRoot != null)
            enemyRoot.SetActive(false);

        if (winStagePanel != null)
            winStagePanel.SetActive(true);

        RebuildRewardsLayout();
        UpdateContinueInteractable();
        _victoryIntroCompletedForCurrentFight = true;
        _flowRoutine = null;
    }

    private void RebuildRewardsLayout()
    {
        if (rewardsLayout == null) return;

        foreach (Transform c in rewardsLayout)
            Destroy(c.gameObject);

        if (_uncollectedGold > 0 && goldRewardRowPrefab != null)
        {
            var go = Instantiate(goldRewardRowPrefab, rewardsLayout);
            var row = go.GetComponent<WinStageGoldRewardRow>();
            if (row == null)
            {
                Debug.LogError("WinStageFlowController: goldRewardRowPrefab needs WinStageGoldRewardRow.");
                Destroy(go);
            }
            else
            {
                row.Setup(_uncollectedGold, () =>
                {
                    _uncollectedGold = 0;
                    UpdateContinueInteractable();
                });
            }
        }

        if (faceRewardManager != null && faceRewardRowPrefab != null)
        {
            var go = Instantiate(faceRewardRowPrefab, rewardsLayout);
            var faceRow = go.GetComponent<WinStageFaceRewardRow>();
            if (faceRow == null)
            {
                Debug.LogError("WinStageFlowController: faceRewardRowPrefab needs WinStageFaceRewardRow.");
                Destroy(go);
            }
            else
                faceRow.Setup(this, faceRewardManager, faceRewardRowLabel);
        }
    }

    /// <summary>Called when the player opens the face picker from the reward row.</summary>
    public void NotifyFacePickerOpening()
    {
        _faceFlowComplete = false;
        if (winStagePanel != null)
            winStagePanel.SetActive(false);
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
        _faceFlowComplete = true;
        if (winStagePanel != null)
            winStagePanel.SetActive(true);
        UpdateContinueInteractable();
    }

    private void UpdateContinueInteractable()
    {
        if (continueButton == null) return;
        continueButton.interactable = _uncollectedGold <= 0 && _faceFlowComplete;
    }

    private void OnContinueClicked()
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
}
