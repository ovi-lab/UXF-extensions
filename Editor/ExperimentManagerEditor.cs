using UnityEngine;
using UnityEditor;
using UXF.UI;
using UnityEditorInternal;

namespace ubco.ovilab.uxf.extensions.editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ExperimentManager<>), true)]
    public class ExperimentManagerEditor: Editor
    {
        protected string[] processedSerializedProps;
        private bool showEvents;
        private SerializedProperty scriptProp;
        private SerializedProperty uxfUIProp;
        private SerializedProperty uxfUIStartupMode;
        private SerializedProperty experimentNameProp;
        private SerializedProperty sessionNumberProp;
        private SerializedProperty dataSourceProp;
        private SerializedProperty onBlockRecievedProp;
        private SerializedProperty askPromptProp;
        private SerializedProperty startNextButtonProp;
        private SerializedProperty displayTextProp;
        private SerializedProperty countTextProp;

        protected virtual void OnEnable()
        {

            processedSerializedProps = new string[]
            {
                "m_Script",
                "UXFUIController",
                "UXFUIStartupMode",
                "experimentName",
                "sessionNumber",
                "dataSource",
                "onBlockRecieved",
                "askPrompt",
                "startNextButton",
                "displayText",
                "countText"
            };

            scriptProp = serializedObject.FindProperty("m_Script");
            uxfUIProp = serializedObject.FindProperty("UXFUIController");
            uxfUIStartupMode = serializedObject.FindProperty("UXFUIStartupMode");
            experimentNameProp = serializedObject.FindProperty("experimentName");
            sessionNumberProp = serializedObject.FindProperty("sessionNumber");
            dataSourceProp = serializedObject.FindProperty("dataSource");
            onBlockRecievedProp = serializedObject.FindProperty("onBlockRecieved");
            askPromptProp = serializedObject.FindProperty("askPrompt");
            startNextButtonProp = serializedObject.FindProperty("startNextButton");
            displayTextProp = serializedObject.FindProperty("displayText");
            countTextProp = serializedObject.FindProperty("countText");
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.PropertyField(scriptProp);

            EditorGUILayout.LabelField("UXF settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(experimentNameProp);
            EditorGUILayout.PropertyField(sessionNumberProp);
            EditorGUILayout.PropertyField(uxfUIProp);
            EditorGUILayout.PropertyField(uxfUIStartupMode);
            if (GUILayout.Button("Configure UXF UI"))
            {
                Object ui = uxfUIProp.objectReferenceValue;
                Selection.activeObject = ui;
                InternalEditorUtility.SetIsInspectorExpanded(ui, true);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UXF Extensions settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(dataSourceProp);
            showEvents = EditorGUILayout.Foldout(showEvents, "Events");
            if (showEvents)
            {
                EditorGUILayout.PropertyField(onBlockRecievedProp);
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UXF Extensions UI settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(askPromptProp);
            EditorGUILayout.PropertyField(startNextButtonProp);
            EditorGUILayout.PropertyField(displayTextProp);
            EditorGUILayout.PropertyField(countTextProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Derieved class settings", EditorStyles.boldLabel);
            DrawPropertiesExcluding(serializedObject, processedSerializedProps);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
