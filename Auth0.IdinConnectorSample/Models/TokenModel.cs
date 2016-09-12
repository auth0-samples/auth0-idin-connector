using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Auth0.IdinConnectorSample.Models
{
    public class TokenModel
    {
        public string client_id { get; set; }
        public string client_secret { get; set; }
        public string grant_type { get; set; }
        public string code { get; set; }
        public string redirect_uri { get; set; }
    }
}