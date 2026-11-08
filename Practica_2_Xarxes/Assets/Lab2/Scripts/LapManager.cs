using System.Collections.Generic;
using UnityEngine;

public class LapManager : MonoBehaviour
{
    [Header("Lap Settings")]
    [SerializeField] private int totalLaps = 3;

    private readonly List<GameObject> checkpoints = new List<GameObject>();
    private readonly List<GameObject> checkpointsTaken = new List<GameObject>();
    private int currentLap = 1;
    private float lapStartTime;
    private bool isFinished = false;
    private NewSocketsUDPClient client;

    void Start()
    {
        client = NewSocketsUDPClient.Instance;
        GameObject.FindGameObjectsWithTag("Checkpoint", checkpoints);
        lapStartTime = Time.time;
    }

    void OnEnable()
    {
        NewSocketsUDPClient.OnRaceRestarted += RestartLaps;
    }

    void OnDisable()
    {
        NewSocketsUDPClient.OnRaceRestarted -= RestartLaps;
    }

    public void RestartLaps()
    {
        currentLap = 1;
        checkpointsTaken.Clear();
        isFinished = false;
        lapStartTime = Time.time;
    }

    private void OnTriggerEnter(Collider collision)
    {
        // Only run lap detection if this is the locally controlled car
        PrometeoCarController controller = GetComponent<PrometeoCarController>();
        if (controller != null && !controller.enabled) return;

        if (collision == null || checkpointsTaken.Contains(collision.gameObject)) return;

        if (checkpoints.Contains(collision.gameObject))
        {
            checkpointsTaken.Add(collision.gameObject);

            if (checkpointsTaken.Count >= checkpoints.Count)
            {
                currentLap++;

                if (client != null && !isFinished)
                {
                    client.SendString("LAPTIME:" + (Time.time - lapStartTime).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    lapStartTime = Time.time;
                }

                if (currentLap > totalLaps)
                {
                    if (!isFinished)
                    {
                        isFinished = true;
                        if (client != null) client.SendString("LAP:" + client.userName);
                    }
                }
                else
                {
                    if (client != null) client.SendString("CHAT:" + client.userName + ": completed lap " + (currentLap - 1) + "/" + totalLaps);
                }

                checkpointsTaken.Clear();
            }
        }
    }
}