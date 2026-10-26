using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InputFieldNavigation : MonoBehaviour
{
    [SerializeField] private Transform userInputs;

    private List<TMP_InputField> userInputFields = new();

    void Start()
    {
        foreach (Transform child in userInputs)
        {
            if (!child.gameObject.activeInHierarchy)
                continue;

            if (child.TryGetComponent(out TMP_InputField inputField))
                userInputFields.Add(inputField);
        }
    }

    void Update()
    {
        if (Keyboard.current == null ||!Keyboard.current.tabKey.wasPressedThisFrame)
            return;

        TMP_InputField currentField = null;

        foreach (TMP_InputField field in userInputFields)
        {
            if (field.isFocused)
            {
                currentField = field;
                break;
            }
        }

        int currentIndex = userInputFields.IndexOf(currentField);
        int nextIndex = (currentIndex + 1) % userInputFields.Count;

        TMP_InputField nextField = userInputFields[nextIndex];

        EventSystem.current.SetSelectedGameObject(nextField.gameObject);
        nextField.ActivateInputField();
    }
}
