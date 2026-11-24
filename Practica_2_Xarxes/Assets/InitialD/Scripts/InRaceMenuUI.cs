using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class InRaceMenuUI : MonoBehaviour
{
    // MENU
    [SerializeField] private RectTransform menuPanel;
    [SerializeField] private float animationSpeed = 10f;

    private Vector2 menuOpenPosition;
    private Vector2 menuClosedPosition;
    private bool isMenuOpen = false;

    // CHAT
    [SerializeField] private RectTransform chatPanel;
    [SerializeField] private RaceChatUI raceChatUI;

    private Vector2 chatOpenPosition;
    private Vector2 chatClosedPosition;
    private bool isChatOpen = false;

    // MENU BUTTONS
    [SerializeField] private Button restartRaceButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button exitRaceButton;

    // RACE
    private RaceSpawner raceSpawner;

    IEnumerator Start()
    {
        raceSpawner = FindFirstObjectByType<RaceSpawner>();

        if (menuPanel == null || chatPanel == null)
        {
            Debug.LogError("[RACE UI] Menu or chat panel is not assigned.");
            enabled = false;
            yield break;
        }

        if (raceChatUI == null)
            raceChatUI = FindFirstObjectByType<RaceChatUI>();

        Canvas.ForceUpdateCanvases();

        // MENU POSITIONS
        menuOpenPosition = menuPanel.anchoredPosition;

        menuClosedPosition =
            menuOpenPosition + Vector2.right * menuPanel.rect.width;

        menuPanel.anchoredPosition = menuClosedPosition;

        // CHAT POSITIONS
        chatOpenPosition = chatPanel.anchoredPosition;

        chatClosedPosition =
            chatOpenPosition + Vector2.left * chatPanel.rect.width;

        chatPanel.anchoredPosition = chatClosedPosition;

        yield return null;

        UpdateRestartButton();

        if (restartRaceButton != null)
            restartRaceButton.onClick.AddListener(RestartRace);

        if (settingsButton != null)
            settingsButton.interactable = false;

        if (controlsButton != null)
            controlsButton.interactable = false;

        if (exitRaceButton != null)
            exitRaceButton.onClick.AddListener(ExitRace);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
            ToggleMenu();

        if (Input.GetKeyDown(KeyCode.C))
            ToggleChat();

        AnimateMenu();
        AnimateChat();
    }

    // MENU UI
    public void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;

        UpdateRestartButton();
    }

    void AnimateMenu()
    {
        if (menuPanel == null) return;

        Vector2 targetPosition =
            isMenuOpen ? menuOpenPosition : menuClosedPosition;

        menuPanel.anchoredPosition = Vector2.Lerp(
            menuPanel.anchoredPosition,
            targetPosition,
            1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime)
        );
    }

    void UpdateRestartButton()
    {
        if (restartRaceButton == null) return;

        restartRaceButton.gameObject.SetActive(
            raceSpawner != null && raceSpawner.CanRestartRace
        );
    }

    // CHAT UI
    public void ToggleChat()
    {
        isChatOpen = !isChatOpen;

        if (raceChatUI != null)
            raceChatUI.SetChatInputActive(isChatOpen);
    }

    void AnimateChat()
    {
        if (chatPanel == null) return;

        Vector2 targetPosition =
            isChatOpen ? chatOpenPosition : chatClosedPosition;

        chatPanel.anchoredPosition = Vector2.Lerp(
            chatPanel.anchoredPosition,
            targetPosition,
            1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime)
        );
    }

    // RESTART RACE
    void RestartRace()
    {
        if (raceSpawner == null || !raceSpawner.CanRestartRace)
            return;

        raceSpawner.OnClickHostRestart();

        isMenuOpen = false;
    }

    // EXIT RACE
    void ExitRace()
    {
        if (raceSpawner == null) return;

        isMenuOpen = false;

        raceSpawner.OnClickExitRace();
    }

    // CLEANUP
    void OnDestroy()
    {
        if (restartRaceButton != null)
            restartRaceButton.onClick.RemoveListener(RestartRace);

        if (exitRaceButton != null)
            exitRaceButton.onClick.RemoveListener(ExitRace);
    }
}