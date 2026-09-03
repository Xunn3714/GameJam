using UnityEngine;
using UnityEngine.EventSystems;

namespace GameJam.Game.UI
{
    public enum MenuButtonAction
    {
        ShowMain,
        ShowStart,
        ShowSettings,
        ToggleBgm,
        ShowPopup,
        StartGame,
        ClosePopup,
    }

    /// <summary>Connects a uGUI Button to a MenuUIController without serialized UnityEvent wiring.</summary>
    [DisallowMultipleComponent]
    public sealed class MenuButtonCommand : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private MenuUIController controller;
        [SerializeField] private MenuButtonAction action;

        public void Configure(MenuUIController menuController, MenuButtonAction menuAction)
        {
            controller = menuController;
            action = menuAction;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && controller != null)
            {
                controller.Execute(action);
            }
        }
    }
}
