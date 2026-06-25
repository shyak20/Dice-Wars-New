using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Multi-enemy targeting: spawns one draggable <see cref="RolledOutcomeToken"/> per enemy-targeted rolled outcome piece (each
/// die's damage and each of its enemy debuffs are separate tokens, so they can go to different enemies), gates the combat flow
/// until every chip is assigned, and deposits assigned pieces into the chosen enemy's element layout (or resolves them instantly
/// when flagged Trigger-Immediately).
/// </summary>
public class RollTargetAssignmentController : MonoBehaviour
{
    [SerializeField] private CombatManager combat;
    [Tooltip("World-space combat canvas the tokens live under.")]
    [SerializeField] private Canvas canvas;
    [Tooltip("Camera used to project the 3D die position to a screen point for token spawn placement.")]
    [SerializeField] private Camera worldCamera;
    [Tooltip("Parent RectTransform for spawned tokens (typically a full-screen panel above the dice).")]
    [SerializeField] private RectTransform tokenParent;
    [SerializeField] private RolledOutcomeToken tokenPrefab;
    [Tooltip("Optional. Prompt shown while the player still has outcomes to assign.")]
    [SerializeField] private GameObject assignPrompt;
    [Header("Drag hover feedback")]
    [Tooltip("Outline material applied to enemy sprites while a token is dragged over them. Per-enemy overrides on EnemyCombatPresentationController take precedence.")]
    [SerializeField] private Material dragHoverOutlineMaterial;

    private readonly List<RolledOutcomeToken> _pendingTokens = new List<RolledOutcomeToken>();
    private Action _onAllAssigned;
    private bool _gateOpen;
    private RectTransform _spawnParentOverride;

    public bool HasPendingAssignments => _pendingTokens.Count > 0;

    public static Material SharedDragHoverOutlineMaterial { get; private set; }

    public bool HasPendingTokensForFace(FaceResult face)
    {
        if (face == null)
            return false;
        for (var i = 0; i < _pendingTokens.Count; i++)
        {
            var token = _pendingTokens[i];
            if (token != null && token.Face == face)
                return true;
        }

        return false;
    }

    /// <summary>Increase Other Elements: update a pending drag token's amount when its pool row is buffed on projectile hit.</summary>
    public bool TryApplyPendingTokenBonus(int batchGatherIndex, PoolRowKey rowKey, int bonusDelta, FaceResult face = null)
    {
        if (batchGatherIndex < 0 || bonusDelta <= 0)
            return false;

        var any = false;
        for (var i = 0; i < _pendingTokens.Count; i++)
        {
            var token = _pendingTokens[i];
            if (token == null || token.Face == null)
                continue;
            if (token.Face.BatchGatherIndex != batchGatherIndex)
                continue;
            if (!token.Line.RowKey.Equals(rowKey))
                continue;

            token.ApplyAmountBonus(bonusDelta);
            any = true;
        }

        return any;
    }

    /// <summary>Parent the spawned tokens live under. Defaults to <see cref="tokenParent"/>; the flyout can override it to its own canvas.</summary>
    public RectTransform TokenSpawnParent => _spawnParentOverride != null ? _spawnParentOverride : tokenParent;

    /// <summary>
    /// Lets <see cref="DiceRollOutcomeFlyoutController"/> spawn tokens directly under its own canvas, so tokens are created
    /// exactly where that controller renders its flyout elements.
    /// </summary>
    public void SetTokenSpawnParent(RectTransform parent)
    {
        _spawnParentOverride = parent;
    }

    private void Awake()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;
        if (combat == null)
            Debug.LogError("RollTargetAssignmentController: assign the CombatManager reference.");
        if (tokenPrefab == null || tokenParent == null)
            Debug.LogError("RollTargetAssignmentController: assign tokenPrefab and tokenParent for drag-to-assign tokens.");

        DisableTokenParentBackgroundRaycast();
        SetPrompt(false);
        SharedDragHoverOutlineMaterial = dragHoverOutlineMaterial;
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(SharedDragHoverOutlineMaterial, dragHoverOutlineMaterial))
            SharedDragHoverOutlineMaterial = null;
    }

    /// <summary>
    /// Creates a draggable token for a single enemy-targeted outcome piece. Position and spawn motion are driven by
    /// <see cref="DiceRollOutcomeFlyoutController"/>; drag stays disabled until spawn motion completes.
    /// </summary>
    public RolledOutcomeToken SpawnToken(FaceResult face, RollOutcomeVisualLine line, ApplyStatusEffectAction sourceAction,
        int pieceIndex, int pieceCount, bool dragEnabled = false)
    {
        var parent = TokenSpawnParent;
        if (tokenPrefab == null || parent == null)
        {
            Debug.LogError("RollTargetAssignmentController: assign tokenPrefab and tokenParent for multi-enemy targeting.");
            return null;
        }

        if (face == null)
            return null;

        parent.SetAsLastSibling();
        EnsureRaycasterOnRenderingCanvas(parent);

        var token = Instantiate(tokenPrefab, parent);
        // Positioning uses localPosition (anchor/pivot-independent), so the prefab's authored anchors are left intact.
        token.RectTransform.localPosition = Vector3.zero;
        var interactionCanvas = parent.GetComponentInParent<Canvas>();
        token.Configure(this, interactionCanvas != null ? interactionCanvas : canvas, face, line, sourceAction);
        token.SetDragEnabled(dragEnabled);
        token.transform.SetAsLastSibling();

        _pendingTokens.Add(token);
        SetPrompt(true);
        NotifyPendingTokensChanged();
        return token;
    }

    private void NotifyPendingTokensChanged()
    {
        CombatEvents.OnRollOutcomeTokensPendingChanged?.Invoke(_pendingTokens.Count > 0);
    }

    private void DisableTokenParentBackgroundRaycast()
    {
        if (tokenParent == null)
            return;

        var bg = tokenParent.GetComponent<Image>();
        if (bg != null)
            bg.raycastTarget = false;
    }

    /// <summary>
    /// A nested <see cref="Canvas"/> with Override Sorting renders its children on top but needs its own
    /// <see cref="GraphicRaycaster"/> for them to receive pointer events. The flyout parent lives under such a
    /// canvas, so without a raycaster tokens would show but never be draggable.
    /// </summary>
    private static void EnsureRaycasterOnRenderingCanvas(RectTransform parent)
    {
        var renderingCanvas = parent != null ? parent.GetComponentInParent<Canvas>() : null;
        if (renderingCanvas == null)
            return;

        if (renderingCanvas.GetComponent<GraphicRaycaster>() == null)
            renderingCanvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    /// <summary>
    /// Called by <see cref="CombatManager"/> at a roll boundary. Invokes <paramref name="onComplete"/> immediately when
    /// nothing needs assigning, otherwise stores it and fires once all tokens are placed.
    /// </summary>
    public void BeginGate(Action onComplete)
    {
        if (_pendingTokens.Count == 0)
        {
            _gateOpen = false;
            SetPrompt(false);
            CombatEvents.OnTargetAssignmentModeChanged?.Invoke(false);
            onComplete?.Invoke();
            return;
        }

        _gateOpen = true;
        _onAllAssigned = onComplete;
        SetPrompt(true);
        CombatEvents.OnTargetAssignmentModeChanged?.Invoke(true);
    }

    public void AssignTokenToEnemy(RolledOutcomeToken token, EnemyController enemy)
    {
        if (token == null || enemy == null || !enemy.IsAlive)
            return;
        if (!_pendingTokens.Contains(token))
            return;

        combat.AssignRolledOutcomePieceToEnemy(token.Face, token.SourceAction, enemy, token.Line, token.ResolvesImmediatelyOnDrop);

        _pendingTokens.Remove(token);
        Destroy(token.gameObject);
        NotifyPendingTokensChanged();
        TryDestroyDieWhenFaceFullyAssigned(token.Face);

        if (_pendingTokens.Count == 0)
            CompleteGateIfOpen();
    }

    /// <summary>When every drag token from this face is assigned, dissolve the 3D die that rolled it.</summary>
    private void TryDestroyDieWhenFaceFullyAssigned(FaceResult face)
    {
        if (face?.DieSource == null)
            return;

        for (var i = 0; i < _pendingTokens.Count; i++)
        {
            if (_pendingTokens[i] != null && _pendingTokens[i].Face == face)
                return;
        }

        if (combat != null)
            combat.NotifyFaceOutcomesSubmitted(face);
    }

    /// <summary>Token drag ended without a valid drop — it stays pending where it was released.</summary>
    public void NotifyTokenDragEnded(RolledOutcomeToken token)
    {
        // Token remains pending; nothing else to do (it keeps its dragged position).
    }

    /// <summary>Perfect Cast: scale every still-unassigned token's amount (jackpot visuals run during presentation).</summary>
    public void MultiplyPendingTokenAmounts(int multiplier)
    {
        if (multiplier <= 1) return;
        foreach (var token in _pendingTokens)
        {
            if (token == null) continue;
            token.MultiplyAmountOnly(multiplier);
        }
    }

    /// <summary>Cast Overload (bust): discard all unassigned tokens without applying them.</summary>
    /// <param name="playBustDestroyVisual">When true, enables the bust destroy root before removal (full bust presentation already did this when false).</param>
    public void CancelPendingAssignments(bool playBustDestroyVisual = false)
    {
        foreach (var token in _pendingTokens)
        {
            if (token == null) continue;
            if (playBustDestroyVisual)
                token.PlayBustDestroyVisual();
            Destroy(token.gameObject);
        }

        _pendingTokens.Clear();
        _gateOpen = false;
        _onAllAssigned = null;
        SetPrompt(false);
        NotifyPendingTokensChanged();
        CombatEvents.OnTargetAssignmentModeChanged?.Invoke(false);
    }

    private void CompleteGateIfOpen()
    {
        SetPrompt(false);
        if (!_gateOpen)
            return;

        _gateOpen = false;
        CombatEvents.OnTargetAssignmentModeChanged?.Invoke(false);
        var cb = _onAllAssigned;
        _onAllAssigned = null;
        cb?.Invoke();
    }

    private void SetPrompt(bool on)
    {
        if (assignPrompt != null)
            assignPrompt.SetActive(on);
    }
}
