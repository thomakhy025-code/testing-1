using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// On-screen stick, actions, and the door button for the local player.
/// </summary>
public class PlayControls
{
    public GameObject Root;
    public VirtualJoystick Joystick;
    public JumpButton Jump;
    public JumpButton Attack;
    public JumpButton Slide;
    public JumpButton Dash;
    public HoldButton Guard;
    public JumpButton TurnLeft;
    public JumpButton TurnRight;
    public GameObject OpenRoot;
    public JumpButton Open;
    public Text OpenLabel;
    public Image SlideImage;
}

public static class ScreenControls
{
    static Sprite circle;

    public static PlayControls Create()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var root = new GameObject("ScreenControls");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        root.AddComponent<GraphicRaycaster>();

        var controls = new PlayControls();
        controls.Root = root;
        controls.Joystick = Stick(root.transform, font);
        controls.Jump = Round(root.transform, "JumpButton", "Jump", font, 22, new Vector2(1f, 0f), new Vector2(-150f, 168f), new Vector2(118f, 118f), new Color(0.95f, 0.85f, 0.35f, 0.92f), false, out _);
        controls.Attack = Round(root.transform, "AttackButton", "Sword", font, 20, new Vector2(1f, 0f), new Vector2(-290f, 168f), new Vector2(108f, 108f), new Color(0.72f, 0.16f, 0.18f, 0.94f), false, out _);
        controls.Dash = Round(root.transform, "DashButton", "Dash\nX", font, 18, new Vector2(1f, 0f), new Vector2(-150f, 310f), new Vector2(104f, 104f), new Color(0.95f, 0.55f, 0.18f, 0.94f), false, out _);
        controls.Slide = Round(root.transform, "SlideButton", "Slide\nC", font, 18, new Vector2(1f, 0f), new Vector2(-290f, 310f), new Vector2(104f, 104f), new Color(0.16f, 0.62f, 0.58f, 0.94f), false, out controls.SlideImage);
        controls.Guard = GuardButton(root.transform, font);
        controls.TurnLeft = Round(root.transform, "TurnLeftButton", "Left\nQ", font, 18, new Vector2(0f, 0f), new Vector2(360f, 210f), new Vector2(96f, 96f), new Color(0.38f, 0.42f, 0.62f, 0.94f), false, out _);
        controls.TurnRight = Round(root.transform, "TurnRightButton", "Right\nE", font, 18, new Vector2(0f, 0f), new Vector2(470f, 210f), new Vector2(96f, 96f), new Color(0.38f, 0.42f, 0.62f, 0.94f), false, out _);
        controls.OpenRoot = OpenButton(root.transform, font, out controls.Open, out controls.OpenLabel);
        return controls;
    }

    static VirtualJoystick Stick(Transform parent, Font font)
    {
        var background = new GameObject("Joystick");
        background.transform.SetParent(parent, false);
        var area = background.AddComponent<RectTransform>();
        area.anchorMin = new Vector2(0f, 0f);
        area.anchorMax = new Vector2(0f, 0f);
        area.pivot = new Vector2(0.5f, 0.5f);
        area.sizeDelta = new Vector2(190f, 190f);
        area.anchoredPosition = new Vector2(170f, 190f);
        var image = background.AddComponent<Image>();
        image.sprite = Circle();
        image.color = new Color(1f, 1f, 1f, 0.28f);

        var handleGo = new GameObject("Handle");
        handleGo.transform.SetParent(background.transform, false);
        var handle = handleGo.AddComponent<RectTransform>();
        handle.sizeDelta = new Vector2(78f, 78f);
        var handleImage = handleGo.AddComponent<Image>();
        handleImage.sprite = Circle();
        handleImage.color = new Color(1f, 1f, 1f, 0.85f);
        handleImage.raycastTarget = false;

        var label = Label(background.transform, "Small tilt walks\nFull tilt runs", font, 16);
        var labelRect = label.rectTransform;
        labelRect.anchoredPosition = new Vector2(0f, -128f);
        labelRect.sizeDelta = new Vector2(220f, 52f);

        var stick = background.AddComponent<VirtualJoystick>();
        stick.handle = handle;
        stick.radius = 70f;
        return stick;
    }

    static JumpButton Round(Transform parent, string name, string caption, Font font, int size, Vector2 anchor, Vector2 position, Vector2 dimensions, Color color, bool hold, out Image image)
    {
        var button = new GameObject(name);
        button.transform.SetParent(parent, false);
        var rect = button.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = dimensions;
        rect.anchoredPosition = position;
        image = button.AddComponent<Image>();
        image.sprite = Circle();
        image.color = color;
        Label(button.transform, caption, font, size);
        return button.AddComponent<JumpButton>();
    }

    static HoldButton GuardButton(Transform parent, Font font)
    {
        var button = new GameObject("GuardButton");
        button.transform.SetParent(parent, false);
        var rect = button.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(104f, 104f);
        rect.anchoredPosition = new Vector2(-150f, 440f);
        var image = button.AddComponent<Image>();
        image.sprite = Circle();
        image.color = new Color(0.25f, 0.45f, 0.78f, 0.94f);
        Label(button.transform, "Guard\nG", font, 18);
        return button.AddComponent<HoldButton>();
    }

    static GameObject OpenButton(Transform parent, Font font, out JumpButton open, out Text label)
    {
        var button = new GameObject("OpenButton");
        button.transform.SetParent(parent, false);
        var rect = button.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(240f, 68f);
        rect.anchoredPosition = new Vector2(0f, 48f);
        var image = button.AddComponent<Image>();
        image.sprite = Circle();
        image.color = new Color(0.20f, 0.58f, 0.32f, 0.95f);
        label = Label(button.transform, "Open", font, 26);
        open = button.AddComponent<JumpButton>();
        button.SetActive(false);
        return button;
    }

    static JumpButton Jump(Transform parent, Font font)
    {
        var button = new GameObject("JumpButton");
        button.transform.SetParent(parent, false);
        var rect = button.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(130f, 130f);
        rect.anchoredPosition = new Vector2(-150f, 190f);
        var image = button.AddComponent<Image>();
        image.sprite = Circle();
        image.color = new Color(0.95f, 0.85f, 0.35f, 0.92f);
        Label(button.transform, "Jump", font, 26);
        return button.AddComponent<JumpButton>();
    }

    static JumpButton Attack(Transform parent, Font font)
    {
        var button = new GameObject("AttackButton");
        button.transform.SetParent(parent, false);
        var rect = button.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(120f, 120f);
        rect.anchoredPosition = new Vector2(-310f, 190f);
        var image = button.AddComponent<Image>();
        image.sprite = Circle();
        image.color = new Color(0.72f, 0.16f, 0.18f, 0.94f);
        Label(button.transform, "Sword", font, 24);
        return button.AddComponent<JumpButton>();
    }

    static Text Label(Transform parent, string value, Font font, int size)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        return text;
    }

    static Sprite Circle()
    {
        if (circle != null)
            return circle;

        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        float radius = size * 0.5f - 1f;
        var pixels = new Color[size * size];
        var center = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                pixels[y * size + x] = distance <= radius ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        circle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return circle;
    }
}
