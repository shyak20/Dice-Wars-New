using UnityEngine;
using System.Collections.Generic;

public class DiceSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject dicePrefab;
    public Transform spawnPoint;

    [Header("Visual Effects")]
    public GameObject attackDestroyEffect;   // Prefab for Attack dice
    public GameObject defenseDestroyEffect;  // Prefab for Defense dice
    public float effectLifetime = 2.0f;      // How long the effect stays alive (X seconds)

    [Header("Throw Force")]
    public float minForwardForce = 15f;
    public float maxForwardForce = 25f;
    public float minUpwardForce = 5f;
    public float maxUpwardForce = 10f;

    [Header("Rotation Settings")]
    public float minTorque = 10f;
    public float maxTorque = 30f;

    private List<GameObject> activeDiceModels = new List<GameObject>();

    /// <summary>
    /// Destroys all current dice and spawns the appropriate "Death" VFX.
    /// </summary>
    public void ClearOldDice()
    {
        foreach (GameObject die in activeDiceModels)
        {
            if (die != null)
            {
                // 1. Get the die's data through its visualizer
                DieVisualizer visualizer = die.GetComponent<DieVisualizer>();
                if (visualizer != null && visualizer.dieData != null)
                {
                    // 2. Pick the correct effect
                    GameObject effectPrefab = (visualizer.dieData.dieType == DieType.Shadow)
                        ? attackDestroyEffect
                        : defenseDestroyEffect;

                    // 3. Spawn the effect at the die's current position/rotation
                    if (effectPrefab != null)
                    {
                        GameObject effectInstance = Instantiate(effectPrefab, die.transform.position, die.transform.rotation);

                        // 4. Destroy the effect after X seconds
                        Destroy(effectInstance, effectLifetime);
                    }
                }

                // 5. Finally, destroy the die model
                Destroy(die);
            }
        }
        activeDiceModels.Clear();
    }

    public void SpawnAndRollBatch(List<DieAssetSO> diceList)
    {
        // Cleanup happens here at the start of a new roll
        ClearOldDice();

        if (dicePrefab == null || spawnPoint == null) return;

        for (int i = 0; i < diceList.Count; i++)
        {
            Vector3 offset = new Vector3(i * 0.4f, 0, 0);
            GameObject die = Instantiate(dicePrefab, spawnPoint.position + offset, Random.rotation);
            activeDiceModels.Add(die);

            DieVisualizer visualizer = die.GetComponent<DieVisualizer>();
            if (visualizer != null) visualizer.Initialize(diceList[i]);

            Rigidbody rb = die.GetComponent<Rigidbody>();
            if (rb != null) ApplyForces(rb);

            DiceRoller roller = die.GetComponent<DiceRoller>();
            if (roller != null) roller.StartCheckingResult();
        }
    }

    private void ApplyForces(Rigidbody rb)
    {
        // Reset velocities to ensure a consistent throw every time
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Calculate Linear Force
        float forwardForce = Random.Range(minForwardForce, maxForwardForce);
        float upwardForce = Random.Range(minUpwardForce, maxUpwardForce);
        Vector3 throwDirection = (spawnPoint.forward * forwardForce) + (spawnPoint.up * upwardForce);

        // Apply Linear Force
        rb.AddForce(throwDirection, ForceMode.Impulse);

        // Calculate and apply Torque (Spin)
        float torqueMagnitude = Random.Range(minTorque, maxTorque);
        rb.AddTorque(Random.insideUnitSphere * torqueMagnitude, ForceMode.Impulse);
    }
}