using UnityEngine;
using UnityEditor;

namespace ubco.ovilab.uxf.extensions.editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(DataSource), true)]
    public class DataSourceEditor: Editor
    {
        SerializedProperty useLocalDataProp;
        SerializedProperty participantIndex;
        SerializedProperty configJsonFile;
        SerializedProperty experimentServerUrl;
        SerializedProperty experimentStartFrom0;

        private void OnEnable()
        {
            useLocalDataProp = serializedObject.FindProperty("useLocalData");
            participantIndex = serializedObject.FindProperty("participantIndex");
            configJsonFile = serializedObject.FindProperty("configJsonFile");
            experimentServerUrl = serializedObject.FindProperty("experimentServerUrl");
            experimentStartFrom0 = serializedObject.FindProperty("experimentStartFrom0");
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(useLocalDataProp);
            bool guiEnabled = GUI.enabled;
            GUI.enabled = useLocalDataProp.boolValue;
            EditorGUILayout.PropertyField(participantIndex);
            EditorGUILayout.PropertyField(configJsonFile);
            GUI.enabled = !useLocalDataProp.boolValue;
            EditorGUILayout.PropertyField(experimentServerUrl);
            EditorGUILayout.PropertyField(experimentStartFrom0);
            GUI.enabled = guiEnabled;
            EditorGUI.indentLevel--;
        }
    }
}
