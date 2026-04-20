using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Perfect-strike: moves the pool container toward a target, then enables each row's jackpot UI
/// (top-to-bottom) on a delay, waits until every row's value text shows the multiplied total, pauses X seconds,
/// then fires <c>EndSequence</c>, waits for the exit clip, animates the pool container home, then applies totals.
/// </summary>
public class JackpotPresentationController : MonoBehaviour
{
    [Header("Optional full-screen / banner (enable during jackpot)")]
    [SerializeField] private GameObject jackpotPresentationRoot;

    [FormerlySerializedAs("elementPoolDisplay")]
    [SerializeField] private StoredActionsPoolDisplay storedActionsPoolDisplay;

    [Header("Container move (optional)")]
    [Tooltip("World position comes from this transform (e.g. empty UI object where the pool should sit).")]
    [SerializeField] private Transform jackpotContainerMoveTarget;

    [Tooltip("Duration in seconds (unscaled). 0 or no target skips the move.")]
    [SerializeField] private float containerMoveDuration = 0.35f;

    [SerializeField] private AnimationCurve containerMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("After the exit-clip wait, tween the pool container back to its saved local position (unscaled). 0 = snap.")]
    [SerializeField] private float containerReturnDuration = 0.35f;

    [SerializeField] private AnimationCurve containerReturnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("If true, pool container returns to its pre-presentation local position after the exit wait.")]
    [SerializeField] private bool restoreContainerLocalPositionAfter = true;

    [Header("Per-row jackpot reveal")]
    [Tooltip("Realtime wait after the container move (and full-screen root, if any) before the first row's jackpot object is enabled.")]
    [SerializeField] private float delayBeforeFirstJackpotReveal;

    [Tooltip("Realtime delay between each following row, top to bottom.")]
    [SerializeField] private float staggerDelayBetweenRows = 0.08f;

    [Tooltip(
        "Per row: after this row's jackpot is shown (animation starts), wait this long (realtime), then the value text updates to the multiplied total with a background scale pulse on StoredActionsPoolIcon.")]
    [SerializeField] private float valueRevealDelayAfterRowJackpotStart = 0.35f;

    [Header("After value text updates")]
    [Tooltip(
        "Realtime pause after every visible pool row that receives a post-multiply value has updated its amount text, before EndSequence runs.")]
    [SerializeField] private float secondsAfterAllPoolValuesUpdatedBeforeEndSequence = 0.35f;

    [Header("Exit animation (optional)")]
    [Tooltip("Animator on the Perfect Strike screen (or root). Receives EndSequence after the pause above.")]
    [SerializeField] private Animator perfectStrikeScreenAnimator;

    [SerializeField] private string endSequenceTriggerParameter = "EndSequence";

    [Tooltip("Realtime wait after EndSequence — set at least to your exit clip length before the UI is torn down.")]
    [SerializeField] private float holdSeconds = 2f;

    public IEnumerator Run(int multiplier, Dictionary<PoolRowKey, int> poolsBefore, Dictionary<PoolRowKey, int> poolsAfter)
    {
        if (storedActionsPoolDisplay == null)
        {
            Debug.LogError($"JackpotPresentationController on '{gameObject.name}': storedActionsPoolDisplay is not assigned.");
            yield break;
        }

        if (jackpotPresentationRoot != null)
            jackpotPresentationRoot.SetActive(true);

        storedActionsPoolDisplay.PrepareJackpotPresentation(poolsBefore);

        var container = storedActionsPoolDisplay.GetIconContainerRect();
        var startLocal = container != null ? container.localPosition : Vector3.zero;
        var endLocal = startLocal;

        if (container != null && jackpotContainerMoveTarget != null && containerMoveDuration > 0f)
        {
            var parent = container.parent;
            endLocal = parent != null
                ? parent.InverseTransformPoint(jackpotContainerMoveTarget.position)
                : jackpotContainerMoveTarget.localPosition;

            foreach (var step in TweenContainerLocalUnscaled(container, startLocal, endLocal, containerMoveDuration, containerMoveCurve))
                yield return step;
        }

        if (delayBeforeFirstJackpotReveal > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeFirstJackpotReveal);

        var icons = storedActionsPoolDisplay.GetVisiblePoolIconsTopToBottom();
        for (var i = 0; i < icons.Count; i++)
        {
            if (i > 0 && staggerDelayBetweenRows > 0f)
                yield return new WaitForSecondsRealtime(staggerDelayBetweenRows);

            var icon = icons[i];
            icon.ShowJackpotMultiplierBadge(multiplier);
            if (poolsAfter != null && poolsAfter.TryGetValue(icon.RowKey, out var postMultiply))
                icon.ScheduleJackpotPostMultiplyValueReveal(postMultiply, valueRevealDelayAfterRowJackpotStart);
        }

        while (AnyScheduledJackpotValueTextStillPending(icons, poolsAfter))
            yield return null;

        var pauseAfterValues = Mathf.Max(0f, secondsAfterAllPoolValuesUpdatedBeforeEndSequence);
        if (pauseAfterValues > 0f)
            yield return new WaitForSecondsRealtime(pauseAfterValues);

        if (perfectStrikeScreenAnimator != null)
        {
            if (string.IsNullOrEmpty(endSequenceTriggerParameter))
            {
                Debug.LogError(
                    $"JackpotPresentationController on '{gameObject.name}': perfectStrikeScreenAnimator is set but endSequenceTriggerParameter is empty.");
            }
            else
            {
                perfectStrikeScreenAnimator.SetTrigger(endSequenceTriggerParameter);
            }
        }

        var wait = Mathf.Max(0f, holdSeconds);
        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);

        if (restoreContainerLocalPositionAfter && container != null)
        {
            var backDur = Mathf.Max(0f, containerReturnDuration);
            if (backDur > 0f)
            {
                foreach (var step in TweenContainerLocalUnscaled(
                             container,
                             container.localPosition,
                             startLocal,
                             backDur,
                             containerReturnCurve))
                    yield return step;
            }
            else
                container.localPosition = startLocal;
        }

        storedActionsPoolDisplay.FinishJackpotPresentation(poolsAfter);

        if (jackpotPresentationRoot != null)
            jackpotPresentationRoot.SetActive(false);
    }

    static IEnumerable TweenContainerLocalUnscaled(
        RectTransform rect,
        Vector3 fromLocal,
        Vector3 toLocal,
        float duration,
        AnimationCurve curve)
    {
        if (rect == null) yield break;

        if (duration <= 0f)
        {
            rect.localPosition = toLocal;
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var u = Mathf.Clamp01(elapsed / duration);
            var c = curve != null && curve.length > 0 ? curve.Evaluate(u) : u;
            rect.localPosition = Vector3.LerpUnclamped(fromLocal, toLocal, c);
            yield return null;
        }

        rect.localPosition = toLocal;
    }

    static bool AnyScheduledJackpotValueTextStillPending(IReadOnlyList<StoredActionsPoolIcon> icons, Dictionary<PoolRowKey, int> poolsAfter)
    {
        if (poolsAfter == null || icons == null) return false;
        for (var i = 0; i < icons.Count; i++)
        {
            var icon = icons[i];
            if (icon == null || !poolsAfter.TryGetValue(icon.RowKey, out _)) continue;
            if (!icon.JackpotPostMultiplyValueTextApplied) return true;
        }

        return false;
    }
}
