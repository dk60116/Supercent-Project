using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CounterWorker))]
public class CounterWorkerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8f);

        if (GUILayout.Button("Auto Fill Resource Stacks"))
        {
            CounterWorker counterWorker = (CounterWorker)target;

            Undo.RecordObject(counterWorker, "Auto Fill CounterWorker Resource Stacks");
            counterWorker.AutoFillResourceStacks();
            EditorUtility.SetDirty(counterWorker);
        }
    }
}
