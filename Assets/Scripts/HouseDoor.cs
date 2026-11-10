using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A house door. It swings open and can be broken by the sword.
/// </summary>
public class HouseDoor : MonoBehaviour
{
    public static readonly List<HouseDoor> All = new List<HouseDoor>();

    public int Id;
    public bool IsOpen;
    public bool Broken;

    Transform hinge;
    Quaternion opened = Quaternion.Euler(0f, -105f, 0f);

    public Vector3 PromptPoint => hinge != null ? hinge.position : transform.position;

    public void Init(int id, Transform hingePivot)
    {
        Id = id;
        hinge = hingePivot;
        All.Add(this);
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    public void SetOpen(bool open)
    {
        if (Broken)
            return;
        IsOpen = open;
    }

    void Update()
    {
        if (hinge == null || Broken)
            return;

        Quaternion target = IsOpen ? opened : Quaternion.identity;
        hinge.localRotation = Quaternion.RotateTowards(hinge.localRotation, target, 240f * Time.deltaTime);
    }
}
