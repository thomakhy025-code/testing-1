using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// On-screen stick. The value is zero at rest and reaches length 1 at the edge.
/// A short push is a walk. A long push is a run.
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform handle;
    public float radius = 78f;
    public Vector2 Value { get; private set; }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        var area = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out var local))
            return;

        Vector2 clamped = Vector2.ClampMagnitude(local, radius);
        if (handle != null)
            handle.anchoredPosition = clamped;
        Value = clamped / radius;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (handle != null)
            handle.anchoredPosition = Vector2.zero;
        Value = Vector2.zero;
    }
}
