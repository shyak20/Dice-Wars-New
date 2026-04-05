using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class DiceSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject dicePrefab;
    public Transform spawnPoint;

    [Header("Spawn area (spawn point local axes: width = right, height = up)")]
    [Tooltip("Full width along the spawn transform's right axis (0 = no spread).")]
    [SerializeField] private float spawnAreaWidth = 0.6f;
    [Tooltip("Full height along the spawn transform's up axis (0 = no vertical spread).")]
    [SerializeField] private float spawnAreaHeight = 0.2f;
    [Tooltip("Seconds to wait after each die before spawning the next (reduces overlapping impulses).")]
    [SerializeField] private float delayBetweenDice = 0.12f;

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
    private Coroutine _spawnRoutine;

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
        if (_spawnRoutine != null)
            StopCoroutine(_spawnRoutine);
        _spawnRoutine = StartCoroutine(SpawnAndRollBatchRoutine(diceList));
    }

    private IEnumerator SpawnAndRollBatchRoutine(List<DieAssetSO> diceList)
    {
        try
        {
            ClearOldDice();
            if (dicePrefab == null || spawnPoint == null || diceList == null) yield break;

            float halfW = Mathf.Max(0f, spawnAreaWidth) * 0.5f;
            float halfH = Mathf.Max(0f, spawnAreaHeight) * 0.5f;

            for (int i = 0; i < diceList.Count; i++)
            {
                Vector3 lateral = spawnPoint.right * Random.Range(-halfW, halfW);
                Vector3 vertical = spawnPoint.up * Random.Range(-halfH, halfH);
                Vector3 pos = spawnPoint.position + lateral + vertical;

                GameObject die = Instantiate(dicePrefab, pos, Random.rotation);
                activeDiceModels.Add(die);
                DieVisualizer visualizer = die.GetComponent<DieVisualizer>();
                if (visualizer != null) visualizer.Initialize(diceList[i]);
                Rigidbody rb = die.GetComponent<Rigidbody>();
                if (rb != null) ApplyForces(rb);
                DiceRoller roller = die.GetComponent<DiceRoller>();
                if (roller != null) roller.StartCheckingResult();

                if (i < diceList.Count - 1 && delayBetweenDice > 0f)
                    yield return new WaitForSeconds(delayBetweenDice);
            }
        }
        finally
        {
            _spawnRoutine = null;
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