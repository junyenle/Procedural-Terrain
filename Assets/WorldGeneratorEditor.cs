using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(WorldGenerator))]
public class WorldGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        WorldGenerator generator = (WorldGenerator)target;
        if (GUILayout.Button("Generate"))
        {
            generator.Generate();
        }
        if (DrawDefaultInspector())
        {
            if (generator.updateContinuously)
            {
                generator.Generate();
            }
        }
    }
}