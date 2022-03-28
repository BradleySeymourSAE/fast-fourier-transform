
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor (typeof(AudioSpectrumVisualizer))]
public class AudioSpectrumVisualizerCustomEditor : Editor {
	public override void OnInspectorGUI () {
		var rhythmVisualizator = (AudioSpectrumVisualizer)target;

		if (GUILayout.Button ("Restart Script")) {
			rhythmVisualizator.Restart ();
		} else if (EditorApplication.isPlaying) {
			if (DrawDefaultInspector ()) {
				rhythmVisualizator.UpdateVisualizations ();
			}

		} else {
			DrawDefaultInspector ();
		}

		if (GUILayout.Button ("Restart Script")) {
			rhythmVisualizator.Restart ();
		}

	}
}
#endif