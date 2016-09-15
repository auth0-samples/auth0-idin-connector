# Auth0 iDIN Connector Sample

## Deployment

### Azure

You can deploy an instance of this sample to your own Azure account by clicking this button:

[![Deploy to Azure](http://azuredeploy.net/deploybutton.png)](https://azuredeploy.net/)

During the install, you will be prompted for several common Azure deployment parameters (eg. App Name) as well as the following parameters that are specific to the iDIN configuration:

| Name | Description |
| --- | --- |
| `AcquirerId` | A unique 4-digit identifier of the Acquirer within an iDx based product, assigned by the product owner when registering the Acquirer. |
| `MerchantId` | The 10-digit contract number for iDIN. The Merchant obtains this ID after registration for iDIN. |
| `DirectoryUrl` \* | The web address of the Acquirer’s Routing service platform from where the list of Issuers is retrieved (using a directory request). |
| `TransactionUrl` \* | The web address of the Acquirer’s Routing Service platform where the transactions (authentication requests) are initiated. |
| `StatusUrl` \* | The web address of the Acquirer’s Routing Service platform to where the library sends status request message |
| `MerchantCertificate` ** | A base-64 formatted string representation of the Merchant certificate, which is the certificate that contains the private key used to sign messages sent by the Merchant to the Acquirer’s Routing Service platform. The public key of this certificate is also used by the Acquirer to authenticate incoming messages from the Merchant. |
| `MerchantCertificatePassword` | The password used to decrypt the `MerchantCertificate` data if its is in PFX format. |
| `AcquirerCertificate` ** | A base-64 formatted string representation of the Acquirer (aka Routing Service) certificate, which is the certificate that contains the public key used to validate incoming messages from Acquirer to the Merchant. |
| `SamlCertificateFormatType` / `SamlCertificateData` ** | The type and data of the SAML certificate, which is the certificate of the Merchant that contains the private key used to decrypt the SAML Response. If `SamlCertificateFormatType` is `Base64String`, then `SamlCertificateData` should be a base-64 encoded string representation of the certificate. If `SamlCertificateFormatType` is `CertKey`, then `SamlCertificateData` should contain the key of an existing certificate, which really should only be `BankId.Merchant.Certificate` (the Merchant Certificate). |
| `Auth0IdinConnectorClientId` / `Auth0IdinConnectorClientSecret` | The client ID and secret that the Auth0 Custom Social Connection will use to communicate with the iDIN Connector. |
| `Auth0Domain` | The Auth0 tenant domain that the iDIN Connector will be redirecting back to after authentication. |
| `RedisConnectionString` | The connection string used to connect to the Redis service, which is used to manage Connector state between requests. |

* \* The `DirectoryUrl`, `TransactionUrl` and `StatusUrl` can be the same.
* ** Base-64 formatted string representations of certificates can be generated from the certificate file itself (`.cer` or `.p12`) using an approach [like this](http://superuser.com/questions/120796/os-x-base64-encode-via-command-line).
