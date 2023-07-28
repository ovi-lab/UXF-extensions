using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UXF;
using Newtonsoft.Json;
using ubc.ok.ovilab.HPUI.Core;
using System.Linq;
using System;
using UnityEngine.UI;
using ubc.ok.ovilab.ViconUnityStream;

namespace ubc.ok.ovilab.uxf.extensions
{
    public class ExperimentManager : MonoBehaviour
    {
        private const string ASK_PROMPT = "When ready ask researcher to proceed with the experiment";
        [SerializeField]
        [Tooltip("The url address to the experiment server.")]
        string experimentServerUrl = "http://127.0.0.1:5000";

        public List<Transform> buttonsRoots;
        public Color defaultColor = Color.white;
        public Color defaultHoverColor = Color.yellow;
        public Color targetButtonColor = Color.red;
        public Color defaultHighlightColor = Color.green;
        public AudioClip hoverAudio;
        public AudioClip contactAudio;
        public AudioSource audioSource;
        public bool disableHover;
        public bool disableHoverAudio;
        public bool trackJoints = true; // Adding this for performance reasons
        public List<string> forceTrackJoints = new List<string>(); // When trackJoints is false, bypass that for the coordinates in this list
        public Button startNextButton;
        public TMPro.TMP_Text outputText;
        public TMPro.TMP_Text displayText;
        public TMPro.TMP_Text countText;
        public ButtonController thumbBaseButton;
        public TargetManager targetManager;

        // NOTE: If a calibration function is set, when appropriate
        // the CalibrationComplete function also should be
        // called. Until then the next block will not get called.
        private Dictionary<string, Action> calibrationFunctions = new Dictionary<string, Action>();

        #region HIDDEN_VARIABLES
        private Dictionary<string, (ButtonController controller, Tracker tracker, Vector3 localScale)> buttons;
        private List<string> activeButtons;
        private ButtonController targetButton;
        private System.Random random;
        private bool blockEnded = true;
        private CalibrationState calibrationState = CalibrationState.none;
        private Dictionary<string, object> calibrationParameters;
        private bool sessionStarted = false;
        private int participant_index = -1;
        private Dictionary<string, List<string>> XORGroupFlattened;
        private int countDisplay_blockNum, countDisplay_blockTotal, countDisplay_trialTotal;
        private bool hideNonTargets = false;
        private BlockData blockData;
        #endregion

        #region UNITY_FUNCTIONS
        public virtual void Start()
        {
            blockEnded = true;
            sessionStarted = false;
            participant_index = -1;
            outputText.text = "";

            Session session = Session.instance;
            session.onSessionBegin.AddListener(OnSessionBegin);
            session.onBlockBegin.AddListener(OnBlockBegin);
            session.onBlockEnd.AddListener(OnBlockEnd);
            session.onTrialBegin.AddListener(OnTrialBegin);
            session.onTrialEnd.AddListener(OnTrialEnd);
            session.onSessionEnd.AddListener(OnSessionEnd);

            startNextButton.onClick.AddListener(OnGoToNextButtonClicked);


            // Strating the session after a few seconds
            StartCoroutine(StartSessionAfterWait(session));
        }

        private IEnumerator StartSessionAfterWait(Session session)
        {
            yield return new WaitForSeconds(2.0f);

            displayText.text = ASK_PROMPT;

            StartCoroutine(
                GetJsonUrl("api/move-to-block/0",  /// Making sure to start from the begining
                           (idx) =>
                           StartCoroutine(GetJsonUrl("api/global-data", (jsonText) =>
                           {
                               ConfigGlobalData data = JsonConvert.DeserializeObject<ConfigGlobalData>(jsonText);
                               participant_index = data.participant_index;
                               countDisplay_blockTotal = data.config_length;
                               Debug.Log($"Recieved session data (pp# {participant_index}): {jsonText}");
                               AddToOutpuText($"Recieved session data (pp# {participant_index}): {jsonText}");
                               GetConfig();
                           })),
                           post: true));
        }

        #endregion

        #region UFX_FUNCTIONS
        private void OnSessionBegin(Session session)
        {
            // Session.instance.BeginNextTrial();
        }

        private void GetConfig()
        {
            StartCoroutine(GetJsonUrl("api/config", (jsonText) =>
            {
                blockData = JsonConvert.DeserializeObject<BlockData>(jsonText);
                AddToOutpuText($"Got new block: {blockData.name}");
            }));
        }

        private void GetNextBlock()
        {
            StartCoroutine(GetJsonUrl("api/move-to-next", (jsonText) =>
            {
                BlockData el = JsonConvert.DeserializeObject<BlockData>(jsonText);
                if (el.name != "end")
                {
                    GetConfig();
                }
                else
                {
                    Session.instance.End();
                    StartCoroutine(GetJsonUrl("api/shutdown", null, post: true));
                }
            }, post: true));
        }

        private Block ConfigureBlock()
        {
            BlockData el = blockData;
            // TODO: take following values from the server
            // - offset for skeleton
            // - offset of thumb collider
            Debug.Log($"Got {el}");
            random = new System.Random();
            Block block = Session.instance.CreateBlock();
            block.settings.SetValue("buttons", el.useButtons);
            block.settings.SetValue("blockName", el.name);
            block.settings.SetValue("buttonSize", el.buttonSize);
            block.settings.SetValue("numSegments", el.numSegments);
            block.settings.SetValue("currentSegment", el.currentSegment);
            block.settings.SetValue("iterations", el.iterations);
            block.settings.SetValue("usePermutations", el.usePermutations);
            block.settings.SetValue("useSensor", el.useSensor);
            block.settings.SetValue("reciprocalStudy", el.reciprocalStudy);
            block.settings.SetValue("XORGroups", el.XORGroups);
            block.settings.SetValue("canceled", false); /// by default block is not canceled
            block.settings.SetValue("hideNonTargets", el.hideNonTargets);
            block.settings.SetValue("calibrationName", el.calibrationName);
            block.settings.SetValue("changeLayout", el.changeLayout);
            block.settings.SetValue("calibrationParameters", calibrationParameters);

            // The config doesn't say which targets are shown/hidden after the calibration
            // determine this by checking if the root is active or not
            List<string> usedButtons = el.useButtons.Where(btn => buttons[btn].Item1.transform.root.gameObject.activeSelf).ToList();
            block.settings.SetValue("usedButtons", usedButtons);

            if (el.XORGroups != null)
            {
                XORGroupFlattened = (from _group in el.XORGroups
                                     from _el in _group
                                     select new
                                     {
                                         el = _el,
                                         g = _group
                                     })
                .ToDictionary(t => t.el, t => t.g);
            }
            else
            {
                XORGroupFlattened = new Dictionary<string, List<string>>();
            }

            block.settings.SetValue("XORGroupFlattened", XORGroupFlattened);

            ((CustomTriggeredInteractionManager)CustomTriggeredInteractionManager.instance).usePhidgetSensor = el.useSensor;

            /// unique seed for participant_index + buttons size
            int seed = (int)Math.Floor(el.participant_index * el.buttonSize * 100);
            // Using seed to ensure the same sequences are genereted with a given seed
            System.Random rng = new System.Random(seed);

            IEnumerable<ButtonController> usedButtonObjects = usedButtons
                .Select(btn => buttons[btn].Item1);

            targetManager.SetupTargets(block, el.changeLayout, usedButtonObjects, random, el.numTrials);

            //NOTE: buttonSize is not used here as the calibration will set them!
            Debug.Log($"Added block with {block.trials.Count} trials");
            blockData = null;

            return block;
        }

        private void OnBlockBegin(Block block)
        {
            // Showing only the buttons relevant to this block
            List<string> usedButtons = block.settings.GetStringList("usedButtons");
            hideNonTargets = block.settings.GetBool("hideNonTargets");

            foreach (KeyValuePair<string, (ButtonController, Tracker, Vector3)> kvp in buttons)
            {
                if (usedButtons.Contains(kvp.Key))
                {
                    // Adding contact event callback
                    kvp.Value.Item1.contactAction.AddListener(OnButtonContact);
                    kvp.Value.Item1.proximateAction.AddListener(OnButtonHover);
                    // FIXME: better way to handle target specific color
                    if (kvp.Value.Item1 == thumbBaseButton)
                    {
                        Color _color = Color.black;
                        _color.a = 0;
                        SetButtonColor(kvp.Value.Item1, _color, true, defaultHoverColor);
                    }
                    else
                    {
                        SetButtonColor(kvp.Value.Item1, defaultColor, true, defaultHoverColor);
                    }

                    if (hideNonTargets)
                    {
                        kvp.Value.Item1.Hide();
                    }
                    else
                    {
                        kvp.Value.Item1.Show();
                    }
                    kvp.Value.Item1.GetComponent<BehindFingerButtonIndicator>()?.SetActive(true);
                }
                else
                {
                    kvp.Value.Item1.Hide();
                    kvp.Value.Item1.GetComponent<BehindFingerButtonIndicator>()?.SetActive(false);
                }
            }

            AddToOutpuText("Block: " + block.settings.GetString("blockName"));
            AddToCountText(true);
            displayText.gameObject.SetActive(false);
            blockEnded = false;
        }

        private void OnTrialBegin(Trial trial)
        {
            targetManager.SetupTrial(trial);
            string targetButtonName = trial.settings.GetString("button");
            ButtonController prevTargetButton = targetButton;
            targetButton = buttons[targetButtonName].controller;
            if (prevTargetButton != targetButton)
            {
                /// Handling colors
                if (prevTargetButton != null)
                {
                    // FIXME: better way to handle target specific color
                    if (prevTargetButton == thumbBaseButton)
                    {
                        thumbBaseButton.Hide();
                    }
                    else
                    {
                        SetButtonColor(prevTargetButton, defaultColor, true, defaultHoverColor);
                    }
                }
                // FIXME: better way to handle target specific color
                if (targetButton == thumbBaseButton)
                {
                    thumbBaseButton.Show();
                    Color _color = new Color(targetButtonColor.r, targetButtonColor.g, targetButtonColor.b, 0.2f);
                    SetButtonColor(targetButton, _color, true, defaultHoverColor);
                }
                else
                {
                    SetButtonColor(targetButton, targetButtonColor, true, defaultHoverColor);
                }

                if (hideNonTargets)
                {
                    SetButtonColor(prevTargetButton, defaultColor, true, defaultHoverColor);
                    prevTargetButton.Hide();
                }
                else
                {
                    /// Handling XORGroups
                    if (XORGroupFlattened.ContainsKey(targetButtonName))
                    {
                        List<string> XORGroup = XORGroupFlattened[targetButtonName];

                        foreach (string button in XORGroupFlattened.Keys)
                        {
                            if (XORGroup.Contains(button))
                            {
                                buttons[button].controller.Show();
                            }
                            else
                            {
                                ButtonController buttonController = buttons[button].controller;
                                SetButtonColor(buttonController, defaultColor, true, defaultHoverColor);
                                buttonController.Hide();
                            }
                        }
                    }
                    else
                    {
                        foreach (string button in XORGroupFlattened.Keys)
                        {
                            buttons[button].controller.Show();
                        }
                    }
                }
            }
            Debug.Log($"Startin trial:   Trial num: {Session.instance.CurrentTrial.number}     " +
                      $"Block num: {Session.instance.CurrentBlock.number}     "+
                      $"Target button: {targetButton.transform.parent.parent.name}");
            AddToCountText();
            // targetButton.contactAction.AddListener(OnButtonContact);
        }
        
        private void OnTrialEnd(Trial trial) {
            // Session.instance.EndIfLastTrial(trial);
        }

        // callback functions
        private void OnButtonContact(ButtonController buttonController)
        {
            audioSource.PlayOneShot(contactAudio);
            if (!Session.instance.hasInitialised)
            {
                Debug.Log($"{Session.instance.hasInitialised}");
                return;
            }

            buttons[buttonController.name].tracker.RecordRow();
            // targetButton.ResetStates();
            // targetButton.contactAction.RemoveListener(OnButtonContact);

            Debug.Log($"Button contact  Trial num: {Session.instance.CurrentTrial.number}     " +
                      $"Block num: {Session.instance.CurrentBlock.number}     "+
                      $"Contact button: {buttonController.transform.parent.parent.name}     "+
                      $"Target button: {targetButton.transform.parent.parent.name}");

            // Doing the following to ensure a failed trial is also recorded.
            // Trial ends only when correct button is selected.
            // NOTE: Starting and ending the trial will cause the result to get overwritten
            if (buttonController == targetButton)
            {
                try
                {
                    Session.instance.EndCurrentTrial();
                    Session.instance.BeginNextTrial();
                }
                catch (NoSuchTrialException)
                {
                    AddToOutpuText($"Session ended. (probably?)");
                    Debug.Log($"Session ended. (probably?)");
                }
            }
        }

        private void OnButtonHover(ButtonController buttonController)
        {
            if (!disableHoverAudio)
            {
                audioSource.PlayOneShot(hoverAudio);
            }
        }

        private void OnBlockEnd(Block block)
        {
            calibrationState = CalibrationState.none;
            // Clearing it to make sure the color gets set next block
            targetButton = null;
            // Adding block information to setting to allow it to be logged
            Session.instance.settings.SetValue($"Block_{block.number}", block.settings.baseDict);

            // Get next only when current block was not cancelled
            if (block.settings.GetBool("canceled"))
            {
                GetConfig();
            }
            else
            {
                GetNextBlock();
            }

            blockEnded = true;
            AddToOutpuText("Ended Block: " + block.settings.GetString("blockName"));
            displayText.gameObject.SetActive(true);
            displayText.text = ASK_PROMPT;
            HideButtons();
        }

        private void OnSessionEnd(Session session)
        {
            Debug.Log($"Ending session");
            AddToOutpuText("Ending session");
            HideButtons();
            startNextButton.gameObject.SetActive(false);
        }

        #endregion

        #region HPUI_Core_functions
        /// <summary>
        /// Callback to run after GetButton is called.
        /// </summary>
        private void PostGetButtonsCallback(IEnumerable<ButtonController> buttons)
        {
            
        }
        #endregion

        #region HELPER_FUNCTIONS
        private void HideButtons()
        {
            foreach ((ButtonController button, Tracker tracker, Vector3 localScale) in buttons.Values)
            {
                button.contactAction.RemoveListener(OnButtonContact);
                button.proximateAction.RemoveListener(OnButtonHover);
                button.GetComponent<BehindFingerButtonIndicator>()?.SetActive(false);
                button.Hide();
            }
        }

        private void OnGoToNextButtonClicked()
        {
            if (!sessionStarted && !Session.instance.hasInitialised)
            {
                if (participant_index == -1)
                {
                    throw new Exception("participant_index is -1, did't get data?");
                }
                Session.instance.Begin("hpuiPredicitveModel.study1", $"{participant_index}");
                Session.instance.settings.SetValue("participant_index", participant_index);
                /// Also record the file names of the streams from vicon
                Dictionary<string, List<string>> subjectScripts = FindObjectsOfType<CustomSubjectScript>()
                    .Where(subject => subject.enabled)
                    .ToDictionary((subject) => subject.transform.name,
                                  (subject) => subject.filePaths?.ToList());
                Session.instance.settings.SetValue("data_streams", subjectScripts);

                // Makes sure the session doesn't get started again
                sessionStarted = true;
                AddToOutpuText("Session started");
                return;
            }

            if (!blockEnded)
            {
                /// We want to stop the block now!

                /// remove all the remaining trials
                Session.instance.CurrentBlock.trials
                    .Where(x => x.status == TrialStatus.NotDone)
                    .ToList()
                    .ForEach((trial) => Session.instance.CurrentBlock.trials.Remove(trial));

                /// Mark as canceled
                Session.instance.CurrentBlock.settings.SetValue("canceled", true);
                countDisplay_blockTotal += 1;

                /// Ending current trial would also end block
                Session.instance.EndCurrentTrial();
            }
            else
            {
                // At this point we hopefully got the data for the next block.
                // Using that to first process the calibration, then configure the block
                if (blockData == null)
                {
                    AddToOutpuText("Block still not available");
                }
                else
                {
                    string calibrationName = blockData.calibrationName;

                    switch (calibrationState) {
                        case CalibrationState.none: {
                            if (!string.IsNullOrEmpty(calibrationName) && calibrationFunctions.ContainsKey(calibrationName))
                            {
                                calibrationState = CalibrationState.started;
                                AddToOutpuText($"Running calibration - {calibrationName}");
                                calibrationFunctions[calibrationName].Invoke();
                            }
                            else
                            {
                                AddToOutpuText($"No calibration - {calibrationName}");
                                calibrationState = CalibrationState.ended;
                            }
                            break;
                        }
                        case CalibrationState.started: {
                            AddToOutpuText($"Waiting for calibration to end - {calibrationName}");
                            break;
                        }
                        case CalibrationState.ended: {
                            ConfigureBlock();
                            Session.instance.BeginNextTrial();
                            InteractionManger.instance.GetButtons();
                            break;
                        }
                    }
                }
            }
        }

        public void CalibrationComplete(Dictionary<string, object> calibrationParameters)
        {
            this.calibrationParameters = calibrationParameters;
            AddToOutpuText($"Calibration completed");
            calibrationState = CalibrationState.ended;
        }

        public void AddCalibrationMethod(String name, Action action)
        {
            if (calibrationFunctions.ContainsKey(name))
            {
                calibrationFunctions[name] = action;
            }
            calibrationFunctions.Add(name, action);
        }

        private void SetButtonColor(ButtonController buttonController, Color color, bool forceHoverColor=false, Color? hoverColor=null)
        {
            buttonController.GetComponent<ButtonColorBehaviour>().DefaultColor = color;
            buttonController.GetComponent<BehindFingerButtonIndicator>()?.SetColor(color);

            if (!disableHover || forceHoverColor)
            {
                if (hoverColor == null)
                {
                    buttonController.GetComponent<ButtonColorBehaviour>().hoverColor = color;  // disabling the hover color
                }
                else
                {
                    buttonController.GetComponent<ButtonColorBehaviour>().hoverColor = (Color) hoverColor;
                }
            }
        }

#if UNITY_EDITOR
        private GameObject dummyObject;

        public void TriggerTargetButton()
        {
            if (dummyObject == null)
            {
                dummyObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dummyObject.transform.localScale = Vector3.one * 0.02f;
                dummyObject.GetComponent<MeshRenderer>().enabled = false;
                dummyObject.AddComponent<ButtonTriggerCollider>();
            }
            if (targetButton == null)
            {
                return;
            }

            dummyObject.transform.position = targetButton.transform.position;
            targetButton.contactZone.TriggerBehaviour(dummyObject.GetComponent<Collider>());
            targetButton.contactAction.AddListener((btn) =>
            {
                dummyObject.transform.position = btn.transform.position - btn.transform.forward.normalized * 0.01f;
                btn.contactZone.state = ButtonZone.State.outside;
                btn.proximalZone.state = ButtonZone.State.outside;
            });
        }
#endif

        // Copied from  UFX.UI.UIController
        // Used to get the trial config from the server
        IEnumerator GetJsonUrl(string endpoint, System.Action<string> action=null, bool post=false)
        {
            string url = $"{experimentServerUrl}/{endpoint}";
            using (UnityWebRequest webRequest = post ? UnityWebRequest.Post(url, "") : UnityWebRequest.Get(url))
            {
                webRequest.timeout = 30;
                yield return webRequest.SendWebRequest();

                bool error;
#if UNITY_2020_OR_NEWER
                error = webRequest.result != UnityWebRequest.Result.Success;
#else
#pragma warning disable
                error = webRequest.isHttpError || webRequest.isNetworkError;
#pragma warning restore
#endif

                if (error)
                {
                    Debug.LogError($"Request for {experimentServerUrl} failed with: {webRequest.error}");
                    yield break;
                }

                if (action != null)
                {
                    action(System.Text.Encoding.UTF8.GetString(webRequest.downloadHandler.data));
                }
            }
        }

        private void AddToOutpuText(string message)
        {
            Debug.Log(message);
            string[] slicedText = outputText.text.Split("\n");
            if (slicedText.Length > 5)
            {
                slicedText = slicedText.Skip(1).ToArray();
            }
            outputText.text = string.Join("\n", slicedText) + $"\n{message}";
        }

        private void AddToCountText(bool updateTotals=false)
        {
            Session session = Session.instance;
            if (updateTotals)
            {
                countDisplay_blockNum = session.currentBlockNum;
                countDisplay_trialTotal = session.Trials.ToList().Count;
            }
            countText.text = $"{session.currentTrialNum}/{countDisplay_trialTotal}  ({countDisplay_blockNum}/{countDisplay_blockTotal})";
        }

        #endregion

        enum CalibrationState {
            none, started, ended
        }
    }

    class BlockData {
        public string name;
        public int participant_index;
        public int numTrials;
        public List<string> useButtons;
        public bool usePermutations;
        public bool useSensor;
        public float buttonSize;
        public int numSegments;
        public int currentSegment;
        public int iterations;
        public bool reciprocalStudy;
        public List<List<string>> XORGroups;
        public bool hideNonTargets;
        public string calibrationName;
        public bool changeLayout;

        public override string ToString()
        {
            return $"Name: {name}" +
                $"Participant Index: {participant_index}" +
                $"Number of Trials: {numTrials}" +
                $"Using permutations: {usePermutations}" +
                $"ReciprocalStudy: {reciprocalStudy}" +
                $"Button size: {buttonSize}" +
                $"Buttons used: {useButtons}" +
                $"Number of segments: {numSegments}" +
                $"Current segment: {currentSegment}" +
                $"Use sensor: {useSensor}" +
                $"Iterations: {iterations}" +
                $"XORGroups: {XORGroups}" +
                $"Hide non-targets: {hideNonTargets}" +
                $"Calibration Name {calibrationName}" +
                $"Change Layout: {changeLayout}";
        }
    }

    internal struct ConfigGlobalData
    {
        public int participant_index;
        public int config_length;

        public ConfigGlobalData(int participant_index, int config_length)
        {
            this.participant_index = participant_index;
            this.config_length = config_length;
        }
    }
}
