using BankId.Merchant.Library;

namespace Auth0.IdinConnectorSample.Models
{
    public class DirectoryModel : AdvancedOptions
    {
        public string DirectoryUrl { get; set; }

        public DirectoryResponse Source { get; set; }
    }
}