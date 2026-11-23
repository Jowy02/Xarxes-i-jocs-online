using System.Collections;
using UnityEngine;

public class PickupSpawner : MonoBehaviour
{
    const float BoostDuration = 3f;
    const int BoostExtraSpeed = 40;
    const float BoostImpulse = 10f;

    [SerializeField] private GameObject boostPickupPrefab;
    [SerializeField] private RaceSpawner raceSpawner;

    private NewSocketsUDPClient client;

    private int activeBoostStacks = 0;

    void Start()
    {
        client = NewSocketsUDPClient.Instance;
        NewSocketsUDPClient.OnPickupCreated += SpawnPickup;
        NewSocketsUDPClient.OnBoostReceived += ApplyBoost;
        NewSocketsUDPClient.OnHostMigrationStarted += ClearOldPickupsOnMigration;
        NewSocketsUDPClient.OnRaceRestarted += ResetActiveBoost;
    }

    void OnDestroy()
    {
        NewSocketsUDPClient.OnPickupCreated -= SpawnPickup;
        NewSocketsUDPClient.OnBoostReceived -= ApplyBoost;
        NewSocketsUDPClient.OnHostMigrationStarted -= ClearOldPickupsOnMigration;
        NewSocketsUDPClient.OnRaceRestarted -= ResetActiveBoost;
        ResetActiveBoost();
    }

    void ClearOldPickupsOnMigration(string _, bool __, bool ___)
    {
        ResetActiveBoost();

        BoostPickupNetObject[] oldPickups = FindObjectsByType<BoostPickupNetObject>(FindObjectsSortMode.None);
        for (int i = 0; i < oldPickups.Length; i++)
        {
            if (client != null && client.LinkingContext != null)
            {
                client.LinkingContext.Unregister(oldPickups[i].id);
            }
            Destroy(oldPickups[i].gameObject);
        }
    }

    void ResetActiveBoost()
    {
        StopAllCoroutines();

        if (activeBoostStacks > 0 && raceSpawner != null && raceSpawner.LocalCar != null)
        {
            PrometeoCarController controller = raceSpawner.LocalCar.GetComponent<PrometeoCarController>();
            if (controller != null)
            {
                controller.maxSpeed -= BoostExtraSpeed * activeBoostStacks;
            }
        }

        activeBoostStacks = 0;
    }

    void SpawnPickup(int id)
    {
        GameObject pickup = Instantiate(boostPickupPrefab);
        pickup.GetComponent<NetObject>().id = id;
        client.LinkingContext.Register(pickup, id);
    }

    // -------------------------------------------------------------------------------------------
    // ------------------------------------ Boost logic ------------------------------------------
    // -------------------------------------------------------------------------------------------
    void ApplyBoost(int pickupId)
    {
        GameObject car = raceSpawner.LocalCar;
        if (car == null) return;
        StartCoroutine(Boost(car));
    }

    IEnumerator Boost(GameObject car)
    {
        PrometeoCarController controller = car.GetComponent<PrometeoCarController>();
        Rigidbody body = car.GetComponent<Rigidbody>();
        if (controller == null || body == null || body.isKinematic) yield break;

        activeBoostStacks++;
        controller.maxSpeed += BoostExtraSpeed;
        body.AddForce(car.transform.forward * BoostImpulse, ForceMode.VelocityChange);

        yield return new WaitForSeconds(BoostDuration);

        if (controller != null && activeBoostStacks > 0)
        {
            controller.maxSpeed -= BoostExtraSpeed;
            activeBoostStacks--;
        }
    }
}