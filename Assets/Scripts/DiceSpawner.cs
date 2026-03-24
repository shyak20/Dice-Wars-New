using TMPro;
using UnityEngine;

public class DiceSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject dicePrefab;
    public Transform spawnPoint;
    public TMP_Text resultText;

    [Header("Throw Force")]
    public float minForwardForce = 5f;
    public float maxForwardForce = 10f;

    public float minUpwardForce = 2f;
    public float maxUpwardForce = 5f;

    [Header("Random Side Force")]
    public float minSideForce = -2f;
    public float maxSideForce = 2f;

    [Header("Torque")]
    public float minTorque = 5f;
    public float maxTorque = 20f;

    [Header("Optional")]
    public bool destroyPreviousDice = false;

    private GameObject currentDice;

    public void SpawnAndRollDice()
    {
        if (dicePrefab == null || spawnPoint == null)
        {
            Debug.LogWarning("DiceSpawner: Missing dicePrefab or spawnPoint.");
            return;
        }

        if (destroyPreviousDice && currentDice != null)
        {
            Destroy(currentDice);
        }

        currentDice = Instantiate(dicePrefab, spawnPoint.position, spawnPoint.rotation);

        Rigidbody rb = currentDice.GetComponent<Rigidbody>();
        DiceRoller diceRoller = currentDice.GetComponent<DiceRoller>();

        if (rb == null)
        {
            Debug.LogWarning("Spawned dice has no Rigidbody.");
            return;
        }

        if (diceRoller == null)
        {
            Debug.LogWarning("Spawned dice has no DiceRoller script.");
            return;
        }

        if (resultText != null)
        {
            resultText.text = "Rolling...";
            diceRoller.resultText = resultText;
        }

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        float forwardForce = Random.Range(minForwardForce, maxForwardForce);
        float upwardForce = Random.Range(minUpwardForce, maxUpwardForce);
        float sideForce = Random.Range(minSideForce, maxSideForce);

        Vector3 throwDirection =
            spawnPoint.forward * forwardForce +
            spawnPoint.up * upwardForce +
            spawnPoint.right * sideForce;

        rb.AddForce(throwDirection, ForceMode.Impulse);

        Vector3 randomTorque = new Vector3(
            Random.Range(minTorque, maxTorque) * RandomSign(),
            Random.Range(minTorque, maxTorque) * RandomSign(),
            Random.Range(minTorque, maxTorque) * RandomSign()
        );

        rb.AddTorque(randomTorque, ForceMode.Impulse);

        diceRoller.StartCheckingResult();
    }

    private int RandomSign()
    {
        return Random.value < 0.5f ? -1 : 1;
    }
}