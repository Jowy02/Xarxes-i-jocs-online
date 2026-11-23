using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class InRaceMenuUI : MonoBehaviour
{
    // MENU
    [SerializeField] private RectTransform menuPanel;
    [SerializeField] private float animationSpeed = 10f;

    private Vector2 openPosition;
    private Vector2 closedPosition;
    private bool isOpen = false;

    // MENU BUTTONS
    [SerializeField] private Button restartRaceButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button exitRaceButton;

    // RACE
    private RaceSpawner raceSpawner;

    // START / UPDATE
    IEnumerator Start()
    {
        raceSpawner = FindFirstObjectByType<RaceSpawner>();

        if (menuPanel == null)
        {
            Debug.LogError("[RACE MENU] Menu panel is not assigned.");
            enabled = false;
            yield break;
        }

        Canvas.ForceUpdateCanvases();

        openPosition = menuPanel.anchoredPosition;
        closedPosition = openPosition + Vector2.right * menuPanel.rect.width;

        menuPanel.anchoredPosition = closedPosition;

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

        AnimateMenu();
    }

    // MENU UI
    public void ToggleMenu()
    {
        isOpen = !isOpen;
        UpdateRestartButton();
    }

    void AnimateMenu()
    {
        if (menuPanel == null) return;

        Vector2 targetPosition = isOpen ? openPosition : closedPosition;

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

    // RESTART RACE
    void RestartRace()
    {
        if (raceSpawner == null || !raceSpawner.CanRestartRace)
            return;

        raceSpawner.OnClickHostRestart();
        isOpen = false;
    }

    // EXIT RACE
    void ExitRace()
    {
        if (raceSpawner == null) return;

        isOpen = false;
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