namespace CodeChallenge
{
    public class ResultInfo
    {
        public int DeviceId { get; set; }
        public required string DeviceName { get; set; }
        public double? LastAverage { get; set; }
        public double Average { get; set; }
        public string Trending { get; set; } = "-";
        public bool IsValid { get; set; } = true;
    }
}