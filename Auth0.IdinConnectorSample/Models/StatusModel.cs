using BankId.Merchant.Library;

namespace Auth0.IdinConnectorSample.Models
{
    public class StatusModel : AdvancedOptions
    {
        public string StatusUrl { get; set; }
        public string TransactionId { get; set; }

        public StatusResponse Source { get; set; }
    }
}