using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class MultiplayerSceneSetup
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string PrefabDir = "Assets/Prefabs";
    const string MaterialDir = "Assets/Materials";

    [MenuItem("Game/Setup Two Player Scene")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[Setup] Exit play mode before setting up the scene.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(MaterialDir))
            AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(PrefabDir))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        var skin = Mat("Skin", new Color(0.93f, 0.76f, 0.63f));
        var boyShirt = Mat("BoyShirt", new Color(0.20f, 0.45f, 0.90f));
        var boyPants = Mat("BoyPants", new Color(0.12f, 0.16f, 0.34f));
        var boyHair = Mat("BoyHair", new Color(0.16f, 0.10f, 0.07f));
        var girlDress = Mat("GirlDress", new Color(0.86f, 0.32f, 0.55f));
        var girlHair = Mat("GirlHair", new Color(0.42f, 0.22f, 0.12f));
        var bow = Mat("Bow", new Color(0.90f, 0.12f, 0.22f));
        var eye = Mat("Eye", Color.white);
        var pupil = Mat("Pupil", new Color(0.08f, 0.08f, 0.1f));
        var grass = Mat("Grass", new Color(0.36f, 0.62f, 0.30f));
        var path = Mat("Path", new Color(0.62f, 0.52f, 0.36f));
        var trunk = Mat("Trunk", new Color(0.40f, 0.26f, 0.14f));
        var leaves = Mat("Leaves", new Color(0.22f, 0.48f, 0.22f));
        var rock = Mat("Rock", new Color(0.55f, 0.55f, 0.58f));

        var boy = BuildBoy(skin, boyShirt, boyPants, boyHair, eye, pupil);
        var girl = BuildGirl(skin, girlDress, girlHair, bow, eye, pupil);
        var boyPrefab = SavePrefab(boy, PrefabDir + "/Boy.prefab");
        var girlPrefab = SavePrefab(girl, PrefabDir + "/Girl.prefab");
        Object.DestroyImmediate(boy);
        Object.DestroyImmediate(girl);

        uint boyHash = EnsureHash(boyPrefab);
        uint girlHash = EnsureHash(girlPrefab);
        Debug.Log("[Setup] Boy hash " + boyHash + " Girl hash " + girlHash);
        if (boyHash == 0 || girlHash == 0)
            Debug.LogError("[Setup] A player prefab hash is 0. Netcode will not spawn that character.");

        EditorSceneManager.SaveOpenScenes();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ClearGenerated();

        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -1f, 0f);
        ground.transform.localScale = new Vector3(160f, 2f, 160f);
        ground.GetComponent<Renderer>().sharedMaterial = grass;

        var walkway = GameObject.CreatePrimitive(PrimitiveType.Cube);
        walkway.name = "Walkway";
        walkway.transform.position = new Vector3(0f, 0.02f, 0f);
        walkway.transform.localScale = new Vector3(20f, 0.08f, 3f);
        walkway.GetComponent<Renderer>().sharedMaterial = path;

        Tree(new Vector3(-8f, 0f, 6f), trunk, leaves);
        Tree(new Vector3(8f, 0f, 6f), trunk, leaves);
        Tree(new Vector3(-10f, 0f, -7f), trunk, leaves);
        Tree(new Vector3(11f, 0f, -5f), trunk, leaves);
        Rock(new Vector3(0f, 0.35f, 7f), rock);
        Rock(new Vector3(-3f, 0.25f, -8f), rock);

        var camGo = GameObject.Find("Main Camera");
        if (camGo == null)
        {
            camGo = new GameObject("Overview Camera");
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";
        }
        else
        {
            camGo.name = "Overview Camera";
        }

        camGo.transform.position = new Vector3(0f, 9f, -16f);
        camGo.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
        var overview = camGo.GetComponent<Camera>();
        overview.clearFlags = CameraClearFlags.Skybox;

        var light = GameObject.Find("Directional Light");
        if (light != null)
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var networkGo = new GameObject("NetworkManager");
        var network = networkGo.AddComponent<NetworkManager>();
        var transport = networkGo.AddComponent<UnityTransport>();
        var session = networkGo.AddComponent<GameSession>();
        session.boyPrefab = boyPrefab;
        session.girlPrefab = girlPrefab;

        var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
        AssetDatabase.CreateAsset(list, PrefabDir + "/NetworkPrefabs.asset");
        list.Add(new NetworkPrefab { Prefab = boyPrefab });
        list.Add(new NetworkPrefab { Prefab = girlPrefab });
        EditorUtility.SetDirty(list);

        var serialized = new SerializedObject(network);
        SetRef(serialized, "NetworkConfig.PlayerPrefab", boyPrefab);
        var approval = serialized.FindProperty("NetworkConfig.ConnectionApproval");
        if (approval != null)
            approval.boolValue = true;
        SetRef(serialized, "NetworkConfig.NetworkTransport", transport);
        var lists = serialized.FindProperty("NetworkConfig.Prefabs.NetworkPrefabsLists");
        if (lists != null)
        {
            lists.ClearArray();
            lists.InsertArrayElementAtIndex(0);
            lists.GetArrayElementAtIndex(0).objectReferenceValue = list;
        }
        else
        {
            Debug.LogError("[Setup] Could not find the network prefab list property.");
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        BuildUi(session);

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool listed = false;
        foreach (var entry in scenes)
        {
            if (entry.path == ScenePath)
                listed = true;
        }
        if (!listed)
        {
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Setup] Two-player scene is ready.");
    }

    static GameObject BuildBoy(Material skin, Material shirt, Material pants, Material hair, Material eye, Material pupil)
    {
        var root = new GameObject("Boy");
        var visuals = new GameObject("Visuals").transform;
        visuals.SetParent(root.transform, false);
        Part(PrimitiveType.Cylinder, "LegL", visuals, new Vector3(-0.12f, 0.28f, 0f), new Vector3(0.16f, 0.28f, 0.16f), pants);
        Part(PrimitiveType.Cylinder, "LegR", visuals, new Vector3(0.12f, 0.28f, 0f), new Vector3(0.16f, 0.28f, 0.16f), pants);
        Part(PrimitiveType.Cube, "Torso", visuals, new Vector3(0f, 0.86f, 0f), new Vector3(0.46f, 0.5f, 0.26f), shirt);
        Part(PrimitiveType.Sphere, "Head", visuals, new Vector3(0f, 1.30f, 0f), Vector3.one * 0.34f, skin);
        Part(PrimitiveType.Sphere, "Hair", visuals, new Vector3(0f, 1.44f, -0.01f), new Vector3(0.36f, 0.22f, 0.36f), hair);
        Part(PrimitiveType.Sphere, "EyeL", visuals, new Vector3(-0.07f, 1.33f, 0.14f), Vector3.one * 0.06f, eye);
        Part(PrimitiveType.Sphere, "EyeR", visuals, new Vector3(0.07f, 1.33f, 0.14f), Vector3.one * 0.06f, eye);
        Part(PrimitiveType.Sphere, "PupilL", visuals, new Vector3(-0.07f, 1.33f, 0.165f), Vector3.one * 0.03f, pupil);
        Part(PrimitiveType.Sphere, "PupilR", visuals, new Vector3(0.07f, 1.33f, 0.165f), Vector3.one * 0.03f, pupil);
        Part(PrimitiveType.Cylinder, "ArmL", visuals, new Vector3(-0.32f, 0.86f, 0f), new Vector3(0.1f, 0.24f, 0.1f), skin);
        Part(PrimitiveType.Cylinder, "ArmR", visuals, new Vector3(0.32f, 0.86f, 0f), new Vector3(0.1f, 0.24f, 0.1f), skin);
        return FinishCharacter(root, visuals, "Boy", new Color(0.75f, 0.88f, 1f));
    }

    static GameObject BuildGirl(Material skin, Material dress, Material hair, Material bow, Material eye, Material pupil)
    {
        var root = new GameObject("Girl");
        var visuals = new GameObject("Visuals").transform;
        visuals.SetParent(root.transform, false);
        Part(PrimitiveType.Cylinder, "LegL", visuals, new Vector3(-0.08f, 0.2f, 0f), new Vector3(0.12f, 0.2f, 0.12f), skin);
        Part(PrimitiveType.Cylinder, "LegR", visuals, new Vector3(0.08f, 0.2f, 0f), new Vector3(0.12f, 0.2f, 0.12f), skin);
        Part(PrimitiveType.Cylinder, "Skirt", visuals, new Vector3(0f, 0.55f, 0f), new Vector3(0.55f, 0.22f, 0.55f), dress);
        Part(PrimitiveType.Cube, "Torso", visuals, new Vector3(0f, 0.95f, 0f), new Vector3(0.38f, 0.4f, 0.24f), dress);
        Part(PrimitiveType.Sphere, "Head", visuals, new Vector3(0f, 1.32f, 0f), Vector3.one * 0.32f, skin);
        Part(PrimitiveType.Sphere, "Hair", visuals, new Vector3(0f, 1.46f, -0.02f), new Vector3(0.38f, 0.28f, 0.4f), hair);
        Part(PrimitiveType.Cylinder, "HairL", visuals, new Vector3(-0.16f, 1.12f, 0.02f), new Vector3(0.08f, 0.28f, 0.08f), hair);
        Part(PrimitiveType.Cylinder, "HairR", visuals, new Vector3(0.16f, 1.12f, 0.02f), new Vector3(0.08f, 0.28f, 0.08f), hair);
        Part(PrimitiveType.Cube, "Bow", visuals, new Vector3(0.12f, 1.52f, 0.08f), new Vector3(0.14f, 0.08f, 0.06f), bow);
        Part(PrimitiveType.Sphere, "EyeL", visuals, new Vector3(-0.07f, 1.34f, 0.13f), Vector3.one * 0.06f, eye);
        Part(PrimitiveType.Sphere, "EyeR", visuals, new Vector3(0.07f, 1.34f, 0.13f), Vector3.one * 0.06f, eye);
        Part(PrimitiveType.Sphere, "PupilL", visuals, new Vector3(-0.07f, 1.34f, 0.155f), Vector3.one * 0.03f, pupil);
        Part(PrimitiveType.Sphere, "PupilR", visuals, new Vector3(0.07f, 1.34f, 0.155f), Vector3.one * 0.03f, pupil);
        Part(PrimitiveType.Cylinder, "ArmL", visuals, new Vector3(-0.28f, 0.95f, 0f), new Vector3(0.09f, 0.22f, 0.09f), skin);
        Part(PrimitiveType.Cylinder, "ArmR", visuals, new Vector3(0.28f, 0.95f, 0f), new Vector3(0.09f, 0.22f, 0.09f), skin);
        return FinishCharacter(root, visuals, "Girl", new Color(1f, 0.82f, 0.88f));
    }

    static GameObject FinishCharacter(GameObject root, Transform visuals, string label, Color labelColor)
    {
        var body = root.AddComponent<CharacterController>();
        body.height = 1.75f;
        body.radius = 0.32f;
        body.center = new Vector3(0f, 0.9f, 0f);
        body.stepOffset = 0.25f;

        root.AddComponent<NetworkObject>();
        var transformSync = root.AddComponent<ClientNetworkTransform>();
        transformSync.Interpolate = true;

        var pivot = new GameObject("CameraPivot").transform;
        pivot.SetParent(root.transform, false);
        pivot.localPosition = new Vector3(0f, 1.4f, 0f);

        var camGo = new GameObject("PlayerCamera");
        camGo.transform.SetParent(pivot, false);
        camGo.transform.localPosition = new Vector3(0f, 0.55f, -4.4f);
        var camera = camGo.AddComponent<Camera>();
        camera.nearClipPlane = 0.15f;
        camera.fieldOfView = 60f;
        camGo.AddComponent<AudioListener>();

        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(root.transform, false);
        nameGo.transform.localPosition = new Vector3(0f, 1.85f, 0f);
        var text = nameGo.AddComponent<TextMesh>();
        text.text = label;
        text.fontSize = 48;
        text.characterSize = 0.08f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = labelColor;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
            text.font = font;
        nameGo.AddComponent<Nameplate>();

        var movement = root.AddComponent<PlayerMovement>();
        movement.cameraPivot = pivot;
        movement.visuals = visuals;
        movement.playerCamera = camera;
        root.AddComponent<PlayerCombat>();
        return root;
    }

    static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material material)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPos;
        part.transform.localScale = localScale;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }

    static void Tree(Vector3 position, Material trunk, Material leaves)
    {
        var root = new GameObject("Tree");
        root.transform.position = position;
        Part(PrimitiveType.Cylinder, "Trunk", root.transform, new Vector3(0f, 0.7f, 0f), new Vector3(0.25f, 0.7f, 0.25f), trunk);
        Part(PrimitiveType.Sphere, "Leaves", root.transform, new Vector3(0f, 1.8f, 0f), new Vector3(1.4f, 1.1f, 1.4f), leaves);
    }

    static void Rock(Vector3 position, Material material)
    {
        var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "Rock";
        rock.transform.position = position;
        rock.transform.localScale = new Vector3(1.2f, 0.7f, 1f);
        rock.GetComponent<Renderer>().sharedMaterial = material;
    }

    static GameObject SavePrefab(GameObject instance, string path)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
        return prefab;
    }

    static void SetRef(SerializedObject serialized, string propertyPath, Object value)
    {
        var property = serialized.FindProperty(propertyPath);
        if (property == null)
        {
            Debug.LogError("[Setup] Missing property " + propertyPath);
            return;
        }

        property.objectReferenceValue = value;
    }

    static uint EnsureHash(GameObject prefab)
    {
        var net = prefab.GetComponent<NetworkObject>();
        var method = typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method != null)
            method.Invoke(net, null);
        EditorUtility.SetDirty(net);
        return net.PrefabIdHash;
    }

    static Material Mat(string name, Color color)
    {
        string path = MaterialDir + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            material.SetFloat("_Glossiness", 0.12f);
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void ClearGenerated()
    {
        string[] names =
        {
            "Ground", "Walkway", "Tree", "Rock", "NetworkManager", "MultiplayerUI", "EventSystem"
        };

        var doomed = new List<GameObject>();
        foreach (var go in Object.FindObjectsOfType<GameObject>())
        {
            foreach (var name in names)
            {
                if (go.name == name)
                    doomed.Add(go);
            }
        }

        foreach (var go in doomed)
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }

        var prefabList = PrefabDir + "/NetworkPrefabs.asset";
        if (AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(prefabList) != null)
            AssetDatabase.DeleteAsset(prefabList);
    }

    static void BuildUi(GameSession session)
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGo = new GameObject("MultiplayerUI");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();
        }

        var menu = new GameObject("MenuPanel");
        menu.transform.SetParent(canvasGo.transform, false);
        var menuRect = menu.AddComponent<RectTransform>();
        menuRect.anchorMin = new Vector2(0.5f, 0.5f);
        menuRect.anchorMax = new Vector2(0.5f, 0.5f);
        menuRect.sizeDelta = new Vector2(520f, 460f);
        var panelImage = menu.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.1f, 0.14f, 0.88f);

        var title = Label(menu.transform, "Title", "Boy and Girl", 36, FontStyle.Bold, new Vector2(0f, 170f), new Vector2(460f, 50f), font);
        title.alignment = TextAnchor.MiddleCenter;

        var hint = Label(menu.transform, "Hint", "", 18, FontStyle.Normal, new Vector2(0f, 115f), new Vector2(480f, 40f), font);
        hint.alignment = TextAnchor.MiddleCenter;

        var address = Input(menu.transform, "Address", "127.0.0.1", new Vector2(0f, 55f), font);
        var host = Button(menu.transform, "HostButton", "Host  -  play the boy", new Vector2(0f, -15f), new Color(0.18f, 0.42f, 0.82f), font);
        var join = Button(menu.transform, "JoinButton", "Join  -  play the girl", new Vector2(0f, -80f), new Color(0.75f, 0.28f, 0.48f), font);
        var help = Label(menu.transform, "Help", "Small tilt walks, full tilt runs. Sword hits for 20 blood. Jump twice and the landing cracks the ground.", 16, FontStyle.Normal, new Vector2(0f, -155f), new Vector2(480f, 72f), font);
        help.alignment = TextAnchor.MiddleCenter;

        var status = Label(canvasGo.transform, "Status", "", 22, FontStyle.Normal, new Vector2(0f, -40f), new Vector2(1100f, 90f), font);
        var statusRect = status.rectTransform;
        statusRect.anchorMin = new Vector2(0.5f, 0f);
        statusRect.anchorMax = new Vector2(0.5f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.anchoredPosition = new Vector2(0f, 24f);
        status.alignment = TextAnchor.LowerCenter;
        status.color = Color.white;

        var disconnect = Button(canvasGo.transform, "DisconnectButton", "Disconnect", new Vector2(0f, 0f), new Color(0.35f, 0.18f, 0.18f), font);
        var disconnectRect = disconnect.GetComponent<RectTransform>();
        disconnectRect.anchorMin = new Vector2(1f, 1f);
        disconnectRect.anchorMax = new Vector2(1f, 1f);
        disconnectRect.pivot = new Vector2(1f, 1f);
        disconnectRect.anchoredPosition = new Vector2(-24f, -24f);
        disconnectRect.sizeDelta = new Vector2(180f, 42f);
        disconnect.gameObject.SetActive(false);

        var view = canvasGo.AddComponent<MultiplayerMenu>();
        view.session = session;
        view.menuPanel = menu;
        view.hostButton = host;
        view.joinButton = join;
        view.disconnectButton = disconnect;
        view.addressField = address;
        view.statusText = status;
        view.addressHint = hint;
    }

    static Text Label(Transform parent, string name, string value, int size, FontStyle style, Vector2 position, Vector2 sizeDelta, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = position;
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    static Button Button(Transform parent, string name, string value, Vector2 position, Color color, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(360f, 52f);
        rect.anchoredPosition = position;
        var image = go.AddComponent<Image>();
        image.color = color;
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        var text = Label(go.transform, "Text", value, 22, FontStyle.Bold, Vector2.zero, new Vector2(340f, 52f), font);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    static InputField Input(Transform parent, string name, string value, Vector2 position, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(360f, 46f);
        rect.anchoredPosition = position;
        var image = go.AddComponent<Image>();
        image.color = Color.white;
        var text = Label(go.transform, "Text", value, 22, FontStyle.Normal, Vector2.zero, new Vector2(340f, 46f), font);
        text.color = new Color(0.1f, 0.1f, 0.12f);
        text.alignment = TextAnchor.MiddleCenter;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(8f, 4f);
        text.rectTransform.offsetMax = new Vector2(-8f, -4f);
        var input = go.AddComponent<InputField>();
        input.targetGraphic = image;
        input.textComponent = text;
        input.text = value;
        return input;
    }
}
