using System;
using System.Collections.Generic;
using UnityEngine;

public class LapManager : MonoBehaviour
{
    [Header("Lap Settings")]
    [SerializeField] private int totalLaps = 3;

    // HUD notification
    public event Action<int, int> OnLapChanged;

    public int CurrentLap => Mathf.Min(currentLap, totalLaps);
    public int TotalLaps => totalLaps;

    private readonly List<GameObject> checkpoints = new List<GameObject>();
    private readonly List<GameObject> checkpointsTaken = new List<GameObject>();

    private int currentLap = 1;
    private float lapStartTime;
    private bool isFinished = false;
    private float pausedAtTime = -1f;

    private NewSocketsUDPClient client;

    void Start()
    {
        client = NewSocketsUDPClient.Instance;

        GameObject.FindGameObjectsWithTag("Checkpoint", checkpoints);

        lapStartTime = Time.time;

        OnLapChanged?.Invoke(CurrentLap, totalLaps);
    }

    void OnEnable()
    {
        NewSocketsUDPClient.OnRaceRestarted += RestartLaps;
        NewSocketsUDPClient.OnHostMigrationStarted += PauseLapTimer;
        NewSocketsUDPClient.OnStartCountdownReceived += ResumeLapTimer;
    }

    void OnDisable()
    {
        NewSocketsUDPClient.OnRaceRestarted -= RestartLaps;
        NewSocketsUDPClient.OnHostMigrationStarted -= PauseLapTimer;
        NewSocketsUDPClient.OnStartCountdownReceived -= ResumeLapTimer;
    }

    void PauseLapTimer(string _, bool __, bool ___)
    {
        if (pausedAtTime < 0f) pausedAtTime = Time.time;
    }

    void ResumeLapTimer()
    {
        if (pausedAtTime >= 0f)
        {
            lapStartTime += (Time.time - pausedAtTime);
            pausedAtTime = -1f;
        }
    }

    public void RestartLaps()
    {
        currentLap = 1;
        checkpointsTaken.Clear();
        isFinished = false;
        lapStartTime = Time.time;

        OnLapChanged?.Invoke(CurrentLap, totalLaps);
    }

    private void OnTriggerEnter(Collider collision)
    {
        PrometeoCarController controller = GetComponent<PrometeoCarController>();

        // Only the locally controlled car counts laps
        if (controller != null && !controller.enabled) return;

        if (collision == null || checkpointsTaken.Contains(collision.gameObject)) return;

        if (checkpoints.Contains(collision.gameObject))
        {
            checkpointsTaken.Add(collision.gameObject);

            if (checkpointsTaken.Count >= checkpoints.Count)
            {
                currentLap++;

                // Update local HUD
                OnLapChanged?.Invoke(CurrentLap, totalLaps);

                if (client != null && !isFinished)
                {
                    client.SendString("LAPTIME:" +
                        (Time.time - lapStartTime).ToString(
                            System.Globalization.CultureInfo.InvariantCulture));

                    lapStartTime = Time.time;
                }

                if (currentLap > totalLaps)
                {
                    if (!isFinished)
                    {
                        isFinished = true;

                        if (client != null)
                            client.SendString("LAP:" + client.userName);
                    }
                }
                else
                {
                    if (client != null)
                    {
                        client.SendString(
                            "CHAT:" + client.userName +
                            ": completed lap " + (currentLap - 1) +
                            "/" + totalLaps);
                    }
                }

                checkpointsTaken.Clear();
            }
        }
    }
}