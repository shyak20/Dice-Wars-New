using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One step in a linear tutorial: a button advances to <see cref="nextInLine"/> or closes <see cref="rootTutorial"/>.
/// Place on the step GameObject (or a child); assign <see cref="currentTutorialObject"/> if this component is not on the panel root.
/// </summary>
public sealed class TutorialStepController : MonoBehaviour
{
    [SerializeField] private Button advanceButton;
    [Tooltip("Entire tutorial overlay — disabled when there is no next step.")]
    [SerializeField] private GameObject rootTutorial;
    [Tooltip("Following step; leave empty to end the tutorial (root is turned off).")]
    [SerializeField] private GameObject nextInLine;
    [Tooltip("Panel hidden when advancing; if unset, uses this GameObject.")]
    [SerializeField] private GameObject currentTutorialObject;

    private void Awake()
    {
        if (advanceButton == null)
            Debug.LogError($"{nameof(TutorialStepController)} on '{name}': assign {nameof(advanceButton)}.", this);
        if (rootTutorial == null)
            Debug.LogError($"{nameof(TutorialStepController)} on '{name}': assign {nameof(rootTutorial)}.", this);
        if (currentTutorialObject == null)
            currentTutorialObject = gameObject;
    }

    private void OnEnable()
    {
        if (advanceButton != null)
            advanceButton.onClick.AddListener(OnAdvanceClicked);
    }

    private void OnDisable()
    {
        if (advanceButton != null)
            advanceButton.onClick.RemoveListener(OnAdvanceClicked);
    }

    private void OnAdvanceClicked()
    {
        if (advanceButton == null || rootTutorial == null || currentTutorialObject == null)
            return;

        if (nextInLine != null)
            nextInLine.SetActive(true);

        currentTutorialObject.SetActive(false);

        if (nextInLine == null)
            rootTutorial.SetActive(false);
    }
}
