using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// After victory: waits, hides the enemy root, shows win popup with rewards (gold collect + optional face pick).
/// </summary>
public class WinStageFlowController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float delayAfterVictorySeconds = 2f;
    [Header("UI")]
    [SerializeField] private GameObject winStagePanel;
    [SerializeField] private Transform rewardsLayout;
    [SerializeField] private GameObject goldRewardRowPrefab;
    [SerializeField] private Button chooseFaceButton;
    [SerializeField] private Button continueButton;
    [FormerlySerializedAs("enemyHealthBarRoot")]
    [Tooltip("Root GameObject for the enemy in the fight (sprite, HP bar, intent UI, etc.). Hidden when the win stage appears.")]
    [SerializeField] private GameObject enemyRoot;
    [Header("Flow")]
    [SerializeField] private FaceRewardManager faceRewardManager;

    private int _uncollectedGold;
    private bool _faceFlowComplete = true;
    private bool _chooseFaceUsedOrSkipped;
    private Coroutine _flowRoutine;

    private void OnDisable()
    {
        FaceRewardEvents.OnFaceRewardCompleted -= OnFaceRewardFlowEnded;
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
        if (chooseFaceButton != null)
            chooseFaceButton.onClick.AddListener(OnChooseFaceClicked);
    }

    /// <summary>Called from <see cref="WinLoseUIController"/> on victory (after <see cref="VictoryRewardBuffer"/> was set).</summary>
    public void BeginVictoryFlow()
    {
        // Multiple listeners on <see cref="CombatEvents.OnPlayerVictory"/> (e.g. duplicate WinLoseUIController) would otherwise
        // run this twice in one invoke: first call clears the buffer, second overwrites _uncollectedGold with 0.
        if (_flowRoutine != null)
            return;

        _uncollectedGold = VictoryRewardBuffer.PendingGold;
        VictoryRewardBuffer.Clear();
        _faceFlowComplete = true;
        _chooseFaceUsedOrSkipped = false;

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
        RefreshChooseFaceButton();
        FaceRewardEvents.OnFaceRewardCompleted -= OnFaceRewardFlowEnded;
        FaceRewardEvents.OnFaceRewardCompleted += OnFaceRewardFlowEnded;

        UpdateContinueInteractable();
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
                return;
            }

            row.Setup(_uncollectedGold, () =>
            {
                _uncollectedGold = 0;
                UpdateContinueInteractable();
            });
        }
    }

    private void RefreshChooseFaceButton()
    {
        if (chooseFaceButton == null) return;
        var canOfferFace = faceRewardManager != null;
        chooseFaceButton.gameObject.SetActive(canOfferFace && !_chooseFaceUsedOrSkipped);
    }

    private void OnChooseFaceClicked()
    {
        if (faceRewardManager == null) return;

        _faceFlowComplete = false;
        UpdateContinueInteractable();

        if (winStagePanel != null)
            winStagePanel.SetActive(false);

        faceRewardManager.StartFaceRewardFromWinStage(OnFacePickerBack, OnFacePickerSkipped);
    }

    private void OnFacePickerBack()
    {
        _faceFlowComplete = true;
        if (winStagePanel != null)
            winStagePanel.SetActive(true);
        UpdateContinueInteractable();
    }

    private void OnFacePickerSkipped()
    {
        _chooseFaceUsedOrSkipped = true;
        _faceFlowComplete = true;
        if (winStagePanel != null)
            winStagePanel.SetActive(true);
        RefreshChooseFaceButton();
        UpdateContinueInteractable();
    }

    private void OnFaceRewardFlowEnded(DieFaceSO _)
    {
        _chooseFaceUsedOrSkipped = true;
        _faceFlowComplete = true;
        if (winStagePanel != null)
            winStagePanel.SetActive(true);
        RefreshChooseFaceButton();
        UpdateContinueInteractable();
    }

    private void UpdateContinueInteractable()
    {
        if (continueButton == null) return;
        continueButton.interactable = _uncollectedGold <= 0 && _faceFlowComplete;
    }

    private void OnContinueClicked()
    {
        FaceRewardEvents.OnFaceRewardCompleted -= OnFaceRewardFlowEnded;

        if (winStagePanel != null)
            winStagePanel.SetActive(false);

        if (RunManager.Instance != null)
            RunManager.Instance.AdvanceToNextRoom();
        else
            Debug.LogError("WinStageFlowController: RunManager missing — cannot advance.");
    }
}
