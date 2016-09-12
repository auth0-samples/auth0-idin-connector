using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Auth0.IdinConnectorSample.Models
{
    public class AuthorizeModel
    {
        public IEnumerable<IssuerModel> Issuers { get; set; }
    }

    public class IssuerModel
    {
        public string Name { get; set; }
        public string Country{ get; set; }
        public string AuthorizeUrl { get; set; }
    }
}