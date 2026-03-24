using System.Collections;
using UnityEngine;

public class PlaceInFrontOfUser : MonoBehaviour
{
    public float distance = 0.6f;
    public float heightOffset = 0.0f;
    public float timeoutSeconds = 3f;

    IEnumerator Start()
    {
        float t = 0f;
        Camera cam = null;

        // Espera hasta que exista una cámara válida
        while (cam == null && t < timeoutSeconds)
        {
            cam = Camera.main;

            // Fallback: si no hay MainCamera, coge cualquier cámara activa
            if (cam == null)
            {
                var cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);

                for (int i = 0; i < cams.Length; i++)
                {
                    if (cams[i].isActiveAndEnabled)
                    {
                        cam = cams[i];
                        break;
                    }
                }
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (cam == null) yield break;

        // Espera 1 frame extra para que el tracking actualice pose
        yield return null;

        Vector3 forwardFlat = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        if (forwardFlat.sqrMagnitude < 0.001f) forwardFlat = cam.transform.forward;

        transform.position = cam.transform.position + forwardFlat * distance + Vector3.up * heightOffset;
        transform.rotation = Quaternion.LookRotation(forwardFlat, Vector3.up);
    }
}