using UnityEngine;

public class OrthoShadowTest : MonoBehaviour
{
    void Start()
    {
        

        // Create plane (ground)
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.transform.localScale = Vector3.one * 5f;
        plane.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        plane.GetComponent<Renderer>().receiveShadows = true;

        // Create cube
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = new Vector3(0, 1, 0);
        cube.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        cube.GetComponent<Renderer>().receiveShadows = true;

        // Directional Light
        GameObject lightObj = new GameObject("Directional Light");
        var light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 1f;
        lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Camera
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            cam = camObj.AddComponent<Camera>();
            cam.tag = "MainCamera";
        }

        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0, 5, -5);
        cam.transform.LookAt(Vector3.zero);
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 50f;
    }
}