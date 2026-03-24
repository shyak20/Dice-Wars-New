using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimulationSpeedController : MonoBehaviour
{
    [Header("UI")]
    public Slider speedSlider;
    public TMP_Text speedLabel;

    [Header("Speed Settings")]
    public float minSpeed = 0.2f;
    public float maxSpeed = 4f;
    public float defaultSpeed = 1f;

    private float baseFixedDeltaTime;

    private void Awake()
    {
        baseFixedDeltaTime = 0.02f;
    }

    private void Start()
    {
        if (speedSlider != null)
        {
            speedSlider.minValue = minSpeed;
            speedSlider.maxValue = maxSpeed;
            speedSlider.value = defaultSpeed;
        }

        SetSimulationSpeed(defaultSpeed);
    }

    public void SetSimulationSpeed(float speed)
    {
        speed = Mathf.Clamp(speed, minSpeed, maxSpeed);

        Time.timeScale = speed;
        Time.fixedDeltaTime = baseFixedDeltaTime * speed;

        if (speedLabel != null)
        {
            speedLabel.text = "Speed: " + speed.ToString("0.0") + "x";
        }
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDeltaTime;
    }
}