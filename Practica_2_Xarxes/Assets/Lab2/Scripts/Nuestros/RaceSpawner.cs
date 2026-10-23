using UnityEngine;

public class RaceSpawner : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // =============================================================================================
        // SERIALIZER TEST
        // =============================================================================================
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

    // Update is called once per frame
    void Update()
    {
        
    }
}
