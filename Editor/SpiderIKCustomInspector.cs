using UnityEngine;
using UnityEditor;
#pragma warning disable IDE0090 // Use 'new(...)'
[CustomEditor(typeof(SpiderIK))]
public class SpiderIKCustomInspector : Editor 
{
    int pointSelectionInt, setupInt;
    public int old;
    SerializedProperty stepHeight, constraintSource, avatar, pairsOfLegs;
    string[] pointSelectionToolbarStrings = { "Single Point", "Multiple Points" };
    string[] setupToolbarStrings = { "Existing VRIK", "Only Hips", "Only Legs" };
    protected void OnEnable() // Remember the last selection on the toolbar incase the script has been unselected
    {
        serializedObject.Update();
        setupInt = serializedObject.FindProperty("setupInt").intValue;
        pointSelectionInt = serializedObject.FindProperty("pointSelectionInt").intValue;
    }
    protected void OnDisable() // Record the last selection on the toolbar for when we come back to the script later
    {
        serializedObject.FindProperty("setupInt").intValue = setupInt;
        serializedObject.FindProperty("pointSelectionInt").intValue = pointSelectionInt;
        serializedObject.ApplyModifiedProperties();
    }
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        pointSelectionInt = GUILayout.Toolbar(pointSelectionInt, pointSelectionToolbarStrings);
        setupInt = GUILayout.Toolbar(setupInt, setupToolbarStrings);
        SpiderIK ik = (SpiderIK)target;
        if (setupInt == 0){EditorGUILayout.HelpBox("For when an entire VRIK skeleton exists for each pair of legs. Keep in mind that the optional bones (neck, chest, and shoulders) cannot be present in these skeletons.", MessageType.Info);}
        if (setupInt == 1){EditorGUILayout.HelpBox("For when only the hips exist for each pair of legs for the VRIK.", MessageType.Info);}
        if (setupInt == 2){EditorGUILayout.HelpBox("This only works if the avatar is not a prefab, if you don't understand the implications of unpacking your avatar, make a duplicate before you do this!", MessageType.Warning);}
        // Below Toolbars
        EditorGUILayout.PropertyField(serializedObject.FindProperty("avatar"));
        // Upper Toolbar Settings
        if (pointSelectionInt == 0) {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("constrainToWhat"));
        }
        else {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("constraintArray"));
        }
        // Lower Toolbar Settings
        if (setupInt <= 1) { // Existing VRIK or Only Hips setup
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pairsOfLegs"));
        }
        else { // "Only Legs" setup
            EditorGUILayout.PropertyField(serializedObject.FindProperty("legArray"));
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty("stepHeight"));
        if (GUILayout.Button("Set up IK"))
        {
            ik.CreateSpider(setupInt, pointSelectionInt);
        }
        if (GUILayout.Button("Prepare for Upload"))
        {
            ik.PrepareUpload();
        }
        serializedObject.ApplyModifiedProperties();
    }

    [MenuItem("Tools/Scionzenos/Spider IK Setup")]
    public static void EnableSpiderIK()
    {
        GameObject singlePointSetup = new GameObject("IK Setup");
        singlePointSetup.AddComponent<SpiderIK>();
    }
}