namespace ubco.ovilab.uxf.extensions
{
    /// <summary>
    /// Object to parse json data from experiment server. An extension
    /// of this class can be used as the generic type in <see
    /// cref="ExperimentManager"/>. Data from experiment server would
    /// automatically add the name and participant_index. The
    /// calibrationName would need to be set in each block in the
    /// experiment server config file.
    /// </summary>
    public class BlockData {
        /// <summary>
        /// The name of the block.
        /// </summary>
        public string name;
        /// <summary>
        /// The participant index.
        /// </summary>
        public int participant_index;
        /// <summary>
        /// The calibration name.
        /// </summary>
        public string calibrationName;
        /// <summary>
        /// The block index in the list of blocks configured in
        /// experiment-server.
        /// </summary>
        public int block_id;

        public override string ToString()
        {
            return $"Name: {name}" +
                $"Participant Index: {participant_index}" +
                $"Calibration name: {calibrationName}" +
                $"Block ID:  {block_id}";
        }
    }
}
