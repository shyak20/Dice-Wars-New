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

    [Header("Destroy VFX (one per die face, by face type)")]
    public GameObject damageDestroyEffect;
    public GameObject armorDestroyEffect;
    public GameObject fireDestroyEffect;
    public GameObject iceDestroyEffect;
    public GameObject natureDestroyEffect;
    public GameObject curseDestroyEffect;
    public float effectLifetime = 2.0f;

    [Header("Throw Force")]
    public float minForwardForce = 15f;
    public float maxForwardForce = 25f;
    public float minUpwardForce = 5f;
    public float maxUpwardForce = 10f;

    [Header("Rotation Settings")]
    public float minTorque = 10f;
    public float maxTorque = 30f;

    private readonly List<GameObject> activeDiceModels = new List<GameObject>();
    private Coroutine _spawnRoutine;

    /// <summary>Copy of spawned dice for this batch (reroll UI / physics).</summary>
    public List<GameObject> GetActiveDiceSnapshot() => new List<GameObject>(activeDiceModels);

    /// <summary>Physical instance for this batch index (same order as <see cref="DiceRoller.BatchIndex"/>).</summary>
    public GameObject GetActiveDieGameObject(int batchIndex)
    {
        if (batchIndex < 0 || batchIndex >= activeDiceModels.Count)
            return null;
        return activeDiceModels[batchIndex];
    }

    /// <summary>Spawn order index for the active batch (matches <see cref="DiceRoller.BatchIndex"/>).</summary>
    public int GetIndexOfActiveDie(GameObject die)
    {
        if (die == null) return -1;
        for (var i = 0; i < activeDiceModels.Count; i++)
            if (activeDiceModels[i] == die)
                return i;
        return -1;
    }

    /// <summary>Apply a fresh throw and wait for settlement again (same die instance).</summary>
    public void RerollDiePhysics(GameObject die)
    {
        if (die == null) return;
        var rb = die.GetComponent<Rigidbody>();
        var roller = die.GetComponent<DiceRoller>();
        if (rb != null) ApplyForces(rb);
        if (roller != null) roller.StartCheckingResult();
    }

    public void ClearOldDice()
    {
        foreach (GameObject die in activeDiceModels)
        {
            if (die != null)
                StartCoroutine(CoDissolveAndDestroyDie(die));
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
                if (roller != null)
                {
                    roller.BatchIndex = i;
                    roller.StartCheckingResult();
                }

                if (i < diceList.Count - 1 && delayBetweenDice > 0f)
                    yield return new WaitForSeconds(delayBetweenDice);
            }
        }
        finally
        {
            _spawnRoutine = null;
        }
    }

    private IEnumerator CoDissolveAndDestroyDie(GameObject die)
    {
        var visualizer = die.GetComponent<DieVisualizer>();
        var roller = die.GetComponent<DiceRoller>();
        if (roller != null)
            roller.enabled = false;

        var rb = die.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        var boxCollider = die.GetComponent<BoxCollider>();
        if (boxCollider != null)
            boxCollider.enabled = false;

        var dissolveFade = die.GetComponent<DissolveFadeController>();
        if (dissolveFade == null)
        {
            Debug.LogError(
                $"DiceSpawner: die prefab '{dicePrefab.name}' needs a disabled {nameof(DissolveFadeController)} on the root.",
                dicePrefab);
            Destroy(die);
            yield break;
        }

        dissolveFade.enabled = true;
        dissolveFade.RefreshRenderers();
        dissolveFade.ShowImmediate();
        dissolveFade.FadeDurationSeconds = effectLifetime;
        dissolveFade.FadeOut();

        SpawnDestroyEffectsForAllFaces(die, visualizer);

        yield return new WaitForSeconds(effectLifetime);

        if (die != null)
            Destroy(die);
    }

    void SpawnDestroyEffectsForAllFaces(GameObject die, DieVisualizer visualizer)
    {
        var meshRenderer = visualizer != null ? visualizer.meshRenderer : null;
        var faces = visualizer?.dieData?.faces;
        var fallbackType = visualizer?.dieData != null ? visualizer.dieData.dieType : DieType.Damage;

        for (var faceIndex = 0; faceIndex < DieFaceTopology.FaceCount; faceIndex++)
        {
            DieFaceSO face = faces != null && faceIndex < faces.Length ? faces[faceIndex] : null;
            var faceType = face != null ? face.type : fallbackType;
            var effectPrefab = GetDestroyEffectPrefab(faceType);
            if (effectPrefab == null)
                continue;

            var effectPos = DieFaceTopology.GetFaceWorldPosition(die.transform, meshRenderer, faceIndex);
            var effectInstance = Instantiate(effectPrefab);
            effectInstance.transform.position = effectPos;
            Destroy(effectInstance, effectLifetime);
        }
    }

    private void ApplyForces(Rigidbody rb)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        float forwardForce = Random.Range(minForwardForce, maxForwardForce);
        float upwardForce = Random.Range(minUpwardForce, maxUpwardForce);
        Vector3 throwDirection = (spawnPoint.forward * forwardForce) + (spawnPoint.up * upwardForce);
        rb.AddForce(throwDirection, ForceMode.Impulse);
        float torqueMagnitude = Random.Range(minTorque, maxTorque);
        rb.AddTorque(Random.insideUnitSphere * torqueMagnitude, ForceMode.Impulse);
    }

    private GameObject GetDestroyEffectPrefab(DieType faceType)
    {
        return faceType switch
        {
            DieType.Damage => damageDestroyEffect,
            DieType.Armor => armorDestroyEffect,
            DieType.Fire => fireDestroyEffect != null ? fireDestroyEffect : damageDestroyEffect,
            DieType.Ice => iceDestroyEffect != null ? iceDestroyEffect : armorDestroyEffect,
            DieType.Nature => natureDestroyEffect != null ? natureDestroyEffect : armorDestroyEffect,
            DieType.Curse => curseDestroyEffect,
            _ => null
        };
    }
}
