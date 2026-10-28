using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class ChatUI : MonoBehaviour
{
    [Header("Connection")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField addressInput;
    [SerializeField] private TMP_InputField portInput;
    [SerializeField] private TMP_Text errorMessage;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image statusIcon;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button leaveButton;

    [Header("Chat")]
    [SerializeField] private TMP_InputField messageInput;
    [SerializeField] private GameObject chatMessageGO;
    [SerializeField] private GameObject chatContentDisplay;
    [SerializeField] private ScrollRect chatScrollRect;

    public string Username => usernameInput.text;
    public string Address => addressInput.text;
    public string PortText => portInput.text;
    public string Message => messageInput.text;


    // Parses the port input field as a ushort, defaulting to 7777 if invalid
    public ushort Port
    {
        get
        {
            if (ushort.TryParse(portInput.text, out ushort port))
            {
                return port;
            }

            return 7777;
        }
    }

    // Sets the status text to the given string
    public void SetStatus(string status, Color color)
    {
        statusText.text = status;
        statusIcon.color = color;
    }

    // Adds a message to the chat display
    public void AddMessage(string username, string message)
    {
        var newMessage = Instantiate(chatMessageGO, chatContentDisplay.transform);

        newMessage.TryGetComponent(out TextMeshProUGUI messageText);
        messageText.text = $"<color=yellow><b>{username}</b></color><br>{message}";

        StartCoroutine(ScrollToBottom());
    }

    // Clears the message input field
    public void ClearMessage()
    {
        messageInput.text = "";
        messageInput.ActivateInputField();
    }

    public void SetAllConnectionFields(bool interactableState, Color textColor)
    {
        usernameInput.interactable = interactableState;
        usernameInput.textComponent.color = textColor;

        addressInput.interactable = interactableState;
        addressInput.textComponent.color = textColor;

        portInput.interactable = interactableState;
        portInput.textComponent.color = textColor;
    }

    public void SetAllConnectionButtons(bool interactableState)
    {
        hostButton.interactable = interactableState;
        joinButton.interactable = interactableState;

        leaveButton.gameObject.SetActive(!interactableState);
    }

    public void ClearAllConnectionFields()
    {
        usernameInput.text = "";
        addressInput.text = "";
        portInput.text = "";
    }

    public bool ValidateConnectionFields()
    {
        // Username is always required
        if (string.IsNullOrWhiteSpace(Username))
            return false;

        // Joining requires an address
        if (string.IsNullOrWhiteSpace(Address))
            return false;

        // Port must be a valid number
        if (!ushort.TryParse(PortText, out ushort port) || port == 0)
            return false;

        return true;
    }

    public void SetErrorMessage(bool visibility, string message)
    {
        errorMessage.gameObject.SetActive(visibility);
        errorMessage.text = message;
    }

    public void ClearErrorMessage()
    {
        SetErrorMessage(false, "");
    }

    public void ClearAllMessagesInChat()
    {
        if (chatContentDisplay.transform.childCount == 0)
            return;

        foreach (Transform child in chatContentDisplay.transform)
        {
            Destroy(child.gameObject);
        }
    }

    private IEnumerator ScrollToBottom()
    {
        // Wait until the end of the frame so Unity can update the UI layout.
        yield return new WaitForEndOfFrame();

        // Force Unity to recalculate the UI layout.
        Canvas.ForceUpdateCanvases();

        // Scroll to the bottom.
        chatScrollRect.verticalNormalizedPosition = 0f;
    }

}
