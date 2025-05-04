namespace ubco.ovilab.uxf.extensions
{
    /// <summary>
    /// Represents the different states the ExperimentManager can be in.
    /// </summary>
    public enum ExperimentManagerState
    {
        /// <summary>
        /// The experiment manager will be in this state at the begining.
        /// </summary>
        UninitializedSession,

        /// <summary>
        /// The experiment manager is waiting to receive experiment-related information
        /// from the data source of the experiment manager. Once the information is
        /// received, it will immediately retrieve the block configuration data from
        /// the data source.
        /// </summary>
        /// <seealso cref="ExperimentManager.dataSource"/>.
        AwaitingInitialization,

        /// <summary>
        /// The experiment manager is waiting to receive the block config from the data
        /// source.
        /// </summary>
        AwaitingBlockConfig,

        /// <summary>
        /// The block configuration has been recieved and the experiment manager is
        /// ready to run the calibration if there is one configured.
        /// </summary>
        /// <seealso cref="ExperimentManager.BlockRecieved"/>
        ReadyForCalibration,

        /// <summary>
        /// If a calibration function is configured and the block config triggers the
        /// calibration, the experiment manager wait for it to complete.  If no
        /// configuration is to be triggered, will move to
        /// </summary>
        /// <see cref="ReadyForBlockBegin"/>.
        Calibrating,

        /// <summary>
        /// Block config has been recieved and calibration function has been exectued
        /// (if any). The block can begin.
        /// </summary>
        ReadyForBlockBegin,

        /// <summary>
        /// The block has begun.
        /// </summary>
        /// <seealso cref="UXF.Session.onBlockBegin"/>
        BlockBegan,

        /// <summary>
        /// The block ended successfully.
        /// </summary>
        /// <seealso cref="BlockCancelled"/>
        /// <seealso cref="UXF.Session.onBlockEnd"/>
        BlockEnded,

        /// <summary>
        /// The block was cancelled.
        /// </summary>
        /// <seealso cref="BlockEnded"/>
        /// <seealso cref="UXF.Session.onBlockEnd"/>
        BlockCancelled,

        /// <summary>
        /// The UXF session has ended.
        /// </summary>
        /// <seealso cref="UXF.Session.onSessionEnd"/>
        SessionEnded,
    }
}
