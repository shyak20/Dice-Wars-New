using UnityEngine;

/// <summary>
/// Combat juice when the player takes damage via <see cref="PlayerStatus.TakeDamage"/> or poison via
/// <see cref="PlayerStatus.TakeTrueDamage"/>.
/// </summary>
public sealed class PlayerPhysicalHitFeedback : DamageHitFeedbackBase
{
    [Header("References")]
    [Tooltip("Defaults to a PlayerStatus on this object or a parent.")]
    [SerializeField] private PlayerStatus playerStatus;

    protected override void Awake()
    {
        base.Awake();

        if (playerStatus == null)
            playerStatus = GetComponentInParent<PlayerStatus>();
        if (playerStatus == null)
            Debug.LogError("PlayerPhysicalHitFeedback: assign Player Status (or parent a PlayerStatus).", this);
    }

    /// <summary>Call after armor and HP resolve for standard player damage.</summary>
    public void OnPlayerDamaged(int grossDamage, int hpLost, int maxHp) => PlayHit(grossDamage, hpLost, maxHp);

    /// <summary>Call after HP resolves for poison and other armor-bypassing damage.</summary>
    public void OnPlayerPoisonDamaged(int grossDamage, int hpLost, int maxHp) => PlayPoisonHit(grossDamage, hpLost, maxHp);
}
