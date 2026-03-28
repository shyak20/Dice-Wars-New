using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [Header("Settings")]
    public float minIntensity = 0.5f;
    public float maxIntensity = 1.5f;

    [Tooltip("How many samples to average. Higher = smoother flicker, Lower = more jittery.")]
    [Range(1, 50)]
    public int smoothing = 5;

    private Light lightSource;
    private Queue<float> smoothQueue;
    private float lastSum = 0;

    private void Awake()
    {
        lightSource = GetComponent<Light>();
        smoothQueue = new Queue<float>(smoothing);
    }

    private void Update()
    {
        if (lightSource == null) return;

        // 1. Pop the oldest value if the queue is full
        while (smoothQueue.Count >= smoothing)
        {
            lastSum -= smoothQueue.Dequeue();
        }

        // 2. Generate a new random intensity
        float newVal = Random.Range(minIntensity, maxIntensity);

        // 3. Add to queue and update the sum
        smoothQueue.Enqueue(newVal);
        lastSum += newVal;

        // 4. Set the light intensity to the average of the queue
        lightSource.intensity = lastSum / smoothQueue.Count;
    }
}