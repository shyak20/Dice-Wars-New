using UnityEngine;

/// <summary>
/// Place on the player UI canvas (or a child). Spawns floating damage for <see cref="CombatEvents.OnPlayerDamageNumber"/>.
/// </summary>
public class PlayerFloatingDamageNumberController : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform spawnParent;
    [SerializeField] private Camera worldCamera;

    [Header("Style")]
    [SerializeField] private FloatingDamageNumberStyle style = new FloatingDamageNumberStyle();

    [Tooltip("When enabled, spawns as a child at the spawn parent’s local center (0,0,0) with anchoredPosition 0,0. Turn on for map/HUD; leave off for 3D world-anchored combat popups.")]
    [SerializeField] private bool spawnAtSpawnParentCenter;

    private void Awake()
    {
        ResolveReferencesIfNeeded();

        if (canvas == null)
            Debug.LogError($"PlayerFloatingDamageNumberController on '{gameObject.name}': canvas is not assigned.", this);
        if (spawnParent == null)
            Debug.LogError($"PlayerFloatingDamageNumberController on '{gameObject.name}': spawnParent is not assigned.", this);
        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    void ResolveReferencesIfNeeded()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (spawnParent == null)
        {
            foreach (var rt in GetComponentsInChildren<RectTransform>(true))
            {
                if (rt.name == "Text Flying Position")
                {
                    spawnParent = rt;
                    break;
                }
            }
        }
    }

    private void OnEnable()
    {
        CombatEvents.OnPlayerDamageNumber += HandlePlayerDamage;
    }

    private void OnDisable()
    {
        CombatEvents.OnPlayerDamageNumber -= HandlePlayerDamage;
    }

    private void HandlePlayerDamage(int amount, Vector3 worldPosition)
    {
        FloatingDamageNumberSpawner.Spawn(this, amount, worldPosition, canvas, spawnParent, worldCamera, style,
            spawnAtSpawnParentCenter);
    }
}
