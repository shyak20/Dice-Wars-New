using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>Lightweight toast for shop (e.g. new die acquired). Falls back to Debug.Log if no instance.</summary>
public class ShopToastUI : MonoBehaviour
{
    public static ShopToastUI Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private float visibleSeconds = 2.2f;

    private Coroutine _routine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void Show(string message)
    {
        if (Instance != null)
            Instance.ShowInternal(message);
        else
            Debug.Log("[Shop] " + message);
    }

    private void ShowInternal(string message)
    {
        if (messageText != null) messageText.text = message;
        if (panel != null) panel.SetActive(true);

        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(visibleSeconds);
        if (panel != null) panel.SetActive(false);
        _routine = null;
    }
}
