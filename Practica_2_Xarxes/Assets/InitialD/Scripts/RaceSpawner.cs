using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RaceSpawner : MonoBehaviour
{
    // Inspector
    [SerializeField] private GameObject carPrefab;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private Transform[] spawnPoints;

    // Test mode
    [SerializeField] private bool testMode = false;
    [SerializeField] private int testCarCount = 12;

    // Client and local player info
    [SerializeField] private TMPro.TMP_Text countdownText;
    [SerializeField] private GameObject controlsTutorialPanel;

    // Host Migration UI
    [SerializeField] private GameObject migrationBWOverlay;
    [SerializeField] private TMPro.TMP_Text migrationStatusText;
    [SerializeField] private TMPro.TMP_Text playersHUDText;

    private NewSocketsUDPClient client;
    private int localPlayerId = -1;

    private GameObject localCar;
    private PrometeoCarController localController;

    // For local scripts & UI
    public GameObject LocalCar => localCar;
    public bool CanRestartRace => !testMode && NewSocketsUDPClient.Instance != null && NewSocketsUDPClient.Instance.isHost;

    // Cars
    private Dictionary<int, GameObject> cars = new Dictionary<int, GameObject>();

    // Target transforms for jitter removal
    private Dictionary<int, Vector3> targetPositions = new Dictionary<int, Vector3>();
    private Dictionary<int, Quaternion> targetRotations = new Dictionary<int, Quaternion>();

    private float sendTimer = 0f;
    private bool raceStarted = false;
    private bool countdownStarted = false;
    private bool tutorialShown = false;

    void Start()
    {
        testMode = testMode || MainMenuUI.StartInTestMode;
        MainMenuUI.StartInTestMode = false;

        client = NewSocketsUDPClient.Instance ?? FindFirstObjectByType<NewSocketsUDPClient>();

        if (testMode && countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }

        if (migrationBWOverlay != null) migrationBWOverlay.SetActive(false);
        if (migrationStatusText != null) migrationStatusText.gameObject.SetActive(false);
        RefreshPlayersHUD();

        if ((!testMode && client == null) || carPrefab == null || cameraFollow == null)
        {
            Debug.LogError("[SPAWN] Assign the car prefab, spawn point and camera");
            return;
        }

        int[] playerIds;
        localPlayerId = client != null ? client.GetLocalPlayerId() : -1;

        if (testMode)
        {
            if (localPlayerId < 0)
            {
                localPlayerId = 0;
            }

            playerIds = CreateTestPlayerIds();
            Debug.Log("[SPAWN] TEST MODE: Spawning " + playerIds.Length + " cars");
        }
        else
        {
            playerIds = client.GetRacePlayerIds();
        }

        if (spawnPoints == null || spawnPoints.Length < playerIds.Length)
        {
            Debug.LogError("[SPAWN] Not enough spawn points");
            return;
        }

        SpawnCars(playerIds);

        if (!testMode && client != null && client.isHost)
        {
            StartCoroutine(TriggerServerCountdownDelay());
        }
    }

    int[] CreateTestPlayerIds()
    {
        int count = Mathf.Clamp(testCarCount, 1, 12);
        int[] testIds = new int[count];

        for (int i = 0; i < count; i++)
        {
            testIds[i] = i;
        }

        if (localPlayerId >= 0)
        {
            int localSkinIndex = localPlayerId % count;
            testIds[localSkinIndex] = localPlayerId;
        }

        return testIds;
    }

    IEnumerator TriggerServerCountdownDelay()
    {
        yield return new WaitForSeconds(0.5f);
        while (!countdownStarted && client != null && client.IsRunning())
        {
            client.SendString("READY_FOR_COUNTDOWN:");
            yield return new WaitForSeconds(0.5f);
        }
    }

    void OnEnable()
    {
        NewSocketsUDPClient.OnCarTransformReceived += UpdateRemoteCar;
        NewSocketsUDPClient.OnCarDestroyed += HandleCarDestroyed;
        NewSocketsUDPClient.OnRaceRestarted += HandleRaceRestart;
        NewSocketsUDPClient.OnFinishCountdown += ShowFinishCountdown;
        NewSocketsUDPClient.OnStartCountdownReceived += HandleStartCountdown;
        NewSocketsUDPClient.OnHostMigrationStarted += HandleHostMigrationStarted;
        NewSocketsUDPClient.OnPlayersListUpdated += RefreshPlayersHUD;
    }

    void OnDisable()
    {
        NewSocketsUDPClient.OnCarTransformReceived -= UpdateRemoteCar;
        NewSocketsUDPClient.OnCarDestroyed -= HandleCarDestroyed;
        NewSocketsUDPClient.OnRaceRestarted -= HandleRaceRestart;
        NewSocketsUDPClient.OnFinishCountdown -= ShowFinishCountdown;
        NewSocketsUDPClient.OnStartCountdownReceived -= HandleStartCountdown;
        NewSocketsUDPClient.OnHostMigrationStarted -= HandleHostMigrationStarted;
        NewSocketsUDPClient.OnPlayersListUpdated -= RefreshPlayersHUD;
    }

    void OnDestroy()
    {
        foreach (KeyValuePair<int, GameObject> kvp in cars)
        {
            if (kvp.Value != null)
            {
                if (client != null && client.LinkingContext != null)
                {
                    client.LinkingContext.Unregister(kvp.Key);
                }
                Destroy(kvp.Value);
            }
        }

        cars.Clear();
        targetPositions.Clear();
        targetRotations.Clear();
    }

    void Update()
    {
        if (!testMode)
        {
            SendLocalCarState();
        }

        SmoothRemoteCars();
    }

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

    void SpawnCars(int[] playerIds)
    {
        for (int i = 0; i < playerIds.Length; i++)
        {
            int pId = playerIds[i];
            GameObject spawnedCar = Instantiate(carPrefab, spawnPoints[i].position, spawnPoints[i].rotation);

            if (client != null && client.LinkingContext != null)
            {
                client.LinkingContext.Register(spawnedCar, pId);
            }

            CarNetObject carNet = spawnedCar.GetComponent<CarNetObject>();
            if (carNet != null)
            {
                carNet.id = pId;
                carNet.skinId = pId;
            }

            PrometeoSkinManager skinManager = spawnedCar.GetComponent<PrometeoSkinManager>();
            if (skinManager != null)
            {
                skinManager.ApplySkin(pId);
            }

            spawnedCar.name = "Car_" + pId;
            cars.Add(pId, spawnedCar);
            targetPositions[pId] = spawnPoints[i].position;
            targetRotations[pId] = spawnPoints[i].rotation;

            bool isLocalPlayer = (pId == localPlayerId);
            PrometeoCarController controller = spawnedCar.GetComponent<PrometeoCarController>();
            Rigidbody carRb = spawnedCar.GetComponent<Rigidbody>();

            if (controller == null || carRb == null)
            {
                Debug.LogError("[SPAWN] Controller or Rigidbody not found on the car");
                return;
            }

            if (isLocalPlayer)
            {
                localCar = spawnedCar;
                localController = controller;
                localController.enabled = testMode;

                cameraFollow.target = spawnedCar.transform;
                cameraFollow.targetRb = carRb;
            }
            else
            {
                controller.enabled = false;
                carRb.isKinematic = true;
            }
        }
    }

    void HandleCarDestroyed(int carId)
    {
        cars.Remove(carId);
        targetPositions.Remove(carId);
        targetRotations.Remove(carId);
        RefreshPlayersHUD();
    }

    void RefreshPlayersHUD()
    {
        if (playersHUDText != null && client != null)
        {
            playersHUDText.text = client.GetPlayerNames();
        }
    }

    void HandleHostMigrationStarted(string newHostName, bool iAmNewHost, bool hostLeftVoluntarily)
    {
        if (testMode) return;

        if (controlsTutorialPanel != null) controlsTutorialPanel.SetActive(false);

        StopAllCoroutines();
        raceStarted = false;

        if (localController != null)
        {
            localController.ResetCarForces();
            localController.enabled = false;
        }

        if (localCar != null)
        {
            Rigidbody rb = localCar.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }

        ClearCarEffects();

        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (migrationBWOverlay != null) migrationBWOverlay.SetActive(true);

        RefreshPlayersHUD();
        StartCoroutine(MigrationTextStagesRoutine(newHostName, iAmNewHost, hostLeftVoluntarily));
    }

    IEnumerator MigrationTextStagesRoutine(string newHostName, bool iAmNewHost, bool hostLeftVoluntarily)
    {
        if (migrationStatusText != null)
        {
            migrationStatusText.gameObject.SetActive(true);
            migrationStatusText.text = hostLeftVoluntarily
                ? "HOST LEFT THE RACE\nSEARCHING FOR NEW HOST..."
                : "HOST CONNECTION LOST\nSEARCHING FOR NEW HOST...";
        }

        yield return new WaitForSecondsRealtime(2.5f);

        if (migrationStatusText != null)
        {
            migrationStatusText.text = iAmNewHost
                ? "NEW HOST ASSIGNED\nYOU ARE THE NEW HOST!"
                : "NEW HOST ASSIGNED\nNEW HOST: " + newHostName;
        }
    }

    void ClearCarEffects()
    {
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
    }

    void HandleStartCountdown()
    {
        if (testMode) return;

        countdownStarted = true;
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

        ClearCarEffects();

        if (migrationBWOverlay != null && migrationBWOverlay.activeSelf && migrationStatusText != null)
        {
            migrationStatusText.gameObject.SetActive(true);
            migrationStatusText.text = "RESUMING RACE...";
        }

        // CONTROLS TUTORIAL
        // CONTROLS TUTORIAL
        if (!tutorialShown && controlsTutorialPanel != null)
        {
            tutorialShown = true;
            controlsTutorialPanel.SetActive(true);
            yield return new WaitForSeconds(5f);
            controlsTutorialPanel.SetActive(false);
        }

        // START COUNTDOWN
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

        if (migrationBWOverlay != null) migrationBWOverlay.SetActive(false);
        if (migrationStatusText != null) migrationStatusText.gameObject.SetActive(false);

        raceStarted = true;

        if (localCar != null)
        {
            Rigidbody rb = localCar.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;
        }

        if (localController != null)
        {
            localController.enabled = true;
        }

        RefreshPlayersHUD();

        yield return new WaitForSeconds(1f);

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }
    }

    public void OnClickHostRestart()
    {
        if (testMode) return;
        if (client == null || !client.isHost) return;

        client.SendString("RESTART_RACE:");
    }

    public void OnClickExitRace()
    {
        if (testMode)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            return;
        }

        if (client == null) return;

        if (client.isHost)
        {
            NewSocketsUDPServer server = NewSocketsUDPServer.Instance;
            if (server != null && server.IsRunning())
            {
                server.NotifyHostLeavingAndStop(client.userName);
            }
            client.isHost = false;
            client.DisconnectWithoutLeave();
        }
        else
        {
            client.Disconnect();
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(client.lobbySceneName);
    }

    void HandleRaceRestart()
    {
        if (testMode) return;

        if (controlsTutorialPanel != null) controlsTutorialPanel.SetActive(false);

        StopAllCoroutines();
        raceStarted = false;

        if (localController != null)
        {
            localController.enabled = false;
        }

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
            CarNetObject carNet = localCar.GetComponent<CarNetObject>();
            int currentSkin = carNet != null ? carNet.skinId : localPlayerId;

            client.SendCarTransform(localPlayerId, localCar.transform.position, localCar.transform.eulerAngles, false, currentSkin);
        }
    }

    void SendLocalCarState()
    {
        if (client == null || !client.IsRunning() || client.IsMigrating()) return;
        if (localCar == null || localController == null) return;

        sendTimer += Time.deltaTime;
        if (sendTimer >= 0.05f)
        {
            sendTimer = 0.0f;

            CarNetObject carNet = localCar.GetComponent<CarNetObject>();
            int currentSkin = carNet != null ? carNet.skinId : localPlayerId;

            client.SendCarTransform(localPlayerId, localCar.transform.position, localCar.transform.eulerAngles, localController.isDrifting, currentSkin);
        }
    }

    void UpdateRemoteCar(UDPPlayer.CarNetworkState state)
    {
        if (testMode) return;
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

            // Sync skin cars remote
            CarNetObject carNet = remoteCar.GetComponent<CarNetObject>();
            if (carNet != null && carNet.skinId != state.skinId)
            {
                carNet.skinId = state.skinId;
                PrometeoSkinManager skinManager = remoteCar.GetComponent<PrometeoSkinManager>();
                if (skinManager != null)
                {
                    skinManager.ApplySkin(state.skinId);
                }
            }

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
        if (testMode) return;
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