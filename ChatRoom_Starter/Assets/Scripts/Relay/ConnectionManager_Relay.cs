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

            // Convert the Relay allocation into the
            // configuration format UnityTransport expects.
            //
            // The conversion includes the Relay server
            // connection information and DTLS settings.
            //
            // We then assign that configuration to UTP.
            //
            // After this, UnityTransport knows to use
            // Relay instead of a direct LAN connection.
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));

            // Request a short join code for this allocation.
            //
            // Relay uses the code to locate the
            // corresponding allocation.
            //
            // The join code is NOT a player ID.
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Now that transport is configured,
            // start Netcode for GameObjects.
            //
            // StartHost() creates:
            //
            // - An authoritative server instance
            // - A local client connected to that server
            //
            // The host now runs our existing RPCs,
            // NetworkVariables, and server-side logic.
            //
            // IMPORTANT:
            // Relay is NOT running our game server.
            //
            // Relay forwards network traffic.
            // Our HOST remains authoritative.
            if (NetworkManager.Singleton.StartHost())
            {
                // Store/display the join code in our UI.
                chatUI.SetJoinCode(joinCode);
            }
            else
            {
                // StartHost() can fail even if the
                // Relay allocation was created successfully.
                ResetConnection();
            }
        }

        catch (Exception e)
        {
            // Handle failures such as:
            //
            // - No internet connection
            // - UGS initialization failure
            // - Authentication failure
            // - Relay service unavailable
            // - Relay allocation failure
            //
            // Log the actual exception for debugging.
            Debug.LogException(e);
            chatUI.SetStatus("Failed to create Relay.", Color.red);
        }


        finally
        {
            // Allow another connection attempt.
            //
            // finally executes whether the operation:
            //
            // - Succeeded
            // - Failed
            // - Threw an exception
            //
            // Note that this means the asynchronous
            // setup attempt has finished.
            //
            // It does not necessarily mean the
            // network session is fully connected.
            isConnecting = false;
        }
    }

    public async void Join()
    {
        // Prevent duplicate connection attempts
        // or attempts to join while already connected.
        if (isConnecting || NetworkManager.Singleton.IsListening)
            return;

        // Make sure the player entered:
        //
        // - A valid username
        // - A valid-looking join code
        //
        // These checks happen before contacting Relay.
        if (!chatUI.ValidateUsernameField() || !chatUI.ValidateJoinCodeField())
        {
            chatUI.SetErrorMessage(true, "Set a Username and a Join Code Before Joining a Session");
            return;
        }

        // Prevent other connection attempts while
        // we contact Unity Gaming Services.
        isConnecting = true;

        try
        {
            chatUI.SetStatus("Joining Relay...", Color.yellow);

            // Make sure Unity services are initialized
            // and the chatter is authenticated.
            await InitializeServices();

            // Send the join code to Unity Relay.
            //
            // Relay uses this code to find the
            // allocation created by the host.
            //
            // Trim() removes leading/trailing whitespace
            // that might have been pasted accidentally.
            //
            // A JoinAllocation contains the information
            // this client needs to communicate through
            // the host's Relay allocation.
            //
            // IMPORTANT:
            // We are not connected to NGO yet.
            //
            // We are only obtaining Relay connection data.
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(chatUI.JoinCode.Trim());

            // Configure this client's transport.
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            // Configure this client's transport using
            // the Relay information returned above.
            //
            // The client and host must use compatible
            // Relay connection settings.
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));

            // StartClient() tells NGO to begin connecting
            // to the host through the configured transport.
            //
            // IMPORTANT:
            // Returning TRUE does not guarantee that
            // the client has connected successfully.
            //
            // It only means NGO successfully started
            // the client connection process.
            //
            // The actual successful connection is
            // reported through OnClientConnected().
            if (!NetworkManager.Singleton.StartClient())
            {
                // Cleans up UI and Shuts down the network if one is running
                ResetConnection();
            }
        }

        catch (Exception e)
        {
            // Possible errors include:
            //
            // - Invalid join code
            // - Expired/unavailable allocation
            // - Full Relay allocation
            // - Authentication failure
            // - Internet connectivity problems
            Debug.LogException(e);
            chatUI.SetStatus("Failed to join Relay.", Color.red);
        }

        finally
        {
            // The asynchronous Relay setup is complete.
            //
            // NGO may still be establishing the
            // actual connection to the host.
            isConnecting = false;
        }
    }

    // NGO invokes this callback when a client
    // successfully connects to the server.
    //
    // clientId identifies the connected NGO client.
    //
    // This ID is assigned by NGO.
    //
    // It is NOT:
    // - A Unity Authentication player ID
    // - A Relay allocation ID
    // - A Steam ID
    // - A chatroom username
    private void OnClientConnected(ulong clientId)
    {
        // On the host, this callback may run when
        // another client connects.
        //
        // We only want to update the local connection
        // status when THIS instance connects.
        //
        // LocalClientId identifies the local NGO client.
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

            // Clear any errors from previous attempts.
            chatUI.SetErrorMessage(false, "");
        }
    }


    // NGO invokes this when a client disconnects.
    //
    // Different instances can receive callbacks
    // for different reasons.
    //
    // HOST:
    // Receives callbacks when remote clients disconnect.
    //
    // CLIENT:
    // Receives a callback when its connection to
    // the server is terminated.
    //
    // This is an NGO lifecycle event, not a direct
    // notification from the Relay service.
    private void OnClientDisconnected(ulong clientId)
    {
        // If this instance is the server, ignore
        // remote client disconnections.
        //
        // We do NOT want the host's entire UI
        // resetting every time a student leaves.
        //
        // NOTE:
        // This also means the host does not use
        // this callback to handle its own shutdown.
        if (NetworkManager.Singleton.IsServer)
            return;

        // Only reset if the disconnected client
        // is the local client on this machine.
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            // Cleans up UI and Shuts down the network if one is running
            ResetConnection();
        }
    }

    // Resets the local connection and chatroom UI.
    //
    // This can be called when:
    //
    // - The player presses Disconnect
    // - A connection attempt fails
    // - The local client loses its connection
    //
    // IMPORTANT:
    // This does not delete the Unity Cloud project
    // or explicitly release a Relay allocation.
    //
    // It stops NGO locally and resets the interface.
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

        // Remove all chat message GameObjects
        // through our existing ChatUI implementation.
        chatUI.ClearAllMessagesInChat();

        // Clear any errors from previous attempts.
        chatUI.SetErrorMessage(false, "");

        // Display the disconnected state.
        chatUI.SetStatus("Disconnected", Color.red);

        // Re-enable Host and Join buttons.
        chatUI.SetAllConnectionButtons(true);

        // Re-enable the username/join-code fields
        // and restore their normal text color.
        chatUI.SetAllConnectionFields(true, Color.white);
        chatUI.ClearMessage();

        // Clear the connection fields so the player
        // can start a new session.
        chatUI.ClearAllConnectionFields();
    }
}
