using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Screen jump button. The press is kept until the character reads it.
/// </summary>
public class JumpButton : MonoBehaviour, IPointerDownHandler
{
    public bool Pressed { get; private set; }

    public void OnPointerDown(PointerEventData eventData)
    {
        Pressed = true;
    }

    public bool Consume()
    {
        bool pressed = Pressed;
        Pressed = false;
        return pressed;
    }
}
