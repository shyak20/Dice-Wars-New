using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(DieVisualizer))] // Ensures we have the data bridge
public class DiceRoller : MonoBehaviour
{
    [Header("Settle Detection")]
    [Tooltip("Linear velocity under this is considered almost stopped")]
    public float velocityThreshold = 0.05f;

    [Tooltip("Angular velocity under this is considered almost stopped")]
    public float angularVelocityThreshold = 0.05f;

    [Tooltip("How long the die must stay under thresholds before result is accepted")]
    public float settleTime = 0.5f;

    private Rigidbody rb;
    private DieVisualizer visualizer;
    private bool isCheckingResult;
    private bool hasResult;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        visualizer = GetComponent<DieVisualizer>();
    }

    public void StartCheckingResult()
    {
        if (isCheckingResult) return;

        hasResult = false;
        isCheckingResult = true;
        StartCoroutine(CheckWhenSettled());
    }

    private IEnumerator CheckWhenSettled()
    {
        float stillTimer = 0f;

        // Small delay so the die has time to start moving after spawn/force
        yield return new WaitForSeconds(0.2f);

        while (!hasResult)
        {
            // Check if the physics body has come to a rest
            bool isSlowEnough =
                rb.velocity.magnitude <= velocityThreshold &&
                rb.angularVelocity.magnitude <= angularVelocityThreshold;

            if (isSlowEnough)
            {
                stillTimer += Time.deltaTime;

                if (stillTimer >= settleTime)
                {
                    // 1. Get the physical orientation index (0-5)
                    int faceIndex = GetTopFaceIndex();

                    // 2. Retrieve the SO Data from the visualizer bridge
                    if (visualizer.currentData != null)
                    {
                        DieFaceSO resultFace = visualizer.currentData.faces[faceIndex];

                        // 3. Report the specific Face Data to the Combat Manager
                        CombatManager manager = FindObjectOfType<CombatManager>();
                        if (manager != null)
                        {
                            manager.ResolveRollResult(resultFace);
                        }
                        else
                        {
                            Debug.LogError("DiceRoller: No CombatManager found in scene!");
                        }
                    }

                    hasResult = true;
                    isCheckingResult = false;
                    yield break;
                }
            }
            else
            {
                stillTimer = 0f;
            }

            yield return null;
        }
    }

    /// <summary>
    /// Returns the index of the face pointing Up (+Y in World Space).
    /// Index mapping matches DieAssetSO: 0:Up, 1:Down, 2:Right, 3:Left, 4:Forward, 5:Back
    /// </summary>
    public int GetTopFaceIndex()
    {
        float bestDot = -Mathf.Infinity;
        int bestIndex = -1;

        // Check local directions against World Up
        CheckFace(transform.up, 0, ref bestDot, ref bestIndex);      // Local +Y
        CheckFace(-transform.up, 1, ref bestDot, ref bestIndex);     // Local -Y
        CheckFace(transform.right, 2, ref bestDot, ref bestIndex);    // Local +X
        CheckFace(-transform.right, 3, ref bestDot, ref bestIndex);   // Local -X
        CheckFace(transform.forward, 4, ref bestDot, ref bestIndex);  // Local +Z
        CheckFace(-transform.forward, 5, ref bestDot, ref bestIndex); // Local -Z

        return bestIndex;
    }

    private void CheckFace(Vector3 faceDirection, int index, ref float bestDot, ref int bestIndex)
    {
        // Dot product of 1 means directions are perfectly aligned
        float dot = Vector3.Dot(faceDirection.normalized, Vector3.up);

        if (dot > bestDot)
        {
            bestDot = dot;
            bestIndex = index;
        }
    }
}