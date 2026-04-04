using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Miner))]
public class MinerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8f);

        if (GUILayout.Button("Set Target Ore"))
        {
            Miner miner = (Miner)target;

            Undo.RecordObject(miner, "Set Miner Target Ore");
            miner.SetTargetOre();
            EditorUtility.SetDirty(miner);
        }
    }
}
