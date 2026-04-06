using UnityEngine;

/// <summary>
/// Place on the enemy prefab (or its UI child). Spawns floating damage for <see cref="CombatEvents.OnEnemyDamageNumber"/>.
/// Assign the enemy's damage-number canvas and parent rect (can be scene objects outside the prefab).
/// </summary>
public class EnemyFloatingDamageNumberController : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform spawnParent;
    [SerializeField] private Camera worldCamera;

    [Header("Style")]
    [SerializeField] private FloatingDamageNumberStyle style = new FloatingDamageNumberStyle();

    private EnemyController _ownerEnemy;

    private void Awake()
    {
        _ownerEnemy = GetComponentInParent<EnemyController>();
        if (_ownerEnemy == null)
            Debug.LogError($"EnemyFloatingDamageNumberController on '{gameObject.name}': must live under a GameObject with EnemyController (use GetComponentInParent).");

        if (canvas == null)
            Debug.LogError($"EnemyFloatingDamageNumberController on '{gameObject.name}': canvas is not assigned.");
        if (spawnParent == null)
            Debug.LogError($"EnemyFloatingDamageNumberController on '{gameObject.name}': spawnParent is not assigned.");
        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    private void OnEnable()
    {
        CombatEvents.OnEnemyDamageNumber += HandleEnemyDamage;
    }

    private void OnDisable()
    {
        CombatEvents.OnEnemyDamageNumber -= HandleEnemyDamage;
    }

    private void HandleEnemyDamage(int amount, Vector3 worldPosition, EnemyController damagedEnemy)
    {
        if (_ownerEnemy == null || damagedEnemy != _ownerEnemy) return;
        FloatingDamageNumberSpawner.Spawn(this, amount, worldPosition, canvas, spawnParent, worldCamera, style);
    }
}
