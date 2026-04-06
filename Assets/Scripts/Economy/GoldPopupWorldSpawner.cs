using TMPro;
using UnityEngine;

/// <summary>Spawns a short-lived world-space gold amount label. Assign on <see cref="RunEconomyManager"/>.</summary>
public class GoldPopupWorldSpawner : MonoBehaviour
{
    [SerializeField] private GameObject popupPrefab;
    [SerializeField] private float lifetime = 1.6f;
    [SerializeField] private float floatSpeed = 1.2f;

    public void Spawn(int amount, Vector3 worldPosition)
    {
        if (popupPrefab == null)
        {
            Debug.LogWarning("GoldPopupWorldSpawner: popupPrefab not assigned.");
            return;
        }

        var go = Instantiate(popupPrefab, worldPosition + Vector3.up * 0.5f, Quaternion.identity);
        var tmp = go.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = $"+{amount} Gold";

        var floater = go.GetComponent<GoldPopupFloater>();
        if (floater != null)
            floater.Begin(lifetime, floatSpeed);
        else
            Destroy(go, lifetime);
    }
}
