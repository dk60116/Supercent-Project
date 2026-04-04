using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerController))]
public class PlayerControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8f);

        if (GUILayout.Button("Auto Fill Resource Stacks"))
        {
            PlayerController controller = (PlayerController)target;

            Undo.RecordObject(controller, "Auto Fill Player Resource Stacks");
            controller.AutoFillResourceStacks();
            EditorUtility.SetDirty(controller);
        }
    }
}
