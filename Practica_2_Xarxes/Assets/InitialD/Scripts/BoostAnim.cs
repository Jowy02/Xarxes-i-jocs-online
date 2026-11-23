using TMPro;
using UnityEngine;
public class BoostAnim : MonoBehaviour
{
    [SerializeField] private float height = 0.3f;     // max and min height
    [SerializeField] private float floatSpeed = 2f;   // up and down speed
    [SerializeField] private float spinSpeed = 90f;   // rotation speed

    private Vector3 m_startPosition;

    void Start()
    {
        m_startPosition = transform.localPosition;
        transform.Rotate(0f, Random.Range(0f, 360f), 0f); // randomize initial rotation
    }

    void Update()
    {
        transform.localPosition = m_startPosition + Vector3.up * Mathf.Sin(Time.time * floatSpeed) * height;
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f);
    }
}