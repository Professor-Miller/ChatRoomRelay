using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ChatNetwork : NetworkBehaviour
{
    [SerializeField] private ChatUI chatUI;

    private Dictionary<ulong, string> _usernameLookup = new();

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            SetUsername();
            chatUI.SetAllConnectionButtons(false);
            chatUI.SetAllConnectionFields(false, Color.gray);
        }

        if (IsServer)
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager == null)
            return;

        if (IsServer)
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    public void SetUsername()
    {
        if (!IsClient || !IsSpawned)
            return;

        string username = chatUI.Username;

        if (string.IsNullOrWhiteSpace(username))
            return;

        RegisterUsernameRpc(username);
    }

    // CLIENT: Requests permission to send a message
    public void SendMessage()
    {
        string message = chatUI.Message;

        if (string.IsNullOrWhiteSpace(message))
            return;

        SendMessageRpc(message);

        chatUI.ClearMessage();
    }

    // SERVER: Validates and authorizes the message
    [Rpc(SendTo.Server)]
    private void SendMessageRpc(string message, RpcParams rpcParams = default)
    {
        // Identify who sent the message
        ulong senderId = rpcParams.Receive.SenderClientId;

        // SERVER VALIDATION: Reject empty messages
        if (string.IsNullOrWhiteSpace(message))
            return;

        // SERVER VALIDATION: Reject messages over 100 characters
        if (message.Length > 100)
            return;

        // SERVER AUTHORITY: Get the sender's registered username
        if (!_usernameLookup.TryGetValue(senderId, out string username))
            return;

        // SERVER DECISION #4: Approve broadcast
        ReceiveMessageRpc(username, message);
    }

    // SERVER: Registers usernames to the dictionary
    [Rpc(SendTo.Server)]
    private void RegisterUsernameRpc(string username, RpcParams rpcParams = default)
    {
        // Identify who actually sent the request
        ulong senderId = rpcParams.Receive.SenderClientId;

        username = username.Trim();

        if (string.IsNullOrWhiteSpace(username) || username.Length > 20)
            return;

        // Check whether this client already has a username
        bool alreadyRegistered = _usernameLookup.ContainsKey(senderId);

        _usernameLookup[senderId] = username;

        if (!alreadyRegistered)
            SystemMessageRpc($"[{username} joined the chat!]", "green");
    }

    // EVERYONE: Displays the approved message
    [Rpc(SendTo.Everyone)]
    private void ReceiveMessageRpc(string username, string message)
    {
        chatUI.AddMessage(username, message);
    }

    [Rpc(SendTo.Everyone)]
    private void SystemMessageRpc(string message, string color)
    {
        chatUI.AddMessage("", $"<color={color}>{message}</color>");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!_usernameLookup.TryGetValue(clientId, out string username))
            return;

        // Announce departure
        SystemMessageRpc($"[{username} left the chat!]", "red");

        // Remove from server's records
        _usernameLookup.Remove(clientId);
    }
}
