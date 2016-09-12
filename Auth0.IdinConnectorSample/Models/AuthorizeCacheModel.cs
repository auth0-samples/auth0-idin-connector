using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Auth0.IdinConnectorSample.Models
{
    public class AuthorizeCacheModel
    {
        public string State { get; set; }
        public string RedirectUri { get; set; }
    }
}