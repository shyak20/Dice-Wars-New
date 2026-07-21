using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Forwards pointer drag and click events from a raycast-catching Graphic (see <see cref="RolledOutcomeToken"/> drag surface)
/// to the owning token. Keeps handlers on the topmost hit target so drags and selection clicks actually start.
/// </summary>
public sealed class RolledOutcomeTokenDragRelay : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IInitializePotentialDragHandler, IPointerClickHandler
{
    private RolledOutcomeToken _token;

    public void Bind(RolledOutcomeToken token) => _token = token;

    public void OnInitializePotentialDrag(PointerEventData eventData) { }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_token != null && _token.IsDragEnabled)
            _token.HandleBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_token != null && _token.IsDragEnabled)
            _token.HandleDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_token != null && _token.IsDragEnabled)
            _token.HandleEndDrag(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_token != null && _token.IsDragEnabled)
            _token.HandleClick(eventData);
    }
}
