using UnityEngine;

/// <summary>
/// An outside house wall the character can mantle when a jump reaches the top.
/// </summary>
public class ClimbWall : MonoBehaviour
{
    public Transform House;
    public float RoofTopLocal = 3.31f;

    public void Bind(Transform house)
    {
        House = house;
    }

    public float RoofWorldY
    {
        get { return House != null ? House.TransformPoint(new Vector3(0f, RoofTopLocal, 0f)).y : transform.position.y; }
    }

    public bool IsOutside(Vector3 worldPoint)
    {
        if (House == null)
            return false;

        Vector3 local = House.InverseTransformPoint(worldPoint);
        bool inside = Mathf.Abs(local.x) < 2.85f && local.z > -2.2f && local.z < 2.45f && local.y < 3.05f;
        return !inside;
    }

    public bool IsNearTop(Vector3 feet, float bodyHeight)
    {
        float head = feet.y + bodyHeight;
        if (feet.y >= RoofWorldY - 0.12f)
            return false;

        return head >= RoofWorldY - 0.55f;
    }

    public bool AtEdge(Vector3 worldPoint)
    {
        if (House == null)
            return false;

        Vector3 local = House.InverseTransformPoint(worldPoint);
        float ax = Mathf.Abs(local.x);
        float az = Mathf.Abs(local.z - 0.15f);
        if (local.y < 2.9f || local.y > 3.6f)
            return false;

        float insetX = 3.5f - ax;
        float insetZ = 3.05f - az;
        bool alongFrontOrBack = insetZ <= 0.7f && insetZ >= -0.85f && ax <= 3.5f + 0.85f;
        bool alongSide = insetX <= 0.7f && insetX >= -0.85f && az <= 3.05f + 0.85f;
        return alongFrontOrBack || alongSide;
    }

    public Vector3 Landing(Vector3 from, float clearance)
    {
        Vector3 local = House.InverseTransformPoint(from);
        local.x = Mathf.Clamp(local.x, -2.35f, 2.35f);
        local.z = Mathf.Clamp(local.z, -1.7f, 1.7f);
        local.y = RoofTopLocal + 1.4f;
        Vector3 above = House.TransformPoint(local);
        if (Physics.Raycast(above, Vector3.down, out var hit, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * clearance;

        local.y = RoofTopLocal + clearance;
        return House.TransformPoint(local);
    }
}
