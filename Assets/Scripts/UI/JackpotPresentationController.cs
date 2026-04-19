using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Perfect-strike: shows ×multiplier on each visible <see cref="StoredActionsPoolIcon"/>, then applies post-multiply totals.
/// </summary>
public class JackpotPresentationController : MonoBehaviour
{
    [Header("Optional full-screen / banner (enable during jackpot)")]
    [SerializeField] private GameObject jackpotPresentationRoot;

    [FormerlySerializedAs("elementPoolDisplay")]
    [SerializeField] private StoredActionsPoolDisplay storedActionsPoolDisplay;

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

        storedActionsPoolDisplay.BeginJackpotPresentation(multiplier, poolsBefore);

        float wait = Mathf.Max(0f, holdSeconds);
        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);

        storedActionsPoolDisplay.FinishJackpotPresentation(poolsAfter);

        if (jackpotPresentationRoot != null)
            jackpotPresentationRoot.SetActive(false);
    }
}
