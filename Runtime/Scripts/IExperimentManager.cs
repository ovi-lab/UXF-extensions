using System;
using System.Collections.Generic;
using UnityEngine.Events;
using UXF;

namespace ubco.ovilab.uxf.extensions
{
    public interface IExperimentManager<out T>
    {
        /// <summary>
        /// The participant index configuted through experiment-server.
        /// Once the exerpiment starts, this should be the same as 
        /// <see cref="UXF.Session.ppid"/>.
        /// </summary>
        public int ParticipantIndex { get; }

        /// <summary>
        /// The total number of blocks configured in the experiment-server.
        /// Note that this would not match the length of <see cref="UXF.Session.blocks"/>
        /// as blocks are added dynamically as they are recieved from the
        /// experiment-server.
        /// </summary>
        public int ConfigsLength { get; }

        /// <summary>
        /// The UnityEvent that will be triggereed when the CurrentState changes.
        /// </summary>
        public UnityEvent<ExperimentManagerState> StateChanged { get; }

        /// <summary>
        /// The current state of the ExperimentManager.
        /// <seealso cref="ExperimentManagerState"/>
        /// </summary>
        public ExperimentManagerState CurrentState { get; }

        /// <summary>
        /// All calibration methods configured through
        /// <see cref="AddCalibrationMethod"/> should call this method
        /// to transfer control back to the
        /// <see cref="ExperimentManager"/>.
        /// </summary>
        /// <param name="calibrationParameters">This value will be
        /// recorded in the settings of the block
        /// (<see cref="ConfigureBlockBase"/>) </param>
        public void CalibrationComplete(Dictionary<string, object> calibrationParameters);

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
        public void AddCalibrationMethod(String name, Action<T> action);

        /// <SUMMARY>
        /// Proceed to next state.
        /// </summary>
        public void MoveToNextState();

        /// <summary>
        /// Requests the ExperimentManager to transition to the specified <paramref
        /// name="targetState"/>, invoking <paramref name="callback"/> upon completion,
        /// cancellation, or session end interruption.  If another state transition is
        /// already in progress, the prior callback will be cancelled and invoked with
        /// <see cref="MoveToStateResult.Cancelled"/>.
        /// </summary>
        /// <param name="targetState">The state to transition to.</param>
        /// <param name="callback">
        /// Callback invoked when the transition completes, is cancelled, or interrupted
        /// by session end.
        /// Arguments: the current state at completion/interruption/cancellation and the
        /// result code.
        /// </param>
        public void MoveToState(ExperimentManagerState targetState, Action<ExperimentManagerState, MoveToStateResult> callback);

        /// <summary>
        /// Setup the initial values used when Session is started <see cref="UXF.Session.Begin"/>.
        /// Calling after session starts will throw an <see cref="System.InvalidOperationException"/>.
        /// If session is started outside of this extensions (e.g., from UI), then the parameters
        /// already set when <see cref="UXF.Session.Begin"/> was called are taken precedence.
        /// That is, `experimentName` and `sessionNumber` passed through this method are ignored
        /// and only new keys in `participantDetails` and `settings` are added.
        /// </summary>
        public void SessionBeginParams(string experimentName, int sessionNumber, Dictionary<string, object> participantDetails, Settings settings);
    }
}
