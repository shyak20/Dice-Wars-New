using UnityEngine;

/// <summary>
/// Place on the enemy prefab (or its UI child). Spawns floating damage for <see cref="CombatEvents.OnEnemyDamagePresentation"/>.
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
    [Tooltip("Optional. When set with a prefab, burn damage uses this style; otherwise physical style is reused.")]
    [SerializeField] private FloatingDamageNumberStyle burnStyle = new FloatingDamageNumberStyle();

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
        CombatEvents.OnEnemyDamagePresentation += HandleEnemyDamage;
    }

    private void OnDisable()
    {
        CombatEvents.OnEnemyDamagePresentation -= HandleEnemyDamage;
    }

    private void HandleEnemyDamage(int amount, Vector3 worldPosition, EnemyController damagedEnemy, EnemyDamagePresentationKind kind)
    {
        if (_ownerEnemy == null || damagedEnemy != _ownerEnemy) return;
        var resolvedStyle = kind == EnemyDamagePresentationKind.Burn && burnStyle.prefab != null ? burnStyle : style;
        FloatingDamageNumberSpawner.Spawn(this, amount, worldPosition, canvas, spawnParent, worldCamera, resolvedStyle);
    }
}
