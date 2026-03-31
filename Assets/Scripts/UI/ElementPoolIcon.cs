using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ElementPoolIcon : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text valueText;

    public void SetValue(int value)
    {
        valueText.text = value.ToString();
    }

    private void Awake()
    {
        if (icon == null)
            Debug.LogError($"ElementPoolIcon on '{gameObject.name}': icon Image is not assigned!");
        if (valueText == null)
            Debug.LogError($"ElementPoolIcon on '{gameObject.name}': valueText is not assigned!");
    }
}
