using UnityEditor;

namespace ubco.ovilab.uxf.extensions.editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ExperimentManager<>), true)]
    public class ExperimentManagerEditor: Editor
    {
        protected string[] processedSerializedProps;
        private bool showEvents;
        private SerializedProperty scriptProp;
        private SerializedProperty studyNameProp;
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
                "studyName",
                "sessionNumber",
                "dataSource",
                "onBlockRecieved",
                "askPrompt",
                "startNextButton",
                "displayText",
                "countText"
            };

            scriptProp = serializedObject.FindProperty("m_Script");
            studyNameProp = serializedObject.FindProperty("studyName");
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
            EditorGUILayout.PropertyField(studyNameProp);
            EditorGUILayout.PropertyField(sessionNumberProp);

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
