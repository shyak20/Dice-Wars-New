using TMPro;
using UnityEngine;

/// <summary>
/// Put this on a GameObject with <see cref="TMP_Text"/>; each frame it copies <see cref="source"/>'s string onto this text.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class TextMeshProTextFollow : MonoBehaviour
{
    [SerializeField] private TMP_Text source;

    private TMP_Text _target;

    private void Awake()
    {
        _target = GetComponent<TMP_Text>();
        if (source == null)
            throw new System.InvalidOperationException(
                $"{nameof(TextMeshProTextFollow)} on '{name}': assign {nameof(source)} (the TextMesh Pro to mirror).");
    }

    private void LateUpdate()
    {
        if (source == null)
            return;

        var s = source.text;
        if (_target.text != s)
            _target.text = s;
    }
}
