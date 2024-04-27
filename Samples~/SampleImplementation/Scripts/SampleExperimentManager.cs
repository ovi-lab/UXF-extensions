using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UXF;
using ubco.ovilab.uxf.extensions;


namespace ubco.ovilab.uxf.extensions.sample
{
    /// <summary>
    /// Sample implemenation of <see cref="ubco.ovilab.uxf.extensions.ExperimentManager"/>.
    /// </summary>
    public class SampleExperimentManager : ExperimentManager<SampleBlockData>
    {
        [SerializeField]
        private Button button1;
        [SerializeField]
        private Button button2;

        private bool showingButton1 = false;

        #region UXF-Extensions
        /// <inheritdoc />
        public override void Start()
        {
            base.Start();
            // This makes sure there is a "conditionVal" is added as a column in the UXF trial results
            // Note that the values  "blockName", "canceled", and "calibrationName" already added.
            Session.instance.settingsToLog.AddRange(new List<string>(){ "conditionVal" });
        }

        /// <inheritdoc /> 
        protected override void OnSessionBegin(Session session)
        {
            // Hiding all buttons at the begining of the session
            HideAllButtons();
        }

        /// <inheritdoc />
        protected override void ConfigureBlock(SampleBlockData el, Block block, bool lastBlockCancelled)
        {
            // Creating 10 trials for each block
            for (int i = 0; i < 10; ++i)
            {
                Trial trial = block.CreateTrial();
                block.settings.SetValue("blockName", el.name);
                block.settings.SetValue("conditionVal", el.conditionVal);
            }
        }

        /// <inheritdoc />
        protected override void OnBlockBegin(Block block)
        {
            // using "conditionVal"
            int conditionVal = block.settings.GetInt("conditionVal");

            SetupButton(button1, 1, conditionVal);
            SetupButton(button2, 2, conditionVal);
        }

        /// <inheritdoc /> 
        protected override void OnTrialBegin(Trial trial)
        {
            // enable/disable the correct button
            button1.interactable = showingButton1;
            button2.interactable = !showingButton1;
        }

        /// <inheritdoc /> 
        protected override void OnTrialEnd(Trial trial) {}

        /// <inheritdoc /> 
        protected override void OnBlockEnd(Block block)
        {
            // Hiding all buttons once block ends
            HideAllButtons();
        }

        /// <inheritdoc /> 
        protected override void OnSessionEnd(Session session) {}
        #endregion

        /// <summary>
        /// Hides all target buttons
        /// </summary>
        private void HideAllButtons()
        {
            button1.gameObject.SetActive(false);
            button1.onClick.RemoveAllListeners();
            button2.gameObject.SetActive(false);
            button2.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// Setup button for trials
        /// </summary>
        private void SetupButton(Button button, int index, int conditionVal)
        {
            // Show button
            button.gameObject.SetActive(true);

            // Setup callback
            button.onClick.AddListener(() => {
                showingButton1 = !showingButton1;
                try
                {
                    Session.instance.EndCurrentTrial();
                    Session.instance.BeginNextTrial();
                }
                catch (NoSuchTrialException)
                {
                    AddToOutpuText($"Block ended.");
                }
            });

            // Use conditionVal
            button.GetComponentInChildren<TMPro.TMP_Text>().text = conditionVal.ToString();
        }
    }

    /// <summary>
    /// Block data used by <see cref="SampleExperimentManager"/>
    /// </summary>
    public class SampleBlockData:BlockData
    {
        public int conditionVal;
    }
}
