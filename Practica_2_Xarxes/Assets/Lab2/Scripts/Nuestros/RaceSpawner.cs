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

    // Cars
    private Dictionary<int, GameObject> cars = new Dictionary<int, GameObject>();

    // Target transforms for jitter removal
    private Dictionary<int, Vector3> targetPositions = new Dictionary<int, Vector3>();
    private Dictionary<int, Quaternion> targetRotations = new Dictionary<int, Quaternion>();

    private float sendTimer = 0f;
    private bool raceStarted = false;

    void Start()
    {
        TestSerializer();

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
        StartCoroutine(StartCountdownRoutine());
    }

    void OnEnable()
    {
        NewSocketsUDPClient.OnCarTransformReceived += UpdateRemoteCar;
        NewSocketsUDPClient.OnRaceRestarted += HandleRaceRestart;
    }

    void OnDisable()
    {
        NewSocketsUDPClient.OnCarTransformReceived -= UpdateRemoteCar;
        NewSocketsUDPClient.OnRaceRestarted -= HandleRaceRestart;
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
        if (raceStarted)
        {
            SendLocalCarState();
        }

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
    IEnumerator StartCountdownRoutine()
    {
        raceStarted = false;

        if (localController != null)
        {
            localController.enabled = false;
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

                Rigidbody rb = car.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        StartCoroutine(StartCountdownRoutine());
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
            // Store targets for interpolation
            targetPositions[state.objectId] = state.position;
            targetRotations[state.objectId] = Quaternion.Euler(state.rotation);

            // Trigger visual drift effects and tire marks on the remote car
            PrometeoCarController remoteController = remoteCar.GetComponent<PrometeoCarController>();

            if (remoteController != null && remoteController.useEffects)
            {
                remoteController.isDrifting = state.isDrifting;
                remoteController.isTractionLocked = state.isDrifting;
                remoteController.DriftCarPS();
            }
        }
    }

    // =============================================================================================
    // SERIALIZER TEST
    // =============================================================================================

    void TestSerializer()
    {
        Vector3 testPos = new Vector3(15.2f, 0.5f, -88.4f);
        Vector3 testRot = new Vector3(12.5f, 180.0f, -4.2f);
        bool testDrift = true;
        int testId = 1;

        // Serialize to binary
        byte[] bytes = NetworkSerializer.SerializeCar(testId, testPos, testRot, testDrift);

        // Deserialize back
        UDPPlayer.CarNetworkState result = NetworkSerializer.DeserializeCar(bytes);

        // Verify values
        bool pass = (result.objectId == testId) &&
                    (Vector3.Distance(result.position, testPos) < 0.001f) &&
                    (Vector3.Distance(result.rotation, testRot) < 0.001f) &&
                    (result.isDrifting == testDrift);

        if (pass && bytes.Length == 30)
        {
            Debug.Log("<color=green>[TEST] SERIALIZATION PASS! Exact 30 bytes in pure binary.</color>");
        }
        else
        {
            Debug.LogError("[TEST] SERIALIZATION FAIL!");
        }
    }
}