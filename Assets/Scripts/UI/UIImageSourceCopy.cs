using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Copies the Source Image (<see cref="Image.sprite"/>) from one UI <see cref="Image"/> to another.
/// </summary>
public sealed class UIImageSourceCopy : MonoBehaviour
{
    [SerializeField] private Image sourceImage;
    [SerializeField] private Image targetImage;
    [SerializeField] private bool copyOnEnable;

    void OnEnable()
    {
        if (copyOnEnable)
            CopySourceImage();
    }

    [ContextMenu("Copy Source Image")]
    public void CopySourceImage()
    {
        CopySourceImage(sourceImage, targetImage);
    }

    public static void CopySourceImage(Image source, Image target)
    {
        if (source == null)
            throw new System.ArgumentNullException(nameof(source));
        if (target == null)
            throw new System.ArgumentNullException(nameof(target));

        target.sprite = source.sprite;
    }
}
