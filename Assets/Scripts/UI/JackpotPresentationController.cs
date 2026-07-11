using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Perfect-strike presentation order:
/// 1) Raise and scale every active Element Value
/// 2) Play multiply / jackpot animation on Element Values
/// 3) Change each amount to its post-multiply value
/// 4) Return every Element Value to its home pose
/// 5) Continue the exit sequence
/// </summary>
public class JackpotPresentationController : MonoBehaviour
{
    [Header("Optional full-screen / banner (enable during jackpot)")]
    [SerializeField] private GameObject jackpotPresentationRoot;

    [FormerlySerializedAs("elementPoolDisplay")]
    [SerializeField] private StoredActionsPoolDisplay storedActionsPoolDisplay;

    [Header("Element move & scale")]
    [Tooltip("Local Y offset applied to every active Element Value during Perfect Cast (UI units). Positive moves up.")]
    [SerializeField] private float elementYMovement = 80f;

    [Tooltip("Uniform local scale multiplier applied to every active Element Value at the Perfect Cast peak (1 = no change).")]
    [SerializeField, Min(0.01f)] private float elementScaleAmount = 1.15f;

    [Tooltip("Unscaled duration to tween every Element Value to the Y offset and scale peak. 0 = snap.")]
    [SerializeField] private float containerMoveDuration = 0.35f;

    [SerializeField] private AnimationCurve containerMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Unscaled duration to tween every Element Value back to its home pose. 0 = snap.")]
    [SerializeField] private float containerReturnDuration = 0.35f;

    [SerializeField] private AnimationCurve containerReturnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Per-row jackpot reveal")]
    [Tooltip("Extra realtime wait after the Element Value move/scale tween before the first row's jackpot object is enabled.")]
    [SerializeField] private float delayBeforeFirstJackpotReveal;

    [Tooltip("Realtime delay between each following row, top to bottom (when each row's jackpot presentation starts).")]
    [SerializeField] private float staggerDelayBetweenRows = 0.08f;

    [Header("After value text updates")]
    [Tooltip(
        "Realtime pause after every visible player Element Value has updated its amount text, before elements return home.")]
    [SerializeField] private float secondsAfterAllPoolValuesUpdatedBeforeEndSequence = 0.35f;

    [Tooltip(
        "Extra realtime delay after all Element Value numbers have already updated, before elements return home. Does not delay individual number reveals.")]
    [SerializeField] private float delayBeforeContainerReturn;

    [Header("Exit animation (optional)")]
    [Tooltip("Animator on the Perfect Strike screen (or root). Receives EndSequence after elements return home.")]
    [SerializeField] private Animator perfectStrikeScreenAnimator;

    [SerializeField] private string endSequenceTriggerParameter = "EndSequence";

    [Tooltip("Realtime wait after EndSequence — set at least to your exit clip length before the UI is torn down.")]
    [SerializeField] private float holdSeconds = 2f;

    struct ElementAnimState
    {
        public StoredActionsPoolIcon Icon;
        public Transform Transform;
        public Vector3 BaseLocalPosition;
        public Vector3 BaseLocalScale;
        public Vector3 PeakLocalPosition;
        public Vector3 PeakLocalScale;
    }

    public IEnumerator Run(int multiplier, Dictionary<PoolRowKey, int> poolsBefore, Dictionary<PoolRowKey, int> poolsAfter)
    {
        if (storedActionsPoolDisplay == null)
        {
            Debug.LogError($"JackpotPresentationController on '{gameObject.name}': storedActionsPoolDisplay is not assigned.");
            yield break;
        }

        if (jackpotPresentationRoot != null)
            jackpotPresentationRoot.SetActive(true);

        // Capture flyout-deposited totals before Prepare overwrites snapshot keys — used when poolsAfter omits a row.
        var displayedBeforePrepare = storedActionsPoolDisplay.CopyDisplayedPools();
        storedActionsPoolDisplay.PrepareJackpotPresentation(poolsBefore);

        var allElements = CollectActiveSceneIconsTopToBottom();
        var animStates = CaptureElementAnimStates(allElements, elementYMovement, elementScaleAmount);
        var suspendedLayouts = SuspendParentLayouts(allElements);

        try
        {
            foreach (var step in TweenElementsUnscaled(
                         animStates,
                         toPeak: true,
                         Mathf.Max(0f, containerMoveDuration),
                         containerMoveCurve))
                yield return step;

            if (delayBeforeFirstJackpotReveal > 0f)
                yield return new WaitForSecondsRealtime(delayBeforeFirstJackpotReveal);

            var scheduledValueReveals = new List<StoredActionsPoolIcon>();
            var scheduledPlayerReveals = new List<StoredActionsPoolIcon>();
            var playerElementRows = storedActionsPoolDisplay.GetVisiblePoolIconsTopToBottom();
            var playerElementSet = new HashSet<StoredActionsPoolIcon>(playerElementRows);
            poolsAfter = EnrichPoolsAfterFromVisiblePlayerRows(
                poolsAfter,
                poolsBefore,
                displayedBeforePrepare,
                storedActionsPoolDisplay,
                playerElementRows,
                multiplier);

            // Arm every Element Value (staggered badge start). Each icon immediately runs its own scale-up pulse and
            // writes the post-multiply number when that scale-up starts — independently of the others.

            // 1) Player Element Values (these gate the return home).
            for (var i = 0; i < playerElementRows.Count; i++)
            {
                if (i > 0 && staggerDelayBetweenRows > 0f)
                    yield return new WaitForSecondsRealtime(staggerDelayBetweenRows);

                var icon = playerElementRows[i];
                icon.ShowJackpotMultiplierBadge(multiplier);

                if (!TryGetPostMultiplyValue(
                        icon,
                        storedActionsPoolDisplay,
                        poolsAfter,
                        poolsBefore,
                        displayedBeforePrepare,
                        multiplier,
                        out var postMultiply))
                {
                    Debug.LogError(
                        $"JackpotPresentationController: player element row '{icon.RowKey.StableId}' has no post-Perfect-Cast value. " +
                        "Cannot arm the mid-sequence value reveal.",
                        icon);
                    continue;
                }

                var amountKey = icon.RowKey;
                if (storedActionsPoolDisplay.TryGetRowAmount(icon, out var mapKey, out _))
                    amountKey = mapKey;
                storedActionsPoolDisplay.SetDisplayedAmountWithoutRefresh(amountKey, postMultiply);
                icon.ArmJackpotPostMultiplyValueReveal(postMultiply, storedActionsPoolDisplay);
                scheduledValueReveals.Add(icon);
                scheduledPlayerReveals.Add(icon);
            }

            // 2) Enemy pools / tokens / other flyout icons — armed the same way, do not gate the return home.
            var icons = CollectActiveSceneIconsTopToBottom();
            var nonPlayerStaggerIndex = 0;
            for (var i = 0; i < icons.Count; i++)
            {
                var icon = icons[i];
                if (playerElementSet.Contains(icon))
                    continue;

                if (nonPlayerStaggerIndex > 0 && staggerDelayBetweenRows > 0f)
                    yield return new WaitForSecondsRealtime(staggerDelayBetweenRows);
                nonPlayerStaggerIndex++;

                icon.ShowJackpotMultiplierBadge(multiplier);
                if (!TryGetPostMultiplyValue(
                        icon,
                        null,
                        poolsAfter,
                        poolsBefore,
                        null,
                        multiplier,
                        out var postMultiply))
                    continue;

                var owningDisplay = icon.GetComponentInParent<StoredActionsPoolDisplay>();
                if (owningDisplay != null && owningDisplay.TryGetRowAmount(icon, out var enemyMapKey, out _))
                    owningDisplay.SetDisplayedAmountWithoutRefresh(enemyMapKey, postMultiply);
                else if (owningDisplay != null && owningDisplay.IsStandalonePerEnemyPool)
                    owningDisplay.SetDisplayedAmountWithoutRefresh(icon.RowKey, postMultiply);

                var runner = owningDisplay != null ? (MonoBehaviour)owningDisplay : this;
                icon.ArmJackpotPostMultiplyValueReveal(postMultiply, runner);
                scheduledValueReveals.Add(icon);
            }

            // Wait until every armed Element Value has written its number at scale-up.
            while (!AllScheduledValuesUpdated(scheduledPlayerReveals) || AnyJackpotValueRevealStillPending(scheduledValueReveals))
                yield return null;

            var pauseAfterValues = Mathf.Max(0f, secondsAfterAllPoolValuesUpdatedBeforeEndSequence);
            if (pauseAfterValues > 0f)
                yield return new WaitForSecondsRealtime(pauseAfterValues);

            var returnDelay = Mathf.Max(0f, delayBeforeContainerReturn);
            if (returnDelay > 0f)
                yield return new WaitForSecondsRealtime(returnDelay);

            // 3) Return home only after values are updated (and any manual return delay).
            foreach (var step in TweenElementsUnscaled(
                         animStates,
                         toPeak: false,
                         Mathf.Max(0f, containerReturnDuration),
                         containerReturnCurve))
                yield return step;

            storedActionsPoolDisplay.FinishJackpotPresentation(poolsAfter);

            // 4) Continue the exit sequence after elements are home.
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

            if (jackpotPresentationRoot != null)
                jackpotPresentationRoot.SetActive(false);
        }
        finally
        {
            SnapElementsToBase(animStates);
            RestoreParentLayouts(suspendedLayouts);
        }
    }

    static List<ElementAnimState> CaptureElementAnimStates(
        IReadOnlyList<StoredActionsPoolIcon> icons,
        float yMovement,
        float scaleAmount)
    {
        var states = new List<ElementAnimState>(icons != null ? icons.Count : 0);
        if (icons == null)
            return states;

        var scale = Mathf.Max(0.01f, scaleAmount);
        for (var i = 0; i < icons.Count; i++)
        {
            var icon = icons[i];
            if (icon == null)
                continue;

            var t = icon.transform;
            var basePos = t.localPosition;
            var baseScale = t.localScale;
            states.Add(new ElementAnimState
            {
                Icon = icon,
                Transform = t,
                BaseLocalPosition = basePos,
                BaseLocalScale = baseScale,
                PeakLocalPosition = basePos + new Vector3(0f, yMovement, 0f),
                PeakLocalScale = baseScale * scale,
            });
        }

        return states;
    }

    static List<Behaviour> SuspendParentLayouts(IReadOnlyList<StoredActionsPoolIcon> icons)
    {
        var suspended = new List<Behaviour>();
        if (icons == null)
            return suspended;

        var seen = new HashSet<Behaviour>();
        for (var i = 0; i < icons.Count; i++)
        {
            var icon = icons[i];
            if (icon == null)
                continue;

            var layouts = icon.GetComponentsInParent<LayoutGroup>(true);
            for (var l = 0; l < layouts.Length; l++)
            {
                var layout = layouts[l];
                if (layout == null || !layout.enabled || !seen.Add(layout))
                    continue;
                layout.enabled = false;
                suspended.Add(layout);
            }
        }

        return suspended;
    }

    static void RestoreParentLayouts(List<Behaviour> suspended)
    {
        if (suspended == null)
            return;

        for (var i = 0; i < suspended.Count; i++)
        {
            var behaviour = suspended[i];
            if (behaviour != null)
                behaviour.enabled = true;
        }
    }

    static void SnapElementsToBase(List<ElementAnimState> states)
    {
        if (states == null)
            return;

        for (var i = 0; i < states.Count; i++)
        {
            var state = states[i];
            if (state.Transform == null)
                continue;
            state.Transform.localPosition = state.BaseLocalPosition;
            state.Transform.localScale = state.BaseLocalScale;
        }
    }

    static IEnumerable TweenElementsUnscaled(
        List<ElementAnimState> states,
        bool toPeak,
        float duration,
        AnimationCurve curve)
    {
        if (states == null || states.Count == 0)
            yield break;

        if (duration <= 0f)
        {
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state.Transform == null)
                    continue;
                state.Transform.localPosition = toPeak ? state.PeakLocalPosition : state.BaseLocalPosition;
                state.Transform.localScale = toPeak ? state.PeakLocalScale : state.BaseLocalScale;
            }

            yield break;
        }

        var fromPos = new Vector3[states.Count];
        var fromScale = new Vector3[states.Count];
        for (var i = 0; i < states.Count; i++)
        {
            var state = states[i];
            if (state.Transform == null)
                continue;
            fromPos[i] = state.Transform.localPosition;
            fromScale[i] = state.Transform.localScale;
        }

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var u = Mathf.Clamp01(elapsed / duration);
            var c = curve != null && curve.length > 0 ? curve.Evaluate(u) : u;
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state.Transform == null)
                    continue;
                var toPos = toPeak ? state.PeakLocalPosition : state.BaseLocalPosition;
                var toScale = toPeak ? state.PeakLocalScale : state.BaseLocalScale;
                state.Transform.localPosition = Vector3.LerpUnclamped(fromPos[i], toPos, c);
                state.Transform.localScale = Vector3.LerpUnclamped(fromScale[i], toScale, c);
            }

            yield return null;
        }

        for (var i = 0; i < states.Count; i++)
        {
            var state = states[i];
            if (state.Transform == null)
                continue;
            state.Transform.localPosition = toPeak ? state.PeakLocalPosition : state.BaseLocalPosition;
            state.Transform.localScale = toPeak ? state.PeakLocalScale : state.BaseLocalScale;
        }
    }

    static bool AllScheduledValuesUpdated(IReadOnlyList<StoredActionsPoolIcon> icons)
    {
        if (icons == null || icons.Count == 0)
            return true;

        for (var i = 0; i < icons.Count; i++)
        {
            var icon = icons[i];
            if (icon == null)
                continue;
            if (!icon.JackpotPostMultiplyValueTextApplied)
                return false;
        }

        return true;
    }

    static List<StoredActionsPoolIcon> CollectActiveSceneIconsTopToBottom()
    {
        var all = StoredActionsPoolIcon.FindAllInLoadedScenes(true);
        var active = new List<StoredActionsPoolIcon>(all.Count);
        for (var i = 0; i < all.Count; i++)
        {
            var icon = all[i];
            if (icon != null && icon.gameObject.activeInHierarchy)
                active.Add(icon);
        }

        StoredActionsPoolIcon.SortTopToBottom(active);
        return active;
    }

    /// <summary>
    /// Ensures every visible player Element Value has a post-multiply entry for arming / FinishJackpotPresentation.
    /// Flyout deposits and hierarchy quirks can leave rows on screen that are missing from BuildStoredActionsPool snapshots.
    /// </summary>
    static Dictionary<PoolRowKey, int> EnrichPoolsAfterFromVisiblePlayerRows(
        Dictionary<PoolRowKey, int> poolsAfter,
        Dictionary<PoolRowKey, int> poolsBefore,
        Dictionary<PoolRowKey, int> displayedBeforePrepare,
        StoredActionsPoolDisplay playerDisplay,
        IReadOnlyList<StoredActionsPoolIcon> playerRows,
        int multiplier)
    {
        var result = poolsAfter != null
            ? new Dictionary<PoolRowKey, int>(poolsAfter)
            : new Dictionary<PoolRowKey, int>();

        if (playerRows == null)
            return result;

        for (var i = 0; i < playerRows.Count; i++)
        {
            var icon = playerRows[i];
            if (icon == null)
                continue;
            if (result.TryGetValue(icon.RowKey, out var existing) && existing > 0)
                continue;
            if (!TryGetPostMultiplyValue(
                    icon,
                    playerDisplay,
                    poolsAfter,
                    poolsBefore,
                    displayedBeforePrepare,
                    multiplier,
                    out var post) || post <= 0)
                continue;
            result[icon.RowKey] = post;
            if (playerDisplay != null && playerDisplay.TryGetRowAmount(icon, out var mapKey, out _) && !mapKey.Equals(icon.RowKey))
                result[mapKey] = post;
        }

        return result;
    }

    static bool TryGetPostMultiplyValue(
        StoredActionsPoolIcon icon,
        StoredActionsPoolDisplay preferredDisplay,
        Dictionary<PoolRowKey, int> poolsAfter,
        Dictionary<PoolRowKey, int> poolsBefore,
        Dictionary<PoolRowKey, int> displayedBeforePrepare,
        int multiplier,
        out int value)
    {
        value = 0;
        if (icon == null)
            return false;

        var display = preferredDisplay != null
            ? preferredDisplay
            : icon.GetComponentInParent<StoredActionsPoolDisplay>();

        PoolRowKey rowKey = icon.RowKey;
        var displayedAmount = 0;
        // Element Value prefabs also carry RolledOutcomeToken for drag chips. Pool-managed rows must use the
        // display / snapshot amounts — token.Line is only valid after Configure on a pending assign token.
        var isPoolManaged = false;
        if (display != null && display.TryGetRowAmount(icon, out var mapKey, out displayedAmount))
        {
            isPoolManaged = true;
            rowKey = mapKey;
        }
        else if (display != null)
            displayedAmount = display.GetDisplayedAmount(rowKey);

        if (!isPoolManaged)
        {
            var token = icon.GetComponentInParent<RolledOutcomeToken>();
            if (token != null && token.OwnsPoolIcon(icon))
            {
                value = token.Line.Amount;
                return value > 0;
            }
        }

        if (display != null && display.IsStandalonePerEnemyPool)
        {
            // CombatManager.ScaleEnemyElementPoolsForPerfectCast runs after flyouts and before this presentation,
            // so GetDisplayedAmount is already the post-multiply total while the visible text is still pre-multiply.
            value = displayedAmount > 0 ? displayedAmount : display.GetDisplayedAmount(rowKey);
            if (value > 0)
                return true;
            // Fall through — do not fail closed when the standalone dict is briefly empty.
        }

        if (poolsAfter != null)
        {
            if (poolsAfter.TryGetValue(rowKey, out value) && value > 0)
                return true;
            if (!rowKey.Equals(icon.RowKey) && poolsAfter.TryGetValue(icon.RowKey, out value) && value > 0)
                return true;
        }

        var preMultiply = displayedAmount;
        if (preMultiply <= 0 && displayedBeforePrepare != null)
        {
            if (displayedBeforePrepare.TryGetValue(rowKey, out var fromPre) && fromPre > 0)
                preMultiply = fromPre;
            else if (displayedBeforePrepare.TryGetValue(icon.RowKey, out fromPre) && fromPre > 0)
                preMultiply = fromPre;
        }

        if (preMultiply <= 0 && poolsBefore != null)
        {
            if (poolsBefore.TryGetValue(rowKey, out var before) && before > 0)
                preMultiply = before;
            else if (poolsBefore.TryGetValue(icon.RowKey, out before) && before > 0)
                preMultiply = before;
        }

        if (preMultiply <= 0)
            icon.TryGetVisibleAmountText(out preMultiply);

        if (preMultiply <= 0)
            return false;

        value = multiplier > 1 ? preMultiply * multiplier : preMultiply;
        return value > 0;
    }

    static bool AnyJackpotValueRevealStillPending(IReadOnlyList<StoredActionsPoolIcon> icons)
    {
        if (icons == null)
            return false;

        for (var i = 0; i < icons.Count; i++)
        {
            var icon = icons[i];
            if (icon == null)
                continue;
            if (icon.IsJackpotValueRevealInProgress)
                return true;
            if (!icon.JackpotPostMultiplyValueTextApplied)
                return true;
        }

        return false;
    }
}
