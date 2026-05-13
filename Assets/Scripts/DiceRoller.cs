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
        // RESTORED & CORRECTED MAPPING:
        // This array matches the submesh order from your Blender export.
        Vector3[] localFaceDirections = {
            Vector3.up,      // Element 0: +Y (Face 1)
            Vector3.down,    // Element 1: -Y (Face 6)
            Vector3.right,   // Element 2: +X (Face 2) -> Swapped to fix "2 getting 5"
            Vector3.left,    // Element 3: -X (Face 5) -> Swapped to fix "2 getting 5"
            Vector3.forward,    // Element 4: -Z (Face 3) -> Swapped to fix "4 getting 3"
            Vector3.back  // Element 5: +Z (Face 4) -> Swapped to fix "4 getting 3"
        };

        float bestDot = -1f;
        int closestIndex = 0;

        for (int i = 0; i < localFaceDirections.Length; i++)
        {
            // We transform the local vector into world space to see which one is "Up"
            Vector3 worldFaceDir = transform.TransformDirection(localFaceDirections[i]);
            float dot = Vector3.Dot(worldFaceDir, Vector3.up);

            if (dot > bestDot)
            {
                bestDot = dot;
                closestIndex = i;
            }
        }

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

        manager.OnDiePhysicsSettled(BatchIndex, resultFace, transform);
    }
}