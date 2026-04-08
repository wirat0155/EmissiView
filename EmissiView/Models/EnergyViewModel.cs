using System;

namespace EmissiView.Models
{
    public class EnergyViewModel
    {
        public string MDB { get; set; }
        public string Plant { get; set; }
        public double kWh { get; set; }
        public string Status { get; set; }
        public DateTime LastUpdate { get; set; }
        public bool IsAlive { get; set; }
        public string LastSeenText { get; set; }
    }
}
