using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Host / Join buttons. Host spawns the boy, Join spawns the girl.
/// </summary>
public class MultiplayerMenu : MonoBehaviour
{
    public GameSession session;
    public GameObject menuPanel;
    public Button hostButton;
    public Button joinButton;
    public Button disconnectButton;
    public InputField addressField;
    public Text statusText;
    public Text addressHint;

    void Awake()
    {
        if (hostButton != null)
            hostButton.onClick.AddListener(() => session.StartHost());
        if (joinButton != null)
            joinButton.onClick.AddListener(() => session.StartClient(addressField != null ? addressField.text : "127.0.0.1"));
        if (disconnectButton != null)
            disconnectButton.onClick.AddListener(() => session.Disconnect());
    }

    void Start()
    {
        if (addressField != null && string.IsNullOrWhiteSpace(addressField.text))
            addressField.text = "127.0.0.1";

        if (addressHint != null)
            addressHint.text = "Same computer: 127.0.0.1    Other computer: " + GameSession.LanAddress();

        var help = transform.Find("MenuPanel/Help");
        if (help != null)
        {
            var helpText = help.GetComponent<Text>();
            if (helpText != null)
            {
                helpText.text = "Walk up to a door and press Open or O. Sword breaks windows, walls, and trees.\nDrag an empty part of the screen to look. A dash drops guard.\nTurns move two body-lengths, 60 degrees off the way the screen faces.";
                helpText.rectTransform.sizeDelta = new Vector2(620f, 96f);
            }
        }
    }

    void Update()
    {
        var network = NetworkManager.Singleton;
        bool inSession = network != null && (network.IsListening || network.IsConnectedClient);
        if (menuPanel != null)
            menuPanel.SetActive(!inSession);
        if (disconnectButton != null)
            disconnectButton.gameObject.SetActive(inSession);
        if (statusText != null && session != null)
            statusText.text = session.Status;
    }
}
