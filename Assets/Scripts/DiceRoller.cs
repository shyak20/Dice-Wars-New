using UnityEngine;

public class DiceRoller : MonoBehaviour
{
    [Header("Detection Settings")]
    public float velocityThreshold = 0.2f;
    public float settleTime = 0.3f;

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

        if (rb.velocity.magnitude < velocityThreshold && rb.angularVelocity.magnitude < velocityThreshold)
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

        if (visualizer != null && visualizer.dieData != null)
        {
            // Pick the SO from the matching index in our faces array
            DieFaceSO resultFace = visualizer.dieData.faces[closestIndex];
            manager.ResolveRollResult(resultFace);
        }
    }
}