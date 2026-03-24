using UnityEngine;

public class PathOverlayManager : MonoBehaviour
{
    [Header("Assign both if you have them")]
    public GridPathVisualizerUI showPathVisualizer;
    public GridPathVisualizerUI feedbackVisualizer;

    public void ClearAllPaths()
    {
        if (showPathVisualizer != null) showPathVisualizer.Clear();
        if (feedbackVisualizer != null) feedbackVisualizer.Clear();
    }
}
