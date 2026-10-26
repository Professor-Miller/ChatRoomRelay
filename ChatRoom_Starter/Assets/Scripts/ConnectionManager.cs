using Unity.Netcode.Transports.UTP;
using UnityEngine;

using NetManager = Unity.Netcode.NetworkManager;

public class ConnectionManager : MonoBehaviour
{
    [SerializeField] private ChatUI chatUI;

    void Start()
    {
        // Subscribe to connection callbacks
        NetManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        chatUI.SetStatus("Disconnected", Color.red);
        chatUI.SetErrorMessage(false, "");
        chatUI.ClearMessage();
        ResetConnection();
    }

    void OnDestroy()
    {
        // If there is no NetworkManager, there are no callbacks to clean up
        if (NetManager.Singleton == null) return;

        // Unsubscribe from connection callbacks
        NetManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    // Host the game on the local machine
    public void Host()
    {
        if (!chatUI.ValidateConnectionFields())
        {
            chatUI.SetErrorMessage(true, "Complete All Connection Fields Before Starting a Host Session");
            return;
        }

        // Get the UnityTransport component from the NetworkManager
        var transport = NetManager.Singleton.GetComponent<UnityTransport>();

        // Set the connection data to the local machine's IP and the chat UI's port
        transport.SetConnectionData("0.0.0.0", chatUI.Port);

        // Start the host
        bool started = NetManager.Singleton.StartHost();

        // If the host started successfully, set the status to "Hosting on port {chatUI.Port}"
        if (started)
        {
            chatUI.SetStatus($"Hosting on port {chatUI.Port}", Color.green);
        }

        // Otherwise, set the status to "Failed to start host"
        else
        {
            chatUI.SetStatus("Failed to start host", Color.yellow);
            ResetConnection();
        }
    }

    // Join the game on the local machine
    public void Join()
    {
        if (!chatUI.ValidateConnectionFields())
        {
            chatUI.SetErrorMessage(true, "Complete All Connection Fields Before Joining a Session");
            return;
        }

        // Get the UnityTransport component from the NetworkManager
        var transport = NetManager.Singleton.GetComponent<UnityTransport>();

        // Set the connection data to the chat UI's address and port
        transport.SetConnectionData(chatUI.Address, chatUI.Port);

        // Set the status to "Connecting to {chatUI.Address}:{chatUI.Port}..."
        chatUI.SetStatus($"Connecting to {chatUI.Address}:{chatUI.Port}...", Color.green);

        // Start the client
        bool started = NetManager.Singleton.StartClient();

        // If the client failed to start, set the status to "Failed to start client"
        if (!started)
        {
            chatUI.SetStatus("Failed to start client", Color.yellow);
            ResetConnection();
        }
    }

    // Called when the client successfully connects to the server
    private void OnClientConnected(ulong clientId)
    {
        // If the client is the local client, set the status to "Hosting" or "Connected"
        if (clientId == NetManager.Singleton.LocalClientId)
        {
            if (NetManager.Singleton.IsHost)
            {
                chatUI.SetStatus("Hosting", Color.green);
            }
            else
            {
                chatUI.SetStatus("Connected", Color.green);
            }
        }
    }

    // Called when the client disconnects from the server
    private void OnClientDisconnected(ulong clientId)
    {
        if (clientId == NetManager.Singleton.LocalClientId)
        {
            chatUI.SetStatus("Disconnected", Color.red);
            chatUI.ClearMessage();
        }
    }

    public void ResetConnection()
    {
        if (NetManager.Singleton.IsListening)
            NetManager.Singleton.Shutdown();

        chatUI.SetAllConnectionButtons(true);
        chatUI.SetAllConnectionFields(true, Color.white);
        chatUI.ClearAllConnectionFields();
    }
}
