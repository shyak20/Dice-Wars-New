using UnityEngine;

/// <summary>
/// One roll-charge pip: shows either Indicator On or Indicator Off.
/// </summary>
public sealed class RollChargeIndicatorView : MonoBehaviour
{
    [SerializeField] private GameObject indicatorOn;
    [SerializeField] private GameObject indicatorOff;

    void Awake() => ValidateRefs();

#if UNITY_EDITOR
    void OnValidate() => ValidateRefs();
#endif

    void ValidateRefs()
    {
        if (indicatorOn == null)
            Debug.LogError($"{nameof(RollChargeIndicatorView)} on '{name}': assign Indicator On.", this);
        if (indicatorOff == null)
            Debug.LogError($"{nameof(RollChargeIndicatorView)} on '{name}': assign Indicator Off.", this);
    }

    public void SetCharged(bool charged)
    {
        if (indicatorOn != null)
            indicatorOn.SetActive(charged);
        if (indicatorOff != null)
            indicatorOff.SetActive(!charged);
    }
}
