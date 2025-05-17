#if UNITY_EDITOR
#endif

namespace ubco.ovilab.uxf.extensions
{
    /// <summary>
    /// Represents the result of a <see cref="ExperimentManager.MoveToState"/>
    /// operation. Indicates whether the attempt to move to the requested
    /// state was cancelled, interrupted by a session ending, or
    /// successfully completed.
    /// </summary>
    public enum MoveToStateResult
    {
        /// <summary>
        /// The state change was cancelled, typically because a new
        /// request was made before completion.
        /// </summary>
        Cancelled,

        /// <summary>
        /// The state change was interrupted because the session ended.
        /// </summary>
        SessionEnded,

        /// <summary>
        /// The ExperimentManager successfully reached the requested
        /// target state.
        /// </summary>
        MovedToTargetState
    }
}
