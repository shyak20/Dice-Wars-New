using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-local coin-collect feedback for reward UI (win screen, map treasure, etc.).
/// Assign existing scene objects (disabled by default); collecting a gold reward row enables them at the row position, then hides them after <see cref="lifetimeSeconds"/>.
/// </summary>
[DisallowMultipleComponent]
public class DisplayCoins : MonoBehaviour
{
    [SerializeField] private GameObject displayRoot;
    [Tooltip("Turned on/off with feedback; keeps its scene transform (not moved to the reward row).")]
    [SerializeField] private GameObject flareEffect;
    [SerializeField, Min(0.1f)] private float lifetimeSeconds = 2.5f;

    Coroutine _hideRoutine;

    void Awake() => SetFeedbackActive(false);

    void OnDisable()
    {
        if (_hideRoutine != null)
        {
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }
    }

    /// <summary>Enables feedback objects at <paramref name="rewardAnchor"/> if a host exists in the same scene.</summary>
    public static void TryShowFromReward(int coinAmount, Transform rewardAnchor)
    {
        if (coinAmount <= 0 || rewardAnchor == null)
            return;

        var display = FindInScene(rewardAnchor.gameObject.scene);
        if (display == null)
        {
            Debug.LogWarning(
                $"{nameof(DisplayCoins)}: no host in scene '{rewardAnchor.gameObject.scene.name}'. " +
                "Add a DisplayCoins object to the win/treasure UI in that scene.",
                rewardAnchor);
            return;
        }

        display.Show(rewardAnchor);
    }

    static DisplayCoins FindInScene(Scene scene)
    {
        if (!scene.IsValid())
            return null;

        var hosts = FindObjectsOfType<DisplayCoins>(true);
        for (var i = 0; i < hosts.Length; i++)
        {
            var host = hosts[i];
            if (host != null && host.gameObject.scene == scene)
                return host;
        }

        return null;
    }

    public void Show(Transform rewardAnchor)
    {
        if (displayRoot == null && flareEffect == null)
        {
            Debug.LogError($"{nameof(DisplayCoins)} on '{name}': assign displayRoot and/or flareEffect.", this);
            return;
        }

        if (rewardAnchor == null)
        {
            Debug.LogError($"{nameof(DisplayCoins)} on '{name}': reward anchor is null.", this);
            return;
        }

        AlignFeedbackToAnchor(rewardAnchor);

        if (_hideRoutine != null)
            StopCoroutine(_hideRoutine);

        SetFeedbackActive(true);
        _hideRoutine = StartCoroutine(HideAfterLifetime());
    }

    IEnumerator HideAfterLifetime()
    {
        yield return new WaitForSecondsRealtime(lifetimeSeconds);
        SetFeedbackActive(false);
        _hideRoutine = null;
    }

    void AlignFeedbackToAnchor(Transform anchor)
    {
        if (displayRoot != null)
            AlignToAnchor(displayRoot.transform, anchor);
    }

    void SetFeedbackActive(bool active)
    {
        if (displayRoot != null)
            displayRoot.SetActive(active);
        if (flareEffect != null)
            flareEffect.SetActive(active);
    }

    static void AlignToAnchor(Transform display, Transform anchor)
    {
        if (display == null || anchor == null)
            return;

        if (display is RectTransform displayRt && anchor is RectTransform anchorRt)
        {
            displayRt.position = anchorRt.position;
            displayRt.rotation = anchorRt.rotation;
            return;
        }

        display.position = anchor.position;
        display.rotation = anchor.rotation;
    }

    void OnValidate()
    {
        if (lifetimeSeconds < 0.1f)
            lifetimeSeconds = 0.1f;
    }
}
