using System.Diagnostics;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Configuration;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Auth0.IdinConnectorSample
{
    public class MvcApplication : HttpApplication
    {
        private const string merchantCertKey = "BankId.Merchant.Certificate";
        private const string routingServiceKey = "BankId.RoutingService.Certificate";
        private const string samlCertKey = "BankId.SAML.Certificate";

        private static void LoadCertificate (string key, IDictionary<string, X509Certificate2> certificates)
        {
            X509Certificate2 cert = null;

            // attempt to get base64 string directly from config
            var base64String = ConfigurationManager.AppSettings[key + "{Base64String}"];
            if (base64String == null)
            {
                // not there - check for a config link
                var existingCertKey = ConfigurationManager.AppSettings[key + "{CertKey}"];
                if (existingCertKey != null)
                {
                    try
                    {
                        cert = certificates[existingCertKey];
                    }
                    catch (KeyNotFoundException ex)
                    {
                        throw new ConfigurationErrorsException("Certificate key referenced by iDIN certificate '" + key + "' does not point to an existing certificate: " + existingCertKey, ex);
                    }
                }
            } else
            {
                byte[] data;
                try
                {
                    data = Convert.FromBase64String(base64String);
                }
                catch (FormatException ex)
                {
                    throw new ConfigurationErrorsException("The base64 string value configured for iDIN certificate '" + key + "' is invalid.", ex);
                }
                // check for a password
                var password = ConfigurationManager.AppSettings[key + "{Password}"];
                // create new cert object
                try
                {
                    cert = password == null ?
                        new X509Certificate2(data) :
                        new X509Certificate2(data, password, X509KeyStorageFlags.MachineKeySet);
                }
                catch (CryptographicException ex)
                {
                    throw new ConfigurationErrorsException("An error occurred trying to load iDIN certificate '" + key + ".", ex);
                }
            }

            if (cert == null)
            {
                throw new ConfigurationErrorsException(
                    "Could not obtain iDIN certificate '" + key + "'. Make sure a valid '" + key + "{Base64String}' or '" + key + "{ConfigKey}' entry exists in the .config file.");
            } else
            {
                certificates[key] = cert;
            }
        }

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            ModelBinders.Binders.Add(typeof(decimal), new DecimalModelBinder());

            // certificates
            var certificates = new Dictionary<string, X509Certificate2>();
            LoadCertificate(merchantCertKey, certificates);
            LoadCertificate(routingServiceKey, certificates);
            LoadCertificate(samlCertKey, certificates);

            BankId.Merchant.Library.Configuration.Setup(new BankId.Merchant.Library.Configuration(
                ConfigurationManager.AppSettings["BankId.Merchant.AcquirerID"],
                ConfigurationManager.AppSettings["BankId.Merchant.MerchantID"],
                new Uri(ConfigurationManager.AppSettings["BankId.Merchant.ReturnUrl"]),
                new Uri(ConfigurationManager.AppSettings["BankId.Acquirer.DirectoryUrl"]),
                new Uri(ConfigurationManager.AppSettings["BankId.Acquirer.TransactionUrl"]),
                new Uri(ConfigurationManager.AppSettings["BankId.Acquirer.StatusUrl"]),
                certificates[merchantCertKey],
                certificates[routingServiceKey],
                certificates[samlCertKey],
                Server.MapPath("~/App_Data/iDINServiceLogs"),
                false,
                "%Y-%M-%D\\%h%m%s.%f-%a.xml"
            ));
            
            Trace.Listeners.Add(new CustomTraceListener());
        }
    }
}
