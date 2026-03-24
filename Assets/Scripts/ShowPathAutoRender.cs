using System.Collections;
using UnityEngine;

public class ShowPathAutoRender : MonoBehaviour
{
    private GridPathVisualizerUI visualizer;

    private void OnEnable()
    {
        StartCoroutine(RenderNextFrame());
    }

    IEnumerator RenderNextFrame()
    {
        yield return null; // espera 1 frame por si el grid se acaba de generar

        // ✅ Busca el visualizer SOLO dentro de este panel
        if (visualizer == null)
            visualizer = GetComponentInChildren<GridPathVisualizerUI>(true);

        if (visualizer != null)
            visualizer.RenderPlannedPathPro();
        else
            Debug.LogWarning("ShowPathAutoRender: no encuentro GridPathVisualizerUI dentro del panel ShowPath.");
    }

    private void OnDisable()
    {
        if (visualizer != null)
            visualizer.Clear();
    }
}
