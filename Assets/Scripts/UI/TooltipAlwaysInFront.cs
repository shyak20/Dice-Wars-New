using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps a tooltip panel rendering in front of 3D geometry for as long as it is active.
/// <see cref="HoverTooltipManager"/> positions the panel once when the pointer enters and never moves it again,
/// so depth (not placement) is all that needs ongoing maintenance: Unity can reset the per-material GUI ZTest
/// mode when a canvas rebuilds, so this re-applies <see cref="TooltipDepthOverride"/> every LateUpdate.
/// </summary>
[DisallowMultipleComponent]
public sealed class TooltipAlwaysInFront : MonoBehaviour
{
    Graphic[] _graphics;

    void OnEnable() => Apply(refresh: true);

    void LateUpdate() => Apply(refresh: _graphics == null);

    void Apply(bool refresh)
    {
        if (refresh)
            _graphics = GetComponentsInChildren<Graphic>(true);
        TooltipDepthOverride.ForceRenderInFront(_graphics);
    }
}
