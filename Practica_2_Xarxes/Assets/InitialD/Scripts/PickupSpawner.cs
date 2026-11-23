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

    void Start()
    {
        client = NewSocketsUDPClient.Instance;
        NewSocketsUDPClient.OnPickupCreated += SpawnPickup;
        NewSocketsUDPClient.OnBoostReceived += ApplyBoost;
    }

    void OnDestroy()
    {
        NewSocketsUDPClient.OnPickupCreated -= SpawnPickup;
        NewSocketsUDPClient.OnBoostReceived -= ApplyBoost;
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

        controller.maxSpeed += BoostExtraSpeed;
        body.AddForce(car.transform.forward * BoostImpulse, ForceMode.VelocityChange);

        yield return new WaitForSeconds(BoostDuration);

        if (controller != null) controller.maxSpeed -= BoostExtraSpeed;
    }
}