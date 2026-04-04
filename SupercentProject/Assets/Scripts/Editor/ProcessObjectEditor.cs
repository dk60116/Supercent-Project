using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ProcessObject), true)]
public class ProcessObjectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8f);

        if (GUILayout.Button("Auto Fill Resource Stacks"))
        {
            ProcessObject processObject = (ProcessObject)target;

            Undo.RecordObject(processObject, "Auto Fill Process Object Resource Stacks");
            processObject.AutoFillResourceStacks();
            EditorUtility.SetDirty(processObject);
        }
    }
}
