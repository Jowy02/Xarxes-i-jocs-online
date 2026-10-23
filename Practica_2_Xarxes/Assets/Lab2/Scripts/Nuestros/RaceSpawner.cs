using UnityEngine;
using System.Collections.Generic;

public class RaceSpawner : MonoBehaviour
{
    // Ispector
    [SerializeField] private GameObject carPrefab;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private Transform[] spawnPoints;

    // Client and local player info
    private NewSocketsUDPClient client;
    private int localPlayerId;

    private GameObject localCar;
    private PrometeoCarController localController;

    // Cars
    private Dictionary<int, GameObject> cars = new Dictionary<int, GameObject>();

    private float sendTimer = 0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        TestSerializer();

        client = FindFirstObjectByType<NewSocketsUDPClient>();

        if (client == null || carPrefab == null || cameraFollow == null)
        {
            Debug.LogError("[SPAWN] Assign the car prefab, spawn point and camera");
            return;
        }

        int[] playerIds = client.GetRacePlayerIds();
        localPlayerId = client.GetLocalPlayerId();

        // Check if we have enough spawn points
        for (int i = 0; i < playerIds.Length; i++)
        {
            if (spawnPoints[i] == null)
            {
                Debug.LogError("[SPAWN] A spawn point is not assigned");
                return;
            }
        }

        if (spawnPoints == null || spawnPoints.Length < playerIds.Length)
        {
            Debug.LogError("[SPAWN] Not enough spawn points");
            return;
        }

        SpawnCars(playerIds);
    }

    // Update is called once per frame
    void Update()
    {
        SendLocalCarState();
    }
    void OnEnable()
    {
        NewSocketsUDPClient.OnCarTransformReceived += UpdateRemoteCar;
    }

    void OnDisable()
    {
        NewSocketsUDPClient.OnCarTransformReceived -= UpdateRemoteCar;
    }

    // Car creation
    void SpawnCars(int[] playerIds)
    {
        for (int i = 0; i < playerIds.Length; i++)
        {
            GameObject spawnedCar = Instantiate(carPrefab, spawnPoints[i].position, spawnPoints[i].rotation);

            spawnedCar.name = "Car_" + playerIds[i];
            cars.Add(playerIds[i], spawnedCar);

            // Revise which car is the local player
            bool isLocalPlayer = playerIds[i] == localPlayerId;

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
                controller.enabled = true;
            }
            else
            {
                controller.enabled = false;
            }

            if (isLocalPlayer)
            {
                localCar = spawnedCar;
                localController = controller;

                cameraFollow.target = spawnedCar.transform;
                cameraFollow.targetRb = carRb;
            }
            else
            {
                // Temporal - For test
                carRb.isKinematic = true;
            }
        }
    }

    void SendLocalCarState()
    {
        if (client == null || !client.IsRunning())
        {
            return;
        }

        if (localCar == null || localController == null)
        {
            return;
        }

        sendTimer += Time.deltaTime;

        if (sendTimer >= 0.05f)
        {
            sendTimer = 0.00f;

            client.SendCarTransform(localPlayerId, localCar.transform.position, localCar.transform.eulerAngles, localController.isDrifting);
        }
    }

    void UpdateRemoteCar(UDPPlayer.CarNetworkState state)
    {
        if (client == null || !client.IsRunning())
        {
            return;
        }

        if (state.objectId == localPlayerId)
        {
            return;
        }

        GameObject remoteCar;

        if (cars.TryGetValue(state.objectId, out remoteCar))
        {
            remoteCar.transform.position = state.position;
            remoteCar.transform.rotation = Quaternion.Euler(state.rotation);
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

        // Check length, exactly 30 bytes
        Debug.Log("[TEST] Serialized packet length: " + bytes.Length + " bytes");

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
