using UnityEngine;

public class DiceRoller : MonoBehaviour
{
    [Header("Detection Settings")]
    public float velocityThreshold = 0.2f;
    public float settleTime = 0.3f;

    /// <summary>Set by <see cref="DiceSpawner"/> — order within the current roll batch.</summary>
    public int BatchIndex { get; internal set; }

    private Rigidbody rb;
    private DieVisualizer visualizer;
    private CombatManager manager;
    private bool isChecking = false;
    private float settleTimer = 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        visualizer = GetComponent<DieVisualizer>();
        manager = FindObjectOfType<CombatManager>();
    }

    public void StartCheckingResult()
    {
        settleTimer = 0f;
        isChecking = true;
    }

    private void Update()
    {
        if (!isChecking) return;

        if (rb.linearVelocity.magnitude < velocityThreshold && rb.angularVelocity.magnitude < velocityThreshold)
        {
            settleTimer += Time.deltaTime;
            if (settleTimer >= settleTime)
            {
                isChecking = false;
                DetermineFinalFace();
            }
        }
        else { settleTimer = 0f; }
    }

    private void DetermineFinalFace()
    {
        var closestIndex = DieFaceTopology.FindTopFaceIndex(transform);

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