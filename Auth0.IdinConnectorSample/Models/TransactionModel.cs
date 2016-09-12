using BankId.Merchant.Library;

namespace Auth0.IdinConnectorSample.Models
{
    public class TransactionModel : AdvancedOptions
    {
        public string AcquirerTransactionURL { get; set; }

        public string TransactionID { get; set; }
        public string IssuerID { get; set; }
        public string ExpirationPeriod { get; set; }
        public string Language { get; set; }
        public string EntranceCode { get; set; }
        public string MerchantReference { get; set; }
        public string RequestedServiceId { get; set; }
        public string LOA { get; set; }

        public AuthenticationResponse Source { get; set; }
    }
}