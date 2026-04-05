using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Perfect-strike presentation: enables a jackpot root object, shows ×multiplier on each visible <see cref="ElementPoolIcon"/>,
/// waits, then updates values to the post-multiply totals.
/// </summary>
public class JackpotPresentationController : MonoBehaviour
{
    [Header("Optional full-screen / banner (enable during jackpot)")]
    [SerializeField] private GameObject jackpotPresentationRoot;

    [SerializeField] private ElementPoolDisplay elementPoolDisplay;

    [SerializeField] private float holdSeconds = 2f;

    /// <summary>Run from <see cref="CombatManager"/> coroutine. Safe to call with null display (logs error).</summary>
    public IEnumerator Run(int multiplier, Dictionary<DieType, int> poolsBefore, Dictionary<DieType, int> poolsAfter)
    {
        if (elementPoolDisplay == null)
        {
            Debug.LogError($"JackpotPresentationController on '{gameObject.name}': elementPoolDisplay is not assigned.");
            yield break;
        }

        if (jackpotPresentationRoot != null)
            jackpotPresentationRoot.SetActive(true);

        elementPoolDisplay.BeginJackpotPresentation(multiplier, poolsBefore);

        float wait = Mathf.Max(0f, holdSeconds);
        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);

        elementPoolDisplay.FinishJackpotPresentation(poolsAfter);

        if (jackpotPresentationRoot != null)
            jackpotPresentationRoot.SetActive(false);
    }
}
