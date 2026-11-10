using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grass, trees, and small houses built the same way on every machine.
/// </summary>
public class Village : MonoBehaviour
{
    public static Village Instance { get; private set; }

    static readonly Dictionary<int, int> authorityBlood = new Dictionary<int, int>();
    static readonly Dictionary<int, int> maxBlood = new Dictionary<int, int>();
    static readonly Dictionary<int, bool> authorityOpen = new Dictionary<int, bool>();

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void Ensure()
    {
        if (Instance != null)
            return;

        authorityBlood.Clear();
        maxBlood.Clear();
        authorityOpen.Clear();
        Breakable.ById.Clear();
        HouseDoor.All.Clear();

        var root = new GameObject("Village");
        root.hideFlags = HideFlags.DontSave;
        Instance = root.AddComponent<Village>();
        Instance.Build();
    }

    public static int BloodOf(int id)
    {
        if (authorityBlood.TryGetValue(id, out int value))
            return value;
        if (maxBlood.TryGetValue(id, out int full))
            return full;
        return 0;
    }

    public static void SetAuthorityBlood(int id, int value)
    {
        authorityBlood[id] = value;
    }

    public static bool ToggleAuthorityDoor(int id)
    {
        bool open = authorityOpen.TryGetValue(id, out bool current) && current;
        open = !open;
        authorityOpen[id] = open;
        return open;
    }

    public static void CopyState(List<int> ids, List<int> bloods, List<int> openDoors)
    {
        foreach (var pair in authorityBlood)
        {
            if (!maxBlood.TryGetValue(pair.Key, out int full) || pair.Value >= full)
                continue;
            ids.Add(pair.Key);
            bloods.Add(pair.Value);
        }

        foreach (var pair in authorityOpen)
        {
            if (pair.Value)
                openDoors.Add(pair.Key);
        }
    }

    public static void ApplyDoor(int id, bool open)
    {
        authorityOpen[id] = open;
        for (int i = 0; i < HouseDoor.All.Count; i++)
        {
            if (HouseDoor.All[i] != null && HouseDoor.All[i].Id == id)
                HouseDoor.All[i].SetOpen(open);
        }
    }

    public static void Burst(Vector3 point, Color color)
    {
        if (Instance != null)
            Instance.StartCoroutine(Instance.BurstRoutine(point, color));
    }

    void Build()
    {
        PaintGrass();
        int next = 1;
        AdoptTrees(ref next);
        Vector3[] houses =
        {
            new Vector3(0f, 0f, 15f),
            new Vector3(-18f, 0f, 8f),
            new Vector3(20f, 0f, -10f)
        };
        for (int i = 0; i < houses.Length; i++)
        {
            Vector3 away = houses[i];
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
                away = Vector3.forward;
            // The door sits on local -Z. Point +Z away from the field so the door faces the players.
            Quaternion rotation = Quaternion.LookRotation(away.normalized);
            BuildHouse(houses[i], rotation, ref next);
        }

        Vector3[] trees =
        {
            new Vector3(12f, 0f, 20f),
            new Vector3(-12f, 0f, 20f),
            new Vector3(28f, 0f, 6f),
            new Vector3(-28f, 0f, 4f),
            new Vector3(8f, 0f, -18f),
            new Vector3(-8f, 0f, -18f),
            new Vector3(22f, 0f, -20f),
            new Vector3(-22f, 0f, -14f)
        };
        var trunk = new Color(0.40f, 0.26f, 0.14f);
        var leaves = new Color(0.22f, 0.48f, 0.22f);
        for (int i = 0; i < trees.Length; i++)
            MakeTree(trees[i], trunk, leaves, ref next);

        ScatterTufts(houses);
    }

    void PaintGrass()
    {
        var ground = GameObject.Find("Ground");
        if (ground == null)
            return;

        var renderer = ground.GetComponent<Renderer>();
        if (renderer == null)
            return;

        var material = renderer.material;
        material.mainTexture = GrassTexture();
        material.mainTextureScale = new Vector2(48f, 48f);
        material.color = Color.white;
    }

    void AdoptTrees(ref int next)
    {
        var transforms = FindObjectsOfType<Transform>();
        var roots = new List<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == "Tree" && transforms[i].parent == null)
                roots.Add(transforms[i]);
        }

        roots.Sort(delegate (Transform a, Transform b)
        {
            int x = a.position.x.CompareTo(b.position.x);
            return x != 0 ? x : a.position.z.CompareTo(b.position.z);
        });

        for (int i = 0; i < roots.Count; i++)
        {
            if (roots[i].GetComponent<Breakable>() != null)
                continue;
            var capsule = roots[i].gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1.15f, 0f);
            capsule.height = 2.5f;
            capsule.radius = 0.4f;
            AddBreakable(roots[i].gameObject, ref next, 60, new Color(0.40f, 0.26f, 0.14f));
        }
    }

    void BuildHouse(Vector3 position, Quaternion rotation, ref int next)
    {
        var house = new GameObject("House");
        house.hideFlags = HideFlags.DontSave;
        house.transform.SetParent(transform, false);
        house.transform.position = position;
        house.transform.rotation = rotation;

        var wall = new Color(0.90f, 0.86f, 0.76f);
        var wood = new Color(0.45f, 0.28f, 0.14f);
        var glass = new Color(0.62f, 0.82f, 0.90f);
        var roofColor = new Color(0.55f, 0.20f, 0.16f);

        Block(house.transform, "Floor", new Vector3(0f, 0.05f, 0.15f), new Vector3(6.1f, 0.1f, 5.1f), wood, false, ref next, 0, wood);
        Wall(house.transform, "WallLeft", new Vector3(-3.15f, 1.5f, 0.15f), new Vector3(0.18f, 3f, 5.3f), wall, ref next);
        Wall(house.transform, "WallRight", new Vector3(3.15f, 1.5f, 0.15f), new Vector3(0.18f, 3f, 5.3f), wall, ref next);
        Wall(house.transform, "WallBack", new Vector3(0f, 1.5f, 2.75f), new Vector3(6.3f, 3f, 0.18f), wall, ref next);

        Wall(house.transform, "FrontOuterL", new Vector3(-2.85f, 1.5f, -2.55f), new Vector3(0.7f, 3f, 0.18f), wall, ref next);
        Wall(house.transform, "FrontOuterR", new Vector3(2.85f, 1.5f, -2.55f), new Vector3(0.7f, 3f, 0.18f), wall, ref next);
        Wall(house.transform, "FrontMidL", new Vector3(-1.25f, 1.5f, -2.55f), new Vector3(0.85f, 3f, 0.18f), wall, ref next);
        Wall(house.transform, "FrontMidR", new Vector3(1.25f, 1.5f, -2.55f), new Vector3(0.85f, 3f, 0.18f), wall, ref next);
        Wall(house.transform, "Lintel", new Vector3(0f, 2.65f, -2.55f), new Vector3(6.3f, 0.7f, 0.18f), wall, ref next);
        Wall(house.transform, "SillL", new Vector3(-2.05f, 0.55f, -2.55f), new Vector3(1.05f, 1.1f, 0.18f), wall, ref next);
        Wall(house.transform, "SillR", new Vector3(2.05f, 0.55f, -2.55f), new Vector3(1.05f, 1.1f, 0.18f), wall, ref next);

        Window(house.transform, "WindowL", new Vector3(-2.05f, 1.65f, -2.52f), ref next, glass);
        Window(house.transform, "WindowR", new Vector3(2.05f, 1.65f, -2.52f), ref next, glass);

        var hinge = new GameObject("DoorHinge").transform;
        hinge.SetParent(house.transform, false);
        hinge.localPosition = new Vector3(-0.78f, 0f, -2.55f);
        var door = Block(house.transform, "Door", Vector3.zero, new Vector3(1.52f, 2.15f, 0.1f), wood, true, ref next, 80, wood);
        door.transform.SetParent(hinge, true);
        door.transform.localPosition = new Vector3(0.76f, 1.08f, 0f);
        var houseDoor = hinge.gameObject.AddComponent<HouseDoor>();
        houseDoor.Init(door.GetComponent<Breakable>().Id, hinge);
        door.GetComponent<Breakable>().Door = houseDoor;
        authorityOpen[houseDoor.Id] = false;

        var roof = Block(house.transform, "Roof", new Vector3(0f, 3.2f, 0.15f), new Vector3(7f, 0.22f, 6.1f), roofColor, true, ref next, 100, roofColor);
        roof.hideFlags = HideFlags.DontSave;
        var climb = roof.AddComponent<ClimbWall>();
        climb.Bind(house.transform);

        var table = Block(house.transform, "Table", new Vector3(0f, 0.72f, 0.35f), new Vector3(1.5f, 0.1f, 0.85f), wood, true, ref next, 40, wood);
        Leg(table.transform, new Vector3(-0.62f, -0.32f, -0.32f));
        Leg(table.transform, new Vector3(0.62f, -0.32f, -0.32f));
        Leg(table.transform, new Vector3(-0.62f, -0.32f, 0.32f));
        Leg(table.transform, new Vector3(0.62f, -0.32f, 0.32f));
        Chair(house.transform, new Vector3(0f, 0f, -0.45f), 0f, ref next, wood);
        Chair(house.transform, new Vector3(0f, 0f, 1.15f), 180f, ref next, wood);
    }

    static void Wall(Transform parent, string name, Vector3 localPosition, Vector3 scale, Color color, ref int next)
    {
        Block(parent, name, localPosition, scale, color, true, ref next, 100, color);
    }

    static void Window(Transform parent, string name, Vector3 localPosition, ref int next, Color color)
    {
        Block(parent, name, localPosition, new Vector3(0.95f, 0.85f, 0.06f), color, true, ref next, 40, color);
    }

    static GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 scale, Color color, bool breakable, ref int next, int blood, Color debris)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = scale;
        Paint(go.GetComponent<Renderer>(), color);
        if (breakable)
            AddBreakable(go, ref next, blood, debris);
        return go;
    }

    static void Leg(Transform parent, Vector3 localPosition)
    {
        var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leg.name = "Leg";
        leg.hideFlags = HideFlags.DontSave;
        Destroy(leg.GetComponent<Collider>());
        leg.transform.SetParent(parent, false);
        leg.transform.localPosition = localPosition;
        leg.transform.localScale = new Vector3(0.08f, 0.55f, 0.08f);
        Paint(leg.GetComponent<Renderer>(), new Color(0.36f, 0.22f, 0.1f));
    }

    static void Chair(Transform parent, Vector3 localPosition, float yaw, ref int next, Color wood)
    {
        var chair = new GameObject("Chair");
        chair.hideFlags = HideFlags.DontSave;
        chair.transform.SetParent(parent, false);
        chair.transform.localPosition = localPosition;
        chair.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        seat.name = "Seat";
        seat.hideFlags = HideFlags.DontSave;
        Destroy(seat.GetComponent<Collider>());
        seat.transform.SetParent(chair.transform, false);
        seat.transform.localPosition = new Vector3(0f, 0.48f, 0f);
        seat.transform.localScale = new Vector3(0.48f, 0.08f, 0.48f);
        Paint(seat.GetComponent<Renderer>(), wood);
        var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
        back.name = "Back";
        back.hideFlags = HideFlags.DontSave;
        Destroy(back.GetComponent<Collider>());
        back.transform.SetParent(chair.transform, false);
        back.transform.localPosition = new Vector3(0f, 0.78f, -0.2f);
        back.transform.localScale = new Vector3(0.48f, 0.55f, 0.08f);
        Paint(back.GetComponent<Renderer>(), wood);
        var box = chair.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.6f, -0.05f);
        box.size = new Vector3(0.5f, 1.1f, 0.5f);
        AddBreakable(chair, ref next, 20, wood);
    }

    void MakeTree(Vector3 position, Color trunk, Color leaves, ref int next)
    {
        var root = new GameObject("Tree");
        root.hideFlags = HideFlags.DontSave;
        root.transform.SetParent(transform, false);
        root.transform.position = position;
        var trunkGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunkGo.name = "Trunk";
        trunkGo.hideFlags = HideFlags.DontSave;
        Destroy(trunkGo.GetComponent<Collider>());
        trunkGo.transform.SetParent(root.transform, false);
        trunkGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        trunkGo.transform.localScale = new Vector3(0.28f, 0.8f, 0.28f);
        Paint(trunkGo.GetComponent<Renderer>(), trunk);
        var leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leaf.name = "Leaves";
        leaf.hideFlags = HideFlags.DontSave;
        Destroy(leaf.GetComponent<Collider>());
        leaf.transform.SetParent(root.transform, false);
        leaf.transform.localPosition = new Vector3(0f, 2f, 0f);
        leaf.transform.localScale = new Vector3(1.5f, 1.15f, 1.5f);
        Paint(leaf.GetComponent<Renderer>(), leaves);
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 1.2f, 0f);
        capsule.height = 2.6f;
        capsule.radius = 0.45f;
        AddBreakable(root, ref next, 60, trunk);
    }

    void ScatterTufts(Vector3[] houses)
    {
        for (int i = 0; i < 36; i++)
        {
            float angle = i * 0.85f;
            float radius = 7.5f + (i % 6) * 2.8f;
            var point = new Vector3(Mathf.Cos(angle) * radius, 0.12f, Mathf.Sin(angle) * radius);
            if (Mathf.Abs(point.x) < 8f && Mathf.Abs(point.z) < 3.5f)
                continue;
            bool crowded = false;
            for (int h = 0; h < houses.Length; h++)
            {
                var flat = houses[h];
                flat.y = 0f;
                if ((new Vector3(point.x, 0f, point.z) - flat).sqrMagnitude < 28f)
                    crowded = true;
            }

            if (crowded)
                continue;

            var tuft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tuft.name = "GrassTuft";
            tuft.hideFlags = HideFlags.DontSave;
            Destroy(tuft.GetComponent<Collider>());
            tuft.transform.SetParent(transform, false);
            tuft.transform.position = point;
            tuft.transform.localScale = new Vector3(0.08f, 0.28f, 0.35f);
            tuft.transform.rotation = Quaternion.Euler(0f, i * 37f, 0f);
            Paint(tuft.GetComponent<Renderer>(), new Color(0.30f, 0.58f, 0.24f));
        }
    }

    static void AddBreakable(GameObject go, ref int next, int blood, Color debris)
    {
        var breakable = go.AddComponent<Breakable>();
        breakable.Setup(next, blood, debris);
        maxBlood[next] = blood;
        authorityBlood[next] = blood;
        next++;
    }

    static void Paint(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;
        var shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Diffuse");
        if (shader == null)
            return;
        var material = new Material(shader);
        material.hideFlags = HideFlags.HideAndDontSave;
        material.mainTexture = Texture2D.whiteTexture;
        material.color = color;
        material.SetColor("_Color", color);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.02f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        renderer.sharedMaterial = material;
    }

    static Texture2D grassTexture;

    static Texture2D GrassTexture()
    {
        if (grassTexture != null)
            return grassTexture;

        const int size = 128;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.12f, y * 0.12f);
                float speck = Mathf.PerlinNoise(x * 0.45f + 8f, y * 0.45f + 3f);
                var color = Color.Lerp(new Color(0.24f, 0.48f, 0.18f), new Color(0.42f, 0.68f, 0.28f), n);
                if (speck > 0.72f)
                    color = Color.Lerp(color, new Color(0.55f, 0.72f, 0.32f), 0.45f);
                if (((x + y * 3) % 17) == 0)
                    color = Color.Lerp(color, new Color(0.18f, 0.36f, 0.12f), 0.35f);
                pixels[y * size + x] = color;
            }
        }

        grassTexture = new Texture2D(size, size, TextureFormat.RGB24, false);
        grassTexture.wrapMode = TextureWrapMode.Repeat;
        grassTexture.filterMode = FilterMode.Bilinear;
        grassTexture.SetPixels(pixels);
        grassTexture.Apply();
        return grassTexture;
    }

    static Mesh chipCube;
    static Mesh chipSphere;
    static readonly List<Material> chipMaterials = new List<Material>();

    static void EnsureChipMeshes()
    {
        if (chipCube != null)
            return;

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.hideFlags = HideFlags.HideAndDontSave;
        chipCube = Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);
        chipCube.hideFlags = HideFlags.HideAndDontSave;
        Destroy(cube);
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.hideFlags = HideFlags.HideAndDontSave;
        chipSphere = Object.Instantiate(sphere.GetComponent<MeshFilter>().sharedMesh);
        chipSphere.hideFlags = HideFlags.HideAndDontSave;
        Destroy(sphere);
    }

    static Material ChipMaterial(Color color)
    {
        var shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("UI/Default");
        if (shader == null)
            return null;

        var material = new Material(shader);
        material.hideFlags = HideFlags.HideAndDontSave;
        material.mainTexture = Texture2D.whiteTexture;
        material.color = color;
        material.SetColor("_Color", color);
        chipMaterials.Add(material);
        return material;
    }

    IEnumerator BurstRoutine(Vector3 point, Color color)
    {
        EnsureChipMeshes();
        var bits = new Transform[8];
        var velocities = new Vector3[8];
        var sizes = new float[8];
        for (int i = 0; i < bits.Length; i++)
        {
            var shade = Color.Lerp(color, Color.Lerp(color, Color.black, 0.4f), Random.value * 0.5f);
            var material = ChipMaterial(shade);
            if (material == null)
                continue;

            var bit = new GameObject("BreakBit");
            bit.hideFlags = HideFlags.DontSave;
            var filter = bit.AddComponent<MeshFilter>();
            filter.sharedMesh = i % 2 == 0 ? chipCube : chipSphere;
            var renderer = bit.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bit.transform.position = point;
            sizes[i] = Random.Range(0.07f, 0.14f);
            bit.transform.localScale = Vector3.one * sizes[i];
            Destroy(bit, 2f);
            bits[i] = bit.transform;
            velocities[i] = Random.insideUnitSphere;
            velocities[i].y = Mathf.Abs(velocities[i].y) + 0.8f;
            velocities[i] *= Random.Range(1.4f, 2.8f);
        }

        float life = 2f;
        while (life > 0f)
        {
            float dt = Time.deltaTime;
            life -= dt;
            float shrink = Mathf.Clamp01(life / 2f);
            for (int i = 0; i < bits.Length; i++)
            {
                if (bits[i] == null)
                    continue;
                velocities[i] += Vector3.down * 8f * dt;
                bits[i].position += velocities[i] * dt;
                bits[i].Rotate(velocities[i] * 40f * dt, Space.World);
                bits[i].localScale = Vector3.one * sizes[i] * shrink;
            }

            yield return null;
        }

        for (int i = 0; i < bits.Length; i++)
        {
            if (bits[i] != null)
                Destroy(bits[i].gameObject);
        }
    }
}
