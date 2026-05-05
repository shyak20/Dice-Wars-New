using System.Collections;
using UnityEngine;

/// <summary>
/// Handles bust UI timing and element-pool destroy sequencing, then emits <see cref="CombatEvents.OnBustResolved"/>.
/// Keeps bust concerns out of <see cref="CombatUIController"/>.
/// </summary>
public sealed class BustPresentationController : MonoBehaviour
{
    [Header("Bust Timing")]
    [Tooltip("The delay before starting to play destroy elements (including any VFX that start with this sequence).")]
    [SerializeField, Min(0f)] private float waitBeforeExplosion = 0.35f;
    [Tooltip("Realtime delay between each element in the stored-actions pool.")]
    [SerializeField, Min(0f)] private float delayBetweenElementDestroy = 0.08f;
    [Tooltip("Shown while bust is being presented.")]
    [SerializeField] private GameObject bustPanel;

    [Header("Dependencies")]
    [SerializeField] private StoredActionsPoolDisplay storedActionsPoolDisplay;

    private Coroutine _routine;

    private void Awake()
    {
        if (storedActionsPoolDisplay == null)
            storedActionsPoolDisplay = FindObjectOfType<StoredActionsPoolDisplay>(true);
    }

    private void OnEnable()
    {
        CombatEvents.OnBustOccurred += OnBustOccurred;
    }

    private void OnDisable()
    {
        CombatEvents.OnBustOccurred -= OnBustOccurred;
        StopActiveRoutine();
        if (bustPanel != null)
            bustPanel.SetActive(false);
        storedActionsPoolDisplay?.HideAllBustDestroyVisuals();
    }

    private void OnBustOccurred(int _currentDmg, int _currentArm)
    {
        StopActiveRoutine();
        _routine = StartCoroutine(CoPresentBust());
    }

    private IEnumerator CoPresentBust()
    {
        if (bustPanel != null)
            bustPanel.SetActive(true);
        storedActionsPoolDisplay?.HideAllBustDestroyVisuals();

        if (waitBeforeExplosion > 0f)
            yield return new WaitForSecondsRealtime(waitBeforeExplosion);

        if (storedActionsPoolDisplay != null)
        {
            var icons = storedActionsPoolDisplay.GetVisiblePoolIconsTopToBottom();
            for (var i = 0; i < icons.Count; i++)
            {
                var icon = icons[i];
                if (icon != null)
                    icon.ShowBustDestroyVisual(true);

                if (delayBetweenElementDestroy > 0f && i < icons.Count - 1)
                    yield return new WaitForSecondsRealtime(delayBetweenElementDestroy);
            }
        }

        CombatEvents.OnBustResolved?.Invoke();

        storedActionsPoolDisplay?.HideAllBustDestroyVisuals();
        if (bustPanel != null)
            bustPanel.SetActive(false);
        _routine = null;
    }

    private void StopActiveRoutine()
    {
        if (_routine == null) return;
        StopCoroutine(_routine);
        _routine = null;
    }
}
