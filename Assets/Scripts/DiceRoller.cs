using UnityEngine;

public class DiceRoller : MonoBehaviour
{
    [Header("Detection Settings")]
    public float velocityThreshold = 0.2f;
    public float settleTime = 0.3f;

    [Header("Orientation")]
    [Tooltip("Minimum dot(topFaceNormal, worldUp) to accept settlement. Flat face ≈ 1, edge ≈ 0.71, vertex ≈ 0.58.")]
    [SerializeField, Range(0.5f, 1f)] private float minUpDot = 0.88f;
    [Tooltip("Safety cap — snap to best face and report result if still unsettled after this many seconds.")]
    [SerializeField, Min(1f)] private float maxSettleSeconds = 10f;

    [Header("Edge nudge")]
    [Tooltip("When velocity is low but the die is not face-up, apply a small torque this many times before forcing a snap.")]
    [SerializeField, Min(0)] private int maxNudgeAttempts = 4;
    [SerializeField, Min(0f)] private float nudgeCooldownSeconds = 0.35f;
    [SerializeField, Min(0f)] private float nudgeTorque = 3f;
    [SerializeField, Min(0f)] private float nudgeImpulse = 0.8f;

    /// <summary>Set by <see cref="DiceSpawner"/> — order within the current roll batch.</summary>
    public int BatchIndex { get; internal set; }

    private Rigidbody rb;
    private DieVisualizer visualizer;
    private CombatManager manager;
    private bool isChecking;
    private float settleTimer;
    private float totalSettleTimer;
    private float nudgeCooldownTimer;
    private int nudgeAttempts;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        visualizer = GetComponent<DieVisualizer>();
        manager = FindObjectOfType<CombatManager>();
    }

    public void StartCheckingResult()
    {
        settleTimer = 0f;
        totalSettleTimer = 0f;
        nudgeCooldownTimer = 0f;
        nudgeAttempts = 0;
        isChecking = true;

        if (rb != null)
            rb.isKinematic = false;
    }

    private void FixedUpdate()
    {
        if (!isChecking || rb == null)
            return;

        totalSettleTimer += Time.fixedDeltaTime;
        if (nudgeCooldownTimer > 0f)
            nudgeCooldownTimer -= Time.fixedDeltaTime;

        if (totalSettleTimer >= maxSettleSeconds)
        {
            FinalizeSettlement();
            return;
        }

        var velocityLow = rb.linearVelocity.magnitude < velocityThreshold
                          && rb.angularVelocity.magnitude < velocityThreshold;
        if (!velocityLow)
        {
            settleTimer = 0f;
            return;
        }

        settleTimer += Time.fixedDeltaTime;
        if (settleTimer < settleTime)
            return;

        DieFaceTopology.TryFindTopFaceIndex(transform, out _, out var upDot);
        if (upDot >= minUpDot)
        {
            FinalizeSettlement();
            return;
        }

        if (nudgeAttempts < maxNudgeAttempts && nudgeCooldownTimer <= 0f)
        {
            ApplyEdgeNudge();
            nudgeAttempts++;
            nudgeCooldownTimer = nudgeCooldownSeconds;
            settleTimer = 0f;
            return;
        }

        FinalizeSettlement();
    }

    private void ApplyEdgeNudge()
    {
        rb.WakeUp();
        var torque = Random.onUnitSphere * nudgeTorque;
        rb.AddTorque(torque, ForceMode.Impulse);

        if (nudgeImpulse > 0f)
        {
            var lateral = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            if (lateral.sqrMagnitude > 1e-6f)
                lateral.Normalize();
            rb.AddForce((Vector3.down + lateral * 0.35f) * nudgeImpulse, ForceMode.Impulse);
        }
    }

    private void FinalizeSettlement()
    {
        isChecking = false;

        DieFaceTopology.TryFindTopFaceIndex(transform, out var faceIndex, out _);
        SnapToFaceUp(faceIndex);
        ReportSettledFace(faceIndex);
    }

    private void SnapToFaceUp(int faceIndex)
    {
        if (!DieFaceTopology.TryGetRotationSnapToFaceUp(transform, faceIndex, out var snappedRotation))
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.rotation = snappedRotation;
        rb.isKinematic = true;
    }

    private void ReportSettledFace(int closestIndex)
    {
        if (visualizer == null || visualizer.dieData == null || manager == null)
            return;

        var faces = visualizer.dieData.faces;
        if (faces == null || faces.Length < 6)
            return;

        var resultFace = faces[closestIndex];
        if (resultFace == null)
        {
            for (var i = 0; i < 6; i++)
            {
                if (faces[i] != null)
                {
                    resultFace = faces[i];
                    break;
                }
            }

            if (resultFace == null)
            {
                Debug.LogError(
                    $"DiceRoller on '{name}': die '{visualizer.dieData.name}' has no non-null faces — cannot finish the roll.",
                    this);
                return;
            }

            Debug.LogError(
                $"DiceRoller on '{name}': die '{visualizer.dieData.name}' landed on face index {closestIndex} but that slot is null; using '{resultFace.name}' so the roll can finish. Fix the die asset.",
                this);
        }

        LastResolvedFaceIndex = closestIndex;
        LastResolvedFace = resultFace;
        manager.OnDiePhysicsSettled(BatchIndex, resultFace, transform);
    }

    /// <summary>Top face from the last settled roll (-1 if none yet).</summary>
    public int LastResolvedFaceIndex { get; private set; } = -1;

    /// <summary>Face SO from the last settled roll.</summary>
    public DieFaceSO LastResolvedFace { get; private set; }
}
