using UnityEngine;

public class EnginePitch : MonoBehaviour
{
    const float SpeedSmoothing = 4f; // lower = smoother but slower to react

    private PrometeoCarController m_controller;
    private Vector3 m_lastPosition;
    private float m_speed;

    void Awake()
    {
        m_controller = GetComponent<PrometeoCarController>();
        m_lastPosition = transform.position;
    }

    void Update()
    {
        if (Time.deltaTime <= 0f) return;

        float rawSpeed = (transform.position - m_lastPosition).magnitude / Time.deltaTime; // v = d/t
        m_lastPosition = transform.position;

        if (rawSpeed < 150f) m_speed = Mathf.Lerp(m_speed, rawSpeed, SpeedSmoothing * Time.deltaTime); // speed smoothing

        if (m_controller.enabled) return; // skip if local car

        m_controller.UpdateSounds(m_speed);
    }
}
