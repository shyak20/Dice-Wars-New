using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>How a tutorial sorting target is found at runtime.</summary>
public enum FightTutorialSortingTargetKind
{
    /// <summary>Use the assigned <see cref="FightTutorialSortingTarget.target"/> scene reference.</summary>
    ExplicitGameObject = 0,

    /// <summary>
    /// Resolve the die button at <see cref="FightTutorialSortingTarget.diceTrayIndex"/> in the fight dice tray
    /// (0 = first die in deck order). Built dynamically by <see cref="CombatUIController"/>.
    /// </summary>
    DiceTrayIndex = 1,
}

/// <summary>
/// One GameObject to temporarily raise above all fight UI for a tutorial phase.
/// UI targets are reparented under a Screen Space Overlay canvas; world sprites bump sorting order.
/// </summary>
[Serializable]
public sealed class FightTutorialSortingTarget
{
    [Tooltip("Explicit scene object, or Dice Tray Index for a runtime-spawned tray die.")]
    public FightTutorialSortingTargetKind kind = FightTutorialSortingTargetKind.ExplicitGameObject;

    [Tooltip("Used when Kind is Explicit Game Object.")]
    public GameObject target;

    [Tooltip("0-based index into the fight dice tray (deck order). Used when Kind is Dice Tray Index.")]
    public int diceTrayIndex;

    [Tooltip(
        "UI: draw priority among highlights on the Overlay (higher draws later / on top). " +
        "SpriteRenderer: temporary sortingOrder while the phase is active.")]
    public int orderInLayer;

    public void Validate(string ownerLabel, int index)
    {
        switch (kind)
        {
            case FightTutorialSortingTargetKind.ExplicitGameObject:
                if (target == null)
                    Debug.LogError($"{ownerLabel} sortingTargets[{index}]: assign target GameObject.");
                break;
            case FightTutorialSortingTargetKind.DiceTrayIndex:
                if (diceTrayIndex < 0)
                    Debug.LogError($"{ownerLabel} sortingTargets[{index}]: diceTrayIndex must be >= 0.");
                break;
            default:
                Debug.LogError($"{ownerLabel} sortingTargets[{index}]: unknown kind {kind}.");
                break;
        }
    }
}

/// <summary>
/// Temporarily lifts tutorial highlight targets above every Screen Space Camera canvas by reparenting
/// UI under a Screen Space Overlay host, then restores hierarchy when the phase ends.
/// </summary>
public sealed class FightTutorialSortingSession
{
    readonly List<Entry> _entries = new List<Entry>();
    readonly List<(GameObject Target, int Order, int Index)> _uiSortBuffer =
        new List<(GameObject, int, int)>();

    /// <param name="resolveTarget">Resolves each spec to a live GameObject (scene ref or runtime tray die).</param>
    public void Apply(
        IReadOnlyList<FightTutorialSortingTarget> targets,
        Canvas overlayCanvas,
        Func<FightTutorialSortingTarget, GameObject> resolveTarget)
    {
        Restore();
        if (targets == null || targets.Count == 0)
            return;

        if (overlayCanvas == null)
        {
            Debug.LogError($"{nameof(FightTutorialSortingSession)}: overlayCanvas is null.");
            return;
        }

        if (resolveTarget == null)
        {
            Debug.LogError($"{nameof(FightTutorialSortingSession)}: resolveTarget is null.");
            return;
        }

        var overlayRect = overlayCanvas.transform as RectTransform;
        if (overlayRect == null)
        {
            Debug.LogError(
                $"{nameof(FightTutorialSortingSession)}: overlayCanvas '{overlayCanvas.name}' has no RectTransform.",
                overlayCanvas);
            return;
        }

        _uiSortBuffer.Clear();

        for (var i = 0; i < targets.Count; i++)
        {
            var spec = targets[i];
            if (spec == null)
            {
                Debug.LogError($"{nameof(FightTutorialSortingSession)}: sortingTargets[{i}] is null.");
                continue;
            }

            var resolved = resolveTarget(spec);
            if (resolved == null)
            {
                Debug.LogError(
                    $"{nameof(FightTutorialSortingSession)}: sortingTargets[{i}] ({spec.kind}) could not be resolved.");
                continue;
            }

            var rect = resolved.transform as RectTransform;
            if (rect != null)
            {
                _uiSortBuffer.Add((resolved, spec.orderInLayer, i));
                continue;
            }

            var sprite = resolved.GetComponent<SpriteRenderer>();
            if (sprite != null)
            {
                _entries.Add(Entry.ForSprite(sprite, sprite.sortingOrder));
                sprite.sortingOrder = spec.orderInLayer;
                continue;
            }

            Debug.LogError(
                $"{nameof(FightTutorialSortingSession)}: '{resolved.name}' has neither RectTransform nor SpriteRenderer.",
                resolved);
        }

        _uiSortBuffer.Sort((a, b) =>
        {
            var cmp = a.Order.CompareTo(b.Order);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });

        for (var i = 0; i < _uiSortBuffer.Count; i++)
            ReparentUiToOverlay(_uiSortBuffer[i].Target.transform as RectTransform, overlayRect);
    }

    public void Restore()
    {
        for (var i = _entries.Count - 1; i >= 0; i--)
            _entries[i].Restore();
        _entries.Clear();
    }

    void ReparentUiToOverlay(RectTransform rect, RectTransform overlayRect)
    {
        if (rect == null || overlayRect == null)
            return;

        var sourceCanvas = rect.GetComponentInParent<Canvas>();
        var sourceCam = ResolveCanvasEventCamera(sourceCanvas);

        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var screenBl = RectTransformUtility.WorldToScreenPoint(sourceCam, corners[0]);
        var screenTr = RectTransformUtility.WorldToScreenPoint(sourceCam, corners[2]);

        var originalParent = rect.parent as RectTransform;
        var siblingIndex = rect.GetSiblingIndex();
        GameObject placeholder = null;
        if (originalParent != null)
            placeholder = CreateLayoutPlaceholder(rect, originalParent, siblingIndex);

        var entry = Entry.ForUiReparent(rect, originalParent, siblingIndex, placeholder);
        _entries.Add(entry);

        rect.SetParent(overlayRect, false);
        rect.SetAsLastSibling();

        var overlayCam = ResolveCanvasEventCamera(overlayRect.GetComponent<Canvas>());
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                overlayRect, screenBl, overlayCam, out var localBl) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                overlayRect, screenTr, overlayCam, out var localTr))
        {
            Debug.LogError(
                $"{nameof(FightTutorialSortingSession)}: failed to map '{rect.name}' onto the tutorial Overlay.",
                rect);
            return;
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.anchoredPosition = (localBl + localTr) * 0.5f;
        rect.sizeDelta = new Vector2(Mathf.Abs(localTr.x - localBl.x), Mathf.Abs(localTr.y - localBl.y));
    }

    /// <summary>
    /// Empty slot left in the original layout (e.g. dice tray) so siblings do not shift when the target is lifted.
    /// </summary>
    static GameObject CreateLayoutPlaceholder(RectTransform source, RectTransform parent, int siblingIndex)
    {
        var go = new GameObject($"{source.name} (Tutorial Slot)", typeof(RectTransform));
        var placeholder = go.GetComponent<RectTransform>();
        placeholder.SetParent(parent, false);
        placeholder.SetSiblingIndex(siblingIndex);

        placeholder.anchorMin = source.anchorMin;
        placeholder.anchorMax = source.anchorMax;
        placeholder.pivot = source.pivot;
        placeholder.sizeDelta = source.sizeDelta;
        placeholder.anchoredPosition = source.anchoredPosition;
        placeholder.localScale = source.localScale;
        placeholder.localRotation = source.localRotation;

        var layoutSize = source.rect.size;
        var sourceLayout = source.GetComponent<LayoutElement>();
        var placeholderLayout = go.AddComponent<LayoutElement>();
        if (sourceLayout != null)
        {
            placeholderLayout.minWidth = sourceLayout.minWidth;
            placeholderLayout.minHeight = sourceLayout.minHeight;
            placeholderLayout.preferredWidth = sourceLayout.preferredWidth >= 0f
                ? sourceLayout.preferredWidth
                : layoutSize.x;
            placeholderLayout.preferredHeight = sourceLayout.preferredHeight >= 0f
                ? sourceLayout.preferredHeight
                : layoutSize.y;
            placeholderLayout.flexibleWidth = sourceLayout.flexibleWidth;
            placeholderLayout.flexibleHeight = sourceLayout.flexibleHeight;
            placeholderLayout.ignoreLayout = sourceLayout.ignoreLayout;
        }
        else
        {
            placeholderLayout.preferredWidth = layoutSize.x;
            placeholderLayout.preferredHeight = layoutSize.y;
        }

        return go;
    }

    static Camera ResolveCanvasEventCamera(Canvas canvas)
    {
        if (canvas == null)
            return null;
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;
        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }

    struct Entry
    {
        enum Kind { Sprite, UiReparent }

        Kind _kind;
        SpriteRenderer _sprite;
        int _spriteOrder;

        RectTransform _rect;
        Transform _originalParent;
        int _originalSiblingIndex;
        GameObject _placeholder;
        Vector2 _anchorMin;
        Vector2 _anchorMax;
        Vector2 _pivot;
        Vector2 _anchoredPosition;
        Vector2 _sizeDelta;
        Vector3 _localScale;
        Quaternion _localRotation;

        public static Entry ForSprite(SpriteRenderer sprite, int order)
        {
            return new Entry
            {
                _kind = Kind.Sprite,
                _sprite = sprite,
                _spriteOrder = order
            };
        }

        public static Entry ForUiReparent(
            RectTransform rect,
            Transform originalParent,
            int originalSiblingIndex,
            GameObject placeholder)
        {
            return new Entry
            {
                _kind = Kind.UiReparent,
                _rect = rect,
                _originalParent = originalParent,
                _originalSiblingIndex = originalSiblingIndex,
                _placeholder = placeholder,
                _anchorMin = rect.anchorMin,
                _anchorMax = rect.anchorMax,
                _pivot = rect.pivot,
                _anchoredPosition = rect.anchoredPosition,
                _sizeDelta = rect.sizeDelta,
                _localScale = rect.localScale,
                _localRotation = rect.localRotation
            };
        }

        public void Restore()
        {
            if (_kind == Kind.Sprite)
            {
                if (_sprite != null)
                    _sprite.sortingOrder = _spriteOrder;
                return;
            }

            if (_placeholder != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_placeholder);
                else
                    UnityEngine.Object.DestroyImmediate(_placeholder);
                _placeholder = null;
            }

            if (_rect == null)
                return;

            if (_originalParent != null)
            {
                _rect.SetParent(_originalParent, false);
                var maxIndex = _originalParent.childCount - 1;
                _rect.SetSiblingIndex(Mathf.Clamp(_originalSiblingIndex, 0, maxIndex));
            }

            _rect.anchorMin = _anchorMin;
            _rect.anchorMax = _anchorMax;
            _rect.pivot = _pivot;
            _rect.anchoredPosition = _anchoredPosition;
            _rect.sizeDelta = _sizeDelta;
            _rect.localScale = _localScale;
            _rect.localRotation = _localRotation;
        }
    }
}
