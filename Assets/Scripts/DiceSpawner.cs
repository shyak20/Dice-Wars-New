using UnityEngine;
using System.Collections.Generic;

public class DiceSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject dicePrefab;
    public Transform spawnPoint;

    [Header("Throw Force")]
    public float minForwardForce = 5f;
    public float maxForwardForce = 10f;
    public float minUpwardForce = 2f;
    public float maxUpwardForce = 5f;

    [Header("Torque Settings")]
    public float minTorque = 5f;
    public float maxTorque = 20f;

    public int LastBatchCount { get; private set; }

    /// <summary>
    /// Spawns all selected dice from the UI and launches them using physics.
    /// </summary>
    public void SpawnAndRollBatch(List<DieAssetSO> diceList)
    {
        if (dicePrefab == null || spawnPoint == null)
        {
            UnityEngine.Debug.LogWarning("DiceSpawner: Missing dicePrefab or spawnPoint.");
            return;
        }

        LastBatchCount = diceList.Count;

        for (int i = 0; i < diceList.Count; i++)
        {
            // Offset each die slightly so they don't spawn inside each other
            Vector3 offset = new Vector3(i * 0.4f, 0, 0);

            // Use UnityEngine.Random for rotation
            GameObject die = Instantiate(dicePrefab, spawnPoint.position + offset, UnityEngine.Random.rotation);

            // Initialize the visuals (materials) and data
            DieVisualizer visualizer = die.GetComponent<DieVisualizer>();
            if (visualizer != null)
            {
                visualizer.Initialize(diceList[i]);
            }

            // Apply physics forces
            Rigidbody rb = die.GetComponent<Rigidbody>();
            if (rb != null)
            {
                ApplyForces(rb);
            }

            // Start the "Settle Check" logic
            DiceRoller roller = die.GetComponent<DiceRoller>();
            if (roller != null)
            {
                roller.StartCheckingResult();
            }
        }
    }

    private void ApplyForces(Rigidbody rb)
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Use UnityEngine.Random for Ranges
        float forwardForce = UnityEngine.Random.Range(minForwardForce, maxForwardForce);
        float upwardForce = UnityEngine.Random.Range(minUpwardForce, maxUpwardForce);

        Vector3 throwDirection = (spawnPoint.forward * forwardForce) + (spawnPoint.up * upwardForce);

        rb.AddForce(throwDirection, ForceMode.Impulse);

        // Use UnityEngine.Random for torque
        Vector3 randomTorque = UnityEngine.Random.insideUnitSphere * UnityEngine.Random.Range(minTorque, maxTorque);
        rb.AddTorque(randomTorque, ForceMode.Impulse);
    }
}