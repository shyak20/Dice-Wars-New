using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Full-screen dim + pick a 3D die (physics raycast) or skip. Nudges dice toward the camera while open.
/// Dice can draw above Canvas UI by assigning <see cref="overlayDiceCamera"/> (URP overlay / second camera).
/// </summary>
public class RerollDieSelectionController : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Button skipButton;
    [Tooltip("Defaults to Camera.main when null. Used for raycasts and layer masking; often the same as gameplay camera.")]
    [SerializeField] private Camera mainGameplayCamera;

    [Header("Dice above canvas (recommended)")]
    [Tooltip("Optional second camera: Clear Flags = Depth only, depth above main, culling mask = RerollDice layer only. Main camera will hide that layer while the picker is open so dice render on top (e.g. Pixel Perfect canvases).")]
    [SerializeField] private Camera overlayDiceCamera;

    [Tooltip("Unity layer name assigned to dice while picking (must exist in Tags & Layers).")]
    [SerializeField] private string rerollDiceLayerName = "RerollDice";

    [Tooltip("How far to nudge each die toward the main camera while the panel is open.")]
    [SerializeField] private float pullTowardCamera = 0.4f;
    [Tooltip("If the die has no collider, add one from mesh bounds so physics raycast can hit it.")]
    [SerializeField] private bool addBoxColliderIfMissing = true;

    private Action<bool, GameObject> _onComplete;
    private bool _active;
    private readonly List<GameObject> _allowed = new List<GameObject>();
    private readonly Dictionary<Transform, Vector3> _savedPos = new Dictionary<Transform, Vector3>();
    private Camera _cam;
    private readonly Dictionary<GameObject, int> _savedLayers = new Dictionary<GameObject, int>();
    private int _rerollDiceLayer = -1;
    private int _storedMainCameraCullingMask;
    /// <summary>Set when we add the overlay to the base camera stack at runtime (scene stack may be empty).</summary>
    private bool _overlayStackEntryAddedByUs;

    private void Awake()
    {
        if (root != null) root.SetActive(false);
        if (skipButton != null) skipButton.onClick.AddListener(OnSkipPressed);
        _rerollDiceLayer = LayerMask.NameToLayer(rerollDiceLayerName);
        if (_rerollDiceLayer < 0)
            Debug.LogWarning($"RerollDieSelectionController: Layer '{rerollDiceLayerName}' is not defined. Add it in Tags & Layers, or dice may stay behind UI.");
    }

    public void BeginSelection(IReadOnlyList<GameObject> dice, Action<bool, GameObject> onComplete)
    {
        if (onComplete == null) return;
        if (dice == null || dice.Count == 0)
        {
            onComplete(true, null);
            return;
        }

        _onComplete = onComplete;
        _active = true;
        _allowed.Clear();
        _allowed.AddRange(dice);
        _cam = mainGameplayCamera != null ? mainGameplayCamera : Camera.main;

        ApplyDiceAboveUiPresentation();

        NudgeDiceForSelection();
        if (root != null) root.SetActive(true);
        CombatEvents.OnRerollDieSelectionModeChanged?.Invoke(true);
    }

    private void OnDisable()
    {
        if (_active)
            ForceClose(true, null);
    }

    private void Update()
    {
        if (!_active) return;
        if (Input.GetMouseButtonDown(0) && _cam != null)
        {
            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 200f, ~0, QueryTriggerInteraction.Collide))
            {
                var vis = hit.collider.transform.GetComponentInParent<DieVisualizer>();
                if (vis == null) return;
                var go = vis.gameObject;
                if (!_allowed.Contains(go)) return;
                EnsurePickCollider(vis);
                ForceClose(false, go);
            }
        }
    }

    private void OnSkipPressed() => ForceClose(true, null);

    private void ForceClose(bool skip, GameObject die)
    {
        if (!_active) return;
        _active = false;
        if (root != null) root.SetActive(false);
        RestoreDicePositions();
        RestoreDiceAboveUiPresentation();
        var cb = _onComplete;
        _onComplete = null;
        CombatEvents.OnRerollDieSelectionModeChanged?.Invoke(false);
        cb?.Invoke(skip, die);
    }

    private void NudgeDiceForSelection()
    {
        _savedPos.Clear();
        if (_cam == null) return;
        foreach (var d in _allowed)
        {
            if (d == null) continue;
            var tr = d.transform;
            _savedPos[tr] = tr.position;
            var toCam = (_cam.transform.position - tr.position).normalized;
            if (toCam.sqrMagnitude > 0.0001f)
                tr.position += toCam * pullTowardCamera;
            EnsurePickCollider(d.GetComponent<DieVisualizer>());
        }
    }

    private void EnsurePickCollider(DieVisualizer vis)
    {
        if (!addBoxColliderIfMissing || vis == null) return;
        if (vis.GetComponentInChildren<Collider>() != null) return;

        var rend = vis.GetComponentInChildren<MeshRenderer>();
        if (rend == null) return;

        var box = vis.gameObject.AddComponent<BoxCollider>();
        var worldCenter = rend.bounds.center;
        box.center = vis.transform.InverseTransformPoint(worldCenter);
        var sz = rend.bounds.size;
        var ls = vis.transform.lossyScale;
        box.size = new Vector3(
            sz.x / Mathf.Max(Mathf.Abs(ls.x), 0.001f),
            sz.y / Mathf.Max(Mathf.Abs(ls.y), 0.001f),
            sz.z / Mathf.Max(Mathf.Abs(ls.z), 0.001f));

        Debug.LogWarning($"RerollDieSelectionController: Added BoxCollider to '{vis.gameObject.name}' — add colliders on the dice prefab for accurate picking.");
    }

    private void RestoreDicePositions()
    {
        foreach (var kvp in _savedPos)
        {
            if (kvp.Key != null)
                kvp.Key.position = kvp.Value;
        }

        _savedPos.Clear();
    }

    private void ApplyDiceAboveUiPresentation()
    {
        if (_rerollDiceLayer < 0) return;

        var mainCam = mainGameplayCamera != null ? mainGameplayCamera : Camera.main;
        if (mainCam != null)
        {
            _storedMainCameraCullingMask = mainCam.cullingMask;
            mainCam.cullingMask &= ~(1 << _rerollDiceLayer);
        }

        foreach (var d in _allowed)
        {
            if (d == null) continue;
            _savedLayers[d] = d.layer;
            SetLayerRecursively(d, _rerollDiceLayer);
        }

        if (overlayDiceCamera != null)
        {
            EnsureOverlayRegisteredInStack(mainCam, overlayDiceCamera);
            overlayDiceCamera.cullingMask = 1 << _rerollDiceLayer;
            overlayDiceCamera.gameObject.SetActive(true);
            if (mainCam != null && overlayDiceCamera.depth <= mainCam.depth)
                overlayDiceCamera.depth = mainCam.depth + 10f;
        }
        else if (_rerollDiceLayer >= 0)
        {
            Debug.LogWarning(
                "RerollDieSelectionController: Assign overlayDiceCamera so dice render above Screen Space / Pixel Perfect UI. Main camera hides the RerollDice layer; overlay draws it after.");
        }
    }

    private void RestoreDiceAboveUiPresentation()
    {
        foreach (var kvp in _savedLayers)
        {
            if (kvp.Key != null)
                SetLayerRecursively(kvp.Key, kvp.Value);
        }

        _savedLayers.Clear();

        var mainCam = mainGameplayCamera != null ? mainGameplayCamera : Camera.main;
        if (mainCam != null)
            mainCam.cullingMask = _storedMainCameraCullingMask;

        if (overlayDiceCamera != null)
        {
            TryUnregisterOverlayFromStack(mainCam, overlayDiceCamera);
            overlayDiceCamera.gameObject.SetActive(false);
        }
    }

    /// <summary>URP only composites overlay cameras listed on the base camera stack; register if the scene omits it.</summary>
    private void EnsureOverlayRegisteredInStack(Camera baseCam, Camera overlayCam)
    {
        if (baseCam == null || overlayCam == null) return;
        var baseData = baseCam.GetUniversalAdditionalCameraData();
        if (baseData == null) return;
        if (baseData.renderType != CameraRenderType.Base)
            return;
        if (baseData.cameraStack.Contains(overlayCam))
        {
            _overlayStackEntryAddedByUs = false;
            return;
        }

        baseData.cameraStack.Add(overlayCam);
        _overlayStackEntryAddedByUs = true;
    }

    private void TryUnregisterOverlayFromStack(Camera baseCam, Camera overlayCam)
    {
        if (!_overlayStackEntryAddedByUs || baseCam == null || overlayCam == null) return;
        var baseData = baseCam.GetUniversalAdditionalCameraData();
        if (baseData != null)
            baseData.cameraStack.Remove(overlayCam);
        _overlayStackEntryAddedByUs = false;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        var t = go.transform;
        for (var i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }
}
