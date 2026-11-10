using UnityEngine;

/// <summary>
/// Turns a world-space label toward whichever camera is currently rendering.
/// </summary>
public class Nameplate : MonoBehaviour
{
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null)
            return;

        Vector3 towardCamera = transform.position - cam.transform.position;
        if (towardCamera.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(towardCamera);
    }
}
