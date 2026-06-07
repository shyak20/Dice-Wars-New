using System.Collections;
using System.Collections.Generic;
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
    [Tooltip("After the last element starts its destroy visual, wait this long before resuming combat flow.")]
    [SerializeField, Min(0f)] private float delayPostAnimation = 0.2f;
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
        StoredActionsPoolIcon.HideAllBustDestroyVisualsInScene();
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
        StoredActionsPoolIcon.HideAllBustDestroyVisualsInScene();

        if (waitBeforeExplosion > 0f)
            yield return new WaitForSecondsRealtime(waitBeforeExplosion);

        var icons = CollectActiveSceneIconsTopToBottom();
        for (var i = 0; i < icons.Count; i++)
        {
            var icon = icons[i];
            if (icon != null)
                icon.ShowBustDestroyVisual(true);

            if (delayBetweenElementDestroy > 0f && i < icons.Count - 1)
                yield return new WaitForSecondsRealtime(delayBetweenElementDestroy);
        }

        if (delayPostAnimation > 0f)
            yield return new WaitForSecondsRealtime(delayPostAnimation);

        CombatEvents.OnBustResolved?.Invoke();

        StoredActionsPoolIcon.HideAllBustDestroyVisualsInScene();
        StoredActionsPoolIcon.RestoreAllDefaultChildVisualStatesInScene();
        if (bustPanel != null)
            bustPanel.SetActive(false);
        _routine = null;
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

    private void StopActiveRoutine()
    {
        if (_routine == null) return;
        StopCoroutine(_routine);
        _routine = null;
    }
}
