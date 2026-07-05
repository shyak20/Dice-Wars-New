using UnityEngine;

/// <summary>
/// Binds a <see cref="Sprite"/> from <see cref="GameIconIndexSO"/> onto a RealToon icon material instance.
/// </summary>
public static class DieFaceEffectIconMaterialUtility
{
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int MainColorId = Shader.PropertyToID("_MainColor");

    public static void ApplySprite(Material material, Sprite sprite)
    {
        if (material == null)
            throw new System.ArgumentNullException(nameof(material));
        if (sprite == null)
            throw new System.ArgumentNullException(nameof(sprite));

        var texture = sprite.texture;
        if (material.HasProperty(MainTexId))
            material.SetTexture(MainTexId, texture);

        material.mainTexture = texture;

        var rect = sprite.textureRect;
        var scale = new Vector2(rect.width / texture.width, rect.height / texture.height);
        var offset = new Vector2(rect.x / texture.width, rect.y / texture.height);
        if (material.HasProperty(MainTexStId))
            material.SetVector(MainTexStId, new Vector4(scale.x, scale.y, offset.x, offset.y));

        if (material.HasProperty(ColorId))
            material.SetColor(ColorId, Color.white);
        if (material.HasProperty(MainColorId))
            material.SetColor(MainColorId, Color.white);
    }
}
