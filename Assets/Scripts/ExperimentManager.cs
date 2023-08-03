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
    public class ExperimentManager<TBlockData> : MonoBehaviour where TBlockData:BlockData
    {
        private const string ASK_PROMPT = "When ready ask researcher to proceed with the experiment";
        [SerializeField]
        [Tooltip("The url address to the experiment server.")]
        string experimentServerUrl = "http://127.0.0.1:5000";

        public Button startNextButton;
        public TMPro.TMP_Text outputText;
        public TMPro.TMP_Text displayText;
        public TMPro.TMP_Text countText;

        #region HIDDEN_VARIABLES
        private System.Random random;
        private bool blockEnded = true;
        private bool sessionStarted = false;
        private int participant_index = -1;
        private int countDisplay_blockNum, countDisplay_blockTotal, countDisplay_trialTotal;
        private TBlockData blockData;
        // NOTE: If a calibration function is set, when appropriate
        // the CalibrationComplete function also should be
        // called. Until then the next block will not get called.
        private Dictionary<string, Action> calibrationFunctions = new Dictionary<string, Action>();
        private CalibrationState calibrationState = CalibrationState.none;
        private Dictionary<string, object> calibrationParameters;
        #endregion

        #region UNITY_FUNCTIONS
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
        private void OnSessionBeginBase(Session session)
        {
            OnSessionBegin(session);
        }

        protected virtual void OnSessionBegin(Session session) { }

        private void ConfigureBlockBase(TBlockData el)
        {
            Debug.Log($"Got {el}");
            Block block = Session.instance.CreateBlock();
            block.settings.SetValue("blockName", el.name);
            block.settings.SetValue("canceled", false); /// by default block is not canceled
            block.settings.SetValue("calibrationName", el.calibrationName);
            block.settings.SetValue("calibrationParameters", calibrationParameters);
            ConfigureBlock(el, block);
            Debug.Log($"Added block with {block.trials.Count} trials");
        }

        protected virtual void ConfigureBlock(TBlockData el, Block block) { }

        private void OnBlockBeginBase(Block block)
        {
            OnBlockBegin(block);
            AddToOutpuText("Block: " + block.settings.GetString("blockName"));
            AddToCountText(true);
            displayText.gameObject.SetActive(false);
            blockEnded = false;
        }

        protected virtual void OnBlockBegin(Block block) { }

        private void OnTrialBeginBase(Trial trial)
        {
            OnTrialBegin(trial);
            AddToCountText();
        }

        protected virtual void OnTrialBegin(Trial trial) { }
        
        private void OnTrialEndBase(Trial trial)
        {
            OnTrialEnd(trial);
        }

        protected virtual void OnTrialEnd(Trial trial) { }

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
            displayText.text = ASK_PROMPT;
        }

        protected virtual void OnBlockEnd(Block block) { }

        private void OnSessionEndBase(Session session)
        {
            OnSessionEnd(session);
            Debug.Log($"Ending session");
            AddToOutpuText("Ending session");
            startNextButton.gameObject.SetActive(false);
        }

        protected virtual void OnSessionEnd(Session session) { }
        #endregion

        #region HELPER_FUNCTIONS
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

                // Makes sure the session doesn't get started again
                sessionStarted = true;
                AddToOutpuText("Session started");
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
                countDisplay_blockTotal += 1;

                // Ending current trial would also end block
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
                            ConfigureBlockBase(blockData);
                            blockData = null;
                            Session.instance.BeginNextTrial();
                            break;
                        }
                    }
                }
            }
        }

        public void CalibrationComplete(Dictionary<string, object> calibrationParameters)
        {
            // This data will be logged with the block settings
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
