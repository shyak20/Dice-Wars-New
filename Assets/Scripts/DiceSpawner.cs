using UnityEngine;
using System.Collections.Generic;

public class DiceSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject dicePrefab;
    public Transform spawnPoint;

    [Header("Visual Effects")]
    public GameObject damageDestroyEffect;  // Renamed from attack
    public GameObject armorDestroyEffect;   // Renamed from defense
    public float effectLifetime = 2.0f;

    [Header("Throw Force")]
    public float minForwardForce = 15f;
    public float maxForwardForce = 25f;
    public float minUpwardForce = 5f;
    public float maxUpwardForce = 10f;

    [Header("Rotation Settings")]
    public float minTorque = 10f;
    public float maxTorque = 30f;

    private List<GameObject> activeDiceModels = new List<GameObject>();

    public void ClearOldDice()
    {
        foreach (GameObject die in activeDiceModels)
        {
            if (die != null)
            {
                DieVisualizer visualizer = die.GetComponent<DieVisualizer>();
                if (visualizer != null && visualizer.dieData != null)
                {
                    // Logic updated for renamed types
                    GameObject effectPrefab = (visualizer.dieData.dieType == DieType.Damage)
                        ? damageDestroyEffect
                        : armorDestroyEffect;

                    if (effectPrefab != null)
                    {
                        GameObject effectInstance = Instantiate(effectPrefab, die.transform.position, die.transform.rotation);
                        Destroy(effectInstance, effectLifetime);
                    }
                }
                Destroy(die);
            }
        }
        activeDiceModels.Clear();
    }

    public void SpawnAndRollBatch(List<DieAssetSO> diceList)
    {
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
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        float forwardForce = Random.Range(minForwardForce, maxForwardForce);
        float upwardForce = Random.Range(minUpwardForce, maxUpwardForce);
        Vector3 throwDirection = (spawnPoint.forward * forwardForce) + (spawnPoint.up * upwardForce);
        rb.AddForce(throwDirection, ForceMode.Impulse);
        float torqueMagnitude = Random.Range(minTorque, maxTorque);
        rb.AddTorque(Random.insideUnitSphere * torqueMagnitude, ForceMode.Impulse);
    }
}