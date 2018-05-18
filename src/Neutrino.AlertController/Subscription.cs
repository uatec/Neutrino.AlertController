using System.Collections.Generic;
using Neutrino.Seyren.Domain;

namespace Neutrino.AlertController
{
    public class Subscription
    {
        public string Target { get; set; }
        public SubscriptionType Type { get; set; }
        public Dictionary<string, string> Selector { get; set; }
    }
}
