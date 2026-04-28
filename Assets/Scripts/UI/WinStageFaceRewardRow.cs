using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Win-screen reward row: Select opens <see cref="FaceRewardManager"/> win flow. Row is removed when the face flow completes (swap or no-match close) or when backing out of the picker.
/// </summary>
public class WinStageFaceRewardRow : MonoBehaviour
{
    [SerializeField] private Button selectButton;
    [SerializeField] private TMP_Text labelText;

    private WinStageFlowController _host;
    private FaceRewardManager _faceRewards;
    private bool _subscribedToCompleted;

    private void Awake()
    {
        if (selectButton == null)
            Debug.LogError($"WinStageFaceRewardRow on '{name}': assign selectButton.");
    }

    public void Setup(WinStageFlowController host, FaceRewardManager faceRewardManager, string label = null)
    {
        _host = host;
        _faceRewards = faceRewardManager;

        if (!string.IsNullOrEmpty(label) && labelText != null)
            labelText.text = label;

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelectClicked);
        }
    }

    private void OnDestroy()
    {
        if (_subscribedToCompleted)
            FaceRewardEvents.OnFaceRewardCompleted -= OnFaceRewardFlowCompletedOnce;
    }

    private void OnSelectClicked()
    {
        if (_host == null || _faceRewards == null)
        {
            Debug.LogError("WinStageFaceRewardRow: not configured — call Setup from WinStageFlowController.");
            return;
        }

        FaceRewardEvents.OnFaceRewardCompleted -= OnFaceRewardFlowCompletedOnce;
        FaceRewardEvents.OnFaceRewardCompleted += OnFaceRewardFlowCompletedOnce;
        _subscribedToCompleted = true;

        _host.NotifyFacePickerOpening();
        _faceRewards.StartFaceRewardFromWinStage(OnPickerBack);
    }

    private void OnPickerBack()
    {
        UnsubscribeCompleted();
        _host.NotifyFacePickerBackedOut();
    }

    private void OnFaceRewardFlowCompletedOnce(DieFaceSO _)
    {
        UnsubscribeCompleted();
        _host.NotifyFaceRewardRowRemoved();
        Destroy(gameObject);
    }

    private void UnsubscribeCompleted()
    {
        if (!_subscribedToCompleted) return;
        FaceRewardEvents.OnFaceRewardCompleted -= OnFaceRewardFlowCompletedOnce;
        _subscribedToCompleted = false;
    }
}
