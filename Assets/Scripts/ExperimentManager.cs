using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UXF;
using Newtonsoft.Json;
using System.Linq;
using System;
using UnityEngine.UI;

namespace ubc.ok.ovilab.uxf.extensions
{
    /// <summary>
    /// Extension class with UXF functions that work with the
    /// experiment server. The json data is parsed with a
    /// <see cref="BlockData"/> or any extension of it. Also provides
    /// abstractions to have a calibration phrase before each block.
    /// </summary>
    public abstract class ExperimentManager<TBlockData> : MonoBehaviour where TBlockData:BlockData
    {
        [SerializeField]
        [Tooltip("The url address to the experiment server.")]
        public string experimentServerUrl = "http://127.0.0.1:5000";

        [Tooltip("The string to display on `displayText`")]
        [SerializeField] private string askPrompt = "When ready ask researcher to proceed with the experiment";
        [Tooltip("The UI button used to move to next/cancel.")]
        [SerializeField] private Button startNextButton;
        [Tooltip("The text where the logs gets printed.")]
        [SerializeField] private TMPro.TMP_Text outputText;
        [Tooltip("The text in the environment where relevant promts will be displayed.")]
        [SerializeField] protected TMPro.TMP_Text displayText;
        [Tooltip("The text which shows the current trial/block counts.")]
        [SerializeField] private TMPro.TMP_Text countText;
        [Tooltip("The text of the start next button.")]
        [SerializeField] private TMPro.TMP_Text startNextButtonText;

        #region HIDDEN_VARIABLES
        private System.Random random;
        private bool blockEnded = true;
        private bool sessionStarted = false;
        private bool lastBlockCancelled = false;
        private int participant_index = -1;
        private int countDisplay_blockNum, countDisplay_blockTotal, countDisplay_trialTotal;
        private TBlockData blockData;
        // NOTE: If a calibration function is set, when appropriate
        // the CalibrationComplete function also should be
        // called. Until then the next block will not get called.
        private Dictionary<string, Action<TBlockData>> calibrationFunctions = new Dictionary<string, Action<TBlockData>>();
        private CalibrationState calibrationState = CalibrationState.none;
        private Dictionary<string, object> calibrationParameters;
        private int getJsonRetryCounter = 0;
        #endregion

        #region UNITY_FUNCTIONS
        /// <summary>
        /// Unity method.  Extending classes must call this.
        /// </summary>
        public virtual void Start()
        {
            blockEnded = true;
            sessionStarted = false;
            participant_index = -1;
            outputText.text = "";

            Session session = Session.instance;
            session.onSessionBegin.AddListener(OnSessionBeginBase);
            session.onBlockBegin.AddListener(OnBlockBeginBase);
            session.onBlockEnd.AddListener(OnBlockEndBase);
            session.onTrialBegin.AddListener(OnTrialBeginBase);
            session.onTrialEnd.AddListener(OnTrialEndBase);
            session.onSessionEnd.AddListener(OnSessionEndBase);

            startNextButton.onClick.AddListener(OnGoToNextButtonClicked);

            // Strating the session after a few seconds
            StartCoroutine(StartSessionAfterWait(session));
        }

        /// <summary>
        /// Delayed callback that starts the session after 2
        /// seconds. Delay added to allow all hooks and setups to take
        /// place.
        /// </summary>
        private IEnumerator StartSessionAfterWait(Session session)
        {
            yield return new WaitForSeconds(2.0f);

            displayText.text = askPrompt;

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
                           post: true,
                           repeatIfFailed: true));
        }
        #endregion

        #region UFX_FUNCTIONS
        /// <summary>
        /// Wrapper for <see cref="OnSessionBegin"/>
        /// </summary>
        private void OnSessionBeginBase(Session session)
        {
            OnSessionBegin(session);
        }

        /// <summary>
        /// Abstract method to be used as a callback for <see cref="UXF.Session.onSessionBeing">.
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
        /// Abstract method that processes the configuration from experiment server.
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
            displayText.gameObject.SetActive(false);
            blockEnded = false;
            lastBlockCancelled = false;
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
            displayText.text = askPrompt;
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
            AddToOutpuText("Ending session");
            startNextButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// Abstract method to be used as a callback for <see cref="UXF.Session.onSessionEnd">.
        /// </summary>
        protected abstract void OnSessionEnd(Session session);
        #endregion

        #region HELPER_FUNCTIONS
        /// <summary>
        /// Callback for the next button click event.
        /// </summary>
        private void OnGoToNextButtonClicked()
        {
            if (!sessionStarted && !Session.instance.hasInitialised)
            {
                if (participant_index == -1)
                {
                    AddToOutpuText("participant_index is -1, did't get data?");
                    return;
                }
                Session.instance.Begin("hpuiPredicitveModel.study1", $"{participant_index}");
                Session.instance.settings.SetValue("participant_index", participant_index);

                // Makes sure the session doesn't get started again
                sessionStarted = true;
                AddToOutpuText("Session started");
                startNextButtonText.text = "Run Calibration";
                return;
            }

            if (!blockEnded)
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
                countDisplay_blockTotal += 1;

                AddToOutpuText("Cancelled block!");
                startNextButtonText.text = "Run Calibration";

                // Ending current trial would also end block
                Session.instance.EndCurrentTrial();
            }
            else
            {
                if (blockData == null)
                {
                    AddToOutpuText("Block still not available");
                }
                else
                {
                    // At this point we hopefully got the data for the next block.
                    // Using that to first process the calibration, then configure the block
                    string calibrationName = blockData.calibrationName;

                    switch (calibrationState) {
                        case CalibrationState.none: {
                            if (!string.IsNullOrEmpty(calibrationName) && calibrationFunctions.ContainsKey(calibrationName))
                            {
                                calibrationState = CalibrationState.started;
                                AddToOutpuText($"Running calibration - {calibrationName}");
                                calibrationFunctions[calibrationName].Invoke(blockData);
                                startNextButtonText.text = "...waiting";
                            }
                            else
                            {
                                AddToOutpuText($"No calibration - {calibrationName}");
                                calibrationState = CalibrationState.ended;
                                startNextButtonText.text = "Run block";
                            }
                            break;
                        }
                        case CalibrationState.started: {
                            AddToOutpuText($"Waiting for calibration to end - {calibrationName}");
                            break;
                        }
                        case CalibrationState.ended: {
                            ConfigureBlockBase(blockData);
                            blockData = null;
                            Session.instance.BeginNextTrial();
                            startNextButtonText.text = "Cancel";
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// All calibration methods configured through
        /// <see cref="AddCalibrationMethod"/> should call this method
        /// to transfer control back to the
        /// <see cref="ExperimentManager"/>.
        /// </summary>
        /// <param name="calibrationParameters">This value will be
        /// recorded in the settings of the block
        /// (<see cref="ConfigureBlockBase"/>) </param>
        public void CalibrationComplete(Dictionary<string, object> calibrationParameters)
        {
            // This data will be logged with the block settings
            this.calibrationParameters = calibrationParameters;
            AddToOutpuText($"Calibration completed");
            calibrationState = CalibrationState.ended;
            startNextButtonText.text = "Run block";
        }

        /// <summary>
        /// Add a calibration method.  If the value of a given blocks
        /// <see cref="BlockData.calibrationName"/> matches this name,
        /// the corresponding action will be called. The action should
        /// call <see cref="CalibrationComplete"/> to allow the
        /// experiment to proceed.
        /// </summary>
        /// <param name="name">Name of the calibration method.</param>
        /// <param name="action">Callback to initiate a calibration
        /// with a <see cref="BlockData"/> as parameter, which
        /// represents the current config of the block. </param>
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
        IEnumerator GetJsonUrl(string endpoint, System.Action<string> action=null, bool post=false, bool repeatIfFailed=false)
        {
            string url = $"{experimentServerUrl}/{endpoint}";
            do
            {
                using (UnityWebRequest webRequest = post ? UnityWebRequest.Post(url, "") : UnityWebRequest.Get(url))
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
                        Debug.LogWarning($"Request for {experimentServerUrl} failed with: {webRequest.error}");
                        AddToOutpuText($"Retrying {getJsonRetryCounter++}");
                        continue;
                    }

                    if (action != null)
                    {
                        action(System.Text.Encoding.UTF8.GetString(webRequest.downloadHandler.data));
                        yield break;
                    }
                }
            } while (repeatIfFailed);

        }

        private void GetConfig()
        {
            StartCoroutine(GetJsonUrl("api/config", (jsonText) =>
            {
                blockData = JsonConvert.DeserializeObject<TBlockData>(jsonText);
                AddToOutpuText($"Got new block: {blockData.name}");
            }));
        }

        private void GetNextBlock()
        {
            StartCoroutine(GetJsonUrl("api/move-to-next", (jsonText) =>
            {
                TBlockData el = JsonConvert.DeserializeObject<TBlockData>(jsonText);
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

        /// <summary>
        /// Add the message as a line in the <see cref="outputText"/>.
        /// </summary>
        protected void AddToOutpuText(string message)
        {
            Debug.Log(message);
            string[] slicedText = outputText.text.Split("\n");
            if (slicedText.Length > 5)
            {
                slicedText = slicedText.Skip(1).ToArray();
            }
            outputText.text = string.Join("\n", slicedText) + $"\n{message}";
        }

        /// <summary>
        /// Add one and update the diplayed counts in <see cref="countText"/>.
        /// </summary>
        /// <param name="updateTotals">If true, recompute the total counts.</param>
        protected void AddToCountText(bool updateTotals=false)
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
