namespace ubc.ok.ovilab.uxf.extensions
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
        public string name;
        public int participant_index;
        public string calibrationName;

        public override string ToString()
        {
            return $"Name: {name}" +
                $"Participant Index: {participant_index}" +
                $"Calibration name: {calibrationName}";
        }
    }
}
