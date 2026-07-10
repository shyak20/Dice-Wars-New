using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Forces UI graphics under a tooltip panel to render in front of 3D geometry while keeping the canvas's
/// Screen Space - Camera plane distance. Both the UI-Default and TMP SDF shaders declare
/// <c>ZTest [unity_GUIZTestMode]</c>; overriding that per-material property to
/// <see cref="CompareFunction.Always"/> makes the graphic ignore the scene depth buffer.
/// Materials are instanced (never the shared asset) so other UI is unaffected.
/// </summary>
public static class TooltipDepthOverride
{
    static readonly int GuiZTestModeProp = Shader.PropertyToID("unity_GUIZTestMode");
    const string InstancedMaterialSuffix = " (Tooltip Front)";

    public static void ForceRenderInFront(GameObject panelRoot)
    {
        if (panelRoot == null)
            return;

        var graphics = panelRoot.GetComponentsInChildren<Graphic>(true);
        ForceRenderInFront(graphics);
    }

    /// <summary>Re-applies the front-most ZTest to a cached graphics set (see <see cref="TooltipAlwaysInFront"/>).</summary>
    public static void ForceRenderInFront(Graphic[] graphics)
    {
        if (graphics == null)
            return;

        for (var i = 0; i < graphics.Length; i++)
            ForceGraphicInFront(graphics[i]);
    }

    static void ForceGraphicInFront(Graphic graphic)
    {
        if (graphic == null)
            return;

        if (graphic is TMP_Text tmp)
        {
            // fontMaterial getter returns a per-text-object material instance managed by TMP.
            var tmpMat = tmp.fontMaterial;
            if (tmpMat != null)
                tmpMat.SetInt(GuiZTestModeProp, (int)CompareFunction.Always);
            return;
        }

        var current = graphic.material;
        var isSharedDefault = current == null || current == graphic.defaultMaterial;
        var alreadyInstanced = current != null && current.name.EndsWith(InstancedMaterialSuffix, System.StringComparison.Ordinal);

        if (isSharedDefault || !alreadyInstanced)
        {
            var source = current != null ? current : graphic.defaultMaterial;
            if (source == null)
                return;

            var instance = new Material(source) { name = source.name + InstancedMaterialSuffix };
            instance.SetInt(GuiZTestModeProp, (int)CompareFunction.Always);
            graphic.material = instance;
            return;
        }

        current.SetInt(GuiZTestModeProp, (int)CompareFunction.Always);
    }
}
