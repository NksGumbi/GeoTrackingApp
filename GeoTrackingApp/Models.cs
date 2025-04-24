using Newtonsoft.Json;
using System.Collections.Generic;

namespace GeoTrackingApp.Models
{
    public class GeoJsonData
    {
        public string type { get; set; }
        public List<GeoJsonFeature> features { get; set; }
    }

    public class GeoJsonFeature
    {
        public string type { get; set; }
        public Geometry geometry { get; set; }
        public Properties properties { get; set; }
    }

    public class Geometry
    {
        public string type { get; set; }
        public double[] coordinates { get; set; }
    }

    public class Properties
    {
        [JsonProperty("Signal Strength")]
        public double SignalStrength { get; set; }
        public double Altitude { get; set; }
        [JsonProperty("Date & Time")]
        public string DateTime { get; set; }
        public double Frequency { get; set; }
        [JsonProperty("TN Bearing")]
        public double TNBearing { get; set; }
    }

    public class ConfigurationSettings
    {
        public string DefaultDirectory { get; set; }
    }
}