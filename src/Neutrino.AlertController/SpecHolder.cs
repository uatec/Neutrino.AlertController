using Newtonsoft.Json.Linq;

namespace Neutrino.AlertController
{
    public class SpecHolder
    {
        public string Kind { get; set; }

        public JObject Spec { get; set; }
    }
}
