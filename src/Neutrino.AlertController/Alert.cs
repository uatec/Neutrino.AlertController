using System.Collections.Generic;

namespace Neutrino.AlertController
{
    public class Alert
    {
        public Dictionary<string, string> MetaData { get; set; }
        public string Name { get; set; }
        public string Target { get; set; }
        public int Warn { get; set; }
        public int Error { get; set; }
    }
}
