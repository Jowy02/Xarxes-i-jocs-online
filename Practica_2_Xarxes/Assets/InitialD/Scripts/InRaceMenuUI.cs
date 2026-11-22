using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class InRaceMenuUI : MonoBehaviour
{
    [Header("Menu")]
    [SerializeField] private RectTransform menuPanel;
    [SerializeField] private float animationSpeed = 10f;

    [Header("Buttons")]
    [SerializeField] private Button restartRaceButton;

    private RaceSpawner raceSpawner;
    private Vector2 openPosition;
    private Vector2 closedPosition;
    private bool isOpen = false;

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

        if (restartRaceButton != null)
        {
            restartRaceButton.gameObject.SetActive(
                raceSpawner != null && raceSpawner.CanRestartRace
            );

            restartRaceButton.onClick.AddListener(RestartRace);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleMenu();
        }

        AnimateMenu();
    }

    public void ToggleMenu()
    {
        isOpen = !isOpen;

        if (restartRaceButton != null)
        {
            restartRaceButton.gameObject.SetActive(
                raceSpawner != null && raceSpawner.CanRestartRace
            );
        }
    }

    private void AnimateMenu()
    {
        if (menuPanel == null) return;

        Vector2 targetPosition = isOpen ? openPosition : closedPosition;

        menuPanel.anchoredPosition = Vector2.Lerp(
            menuPanel.anchoredPosition,
            targetPosition,
            1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime)
        );
    }

    private void RestartRace()
    {
        if (raceSpawner == null || !raceSpawner.CanRestartRace)
            return;

        raceSpawner.OnClickHostRestart();
        isOpen = false;
    }

    void OnDestroy()
    {
        if (restartRaceButton != null)
        {
            restartRaceButton.onClick.RemoveListener(RestartRace);
        }
    }
}