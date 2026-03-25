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

    // Track active dice models to destroy them later
    private List<GameObject> activeDiceModels = new List<GameObject>();

    public int LastBatchCount { get; private set; }

    /// <summary>
    /// Destroys all current dice on the table.
    /// </summary>
    public void ClearOldDice()
    {
        foreach (GameObject die in activeDiceModels)
        {
            if (die != null)
            {
                Destroy(die);
            }
        }
        activeDiceModels.Clear();
    }

    /// <summary>
    /// Clears the table and spawns a new batch.
    /// </summary>
    public void SpawnAndRollBatch(List<DieAssetSO> diceList)
    {
        // 1. Clean up before spawning new ones
        ClearOldDice();

        if (dicePrefab == null || spawnPoint == null)
        {
            UnityEngine.Debug.LogWarning("DiceSpawner: Missing dicePrefab or spawnPoint.");
            return;
        }

        LastBatchCount = diceList.Count;

        for (int i = 0; i < diceList.Count; i++)
        {
            Vector3 offset = new Vector3(i * 0.4f, 0, 0);

            // 2. Spawn the new die
            GameObject die = Instantiate(dicePrefab, spawnPoint.position + offset, UnityEngine.Random.rotation);

            // 3. Add to our tracking list
            activeDiceModels.Add(die);

            // Initialize visuals
            DieVisualizer visualizer = die.GetComponent<DieVisualizer>();
            if (visualizer != null) visualizer.Initialize(diceList[i]);

            // Apply physics
            Rigidbody rb = die.GetComponent<Rigidbody>();
            if (rb != null) ApplyForces(rb);

            // Start settling logic
            DiceRoller roller = die.GetComponent<DiceRoller>();
            if (roller != null) roller.StartCheckingResult();
        }
    }

    private void ApplyForces(Rigidbody rb)
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        float forwardForce = UnityEngine.Random.Range(minForwardForce, maxForwardForce);
        float upwardForce = UnityEngine.Random.Range(minUpwardForce, maxUpwardForce);

        Vector3 throwDirection = (spawnPoint.forward * forwardForce) + (spawnPoint.up * upwardForce);
        rb.AddForce(throwDirection, ForceMode.Impulse);

        Vector3 randomTorque = UnityEngine.Random.insideUnitSphere * UnityEngine.Random.Range(minTorque, maxTorque);
        rb.AddTorque(randomTorque, ForceMode.Impulse);
    }
}