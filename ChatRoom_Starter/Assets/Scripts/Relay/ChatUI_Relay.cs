using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class ChatUI_Relay : MonoBehaviour
{
    [Header("Connection")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField joinCodeInput;
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
    public string JoinCode => joinCodeInput.text;
    public string Message => messageInput.text;


    // Sets the status text to the given string
    public void SetStatus(string status, Color color)
    {
        statusText.text = status;
        statusIcon.color = color;
    }

    public void SetJoinCode(string joinCode)
    {
        joinCodeInput.text = joinCode;
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

        joinCodeInput.interactable = interactableState;
        joinCodeInput.textComponent.color = textColor;
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
        joinCodeInput.text = "";
    }

    public bool ValidateUsernameField()
    {
        // Username is always required
        if (string.IsNullOrWhiteSpace(Username))
            return false;

        return true;
    }

    public bool ValidateJoinCodeField()
    {
        // Joining requires an address
        if (string.IsNullOrWhiteSpace(JoinCode))
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
