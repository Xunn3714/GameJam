using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CreditsSceneController : MonoBehaviour
{
    private const string RuntimeSceneName = "Credits";

    [Header("Camera Scroll")]
    [SerializeField] private Camera scrollingCamera;
    [SerializeField] private Transform startMarker;
    [SerializeField] private Transform endMarker;
    [SerializeField, Min(0f)] private float startDelay = 2.25f;
    [SerializeField, Min(0.1f)] private float scrollSpeed = 3.6f;
    [SerializeField, Min(1f)] private float fastForwardMultiplier = 3f;

    [Header("Controls")]
    [SerializeField] private RectTransform backButtonRect;
    [SerializeField] private Graphic backButtonGraphic;
    [SerializeField] private Color backButtonNormalColor = Color.white;
    [SerializeField] private Color backButtonHoverColor = new Color32(255, 236, 194, 255);

    private float delayRemaining;
    private bool isLeaving;


    public Camera ScrollingCamera => scrollingCamera;
    public Transform StartMarker => startMarker;
    public Transform EndMarker => endMarker;
    public RectTransform BackButtonRect => backButtonRect;
    public float StartDelay => startDelay;


    private void Start()
    {
        Time.timeScale = 1f;
        delayRemaining = startDelay;

        if (scrollingCamera == null || startMarker == null || endMarker == null)
        {
            Debug.LogError("CreditsSceneController: camera scroll references are missing.", this);
            enabled = false;
            return;
        }

        SetCameraPosition(startMarker.position);
    }


    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            ReturnToMainMenu();
            return;
        }

        UpdatePointerControls();
        if (isLeaving)
            return;

        if (delayRemaining > 0f)
        {
            delayRemaining -= Time.unscaledDeltaTime;
            return;
        }

        float speedMultiplier = keyboard != null &&
            (keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed)
            ? fastForwardMultiplier
            : 1f;

        Vector3 current = scrollingCamera.transform.position;
        Vector3 target = new Vector3(
            endMarker.position.x,
            endMarker.position.y,
            current.z);

        scrollingCamera.transform.position = Vector3.MoveTowards(
            current,
            target,
            scrollSpeed * speedMultiplier * Time.unscaledDeltaTime);
    }


    public void Configure(Camera camera, Transform start, Transform end)
    {
        scrollingCamera = camera;
        startMarker = start;
        endMarker = end;
    }


    public void ConfigureBackButton(RectTransform buttonRect, Graphic buttonGraphic)
    {
        backButtonRect = buttonRect;
        backButtonGraphic = buttonGraphic;
    }


    public void ReturnToMainMenu()
    {
        if (isLeaving)
            return;

        isLeaving = true;
        bool started = SceneFadeTransition.Begin(LoadMainMenu, 0.35f);
        if (!started)
            isLeaving = false;
    }


    public static bool OpenRuntimeScene(GameObject scenePrefab)
    {
        if (scenePrefab == null)
        {
            Debug.LogError("CreditsSceneController: credits scene prefab is missing.");
            return false;
        }

        Scene currentScene = SceneManager.GetActiveScene();
        Scene existingCredits = SceneManager.GetSceneByName(RuntimeSceneName);
        if (existingCredits.IsValid() && existingCredits.isLoaded)
            return false;

        Scene creditsScene = SceneManager.CreateScene(RuntimeSceneName);
        GameObject sceneRoot = Instantiate(scenePrefab);
        SceneManager.MoveGameObjectToScene(sceneRoot, creditsScene);

        // The previous scene is hidden before the new root is enabled so there is
        // never a live frame with two cameras, AudioListeners, or EventSystems.
        foreach (GameObject root in currentScene.GetRootGameObjects())
            root.SetActive(false);

        SceneManager.SetActiveScene(creditsScene);
        sceneRoot.SetActive(true);

        if (currentScene.IsValid() && currentScene.isLoaded)
            SceneManager.UnloadSceneAsync(currentScene);

        return true;
    }


    private void SetCameraPosition(Vector3 markerPosition)
    {
        Vector3 cameraPosition = scrollingCamera.transform.position;
        cameraPosition.x = markerPosition.x;
        cameraPosition.y = markerPosition.y;
        scrollingCamera.transform.position = cameraPosition;
    }


    private void UpdatePointerControls()
    {
        Vector2 pointerPosition = default;
        bool hasPointer = false;
        bool pressed = false;

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            pointerPosition = mouse.position.ReadValue();
            hasPointer = true;
            pressed = mouse.leftButton.wasPressedThisFrame;
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
        {
            pointerPosition = touchscreen.primaryTouch.position.ReadValue();
            hasPointer = true;
            pressed |= touchscreen.primaryTouch.press.wasPressedThisFrame;
        }

        bool hovered = false;
        if (hasPointer)
        {
            hovered = backButtonRect != null
                ? RectTransformUtility.RectangleContainsScreenPoint(backButtonRect, pointerPosition)
                : pointerPosition.x >= Screen.width * 0.82f &&
                  pointerPosition.y >= Screen.height * 0.88f;
        }
        if (backButtonGraphic != null)
            backButtonGraphic.color = hovered ? backButtonHoverColor : backButtonNormalColor;

        if (hovered && pressed)
            ReturnToMainMenu();
    }


    private static void LoadMainMenu()
    {
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadMainMenu();
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
