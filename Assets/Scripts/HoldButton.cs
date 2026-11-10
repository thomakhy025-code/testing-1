using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Stays on while the pointer is held down. Used for the guard.
/// </summary>
public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public bool Held { get; private set; }

    public void OnPointerDown(PointerEventData eventData)
    {
        Held = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Held = false;
    }

    void OnDisable()
    {
        Held = false;
    }
}
