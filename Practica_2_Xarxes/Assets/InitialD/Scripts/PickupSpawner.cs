using UnityEngine;

public class PickupSpawner : MonoBehaviour
{
    [SerializeField] private GameObject boostPickupPrefab;

    private NewSocketsUDPClient client;

    void Start()
    {
        client = NewSocketsUDPClient.Instance;
        NewSocketsUDPClient.OnPickupCreated += SpawnPickup;
    }

    void OnDestroy()
    {
        NewSocketsUDPClient.OnPickupCreated -= SpawnPickup;
    }

    void SpawnPickup(int id)
    {
        GameObject pickup = Instantiate(boostPickupPrefab);
        pickup.GetComponent<NetObject>().id = id;
        client.LinkingContext.Register(pickup, id);
    }
}