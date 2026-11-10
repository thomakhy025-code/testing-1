using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A landing scar. The cracked patch and loose slabs stay until the session ends.
/// </summary>
public class GroundBreak : MonoBehaviour
{
    public const float EffectRadius = 1.65f;
    class Piece
    {
        public Transform Transform;
        public Vector3 From;
        public Vector3 To;
        public Quaternion FromRotation;
        public Quaternion ToRotation;
        public Vector3 FromScale;
        public Vector3 ToScale;
        public float Hop;
        public float Duration;
        public float Age;
    }

    readonly List<Piece> pieces = new List<Piece>();

    public static void Spawn(Vector3 point, int seed)
    {
        Vector3 landed = SnapToGround(point);
        var rootGo = new GameObject("GroundScar");
        rootGo.hideFlags = HideFlags.DontSave;
        var root = rootGo.AddComponent<GroundBreak>();
        root.Build(landed, seed);
        Destroy(rootGo, 2f);
    }

    public static void Clear()
    {
        var scars = Resources.FindObjectsOfTypeAll<GroundBreak>();
        for (int i = 0; i < scars.Length; i++)
        {
            if (scars[i] != null && scars[i].gameObject.scene.IsValid())
                Destroy(scars[i].gameObject);
        }
    }

    static Vector3 SnapToGround(Vector3 point)
    {
        var hits = Physics.RaycastAll(point + Vector3.up * 1.5f, Vector3.down, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 chosen = new Vector3(point.x, 0.02f, point.z);
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit.collider != null && hit.collider.GetComponentInParent<PlayerMovement>() != null)
                continue;
            if (hit.normal.y < 0.45f)
                continue;
            if (hit.distance < best)
            {
                best = hit.distance;
                chosen = hit.point + Vector3.up * 0.03f;
            }
        }

        return chosen;
    }

    void Build(Vector3 point, int seed)
    {
        var rng = new System.Random(seed);
        transform.position = point;
        transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

        var patch = GameObject.CreatePrimitive(PrimitiveType.Quad);
        patch.name = "Crack";
        patch.hideFlags = HideFlags.DontSave;
        Destroy(patch.GetComponent<Collider>());
        patch.transform.SetParent(transform, false);
        patch.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        patch.transform.localPosition = new Vector3(0f, 0.012f, 0f);
        patch.transform.localScale = new Vector3(2.5f, 2.5f, 1f);
        var crack = patch.GetComponent<Renderer>();
        crack.sharedMaterial = CrackMaterial(seed);

        var hole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hole.name = "Hole";
        hole.hideFlags = HideFlags.DontSave;
        Destroy(hole.GetComponent<Collider>());
        hole.transform.SetParent(transform, false);
        hole.transform.localPosition = new Vector3(0f, -0.02f, 0f);
        hole.transform.localScale = new Vector3(0.85f, 0.02f, 0.85f);
        Paint(hole, new Color(0.06f, 0.045f, 0.035f));

        int slabs = 8;
        for (int i = 0; i < slabs; i++)
        {
            float angle = (i / (float)slabs) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Slab";
            slab.hideFlags = HideFlags.DontSave;
            Destroy(slab.GetComponent<Collider>());
            slab.transform.SetParent(transform, false);
            float radius = 0.72f + (float)rng.NextDouble() * 0.18f;
            var flat = new Vector3(Mathf.Cos(angle) * 0.28f, 0.02f, Mathf.Sin(angle) * 0.28f);
            var landed = new Vector3(Mathf.Cos(angle) * radius, 0.06f + (float)rng.NextDouble() * 0.05f, Mathf.Sin(angle) * radius);
            var tilt = Quaternion.Euler((float)rng.NextDouble() * 28f + 12f, -angle * Mathf.Rad2Deg + 90f, (float)rng.NextDouble() * 16f - 8f);
            bool turf = rng.NextDouble() > 0.35;
            Paint(slab, turf ? new Color(0.34f, 0.55f, 0.28f) : new Color(0.38f, 0.24f, 0.14f));
            var scale = new Vector3(0.34f + (float)rng.NextDouble() * 0.16f, 0.07f, 0.22f + (float)rng.NextDouble() * 0.12f);
            pieces.Add(new Piece
            {
                Transform = slab.transform,
                From = flat,
                To = landed,
                FromRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f),
                ToRotation = tilt,
                FromScale = scale,
                ToScale = scale,
                Hop = 0.18f + (float)rng.NextDouble() * 0.16f,
                Duration = 0.42f,
                Age = 0f
            });
        }

        int rocks = 5;
        for (int i = 0; i < rocks; i++)
        {
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            var rock = GameObject.CreatePrimitive(rng.NextDouble() > 0.5 ? PrimitiveType.Cube : PrimitiveType.Sphere);
            rock.name = "Rubble";
            rock.hideFlags = HideFlags.DontSave;
            Destroy(rock.GetComponent<Collider>());
            rock.transform.SetParent(transform, false);
            float distance = 1.05f + (float)rng.NextDouble() * 0.55f;
            var scale = Vector3.one * (0.08f + (float)rng.NextDouble() * 0.1f);
            scale.y *= 0.7f;
            Paint(rock, new Color(0.32f, 0.22f, 0.14f));
            pieces.Add(new Piece
            {
                Transform = rock.transform,
                From = Vector3.zero,
                To = new Vector3(Mathf.Cos(angle) * distance, 0.05f, Mathf.Sin(angle) * distance),
                FromRotation = Quaternion.identity,
                ToRotation = Quaternion.Euler((float)rng.NextDouble() * 80f, (float)rng.NextDouble() * 180f, 0f),
                FromScale = scale,
                ToScale = scale,
                Hop = 0.45f + (float)rng.NextDouble() * 0.35f,
                Duration = 0.5f,
                Age = 0f
            });
        }

        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Shock";
        ring.hideFlags = HideFlags.DontSave;
        Destroy(ring.GetComponent<Collider>());
        ring.transform.SetParent(transform, false);
        Paint(ring, new Color(0.22f, 0.16f, 0.1f));
        pieces.Add(new Piece
        {
            Transform = ring.transform,
            From = new Vector3(0f, 0.02f, 0f),
            To = new Vector3(0f, 0.02f, 0f),
            FromRotation = Quaternion.identity,
            ToRotation = Quaternion.identity,
            FromScale = new Vector3(0.4f, 0.012f, 0.4f),
            ToScale = new Vector3(2.8f, 0.012f, 2.8f),
            Hop = 0f,
            Duration = 0.38f,
            Age = 0f
        });

        Puff(point);
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            piece.Transform.localPosition = piece.From;
            piece.Transform.localRotation = piece.FromRotation;
            piece.Transform.localScale = piece.FromScale;
        }
    }

    void Puff(Vector3 point)
    {
        var go = new GameObject("Dust");
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);
        go.transform.position = point + Vector3.up * 0.15f;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.4f;
        main.startLifetime = 0.7f;
        main.startSpeed = 2.2f;
        main.startSize = 0.28f;
        main.maxParticles = 40;
        main.startColor = new Color(0.55f, 0.42f, 0.28f, 0.85f);
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.45f;
        ps.Play();
        Destroy(go, 2f);
    }

    void Update()
    {
        bool busy = false;
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            if (piece.Transform == null)
                continue;
            piece.Age += Time.deltaTime;
            float t = Mathf.Clamp01(piece.Age / piece.Duration);
            float eased = 1f - (1f - t) * (1f - t);
            float hop = Mathf.Sin(t * Mathf.PI) * piece.Hop;
            piece.Transform.localPosition = Vector3.Lerp(piece.From, piece.To, eased) + Vector3.up * hop;
            piece.Transform.localRotation = Quaternion.Slerp(piece.FromRotation, piece.ToRotation, eased);
            piece.Transform.localScale = Vector3.Lerp(piece.FromScale, piece.ToScale, eased);
            if (t < 1f)
                busy = true;
            else if (piece.Hop <= 0f && piece.ToScale.x > 2f)
                piece.Transform.gameObject.SetActive(false);
        }

        if (!busy)
            enabled = false;
    }

    static void Paint(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        var shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Diffuse");
        if (shader == null || renderer == null)
            return;

        var material = new Material(shader);
        material.mainTexture = Texture2D.whiteTexture;
        material.color = color;
        material.SetColor("_Color", color);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.02f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        renderer.sharedMaterial = material;
    }

    static Material CrackMaterial(int seed)
    {
        var shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader);
        material.mainTexture = CrackTexture(seed);
        material.color = Color.white;
        return material;
    }

    static Texture2D CrackTexture(int seed)
    {
        const int size = 96;
        var rng = new System.Random(seed);
        var pixels = new Color[size * size];
        var grass = new Color(0.33f, 0.52f, 0.27f, 1f);
        var dirt = new Color(0.4f, 0.25f, 0.13f, 1f);
        var hole = new Color(0.05f, 0.035f, 0.025f, 1f);
        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy) / center;
                Color color = Color.Lerp(dirt, grass, Mathf.Clamp01((distance - 0.28f) * 2.2f));
                if (distance < 0.2f)
                    color = hole;
                color.a = distance > 0.96f ? 0f : (distance > 0.82f ? (0.96f - distance) / 0.14f : 1f);
                pixels[y * size + x] = color;
            }
        }

        int rays = 8 + rng.Next(4);
        for (int i = 0; i < rays; i++)
        {
            float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float length = size * (0.26f + (float)rng.NextDouble() * 0.24f);
            for (int step = 0; step < length; step++)
            {
                int x = Mathf.RoundToInt(center + Mathf.Cos(angle) * step);
                int y = Mathf.RoundToInt(center + Mathf.Sin(angle) * step);
                Stamp(pixels, size, x, y, step < 8 ? 2 : 1, hole);
                if (rng.NextDouble() < 0.1)
                    angle += (float)(rng.NextDouble() - 0.5) * 1.1f;
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    static void Stamp(Color[] pixels, int size, int x, int y, int radius, Color color)
    {
        for (int oy = -radius; oy <= radius; oy++)
        {
            for (int ox = -radius; ox <= radius; ox++)
            {
                int px = x + ox;
                int py = y + oy;
                if (px < 0 || py < 0 || px >= size || py >= size)
                    continue;
                int index = py * size + px;
                Color current = pixels[index];
                current.r = color.r;
                current.g = color.g;
                current.b = color.b;
                pixels[index] = current;
            }
        }
    }
}
