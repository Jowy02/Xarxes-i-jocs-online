using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RaceSpawner : MonoBehaviour
{
    // Ispector
    [SerializeField] private GameObject carPrefab;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private Transform[] spawnPoints;

    // Client and local player info
    [SerializeField] private TMPro.TMP_Text countdownText;
    [SerializeField] private GameObject hostRestartButton;

    private NewSocketsUDPClient client;
    private int localPlayerId = -1;

    private GameObject localCar;
    private PrometeoCarController localController;

    //For local Scrips
    public GameObject LocalCar => localCar;

    // Cars
    private Dictionary<int, GameObject> cars = new Dictionary<int, GameObject>();

    // Target transforms for jitter removal
    private Dictionary<int, Vector3> targetPositions = new Dictionary<int, Vector3>();
    private Dictionary<int, Quaternion> targetRotations = new Dictionary<int, Quaternion>();

    private float sendTimer = 0f;
    private bool raceStarted = false;

    void Start()
    {

        client = FindFirstObjectByType<NewSocketsUDPClient>();

        if (client == null || carPrefab == null || cameraFollow == null)
        {
            Debug.LogError("[SPAWN] Assign the car prefab, spawn point and camera");
            return;
        }

        // Show host restart button only for the host
        if (hostRestartButton != null)
        {
            hostRestartButton.SetActive(client.isHost);
        }

        int[] playerIds = client.GetRacePlayerIds();
        localPlayerId = client.GetLocalPlayerId();

        // Check if we have enough spawn points
        if (spawnPoints == null || spawnPoints.Length < playerIds.Length)
        {
            Debug.LogError("[SPAWN] Not enough spawn points");
            return;
        }

        SpawnCars(playerIds);

        // Tell the server we are ready for the starting countdown (host triggers it)
        if (client.isHost)
        {
            StartCoroutine(TriggerServerCountdownDelay());
        }
    }

    IEnumerator TriggerServerCountdownDelay()
    {
        yield return new WaitForSeconds(0.5f); // Short buffer to ensure all scenes loaded
        if (client != null && client.IsRunning())
        {
            client.SendString("READY_FOR_COUNTDOWN:");
        }
    }

    void OnEnable()
    {
        NewSocketsUDPClient.OnCarTransformReceived += UpdateRemoteCar;
        NewSocketsUDPClient.OnRaceRestarted += HandleRaceRestart;
        NewSocketsUDPClient.OnFinishCountdown += ShowFinishCountdown;
        NewSocketsUDPClient.OnStartCountdownReceived += HandleStartCountdown;
    }

    void OnDisable()
    {
        NewSocketsUDPClient.OnCarTransformReceived -= UpdateRemoteCar;
        NewSocketsUDPClient.OnRaceRestarted -= HandleRaceRestart;
        NewSocketsUDPClient.OnFinishCountdown -= ShowFinishCountdown;  
        NewSocketsUDPClient.OnStartCountdownReceived -= HandleStartCountdown;
    }

    void OnDestroy()
    {
        // Cleanup all instantiated cars from scene
        foreach (KeyValuePair<int, GameObject> kvp in cars)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }

        cars.Clear();
        targetPositions.Clear();
        targetRotations.Clear();
    }

    void Update()
    {
        SendLocalCarState();
        
        SmoothRemoteCars();
    }

    // Smooth remote cars removes 20Hz network jitter
    void SmoothRemoteCars()
    {
        foreach (KeyValuePair<int, GameObject> entry in cars)
        {
            int carId = entry.Key;
            if (carId == localPlayerId) continue;

            GameObject remoteCar = entry.Value;
            if (remoteCar == null) continue;

            if (targetPositions.ContainsKey(carId))
            {
                remoteCar.transform.position = Vector3.Lerp(remoteCar.transform.position, targetPositions[carId], Time.deltaTime * 15f);
            }

            if (targetRotations.ContainsKey(carId))
            {
                remoteCar.transform.rotation = Quaternion.Slerp(remoteCar.transform.rotation, targetRotations[carId], Time.deltaTime * 15f);
            }
        }
    }

    // Car creation
    void SpawnCars(int[] playerIds)
    {
        for (int i = 0; i < playerIds.Length; i++)
        {
            int pId = playerIds[i];
            GameObject spawnedCar = Instantiate(carPrefab, spawnPoints[i].position, spawnPoints[i].rotation);

            spawnedCar.name = "Car_" + pId;
            cars.Add(pId, spawnedCar);
            targetPositions[pId] = spawnPoints[i].position;
            targetRotations[pId] = spawnPoints[i].rotation;

            bool isLocalPlayer = (pId == localPlayerId);
            PrometeoCarController controller = spawnedCar.GetComponent<PrometeoCarController>();
            Rigidbody carRb = spawnedCar.GetComponent<Rigidbody>();

            if (controller == null || carRb == null)
            {
                Debug.LogError("[SPAWN] Controller or Rigidbody not found on the Car");
                return;
            }

            // Only enable for the local player
            if (isLocalPlayer)
            {
                localCar = spawnedCar;
                localController = controller;
                localController.enabled = false; // Block until countdown ends

                cameraFollow.target = spawnedCar.transform;
                cameraFollow.targetRb = carRb;
            }
            else
            {
                controller.enabled = false;
                carRb.isKinematic = true; // Allows local car to collide against remote car as a solid body
            }
        }
    }

    // 5 Seconds countdown timer
    void HandleStartCountdown()
    {
        StopAllCoroutines();
        StartCoroutine(StartCountdownRoutine());
    }

    IEnumerator StartCountdownRoutine()
    {
        raceStarted = false;

        if (localController != null)
        {
            localController.ResetCarForces();
            localController.enabled = false;
        }

        foreach (KeyValuePair<int, GameObject> entry in cars)
        {
            if (entry.Value != null)
            {
                PrometeoCarController pController = entry.Value.GetComponent<PrometeoCarController>();
                if (pController != null)
                {
                    pController.ResetCarForces();
                }

                ParticleSystem[] parts = entry.Value.GetComponentsInChildren<ParticleSystem>();
                foreach (ParticleSystem ps in parts)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Clear(true);
                }

                TrailRenderer[] trails = entry.Value.GetComponentsInChildren<TrailRenderer>();
                foreach (TrailRenderer tr in trails)
                {
                    tr.emitting = false;
                    tr.Clear();
                }
            }
        }

        int count = 5;

        while (count > 0)
        {
            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(true);
                countdownText.text = count.ToString();
            }

            yield return new WaitForSeconds(1f);
            count--;
        }

        if (countdownText != null)
        {
            countdownText.text = "GO!";
        }

        raceStarted = true;

        if (localController != null)
        {
            localController.enabled = true;
        }

        yield return new WaitForSeconds(1f);

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }
    }

    public void OnClickHostRestart()
    {
        if (client == null || !client.isHost) return;

        client.SendString("RESTART_RACE:");
    }

    void HandleRaceRestart()
    {
        StopAllCoroutines();
        raceStarted = false;

        if (localController != null)
        {
            localController.enabled = false;
        }

        // Reposition all cars to starting grid
        int[] playerIds = client.GetRacePlayerIds();

        for (int i = 0; i < playerIds.Length; i++)
        {
            int pId = playerIds[i];

            if (cars.TryGetValue(pId, out GameObject car) && car != null)
            {
                car.transform.position = spawnPoints[i].position;
                car.transform.rotation = spawnPoints[i].rotation;
                targetPositions[pId] = spawnPoints[i].position;
                targetRotations[pId] = spawnPoints[i].rotation;

                PrometeoCarController controller = car.GetComponent<PrometeoCarController>();
                if (controller != null)
                {
                    controller.ResetCarForces();
                }
            }
        }
        if (localCar != null && client != null && client.IsRunning())
        {
            client.SendCarTransform(localPlayerId, localCar.transform.position, localCar.transform.eulerAngles, false);
        }
    }

    void SendLocalCarState()
    {
        if (client == null || !client.IsRunning()) return;
        if (localCar == null || localController == null) return;

        sendTimer += Time.deltaTime;

        if (sendTimer >= 0.05f) // 20 times per second
        {
            sendTimer = 0.0f;
            client.SendCarTransform(localPlayerId, localCar.transform.position, localCar.transform.eulerAngles, localController.isDrifting);
        }
    }

    void UpdateRemoteCar(UDPPlayer.CarNetworkState state)
    {
        if (client == null || !client.IsRunning()) return;
        if (state.objectId == localPlayerId) return;

        GameObject remoteCar;

        if (cars.TryGetValue(state.objectId, out remoteCar) && remoteCar != null)
        {
            PrometeoCarController remoteController = remoteCar.GetComponent<PrometeoCarController>();
            float distance = Vector3.Distance(remoteCar.transform.position, state.position);
            if (distance > 8f)
            {
                remoteCar.transform.position = state.position;
                remoteCar.transform.rotation = Quaternion.Euler(state.rotation);
                targetPositions[state.objectId] = state.position;
                targetRotations[state.objectId] = Quaternion.Euler(state.rotation);

                if (remoteController != null)
                {
                    remoteController.ResetCarForces();
                }
                return;
            }

            targetPositions[state.objectId] = state.position;
            targetRotations[state.objectId] = Quaternion.Euler(state.rotation);

            if (remoteController != null && remoteController.useEffects)
            {
                if (!raceStarted)
                {
                    remoteController.isDrifting = false;
                    remoteController.isTractionLocked = false;

                    if (remoteController.RLWParticleSystem != null) remoteController.RLWParticleSystem.Clear(true);
                    if (remoteController.RRWParticleSystem != null) remoteController.RRWParticleSystem.Clear(true);
                    if (remoteController.RLWTireSkid != null) remoteController.RLWTireSkid.Clear();
                    if (remoteController.RRWTireSkid != null) remoteController.RRWTireSkid.Clear();
                }
                else
                {
                    remoteController.isDrifting = state.isDrifting;
                    remoteController.isTractionLocked = state.isDrifting;
                    remoteController.DriftCarPS();
                }
            }
        }
    }

    void ShowFinishCountdown(int secs)
    {
        StartCoroutine(FinishCountdownRoutine(secs));
    }

    IEnumerator FinishCountdownRoutine(int secs)
    {
        if (countdownText == null) yield break;
        countdownText.gameObject.SetActive(true);
        while (secs > 0)
        {
            countdownText.text = secs.ToString();
            yield return new WaitForSeconds(1f);
            secs--;
        }
    }
}