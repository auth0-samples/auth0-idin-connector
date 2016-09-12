using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Auth0.IdinConnectorSample.Models
{
    public class CallbackCacheModel
    {
        public string RedirectUri { get; set; }
        public Dictionary<string, string> Profile { get; set; }
    }
}