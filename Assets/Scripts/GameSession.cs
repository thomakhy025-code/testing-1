#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net;
using System.Net.Sockets;
#endif
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Client-hosted session from the Netcode tutorials.
/// The host plays the boy. The joining player plays the girl.
/// Both objects are spawned on the network, so each screen shows both characters.
/// </summary>
public class GameSession : MonoBehaviour
{
    public const ushort Port = 7777;

    public static bool AutoMove { get; private set; }

    public GameObject boyPrefab;
    public GameObject girlPrefab;
    public string Status { get; private set; } = "Host plays the boy. Join plays the girl.";

    UnityTransport transport;

    void Awake()
    {
        var network = GetComponent<NetworkManager>();
        transport = GetComponent<UnityTransport>();
        network.NetworkConfig.ConnectionApproval = true;
        if (network.ConnectionApprovalCallback == null)
            network.ConnectionApprovalCallback = Approve;
    }

    void Start()
    {
        Village.Ensure();
        var args = System.Environment.GetCommandLineArgs();
        AutoMove = false;
        string autoAddress = "127.0.0.1";
        bool host = false;
        bool client = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-autohost")
                host = true;
            else if (args[i] == "-autoclient")
            {
                client = true;
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    autoAddress = args[i + 1];
            }
            else if (args[i] == "-automove")
                AutoMove = true;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        host = true;
#endif
        if (host)
            StartHost();
        else if (client)
            StartClient(autoAddress);
    }

    void OnEnable()
    {
        var network = NetworkManager.Singleton;
        if (network == null)
            return;

        network.OnClientConnectedCallback += OnClientConnected;
        network.OnClientDisconnectCallback += OnClientDisconnected;
    }

    void OnDisable()
    {
        var network = NetworkManager.Singleton;
        if (network == null)
            return;

        network.OnClientConnectedCallback -= OnClientConnected;
        network.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    public void StartHost()
    {
        var network = NetworkManager.Singleton;
        if (network == null || network.IsListening)
            return;

        transport.SetConnectionData("127.0.0.1", Port, "0.0.0.0");
        Status = "Starting host...";
        if (!network.StartHost())
        {
            Status = "Could not start the host. The port may already be in use.";
            Debug.LogError(Status);
        }
    }

    public void StartClient(string address)
    {
        var network = NetworkManager.Singleton;
        if (network == null || network.IsListening)
            return;

        if (string.IsNullOrWhiteSpace(address))
            address = "127.0.0.1";

        address = address.Trim();
        transport.SetConnectionData(address, Port);
        Status = "Joining " + address + "...";
        if (!network.StartClient())
        {
            Status = "Could not start the client.";
            Debug.LogError(Status);
        }
    }

    public void Disconnect()
    {
        var network = NetworkManager.Singleton;
        if (network != null && network.IsListening)
            network.Shutdown();

        Status = "Disconnected. Host plays the boy. Join plays the girl.";
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GroundBreak.Clear();
        ReactivateOverviewCamera();
    }

    void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        var network = NetworkManager.Singleton;
        bool isBoy = request.ClientNetworkId == NetworkManager.ServerClientId;
        if (!isBoy && network.ConnectedClientsIds.Count >= 2)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = "The boy and the girl are already in the session.";
            return;
        }

        GameObject prefab = isBoy ? boyPrefab : girlPrefab;
        var netObject = prefab.GetComponent<NetworkObject>();
        response.Approved = true;
        response.CreatePlayerObject = true;
        response.PlayerPrefabHash = netObject.PrefabIdHash;
        response.Position = isBoy ? new Vector3(-6f, 0.2f, 0f) : new Vector3(6f, 0.2f, 0f);
        response.Rotation = Quaternion.Euler(0f, isBoy ? 90f : -90f, 0f);
        response.Pending = false;
    }

    void OnClientConnected(ulong clientId)
    {
        var network = NetworkManager.Singleton;
        if (clientId != network.LocalClientId)
        {
            Status = "Both players are in. You are the boy.";
            Debug.Log("[Session] Other player joined. Clients=" + network.ConnectedClientsIds.Count);
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        Status = "You are in the village. Small stick walks, full stick runs. Sword hits for 20 blood. A flip landing cracks the ground.";
#else
        Status = network.IsHost
            ? "You are the boy. Other player joins " + LanAddress() + " (or 127.0.0.1 on this PC).\nSmall stick walks, full stick runs. Sword hits for 20 blood. A flip landing cracks the ground."
            : "You are the girl.\nSmall stick walks, full stick runs. Sword hits for 20 blood. A flip landing cracks the ground.";
#endif
        Debug.Log("[Session] Local player connected as " + (network.IsHost ? "boy" : "girl"));
    }

    void OnClientDisconnected(ulong clientId)
    {
        var network = NetworkManager.Singleton;
        if (network != null && network.IsServer && clientId != network.LocalClientId)
        {
            Status = "The other player left. You are still the boy.";
            return;
        }

        Status = "Disconnected. Host plays the boy. Join plays the girl.";
        GroundBreak.Clear();
        ReactivateOverviewCamera();
    }

    public static void ReactivateOverviewCamera()
    {
        var overview = GameObject.Find("Overview Camera");
        if (overview != null && !overview.activeSelf)
            overview.SetActive(true);
    }

    public static string LanAddress()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return "127.0.0.1";
#else
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var address in host.AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                    return address.ToString();
            }
        }
        catch (System.Exception)
        {
            // Keep the loopback hint when no LAN address is available.
        }

        return "127.0.0.1";
#endif
    }
}
