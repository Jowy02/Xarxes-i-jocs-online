using UnityEngine;
using TMPro;

public class LocalSpeedometerUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text speedText;

    [Header("Settings")]
    [SerializeField] private float updateInterval = 0.05f;
    [SerializeField] private string speedUnit = "KM/H";

    private Rigidbody localCarRb;
    private float updateTimer = 0f;

    private void Awake()
    {
        if (speedText != null)
        {
            speedText.text = "0 " + speedUnit;
        }
    }

    private void Start()
    {
        // Find the RaceSpawner in the local scene
        RaceSpawner spawner = FindFirstObjectByType<RaceSpawner>();

        if (spawner == null || spawner.LocalCar == null)
        {
            Debug.LogWarning("[SPEEDOMETER UI] Local car not found.");
            return;
        }

        localCarRb = spawner.LocalCar.GetComponent<Rigidbody>();

        if (localCarRb == null)
        {
            Debug.LogWarning("[SPEEDOMETER UI] Rigidbody not found.");
        }
    }

    private void Update()
    {
        if (localCarRb == null || speedText == null)
            return;

        updateTimer += Time.deltaTime;

        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;

            float speed = localCarRb.linearVelocity.magnitude * 3.6f;

            speedText.text = Mathf.RoundToInt(speed) + " " + speedUnit;
        }
    }
}