using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UXF;
using Newtonsoft.Json;
using System.Linq;
using System;
using UnityEngine.UI;
using UnityEngine.Events;
using UXF.UI;
using UnityEngine.Assertions;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace ubco.ovilab.uxf.extensions
{
    // TODO: Allow using ppid
    // TODO: Allow registering calibration functions from inspector
    /// <summary>
    /// Extension class with UXF functions that work with the
    /// experiment server. The json data is parsed with a
    /// <see cref="BlockData"/> or any extension of it. Also provides
    /// abstractions to have a calibration phrase before each block.
    /// </summary>
    public abstract class ExperimentManager<TBlockData> : MonoBehaviour, IExperimentManager<TBlockData> where TBlockData : BlockData
    {
        private const string UXF_RIG_PATH = "Packages/ubco.ovilab.uxf.extensions/Runtime/UXF/Prefabs/[UXF_Rig].prefab";

        [SerializeField] private UIController UXFUIController;
        [SerializeField] private StartupMode UXFUIStartupMode;

        /// <summary>
        /// The study name used when starting session (See Session.Begin).
        /// This could be overridden by calling <see cref="SessionBeginParams"/>
        /// If UXF UI is used to start a session, the value provided there is taken precedence.
        /// </summary>
        [Tooltip("The study name used when starting session (See Session.Begin).")]
        [SerializeField] public string experimentName = "study";

        /// <summary>
        /// The sesstion ID used when starting session (See Session.Begin).
        /// This could be overridden by calling <see cref="SessionBeginParams"/>.
        /// If UXF UI is used to start a session, the value provided there is taken precedence.
        /// </summary>
        [Tooltip("The sesstion ID used when starting session (See Session.Begin). If UXF UI is used to start a session, the value provided there is taken precedence.")]
        [SerializeField] public int sessionNumber = 1;

        [Tooltip("Data source being used.")]
        /// <summary>
        /// Data source being used.
        /// </summary>
        public DataSource dataSource;

        [Tooltip("Event called when block data is recieved.")]
        /// <summary>
        /// The event triggered when block data is recieved.
        /// </summary>
        public UnityEvent<TBlockData> BlockRecieved = new UnityEvent<TBlockData>();

        private string outputTextInfo;
        private string displayTextInfo;
        private string countTextInfo;

        public string OutputTextInfo => outputTextInfo;
        public string DisplayTextInfo => displayTextInfo;
        public string CountTextInfo => countTextInfo;

        [Tooltip("Event triggered when state changes.")]
        [SerializeField] private UnityEvent<ExperimentManagerState> stateChanged = new UnityEvent<ExperimentManagerState>();

        [Tooltip("(optional) The string to display on `Display Text`")]
        [Multiline][SerializeField] private string askPrompt = "When ready ask researcher to proceed with the experiment";
        [Tooltip("(optional) The UI button used to move to next/cancel. Note that if not set, `MoveToNextState` has to be called to proceed through experiments.")]
        [SerializeField] private Button startNextButton;
        [Tooltip("(optional) The text where the logs gets printed.")]
        [SerializeField] private TMPro.TMP_Text outputText;
        [Tooltip("(optional) The text in the environment where relevant promts will be displayed.")]
        [SerializeField] protected TMPro.TMP_Text displayText;
        [Tooltip("(optional) The text which shows the current trial/block counts.")]
        [SerializeField] private TMPro.TMP_Text countText;
        [Tooltip("(optional) The text of the start next button. When `Start Next Button` is set, this would help indicate the current state of the progress on screen.")]

        /// <inheritdoc />
        public int ParticipantIndex
        {
            get
            {
                Assert.IsNotNull(configSummary, "Config summary not set yet!");
                return configSummary.ParticipantIndex;
            }
        }

        /// <inheritdoc />
        public int ConfigsLength
        {
            get
            {
                Assert.IsNotNull(configSummary, "Config summary not set yet!");
                return configSummary.ConfigsLength;
            }
        }

        /// <inheritdoc />
        public ExperimentManagerState CurrentState
        {
            get => currentState;
            private set
            {
                bool triggerStateChanged = currentState != value;
                currentState = value;

                if (triggerStateChanged)
                {
                    stateChangeHappened = true;
                    StateChanged.Invoke(currentState);
                }
            }
        }

        /// <inheritdoc />
        public UnityEvent<ExperimentManagerState> StateChanged { get => stateChanged; }

        #region HIDDEN_VARIABLES
        private ExperimentManagerState currentState = ExperimentManagerState.UninitializedSession;
        private ExperimentManagerState desiredTargetState = ExperimentManagerState.UninitializedSession;
        private Action<ExperimentManagerState, MoveToStateResult> moveToStateCallback;
        private Settings initialSettings;
        private Dictionary<string, object> initialParticipantDetails;
        private TMPro.TMP_Text startNextButtonText;
        private System.Random random;
        private bool blockEnded = true;
        private bool sessionStarted = false;
        private bool tryingToGetData = false;
        private bool waitToGetData = false;
        private bool lastBlockCancelled = false;
        private bool stateChangeHappened = false;
        private bool movingToTargetState = false;
        private ConfigSummaryData configSummary;
        private int countDisplay_trialTotal;
        private string statusString;
        private TBlockData blockData;
        // NOTE: If a calibration function is set, when appropriate
        // the CalibrationComplete function also should be
        // called. Until then the next block will not get called.
        private Dictionary<string, Action<TBlockData>> calibrationFunctions = new Dictionary<string, Action<TBlockData>>();
        private CalibrationState calibrationState = CalibrationState.none;
        private Dictionary<string, object> calibrationParameters;
        private int getJsonRetryCounter = 0;

        private List<TBlockData> defaultData;
        private int currentDefaultDataIndex = 0;
        #endregion

        #region UNITY_FUNCTIONS
        /// <summary>
        /// Unity method.  Extending classes must call this.
        /// </summary>
        public virtual void Start()
        {
            blockEnded = true;
            sessionStarted = false;
            tryingToGetData = false;
            waitToGetData = false;
            configSummary = null;
            if (outputText != null)
            {
                outputText.SetText("");
            }

            OnValidate();

            Session session = Session.instance;
            session.onSessionBegin.AddListener(OnSessionBeginBase);
            session.onBlockBegin.AddListener(OnBlockBeginBase);
            session.onBlockEnd.AddListener(OnBlockEndBase);
            session.onTrialBegin.AddListener(OnTrialBeginBase);
            session.onTrialEnd.AddListener(OnTrialEndBase);
            session.onSessionEnd.AddListener(OnSessionEndBase);

            session.settingsToLog.AddRange(new List<string>() { "blockName", "canceled", "calibrationName" });

            if (startNextButton != null)
            {
                startNextButton.onClick.AddListener(MoveToNextState);
                startNextButtonText = startNextButton.GetComponentInChildren<TMPro.TMP_Text>();
                if (startNextButtonText != null)
                {
                    startNextButtonText.SetText("Load session data");
                }
            }

            if (displayText != null)
            {
                displayText.SetText(askPrompt);
            }
            displayTextInfo = askPrompt;
        }

        public void OnValidate()
        {
            if (UXFUIController == null)
            {
#if UNITY_EDITOR
                UIController[] uiControllerObjs = FindObjectsByType<UIController>(FindObjectsSortMode.None);
                bool foundUIControllerObj = false;
                foreach (UIController uiControllerObj in uiControllerObjs)
                {
                    if (uiControllerObj != null)
                    {
                        UXFUIController = uiControllerObj;
                        foundUIControllerObj = true;
                    }
                }

                if (!foundUIControllerObj)
                {
                    UXFUIController = GameObject.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(UXF_RIG_PATH)).GetComponentInChildren<UIController>();
                }

                UXFUIController.experimentName = experimentName;
#else
                Debug.LogWarning("The UXF Rig has not been configured?");
#endif
            }

            if (UXFUIController.startupMode != UXFUIStartupMode)
            {
                StartCoroutine(DelayedSetUXFUIStartupMode(UXFUIStartupMode));
            }
        }

        /// <inheritdoc />
        public virtual void Update()
        {
            if (stateChangeHappened)
            {
                stateChangeHappened = false;
                if (movingToTargetState)
                {
                    if (CurrentState != desiredTargetState)
                    {
                        MoveToNextState();
                    }
                    else
                    {
                        movingToTargetState = false;
                        moveToStateCallback.Invoke(CurrentState, MoveToStateResult.MovedToTargetState);
                        moveToStateCallback = null;
                    }
                }
            }
        }

        /// <summary>
        /// Get the data necessary to initiate the session.
        /// </summary>
        private void InitiateSessionStart(string nextButtonTextOndataRecieved)
        {
            tryingToGetData = true;

            if (dataSource.useLocalData)
            {
                defaultData = JsonConvert.DeserializeObject<List<TBlockData>>(dataSource.configJsonFile.text);
                Debug.Assert(defaultData.Select(d => d.participant_index).Distinct().Count() == 1, "There are more than 1 distinct participant indices in the provided config file");

                if (configSummary == null)
                {
                    configSummary = new ConfigSummaryData(defaultData[0].participant_index, defaultData.Count);
                }

                CurrentState = ExperimentManagerState.AwaitingBlockConfig;
                Debug.Log($"Recieved session data (pp# {configSummary.ParticipantIndex})");
                AddToOutpuText($"Recieved session data (pp# {configSummary.ParticipantIndex})");
                GetConfig();
                if (startNextButtonText != null)
                {
                    startNextButtonText.SetText(nextButtonTextOndataRecieved);
                }
            }
            else
            {
                StartCoroutine(
#if UNITY_EDITOR
                    /// Making sure to start from the begining when experimentStartFrom0 is true
                    GetJsonUrl(dataSource.experimentStartFrom0 ? "api/move-to-block/0" : "api/active",
#else
                    GetJsonUrl("api/active",
#endif
                               (isActive) =>
                               {
                                   Debug.Log(isActive);
                                   if (isActive != "false")
                                   {
                                       GetSummaryData(nextButtonTextOndataRecieved);
                                   }
                                   else
                                   {
                                       StartCoroutine(GetJsonUrl("api/move-to-next",
                                                                 (jsonText) =>
                                                                 {
                                                                     GetSummaryData(nextButtonTextOndataRecieved);
                                                                 },
                                                                 post: true));
                                   }
                               },
#if UNITY_EDITOR
                               post: dataSource.experimentStartFrom0,
#endif
                               repeatIfFailed: true));
            }
        }

        private void GetSummaryData(string nextButtonTextOndataRecieved)
        {
            StartCoroutine(GetJsonUrl("api/summary-data", (jsonText) =>
            {
                ConfigSummaryData data = JsonConvert.DeserializeObject<ConfigSummaryData>(jsonText);

                if (configSummary == null)
                {
                    configSummary = data;
                }
                Assert.AreEqual(data.ParticipantIndex, configSummary.ParticipantIndex,
                                $"Summary data from server (ppid: {data.ParticipantIndex}) does not match the cached summary data (ppid: {configSummary.ParticipantIndex}).");

                CurrentState = ExperimentManagerState.AwaitingBlockConfig;

                Debug.Log($"Recieved session data (pp# {configSummary.ParticipantIndex}): {jsonText}");
                AddToOutpuText($"Recieved session data (pp# {configSummary.ParticipantIndex}): {jsonText}");
                GetConfig();
                if (startNextButtonText != null)
                {
                    startNextButtonText.SetText(nextButtonTextOndataRecieved);
                }
            }));
        }


        #endregion

        #region UFX_FUNCTIONS
        /// <summary>
        /// Wrapper for <see cref="OnSessionBegin"/>
        /// </summary>
        private void OnSessionBeginBase(Session session)
        {
            if (!sessionStarted)
            {
                CurrentState = ExperimentManagerState.AwaitingInitialization;
                sessionStarted = true;
                // TODO: Use the ppid endpoints of experiment manager.
                Debug.LogWarning($"Starting server from UI? ExperimentManager does not handle/respect ppid set in UI.");
                AddToOutpuText("Session started");
                InitiateSessionStart("Run calibration");
            }

            OnSessionBegin(session);
        }

        /// <summary>
        /// Abstract method to be used as a callback for <see cref="UXF.Session.onSessionBegin">.
        /// </summary>
        protected abstract void OnSessionBegin(Session session);

        /// <summary>
        /// Call <see cref="ConfigureBlock"/>. Also update the UI and
        /// add the default data from the exeperiment server (i.e.,
        /// name, calibrationName), any calibration parameters passed
        /// to the <see cref="CalibrationComplete"/> and the canceled
        /// (false by default) status. The canceled status will be
        /// updated if the block gets canceled.
        /// </summary>
        private void ConfigureBlockBase(TBlockData el)
        {
            Debug.Log($"Got {el}");
            Block block = Session.instance.CreateBlock();
            block.settings.SetValue("blockName", el.name);
            block.settings.SetValue("canceled", false); /// by default block is not canceled
            block.settings.SetValue("calibrationName", el.calibrationName);
            block.settings.SetValue("calibrationParameters", calibrationParameters);
            ConfigureBlock(el, block, lastBlockCancelled);
            Debug.Log($"Added block with {block.trials.Count} trials");
        }

        /// <summary>
        /// Abstract method that processes the configuration from experiment
        /// server. Note that a new block is added each time a new configuration
        /// is fetched from the experiment server.
        /// </summary>
        /// <param name="el">The parsed config data from experiment server.</param>
        /// <param name="block">Current block being configured.</param>
        /// <param name="lastBlockCancelled">If true, the last block was cancelled.</param>
        protected abstract void ConfigureBlock(TBlockData el, Block block, bool lastBlockCancelled);

        /// <summary>
        /// Call <see cref="OnBlockBegin"/>, update the states and hide the <see cref="displayText"/>
        /// </summary>
        private void OnBlockBeginBase(Block block)
        {
            OnBlockBegin(block);
            AddToOutpuText("Block: " + block.settings.GetString("blockName"));
            AddToCountText(true);

            if (displayText != null)
            {
                displayText.gameObject.SetActive(false);
            }

            blockEnded = false;
            lastBlockCancelled = false;
            CurrentState = ExperimentManagerState.BlockBegan;
        }

        /// <summary>
        /// Abstract method to be used as a callback for <see cref="UXF.Session.onBlockBegin">.
        /// </summary>
        protected abstract void OnBlockBegin(Block block);

        /// <summary>
        /// Call <see cref="OnTrialBegin"/> and update the counts on the UI.
        /// </summary>
        private void OnTrialBeginBase(Trial trial)
        {
            OnTrialBegin(trial);
            AddToCountText();
        }

        /// <summary>
        /// Abstract method to be used as a callback for <see cref="UXF.Session.onTrialBegin">.
        /// </summary>
        protected abstract void OnTrialBegin(Trial trial);

        /// <summary>
        /// Call <see cref="OnTrialEnd"/>
        /// </summary>
        private void OnTrialEndBase(Trial trial)
        {
            OnTrialEnd(trial);
        }

        /// <summary>
        /// Abstract method to be used as a callback for <see cref="UXF.Session.onTrialEnd">.
        /// </summary>
        protected abstract void OnTrialEnd(Trial trial);

        /// <summary>
        /// Wrapper for <see cref="OnBlockEnd"/>. Also, save the block
        /// settings to the session settings so that the block
        /// information is also stored. The block data is stored with
        /// key $"Block_{block.number}" Also, updates the states. If
        /// The block was not cancelled, get the next block, else get
        /// the canceled block again. Also, show the
        /// <see cref="displayText"/> and update its text.
        /// </summary>
        private void OnBlockEndBase(Block block)
        {
            calibrationState = CalibrationState.none;
            OnBlockEnd(block);
            // Adding block information to setting to allow it to be logged
            Session.instance.settings.SetValue($"Block_{block.number}", block.settings.baseDict);

            blockEnded = true;
            AddToOutpuText("Ended Block: " + block.settings.GetString("blockName"));
            if (displayText != null)
            {
                displayText.gameObject.SetActive(true);
                displayText.text = askPrompt;
            }
            displayTextInfo = askPrompt;

            if (lastBlockCancelled)
            {
                CurrentState = ExperimentManagerState.BlockCancelled;
            }
            else
            {
                CurrentState = ExperimentManagerState.BlockEnded;
            }

            if (startNextButtonText != null)
            {
                startNextButtonText.SetText("Get config");
            }
            waitToGetData = true;
        }

        /// <summary>
        /// Abstract method to be used as a callback for <see cref="UXF.Session.onBlockEnd">.
        /// </summary>
        protected abstract void OnBlockEnd(Block block);

        /// <summary>
        /// Wraper for <see cref="OnSessionEnd"/>. Also updates the UI
        /// & hides the next button to avoid accidentally restarting
        /// the session.
        /// </summary>
        private void OnSessionEndBase(Session session)
        {
            OnSessionEnd(session);
            Debug.Log($"Ending session");
            CurrentState = ExperimentManagerState.SessionEnded;
            AddToOutpuText("Ending session");
            if (startNextButtonText != null)
            {
                startNextButton.gameObject.SetActive(false);
            }

            if (movingToTargetState)
            {
                moveToStateCallback.Invoke(CurrentState, MoveToStateResult.SessionEnded);
                moveToStateCallback = null;
                movingToTargetState = false;
            }
        }

        /// <summary>
        /// Abstract method to be used as a callback for <see cref="UXF.Session.onSessionEnd">.
        /// </summary>
        protected abstract void OnSessionEnd(Session session);
        #endregion

        #region HELPER_FUNCTIONS
        /// <inheritdoc />
        public void SessionBeginParams(string experimentName, int sessionNumber, Dictionary<string, object> participantDetails, Settings settings)
        {
            if (Session.instance.hasInitialised)
            {
                throw new InvalidOperationException("SessionBeginParams called after session started.");
            }

            this.experimentName = experimentName;
            this.sessionNumber = sessionNumber;
            initialParticipantDetails = participantDetails;
            initialSettings = settings;
        }

        /// <summary>
        /// Set which UXF UI Strtup mode to use. If set to manual, session would start when <see cref="MoveToNextState"/> is called.
        /// If not the appropriate UXF UI startup mode would be used.
        /// </summary>
        public void SetUXFUIStartupMode(StartupMode startupMode)
        {
            UXFUIStartupMode = startupMode;
            UXFUIController.startupMode = startupMode;
            UXFUIController.LateValidate();
        }

        private IEnumerator DelayedSetUXFUIStartupMode(StartupMode startupMode)
        {
            yield return null;
            SetUXFUIStartupMode(startupMode);
        }

        /// <inheritdoc />
        public void MoveToNextState()
        {
            if (!sessionStarted && blockData == null && !tryingToGetData)
            {
                CurrentState = ExperimentManagerState.AwaitingInitialization;
                InitiateSessionStart("Start session");
            }
            else if (!sessionStarted && !Session.instance.hasInitialised)
            {
                if (configSummary.ParticipantIndex == -1)
                {
                    AddToOutpuText("participant_index is -1, did't get data?");
                    return;
                }
                try
                {
                    // Makes sure the session doesn't get started again
                    sessionStarted = true;
                    Session.instance.Begin(experimentName, $"{configSummary.ParticipantIndex}", sessionNumber, initialParticipantDetails, initialSettings);
                    Session.instance.settings.SetValue("participant_index", configSummary.ParticipantIndex);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    // Makes sure the session can be stared again if something had gone wrong
                    sessionStarted = Session.instance.hasInitialised;
                    if (!sessionStarted)
                    {
                        return;
                    }
                }

                AddToOutpuText("Session started");
                if (startNextButtonText != null)
                {
                    startNextButtonText.SetText("Run Calibration");
                }
                return;
            }
            else if (!blockEnded)
            {
                // We want to stop the block now!

                // remove all the remaining trials
                Session.instance.CurrentBlock.trials
                    .Where(x => x.status == TrialStatus.NotDone)
                    .ToList()
                    .ForEach((trial) => Session.instance.CurrentBlock.trials.Remove(trial));

                // Mark as canceled
                Session.instance.CurrentBlock.settings.SetValue("canceled", true);
                lastBlockCancelled = true;

                AddToOutpuText("Cancelled block!");

                // Ending current trial would also end block
                Session.instance.EndCurrentTrial();
            }
            else
            {
                if (blockData == null)
                {
                    if (waitToGetData)
                    {
                        GetNextOrCurrentConfig();
                    }
                    else
                    {
                        AddToOutpuText("Block still not available");
                    }
                }
                else
                {
                    // At this point we hopefully got the data for the next block.
                    // Using that to first process the calibration, then configure the block
                    string calibrationName = blockData.calibrationName;

                    switch (calibrationState)
                    {
                        case CalibrationState.none:
                            {
                                if (!string.IsNullOrEmpty(calibrationName) && calibrationFunctions.ContainsKey(calibrationName))
                                {
                                    calibrationState = CalibrationState.started;
                                    AddToOutpuText($"Running calibration - {calibrationName}");
                                    calibrationFunctions[calibrationName].Invoke(blockData);
                                    CurrentState = ExperimentManagerState.Calibrating;
                                    if (startNextButtonText != null)
                                    {
                                        startNextButtonText.SetText("...waiting");
                                    }
                                }
                                else
                                {
                                    AddToOutpuText($"No calibration - {calibrationName}");
                                    calibrationState = CalibrationState.ended;
                                    CurrentState = ExperimentManagerState.ReadyForBlockBegin;
                                    if (startNextButtonText != null)
                                    {
                                        startNextButtonText.SetText("Run block");
                                    }
                                }
                                break;
                            }
                        case CalibrationState.started:
                            {
                                AddToOutpuText($"Waiting for calibration to end - {calibrationName}");
                                break;
                            }
                        case CalibrationState.ended:
                            {
                                ConfigureBlockBase(blockData);
                                blockData = null;
                                Session.instance.BeginNextTrial();
                                if (startNextButtonText != null)
                                {
                                    startNextButtonText.SetText("Cancel");
                                }
                                break;
                            }
                    }
                }
            }
        }

        /// <inheritdoc />
        public void MoveToState(ExperimentManagerState targetState, Action<ExperimentManagerState, MoveToStateResult> callback)
        {
            if (movingToTargetState)
            {
                moveToStateCallback.Invoke(CurrentState, MoveToStateResult.Cancelled);
            }
            moveToStateCallback = callback;
            movingToTargetState = true;
            desiredTargetState = targetState;
        }

        /// <inheritdoc />
        public void CalibrationComplete(Dictionary<string, object> calibrationParameters)
        {
            // This data will be logged with the block settings
            this.calibrationParameters = calibrationParameters;
            AddToOutpuText($"Calibration completed");
            calibrationState = CalibrationState.ended;
            CurrentState = ExperimentManagerState.ReadyForBlockBegin;
            if (startNextButtonText != null)
            {
                startNextButtonText.SetText("Run block");
            }
        }

        /// <inheritdoc />
        public void AddCalibrationMethod(String name, Action<TBlockData> action)
        {
            if (calibrationFunctions.ContainsKey(name))
            {
                calibrationFunctions[name] = action;
            }
            calibrationFunctions.Add(name, action);
        }

        // Copied from  UFX.UI.UIController
        // Used to get the trial config from the server
        IEnumerator GetJsonUrl(string endpoint, System.Action<string> action = null, System.Action<long> errorAction = null, bool post = false, bool repeatIfFailed = false)
        {
            string url = $"{dataSource.experimentServerUrl}/{endpoint}";
            Debug.Log($"Request: {url}");
            do
            {
                using (UnityWebRequest webRequest = post ? UnityWebRequest.PostWwwForm(url, "") : UnityWebRequest.Get(url))
                {
                    webRequest.timeout = 5;
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
                        Debug.LogWarning($"Request for {dataSource.experimentServerUrl} failed with: {webRequest.error}");
                        AddToOutpuText($"Retrying {getJsonRetryCounter++}");
                        errorAction.Invoke(webRequest.responseCode);
                        yield return new WaitForSeconds(5);
                        continue;
                    }
                    getJsonRetryCounter = 0;

                    if (action != null)
                    {
                        action(System.Text.Encoding.UTF8.GetString(webRequest.downloadHandler.data));
                        yield break;
                    }
                }
            } while (repeatIfFailed);

        }

        private void GetNextOrCurrentConfig()
        {
            CurrentState = ExperimentManagerState.AwaitingBlockConfig;
            // Get next only when current block was not cancelled
            if (lastBlockCancelled)
            {
                GetConfig();
            }
            else
            {
                GetNextBlock();
            }
            waitToGetData = false;

            if (startNextButtonText != null)
            {
                startNextButtonText.SetText("Run calibration");
            }
        }

        private void GetConfig()
        {
            if (dataSource.useLocalData)
            {
                blockData = defaultData[currentDefaultDataIndex];
                BlockRecieved.Invoke(blockData);
                AddToOutpuText($"Got new block: {blockData.name}");
                tryingToGetData = false;
                CurrentState = ExperimentManagerState.ReadyForCalibration;
            }
            else
            {
                StartCoroutine(
                    GetJsonUrl(
                        "api/config",
                        (jsonText) =>
                        {
                            blockData = JsonConvert.DeserializeObject<TBlockData>(jsonText);
                            BlockRecieved.Invoke(blockData);
                            AddToOutpuText($"Got new block: {blockData.name}");
                            tryingToGetData = false;
                            CurrentState = ExperimentManagerState.ReadyForCalibration;
                        },
                        (errorCode) =>
                        {
                            // This happens only if /move-to-next needs to be called
                            if (errorCode == 406)
                            {
                                AddToOutpuText("Server moving to next");
                                StartCoroutine(GetJsonUrl("api/move-to-next", post: true));
                            }
                        },
                        repeatIfFailed: true));
            }
        }

        private void GetNextBlock()
        {
            if (dataSource.useLocalData)
            {
                if (++currentDefaultDataIndex != defaultData.Count)
                {
                    GetConfig();
                }
                else
                {
                    Session.instance.End();
                }
            }
            else
            {
                StartCoroutine(GetJsonUrl("api/move-to-next", (jsonText) =>
                {
                    TBlockData el = JsonConvert.DeserializeObject<TBlockData>(jsonText);
                    if (el.name != "END")
                    {
                        GetConfig();
                    }
                    else
                    {
                        Session.instance.End();
                    }
                }, post: true));
            }
        }

        /// <summary>
        /// Print messages and add the message as a line in the <see cref="outputText"/> if it has been set.
        /// </summary>
        protected void AddToOutpuText(string message)
        {
            Debug.Log(message);
            string[] slicedText = outputText.text.Split("\n");
            if (slicedText.Length > 5)
            {
                slicedText = slicedText.Skip(1).ToArray();
            }

            if (outputText != null)
            {
                outputText.text = string.Join("\n", slicedText) + $"\n{message}";
            }

            outputTextInfo = string.Join("\n", slicedText) + $"\n{message}";
        }

        /// <summary>
        /// Add one and update the diplayed counts in <see cref="countText"/>.
        /// If <see cref="countText"/> is not set, this does nothing.
        /// </summary>
        /// <param name="updateTotals">If true, recompute the total counts.</param>
        protected void AddToCountText(bool updateTotals = false)
        {
            Session session = Session.instance;
            if (updateTotals)
            {
                countDisplay_trialTotal = session.Trials.ToList().Count;

                if (dataSource.useLocalData)
                {
                    statusString = $"\nppid: {defaultData[0].participant_index}    Block: {currentDefaultDataIndex}/{defaultData.Count}    Name: {defaultData[currentDefaultDataIndex].name}";
                }
                else
                {
                    StartCoroutine(GetJsonUrl("api/status-string", (text) =>
                    {
                        text = text.Replace("&nbsp;", " ").Replace("Participant index", "ppid");
                        statusString = $"\n{text}";
                        if (countText != null)
                        {
                            countText.text = $"{session.currentTrialNum}/{countDisplay_trialTotal}  {statusString}";
                        }
                        countTextInfo = $"{session.currentTrialNum}/{countDisplay_trialTotal}  {statusString}";
                    }));
                }
            }
            else
            {
                if (countText != null)
                {
                    countText.text = $"{session.currentTrialNum}/{countDisplay_trialTotal}  {statusString}";
                }
                countTextInfo = $"{session.currentTrialNum}/{countDisplay_trialTotal}  {statusString}";
            }
        }

        #endregion

        enum CalibrationState
        {
            none, started, ended
        }
    }

    internal class ConfigSummaryData
    {
        public int ParticipantIndex { get; set; }
        public int ConfigsLength { get; set; }

        public ConfigSummaryData(int participant_index, int config_length)
        {
            this.ParticipantIndex = participant_index;
            this.ConfigsLength = config_length;
        }
    }
}
