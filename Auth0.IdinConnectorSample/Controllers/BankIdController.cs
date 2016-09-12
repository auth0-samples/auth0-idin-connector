using System;
using System.Web.Mvc;
using System.Xml;
using Auth0.IdinConnectorSample.Models;
using BankId.Merchant.Library;

namespace Auth0.IdinConnectorSample.Controllers
{
    public class BankIdController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            return View("Directory");
        }

        #region Directory
        [HttpGet]
        public ActionResult Directory()
        {
            var model = new DirectoryModel
            {
                DirectoryUrl = Configuration.Instance.AcquirerDirectoryUrl.AbsoluteUri,
                MerchantId = Configuration.Instance.MerchantId,
                ReturnUrl = Configuration.Instance.MerchantReturnUrl,
                SubId = Configuration.Instance.MerchantSubId
            };

            return View(model);
        }


        [HttpPost]
        public ActionResult Directory(DirectoryModel model)
        {
            Configuration.Instance.AcquirerDirectoryUrl = new Uri(model.DirectoryUrl);
            Configuration.Instance.MerchantId = model.MerchantId;
            Configuration.Instance.MerchantReturnUrl = model.ReturnUrl;
            Configuration.Instance.MerchantSubId = model.SubId;

            try
            {
                var communicator = new Communicator();
                model.Source = communicator.GetDirectory();
            }
            catch (Exception ex)
            {
                model.CustomError = ex.Message;
            }

            return View("DirectoryResponse", model);
        }
        #endregion

        #region AuthenticationRequest
        [HttpGet]
        public ActionResult AuthenticationRequest()
        {
            var model = new TransactionModel
            {
                IssuerID = "INGBNL2A",
                ExpirationPeriod = "PT5M",
                EntranceCode = "entranceCode",
                MerchantReference = "merchantReference",
                RequestedServiceId = "21952",
                AcquirerTransactionURL = Configuration.Instance.AcquirerTransactionUrl.AbsoluteUri,
                MerchantId = Configuration.Instance.MerchantId,
                ReturnUrl = Configuration.Instance.MerchantReturnUrl,
                SubId = Configuration.Instance.MerchantSubId
            };

            return View(model);
        }

        [HttpPost]
        public ActionResult AuthenticationRequest(TransactionModel model)
        {
            Configuration.Instance.AcquirerTransactionUrl = new Uri(model.AcquirerTransactionURL);
            Configuration.Instance.MerchantId = model.MerchantId;
            Configuration.Instance.MerchantReturnUrl = model.ReturnUrl;
            Configuration.Instance.MerchantSubId = model.SubId;

            var assuranceLevel = model.LOA == "nl:bvn:bankid:1.0:loa2" ? AssuranceLevel.Loa2 : AssuranceLevel.Loa3;

            try
            {
                var communicator = new Communicator();

                var serviceid = (ServiceIds)Enum.Parse(typeof(ServiceIds), model.RequestedServiceId);
                TimeSpan? time = null;

                if (!string.IsNullOrEmpty(model.ExpirationPeriod))
                    time = XmlConvert.ToTimeSpan(model.ExpirationPeriod);

                var transactionRequest = new AuthenticationRequest(model.EntranceCode, serviceid, model.IssuerID,
                    model.MerchantReference, assuranceLevel, time, model.Language);

                model.Source = communicator.NewAuthenticationRequest(transactionRequest);
            }
            catch (Exception ex)
            {
                model.CustomError = ex.Message;
            }

            return View("AuthenticationResponse", model);
        }
        #endregion


        #region GetResponse
        [HttpGet]
        public ActionResult GetResponse()
        {
            var model = new StatusModel
            {
                StatusUrl = Configuration.Instance.AcquirerStatusUrl.AbsoluteUri,
                TransactionId = "1234567890123456",
                MerchantId = Configuration.Instance.MerchantId,
                ReturnUrl = Configuration.Instance.MerchantReturnUrl,
                SubId = Configuration.Instance.MerchantSubId
            };

            return View(model);
        }

        [HttpPost]
        public ActionResult GetResponse(StatusModel model)
        {
            Configuration.Instance.MerchantId = model.MerchantId;
            Configuration.Instance.MerchantReturnUrl = model.ReturnUrl;
            Configuration.Instance.MerchantSubId = model.SubId;
            Configuration.Instance.AcquirerStatusUrl = new Uri(model.StatusUrl);

            try
            {
                var communicator = new Communicator();

                var statusRequest = new StatusRequest(model.TransactionId);
                model.Source = communicator.GetResponse(statusRequest);
            }
            catch (Exception ex)
            {
                model.CustomError = ex.Message;
            }
            return View("StatusResponse", model);
        }
        #endregion
    }
}