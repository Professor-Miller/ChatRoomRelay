
using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;

public class ConnectionManager_Relay : MonoBehaviour
{
    [SerializeField] private ChatUI_Relay chatUI;

    // Maximum number of CLIENTS that can join the Relay allocation.
    // IMPORTANT:  The host does not count toward this number.
    private const int MaxChatters = 15;

    // Determines how Relay communicates with our game.
    // "dtls" = Datagram Transport Layer Security.
    //
    // DTLS provides encryption for UDP-based communication.
     //
    // UDP is commonly used for multiplayer games because
    // it avoids some of the overhead associated with TCP.
    private const string ConnectionType = "dtls";

    // Tracks whether we are currently attempting to connect.
    //
    // This prevents players from repeatedly clicking Host
    // or Join while a previous request is still processing.
    private bool isConnecting;

    void Start()
    {
        // Subscribe to NGO connection events.
        //
        // These events are raised by NetworkManager when
        // clients connect to or disconnect from a session.
        //
        // IMPORTANT:
        // These are NOT Relay service events.
        //
        // Relay handles connectivity.
        // NGO manages the actual multiplayer session.
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        // Cleans up UI and Shuts down the network if one is running
        ResetConnection();
    }

    void OnDestroy()
    {
        // If there is no NetworkManager, there are no callbacks to clean up
        if (NetworkManager.Singleton == null) return;

        // When this MonoBehaviour is destroyed,
        // unsubscribe from the NetworkManager events.
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    // Initializes Unity Gaming Services and signs in the player.
    //
    // Relay is a Unity Gaming Service.
    // Before using it, Unity needs to know which project
    // and authenticated player are making the request.
    //
    // This does NOT connect the player to our chatroom.
    // It only prepares access to Unity's online services.
    //
    // "async" allows the method to perform asynchronous
    // operations without blocking Unity's main thread.
    //
    // "Task" represents an operation that will finish later.
    //
    // The caller can use "await" to wait for completion.
    private async System.Threading.Tasks.Task InitializeServices()
    {
        // Initialize the Unity Gaming Services SDK.
        //
        // This prepares services associated with the
        // Unity Cloud project linked to our game.
        //
        // "await" means:
        // Wait for this operation to complete before
        // continuing with the next line.
        //
        // Unity can continue running while the request
        // is being processed.
        await UnityServices.InitializeAsync();

        // Check whether this player has already
        // authenticated with Unity Gaming Services.
        //
        // We don't want to sign in again unnecessarily.
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            // Sign in anonymously.
            //
            // Anonymous authentication means the player
            // does not need to manually enter credentials.
            //
            // Unity assigns the player a service identity.
            //
            // IMPORTANT:
            // This identity is NOT the same as:
            // - Their chatroom username
            // - Their NGO ClientId
            // - Their Relay join code
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    // Called when the player presses the Host button.
    public async void Host()
    {
        // Prevent starting another connection if:
        // - A connection request is already processing.
        // - NetworkManager is already running.
        //
        // IsListening becomes true when NGO is running
        // as a host, server, or client.
        if (isConnecting || NetworkManager.Singleton.IsListening)
            return;

        // Check that the user entered a valid username.
        if (!chatUI.ValidateUsernameField())
        {
            chatUI.SetErrorMessage(true, "Set a Username Before Hosting a Session");
            return;
        }

        // Mark the connection process as active.
        //
        // This prevents additional Host/Join requests
        // while we are waiting for Unity's services.
        isConnecting = true;

        // try/catch/finally handles asynchronous errors.
        //
        // try:
        // Attempt the connection process.
        //
        // catch:
        // Handle an exception if something fails.
        //
        // finally:
        // Execute cleanup regardless of success/failure.
        try
        {
            chatUI.SetStatus("Creating Relay...", Color.yellow);

            // Prepare Unity's online services and
            // authenticate the local player.
            await InitializeServices();

            // Ask Unity Relay to reserve space for
            // up to 15 joining clients.
            //
            // An Allocation contains the information
            // required to communicate through Relay.
            //
            // This includes identifiers, connection
            // information, and authentication data.
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxChatters);

            // Get the UnityTransport component attached
            // to the same GameObject as NetworkManager.
            //
            // UnityTransport is responsible for sending
            // and receiving the actual network packets.
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));

            // Generate a join code for the classroom.
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            if (NetworkManager.Singleton.StartHost())
            {
                chatUI.SetJoinCode(joinCode);
            }
            else
            {
                // Cleans up UI and Shuts down the network if one is running
                ResetConnection();
            }
        }

        catch (Exception e)
        {
            Debug.LogException(e);
            chatUI.SetStatus("Failed to create Relay.", Color.red);
        }

        finally
        {
            isConnecting = false;
        }
    }

    public async void Join()
    {
        if (isConnecting || NetworkManager.Singleton.IsListening)
            return;

        if (!chatUI.ValidateUsernameField() || !chatUI.ValidateJoinCodeField())
        {
            chatUI.SetErrorMessage(true, "Set a Username and a Join Code Before Joining a Session");
            return;
        }

        isConnecting = true;

        try
        {
            chatUI.SetStatus("Joining Relay...", Color.yellow);

            await InitializeServices();

            // Find the host's Relay allocation.
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(chatUI.JoinCode.Trim());

            // Configure this client's transport.
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));

            if (!NetworkManager.Singleton.StartClient())
            {
                // Cleans up UI and Shuts down the network if one is running
                ResetConnection();
            }
        }

        catch (Exception e)
        {
            Debug.LogException(e);
            chatUI.SetStatus("Failed to join Relay.", Color.red);
        }

        finally
        {
            isConnecting = false;
        }
    }

    // Called when the client successfully connects to the server
    private void OnClientConnected(ulong clientId)
    {
        // If the client is the local client, set the status to "Hosting" or "Connected"
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            if (NetworkManager.Singleton.IsHost)
            {
                chatUI.SetStatus("Hosting", Color.green);
            }
            else
            {
                chatUI.SetStatus("Connected", Color.green);
            }

            chatUI.SetErrorMessage(false, "");
        }
    }

    // Called when the client disconnects from the server
    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer)
            return;

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            // Cleans up UI and Shuts down the network if one is running
            ResetConnection();
        }
    }

    public void ResetConnection()
    {
        // If NGO is currently running, shut it down.
        //
        // HOST:  Stops the server and disconnects its clients.
        // CLIENT:  Disconnects this client from the host.
        //
        // IsListening is used here to avoid calling
        // Shutdown() on an inactive NetworkManager.
        if (NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        // Remove all Chats in
        chatUI.ClearAllMessagesInChat();
        chatUI.SetErrorMessage(false, "");
        chatUI.SetStatus("Disconnected", Color.red);
        chatUI.SetAllConnectionButtons(true);
        chatUI.SetAllConnectionFields(true, Color.white);
        chatUI.ClearMessage();
        chatUI.ClearAllConnectionFields();
    }
}
