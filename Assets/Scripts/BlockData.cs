namespace ubc.ok.ovilab.uxf.extensions
{
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
