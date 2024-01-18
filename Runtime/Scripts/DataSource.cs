using UnityEngine;

namespace ubco.ovilab.uxf.extensions
{
    [CreateAssetMenu(fileName = "Data Source", menuName = "UXF/Extensions/DataSource", order = 1)]
    public class DataSource: ScriptableObject
    {
        [Tooltip("If set, will use the data set in `Config Json File`")]
        public bool useLocalData = false;

        public int participantIndex;
        public TextAsset configJsonFile;

        [Tooltip("The url address to the experiment server.")]
        public string experimentServerUrl = "http://127.0.0.1:5000";
        [Tooltip("If set to true, everytime the application starts, it will force the server to block 0. Only for in editor.")]
        public bool experimentStartFrom0 = false;

    }
}
