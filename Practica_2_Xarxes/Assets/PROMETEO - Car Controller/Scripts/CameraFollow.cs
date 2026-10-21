using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour {

    public Transform target;
    public Rigidbody targetRb;

    [Header("Pos")]
    public Vector3 offset = new Vector3(0f, 2.0f, -5.5f);
    public float extraDistanceAtMaxSpeed = 1.5f;
    public float positionSmoothTime = 0.08f;

    [Header("FOV")]
    public float minFov = 60f;
    public float maxFov = 85f;
    public float fovSmooth = 3f;

    [Header("Vel")]
    public float maxSpeed = 60f;

    private Camera cam;
    private Vector3 heading = Vector3.forward;
    private Vector3 smoothPos;
    private Vector3 posVelocity;

    float velocityDirectionBlend = 0.6f;
    float minSpeedForVelocityDir = 5f;
    float headingSmooth = 4f;

    float lookHeight = 1.0f;
    float lookAheadDistance = 4f;
    float rotationSmooth = 8f;
    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        if (target == null) return;
        heading = Flat(target.forward);
        smoothPos = target.position + Quaternion.LookRotation(heading) * offset;
        transform.position = smoothPos;
        transform.rotation = Quaternion.LookRotation(target.position - smoothPos);
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 vel = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
        float speed = vel.magnitude;
        float speed01 = Mathf.Clamp01(speed / maxSpeed);

        // Dirección
        Vector3 desiredHeading = Flat(target.forward);
        if (speed > minSpeedForVelocityDir)
        {
            Vector3 velDir = Flat(vel);
            if (Vector3.Dot(velDir, desiredHeading) > 0f)
                desiredHeading = Vector3.Slerp(desiredHeading, velDir, velocityDirectionBlend);
        }
        heading = Vector3.Slerp(heading, desiredHeading, 1f - Mathf.Exp(-headingSmooth * Time.deltaTime));
        Quaternion headingRot = Quaternion.LookRotation(heading, Vector3.up);

        // Posición
        Vector3 dynamicOffset = offset + new Vector3(0f, 0f, -extraDistanceAtMaxSpeed * speed01);
        Vector3 desiredPos = target.position + headingRot * dynamicOffset;
        transform.position = desiredPos;

        // Rotación 
        smoothPos = Vector3.SmoothDamp(smoothPos, desiredPos, ref posVelocity, positionSmoothTime);
        Vector3 lookPoint = target.position + Vector3.up * lookHeight + heading * lookAheadDistance;
        Quaternion desiredRot = Quaternion.LookRotation(lookPoint - smoothPos, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime));

        // FOV
        float targetFov = Mathf.Lerp(minFov, maxFov, speed01);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-fovSmooth * Time.deltaTime));
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
    }
}
