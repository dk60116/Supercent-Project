using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(OreArea))]
[CanEditMultipleObjects]
public class RandomRotateEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (!GUILayout.Button("Rotate"))
        {
            return;
        }

        foreach (Object targetObject in targets)
        {
            OreArea randomRotate = (OreArea)targetObject;
            List<Object> undoTargets = new List<Object>();

            for (int i = 0; i < randomRotate.transform.childCount; ++i)
            {
                undoTargets.Add(randomRotate.transform.GetChild(i));
            }

            if (undoTargets.Count > 0)
            {
                Undo.RecordObjects(undoTargets.ToArray(), "Random Rotate");
            }

            randomRotate.Rotate();
            EditorUtility.SetDirty(randomRotate);

            if (randomRotate.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(randomRotate.gameObject.scene);
            }
        }
    }
}
