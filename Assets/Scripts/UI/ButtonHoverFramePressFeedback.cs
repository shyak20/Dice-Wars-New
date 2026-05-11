using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hover: shows a frame. Pointer exit: fades the frame out over time (Canvas Group), then hides it — same idea as map tile button hover.
/// Pointer down: scales <see cref="pressScaleTarget"/> by <see cref="pressScaleMultiplier"/>; pointer up restores base scale.
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonHoverFramePressFeedback : MonoBehaviour
{
    [SerializeField] private Button button;
    [Tooltip("Border / highlight shown while the pointer is over the button.")]
    [SerializeField] private GameObject hoverFrame;
    [Tooltip("Optional. If unset, uses a Canvas Group on hoverFrame or adds one for the fade-out.")]
    [SerializeField] private CanvasGroup hoverFrameCanvasGroup;
    [Tooltip("Object whose local scale is reduced while the pointer is pressed (e.g. label parent or icon).")]
    [SerializeField] private GameObject pressScaleTarget;
    [Tooltip("Uniform scale multiplier while the pointer is pressed (e.g. 0.92 = slightly smaller).")]
    [SerializeField, Min(0.01f)] private float pressScaleMultiplier = 0.92f;
    [Tooltip("Linear fade from opaque to transparent when hover ends, then the frame is hidden.")]
    [SerializeField, Min(0f)] private float hoverFrameFadeOutSeconds = 0.25f;

    private Vector3 _pressTargetBaseLocalScale = Vector3.one;
    private CanvasGroup _resolvedFrameCanvasGroup;
    private Coroutine _fadeOutRoutine;
    private bool _eventHandlersRegistered;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
            Debug.LogError($"{nameof(ButtonHoverFramePressFeedback)} on '{name}': requires a {nameof(Button)}.", this);
        if (hoverFrame == null)
            Debug.LogError($"{nameof(ButtonHoverFramePressFeedback)} on '{name}': assign hover frame.", this);
        if (pressScaleTarget == null)
            Debug.LogError($"{nameof(ButtonHoverFramePressFeedback)} on '{name}': assign press scale target.", this);

        if (pressScaleTarget != null)
            _pressTargetBaseLocalScale = pressScaleTarget.transform.localScale;

        if (hoverFrame != null)
        {
            hoverFrame.SetActive(false);
            ResolveFrameCanvasGroup();
            if (_resolvedFrameCanvasGroup != null)
                _resolvedFrameCanvasGroup.alpha = 1f;
        }

        RegisterEventTriggerEntries();
    }

    private void OnDisable()
    {
        StopFadeOutCoroutine();
        if (hoverFrame != null)
            hoverFrame.SetActive(false);
        if (_resolvedFrameCanvasGroup != null)
            _resolvedFrameCanvasGroup.alpha = 1f;
        RestorePressTargetScale();
    }

    private void RegisterEventTriggerEntries()
    {
        if (button == null || _eventHandlersRegistered)
            return;

        var trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        AddEntry(trigger, EventTriggerType.PointerEnter, _ => OnPointerEnter());
        AddEntry(trigger, EventTriggerType.PointerExit, _ => OnPointerExit());
        AddEntry(trigger, EventTriggerType.PointerDown, _ => OnPointerDown());
        AddEntry(trigger, EventTriggerType.PointerUp, _ => OnPointerUp());

        _eventHandlersRegistered = true;
    }

    private static void AddEntry(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        var e = new EventTrigger.Entry { eventID = type };
        e.callback.AddListener(action);
        trigger.triggers.Add(e);
    }

    private CanvasGroup ResolveFrameCanvasGroup()
    {
        if (hoverFrame == null)
            return null;
        if (hoverFrameCanvasGroup != null)
        {
            _resolvedFrameCanvasGroup = hoverFrameCanvasGroup;
            return _resolvedFrameCanvasGroup;
        }

        if (_resolvedFrameCanvasGroup == null)
        {
            _resolvedFrameCanvasGroup = hoverFrame.GetComponent<CanvasGroup>();
            if (_resolvedFrameCanvasGroup == null)
                _resolvedFrameCanvasGroup = hoverFrame.GetComponentInChildren<CanvasGroup>(true);
            if (_resolvedFrameCanvasGroup == null)
            {
                _resolvedFrameCanvasGroup = hoverFrame.AddComponent<CanvasGroup>();
                _resolvedFrameCanvasGroup.blocksRaycasts = false;
                _resolvedFrameCanvasGroup.interactable = false;
            }
        }

        return _resolvedFrameCanvasGroup;
    }

    private void OnPointerEnter()
    {
        StopFadeOutCoroutine();
        if (hoverFrame == null)
            return;

        var cg = ResolveFrameCanvasGroup();
        if (cg != null)
            cg.alpha = 1f;
        hoverFrame.SetActive(true);
    }

    private void OnPointerExit()
    {
        RestorePressTargetScale();
        if (hoverFrame == null || !hoverFrame.activeSelf)
            return;

        StopFadeOutCoroutine();
        _fadeOutRoutine = StartCoroutine(CoFadeOutFrame());
    }

    private void OnPointerDown()
    {
        if (pressScaleTarget == null)
            return;
        pressScaleTarget.transform.localScale = _pressTargetBaseLocalScale * pressScaleMultiplier;
    }

    private void OnPointerUp()
    {
        RestorePressTargetScale();
    }

    private void RestorePressTargetScale()
    {
        if (pressScaleTarget != null)
            pressScaleTarget.transform.localScale = _pressTargetBaseLocalScale;
    }

    private void StopFadeOutCoroutine()
    {
        if (_fadeOutRoutine == null)
            return;
        StopCoroutine(_fadeOutRoutine);
        _fadeOutRoutine = null;
    }

    private IEnumerator CoFadeOutFrame()
    {
        var cg = ResolveFrameCanvasGroup();
        if (cg == null)
        {
            hoverFrame.SetActive(false);
            _fadeOutRoutine = null;
            yield break;
        }

        if (hoverFrameFadeOutSeconds <= 0f)
        {
            cg.alpha = 0f;
            hoverFrame.SetActive(false);
            cg.alpha = 1f;
            _fadeOutRoutine = null;
            yield break;
        }

        var startAlpha = cg.alpha;
        var t = 0f;
        while (t < hoverFrameFadeOutSeconds)
        {
            t += Time.unscaledDeltaTime;
            var u = Mathf.Clamp01(t / hoverFrameFadeOutSeconds);
            cg.alpha = Mathf.Lerp(startAlpha, 0f, u);
            yield return null;
        }

        cg.alpha = 0f;
        hoverFrame.SetActive(false);
        cg.alpha = 1f;
        _fadeOutRoutine = null;
    }
}
