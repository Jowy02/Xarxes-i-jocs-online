using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string lobbySceneName = "LobbyUDP";
    [SerializeField] private string raceSceneName = "RaceTrack";

    [Header("Logo Animation")]
    [SerializeField] private RectTransform logo;
    [SerializeField] private float slideDuration = 5f;
    [SerializeField] private float bounceStrength = 0.035f;

    public static bool StartInTestMode = false;

    private Vector3 targetPosition;
    private Vector3 startPosition;

    void Start()
    {
        if (logo == null)
        {
            Debug.LogWarning("[MAIN MENU] Logo reference is missing.");
            return;
        }

        RectTransform canvasRect = logo.parent as RectTransform;

        if (canvasRect == null)
        {
            Debug.LogError("[MAIN MENU] Logo must be a direct child of a UI RectTransform.");
            return;
        }

        Canvas.ForceUpdateCanvases();

        targetPosition = logo.localPosition;

        float leftEdge = canvasRect.rect.xMin;
        float logoHalfWidth = logo.rect.width * 0.5f;

        startPosition = new Vector3(
            leftEdge - logoHalfWidth,
            targetPosition.y,
            targetPosition.z
        );

        logo.localPosition = startPosition;

        StartCoroutine(PlayLogoAnimation());
    }

    IEnumerator PlayLogoAnimation()
    {
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / slideDuration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            float bounce = Mathf.Sin(t * Mathf.PI) * bounceStrength;

            logo.localPosition = Vector3.LerpUnclamped(
                startPosition,
                targetPosition,
                easedT + bounce
            );

            yield return null;
        }

        logo.localPosition = targetPosition;
    }

    public void OnClickTestCircuit()
    {
        StartInTestMode = true;
        SceneManager.LoadScene(raceSceneName);
    }

    public void OnClickOnlineRace()
    {
        StartInTestMode = false;
        SceneManager.LoadScene(lobbySceneName);
    }
}