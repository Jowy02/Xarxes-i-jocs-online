using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DirectionTrigger : MonoBehaviour
{
    [Header("Direction")]
    [SerializeField] private RaceDirectionUI.Direction direction = RaceDirectionUI.Direction.Left;

    [Header("Settings")]
    [SerializeField] private float displayDuration = 3f;

    [Header("References (optional)")]
    [SerializeField] private RaceSpawner raceSpawner;
    [SerializeField] private RaceDirectionUI directionUI;

    //Ensures the collider acts as a trigger instead of a physical obstacle
    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    //Finds the required references automatically if they were not assigned in the Inspector
    private void Start()
    {
        if (raceSpawner == null) raceSpawner = FindFirstObjectByType<RaceSpawner>();
        if (directionUI == null) directionUI = FindFirstObjectByType<RaceDirectionUI>();

        if (raceSpawner == null) Debug.LogError("[DIRECTION TRIGGER] RaceSpawner not found.", this);
        if (directionUI == null) Debug.LogError("[DIRECTION TRIGGER] RaceDirectionUI not found.", this);
    }

    //Checks whether the entering collider belongs to the local player's car
    private void OnTriggerEnter(Collider other)
    {
        if (raceSpawner == null || directionUI == null) return;

        GameObject localCar = raceSpawner.LocalCar;
        if (localCar == null) return;

        Transform otherTransform = other.transform;
        bool isLocalCar = otherTransform == localCar.transform || otherTransform.IsChildOf(localCar.transform);
        if (!isLocalCar) return;

        directionUI.ShowDirection(direction, displayDuration);
    }
}