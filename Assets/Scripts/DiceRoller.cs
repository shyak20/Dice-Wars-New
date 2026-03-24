using System;
using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DiceRoller : MonoBehaviour
{
    [Header("Face values by local direction")]
    [Tooltip("Value on the face pointing to local +Y")]
    public int upFace = 1;

    [Tooltip("Value on the face pointing to local -Y")]
    public int downFace = 6;

    [Tooltip("Value on the face pointing to local +X")]
    public int rightFace = 3;

    [Tooltip("Value on the face pointing to local -X")]
    public int leftFace = 4;

    [Tooltip("Value on the face pointing to local +Z")]
    public int forwardFace = 2;

    [Tooltip("Value on the face pointing to local -Z")]
    public int backFace = 5;

    [Header("Settle detection")]
    [Tooltip("Linear velocity under this is considered almost stopped")]
    public float velocityThreshold = 0.05f;

    [Tooltip("Angular velocity under this is considered almost stopped")]
    public float angularVelocityThreshold = 0.05f;

    [Tooltip("How long the die must stay under thresholds before result is accepted")]
    public float settleTime = 0.5f;

    [Header("Optional UI")]
    public TMP_Text resultText;

    public Action<int> OnDiceResult;

    private Rigidbody rb;
    private bool isCheckingResult;
    private bool hasResult;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
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
        yield return new WaitForSeconds(0.1f);

        while (!hasResult)
        {
            bool isSlowEnough =
                rb.velocity.magnitude <= velocityThreshold &&
                rb.angularVelocity.magnitude <= angularVelocityThreshold;

            if (isSlowEnough)
            {
                stillTimer += Time.deltaTime;

                if (stillTimer >= settleTime)
                {
                    int result = GetTopFaceValue();
                    hasResult = true;
                    isCheckingResult = false;

                    if (resultText != null)
                        resultText.text = "Rolled: " + result;

                    OnDiceResult?.Invoke(result);
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

    public int GetTopFaceValue()
    {
        float bestDot = -Mathf.Infinity;
        int bestValue = -1;

        CheckFace(transform.up, upFace, ref bestDot, ref bestValue);
        CheckFace(-transform.up, downFace, ref bestDot, ref bestValue);
        CheckFace(transform.right, rightFace, ref bestDot, ref bestValue);
        CheckFace(-transform.right, leftFace, ref bestDot, ref bestValue);
        CheckFace(transform.forward, forwardFace, ref bestDot, ref bestValue);
        CheckFace(-transform.forward, backFace, ref bestDot, ref bestValue);

        return bestValue;
    }

    private void CheckFace(Vector3 faceDirection, int faceValue, ref float bestDot, ref int bestValue)
    {
        float dot = Vector3.Dot(faceDirection.normalized, Vector3.up);

        if (dot > bestDot)
        {
            bestDot = dot;
            bestValue = faceValue;
        }
    }
}