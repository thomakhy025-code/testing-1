using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A tree, wall, window, or piece of furniture the sword can wear down.
/// </summary>
public class Breakable : MonoBehaviour
{
    public static readonly Dictionary<int, Breakable> ById = new Dictionary<int, Breakable>();

    public int Id { get; private set; }
    public int MaxBlood { get; private set; }
    public int Blood { get; private set; }
    public bool Broken { get; private set; }
    public Color DebrisColor { get; private set; }
    public HouseDoor Door { get; set; }

    Renderer[] renderers;
    Color[] baseColors;
    static MaterialPropertyBlock tintBlock;

    public void Setup(int id, int maxBlood, Color debrisColor)
    {
        Id = id;
        MaxBlood = maxBlood;
        Blood = maxBlood;
        DebrisColor = debrisColor;
        ById[id] = this;
        renderers = GetComponentsInChildren<Renderer>(true);
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].sharedMaterial != null)
                baseColors[i] = renderers[i].sharedMaterial.color;
        }
    }

    void OnDestroy()
    {
        if (ById.TryGetValue(Id, out var current) && current == this)
            ById.Remove(Id);
    }

    public void SetBlood(int value)
    {
        if (Broken)
            return;

        int next = Mathf.Clamp(value, 0, MaxBlood);
        bool damaged = next < Blood;
        Blood = next;
        if (damaged && Blood > 0)
            Pulse();
        if (Blood <= 0)
            Shatter();
    }

    void Pulse()
    {
        Tint(0.7f);
        CancelInvoke(nameof(Restore));
        Invoke(nameof(Restore), 0.12f);
    }

    void Restore()
    {
        if (Broken || renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].SetPropertyBlock(null);
        }
    }

    void Tint(float amount)
    {
        if (renderers == null)
            return;

        if (tintBlock == null)
            tintBlock = new MaterialPropertyBlock();

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            renderers[i].GetPropertyBlock(tintBlock);
            Color color = Color.Lerp(baseColors[i], new Color(1f, 0.35f, 0.28f), amount);
            tintBlock.SetColor("_Color", color);
            tintBlock.SetColor("_BaseColor", color);
            renderers[i].SetPropertyBlock(tintBlock);
        }
    }

    void Shatter()
    {
        if (Broken)
            return;

        Broken = true;
        Blood = 0;
        CancelInvoke(nameof(Restore));
        if (Door != null)
            Door.Broken = true;

        var cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                cols[i].enabled = false;
        }

        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;
                renderers[i].SetPropertyBlock(null);
                renderers[i].enabled = false;
                Destroy(renderers[i]);
            }
        }

        var filters = GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i] != null)
                Destroy(filters[i]);
        }

        Village.Burst(transform.position + Vector3.up * 0.35f, DebrisColor);
        gameObject.SetActive(false);
    }
}
