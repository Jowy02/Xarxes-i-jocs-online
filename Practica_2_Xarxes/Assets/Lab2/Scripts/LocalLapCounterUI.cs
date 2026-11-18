using UnityEngine;
using TMPro;

public class LocalLapCounterUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text lapText;

    private LapManager localLapManager;

    private void Start()
    {
        TryConnectToLocalLapManager();
    }

    private void Update()
    {
        if (localLapManager == null)
        {
            TryConnectToLocalLapManager();
        }
    }

    private void TryConnectToLocalLapManager()
    {
        RaceSpawner spawner = FindFirstObjectByType<RaceSpawner>();

        if (spawner == null || spawner.LocalCar == null)
            return;

        LapManager lapManager =
            spawner.LocalCar.GetComponent<LapManager>();

        if (lapManager == null)
            return;

        localLapManager = lapManager;

        localLapManager.OnLapChanged += UpdateLapDisplay;

        UpdateLapDisplay(
            localLapManager.CurrentLap,
            localLapManager.TotalLaps
        );
    }

    private void UpdateLapDisplay(int currentLap, int totalLaps)
    {
        if (lapText == null)
            return;

        lapText.text = "LAP " + currentLap + " / " + totalLaps;
    }

    private void OnDestroy()
    {
        if (localLapManager != null)
        {
            localLapManager.OnLapChanged -= UpdateLapDisplay;
        }
    }
}