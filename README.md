# Auth0 iDIN Connector Sample

An OAuth2 authorization server that integrates Auth0 with an [iDIN](http://www.connective.eu/financial/idin/) IDP:

* Auth0 integration is accomplished via OAuth2 as this sample implements the required OAuth2 Authorization Server endpoints such that Auth0 can use it as an Auth0 [Custom Social Connection](https://auth0.com/docs/extensions/custom-social-extensions).
* iDIN integration is accomplished by implementing the iDIN [Merchant role](#merchant) using the iDIN client library for .NET ([`BankId.Merchant.Library.dll`](https://github.com/auth0-samples/auth0-idin-connector/blob/master/lib/BankId.Merchant.Library.dll)), which enables a browser-based interaction with an iDIN [Acquirer](#acquirer).

## iDIN Roles

The following iDIN roles and their definitions are useful when understanding how this sample works and how it is configured:

### Consumer

*The role of Consumer is fulfilled by a natural person, holding credentials provided by the Issuer.*

In Auth0, this is a user.

### Merchant

*The role of Merchant must be fulfilled by a legal entity that wishes to identify its Consumers in an authentic manner.*

This iDIN Connector service sample acts as this role. A Merchant therefore does not equate to an Auth0 application (client), but to an Auth0 connection (specifically a Custom Social Connection). However, often times, there may only be a single Auth0 client that uses a single instance of the iDIN Connector.

### Acquirer

*The role of Acquirer must be fulfilled by a legal entity, providing iDIN services to its Merchants.*

This is the IDP that this service communicates with to perform an authentication flow. An example would be a chain of banks.

### Issuer

*The role of Issuer must be fulfilled by a legal entity, providing digital identities and credentials to its Consumers.*

when authenticating the Consumer will get presented with a list of possible Issuers. Once selected, they are redirected to that Issuer to perform the actual login. An example of an Issuer would be single bank where a user has a login they use to log into online banking.

### Routing Service

*The role of Routing Service must be fulfilled by an Acquirer or by a third party endorsed and contracted by an Acquirer.*

The Routing Service is a service hosted by the Acquirer. Any configuration required for this role will be provided by the Acquirer.

### Validation Service

*The role of Validation Service must be fulfilled by an Issuer or by a third party endorsed and contracted by an Issuer.*

For the purposes of this sample, the Validation Service is essentially the Issuer. In fact there is no configuration required that makes direct reference to the "Validation Service".

## Flow

When an authentication is performed in Auth0 that uses a Custom Social Connection configured to use this iDIN Connector sample, Auth0 will in turn perform an _OAuth2 Authorization Code Grant Flow_ with that sample instance. That flow will look something like this:

1. Auth0 redirects to the `/oauth2/authorize` endpoint of the iDIN Connector, passing query parameters `client_id`, `response_type`, `state`, and `redirect_uri`, which gets handled by the [OAuth2Controller.Authorize](SampleCode/Controllers/OAuth2Controller.cs#L40) MVC action method:

  ```
  Auth0 redirect ->
  https://IDIN_CONNECTOR/oauth2/authorize?client_id=CLIENT_ID&response_type=code&state=STATE&redirect_uri=https://AUTH0_DOMAIN/login/callback
  ```

2. This will render the [Authorize view](Auth0.IdinConnectorSample/Views/OAuth2/Authorize.cshtml), displaying a list of available Issuers that the user can log into.  
  > This list was obtained by calling the `Communicator.GetDirectory` method on the iDIN client library.

3. When the user clicks on a given issuer, they are basically calling `/oauth2/authorize` endpoint again, but passing an additional `issuer_id` query param, which allows the endpoint to initiate an actual authentication transaction against the iDIN IDP:  

  ```
  user clicks issuer link ->
  https://IDIN_CONNECTOR/oauth2/authorize?client_id=CLIENT_ID&response_type=code&state=STATE&redirect_uri=https://AUTH0_DOMAIN/login/callback&issuer_id=ISSUER_ID
  ```

4. If successful, an `IssuerAuthenticationUrl` is obtained from the transaction, which the endpoint redirects the user to:  

 ```
 iDIN Connector redirect ->
 https://ISSUER_AUTHENTICATION_URL/
 ```

5. The user then completes the Issuer login form and submits.  

6. The Issuer redirects back to the configured `BankId.Merchant.ReturnUrl` appSetting (which is the iDIN Connector's `/oauth2/callback` endpoint), passing query parameters `ec` (entrance code) and `trxid` (transaction ID), which gets handled by the [OAuth2Controller.Callback](SampleCode/Controllers/OAuth2Controller.cs#L141) MVC action method:  

  ```
  Issuer redirect ->
  https://IDIN_CONNECTOR/oauth2/callback?trxid=TRANSACTION_ID&ec=ENTRANCE_CODE
  ```

7. The `/oauth2/callback` endpoint uses the transaction ID to call the iDIN Status function to get the status of the authentication request. If successful, the endpoint stores the user profile and generates an OAuth2 `code` that Auth0 (the client) can use to obtain an access token.
8. Finally the endpoint redirects to the `redirect_uri` originally passed to the `/oauth2/authorize` endpoint (the Auth0 callback), passing back query parameters `code` and `state`:  

 ```
 iDIN Connector redirect ->
 https://AUTH0_DOMAIN/login/callback?code=CODE&state=STATE
 ```

9. The Auth0 callback endpoint performs the Authorization Code Flow code-to-token exchange by making a REST call to the iDIN Connector's `/oauth2/token` endpoint, passing all the required post body parameters, including the `code` that was returned in the previous step:  

 ```
 Auth0 /login/callback:
 POST https://IDIN_CONNECTOR/oauth2/token
 client_id=CLIENT_ID&client_secret=CLIENT_SECRET&grant_type=authorization_code&code=CODE&redirect_uri=https://AUTH0_DOMAIN/login/callback
 ```

10. The `/oauth2/token` endpoint handles this call (via the [OAuth2Controller.Token](Auth0.IdinConnectorSample/Controllers/OAuth2Controller.cs#L208) MVC action method) by fetching the user profile from cache and generating an `access_token`, which it returns in a JSON response:  

  ```json
  {
    "access_token": "ACCESS_TOKEN",
    "token_type": "bearer"
  }
  ```

11. Auth0 then uses the returned `access_token` to make another REST call to the iDIN Connector's `/oauth2/userinfo` endpoint to obtain the full user profile, passing the `access_token` in the `Authorization` HTTP request header:  

 ```
 Auth0 /login/callback:
 GET https://IDIN_CONNECTOR/oauth2/userinfo
 Authorization: bearer ACCESS_TOKEN
 ```

12. The `/oauth2/userinfo` endpoint handles this call (via the [OAuth2Controller.UserInfo](Auth0.IdinConnectorSample/Controllers/OAuth2Controller.cs#L259) MVC action method), fetches the user profile from cache, and returns it as a JSON response:  

  ```json
  {
    "urn:nl:bvn:bankid:1.0:consumer.bin": "USER_ID",
    "urn:nl:bvn:bankid:1.0:consumer.prefferedlastname": "Lastname",
    ...
  }
  ```

13. Auth0 then maps these claims over to Auth0 profile properties.

## Responses

As shown above once a user has successfully performed an authentication flow via iDIN, the iDIN Connector will make available the iDIN user profile via the `/oauth2/userinfo` endpoint. However, if a non-successful result occurs, per the OAuth2 spec ([4.1.2.1](https://tools.ietf.org/html/rfc6749#section-4.1.2.1) and [5.2](https://tools.ietf.org/html/rfc6749#section-5.2)), the iDIN Connector will respond with appropriate OAuth2 errors, depending on the situation.

For errors that have an `error` field value of either `access_denied` or `server_error`, the accompanying `error_description` field will contain a serialized JSON object that contains more structured detail on the underlying cause. These are important because Auth0 will pass along this `error_description` to the application (eg. as an OAuth2, SAML, or WS-Fed error response). The application can then deserialize the JSON into a structured object that it can use to make decisions based on the data in the object.

### Access Denied Errors

An `access_denied` error occurs if the authentication does not succeed (eg. user didn't present proper credentials or they took too long to respond). In this case, the `error_description` field will contain a string value that's a serialized JSON object that looks like this:

```json
{
  "error": "access_denied",
  "error_description": "{\"idin_status\": \"Cancelled\", \"message\": \"IDIN transaction did not return a successful status. Status = Cancelled\"}"
}
```

Where:
* `idin_status` the actual final status returned by iDIN (eg. `Cancelled`)
* `message`: a user-friendly error message generated by the iDIN Connector
* `idin_status_date_timestamp`: the time at which the Issuer established the current status of the transaction (not populated for statuses `Open` and `Pending`)

### Server Errors

A `server_error` error occurs if something unexpected happens in the iDIN system itself during a transaction. In this case, the `error_description` field will contain a string value that's a serialized JSON object that looks like this:

```json
{
  "error": "server_error",
  "error_description": "{\"idin_error_code\": \"SO1100\", \"idin_error_message\": \"Failure in system\", \"idin_error_details\": \"System generating error: TEST NL 20007, error response\", \"message\": \"The selected bank is currently unavailable. Please try again later.\"}"
}
```
Where:
* `idin_error_code`: the iDIN error code
* `idin_error_message`: descriptive text accompanying `idin_error_code`
* `idin_error_details`: details of the error
* `message`: a user-friendly error message generated by the Issuer (may not be populated)
* `idin_suggested_action`: a message that suggests a means to resolve the error (may not be populated)

## Deployment

### Redis

The iDIN Connector uses Redis to store session state between requests in an authentication flow. If you are using the "Deploy to Azure" button [below](#azure), it will automatically deploy a Redis cache for the iDIN Connector instance. If not, you will need to stand up your own Redis service and configure the `RedisConnectionString` setting in the [`appSettings.config` file](./Auth0.IdinConnectorSample/appSettings.config).

### The Service

You can deploy the iDIN Connector service anywhere that can host an ASP.NET web application. We describe two options for this below:

#### Azure

You can deploy an instance of the iDIN Connector service itself to your own Azure account by clicking this button:

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

\* The `DirectoryUrl`, `TransactionUrl` and `StatusUrl` can be the same.

** Base-64 formatted string representations of certificates can be generated from the certificate file itself (`.cer` or `.p12`) using an approach [like this](http://superuser.com/questions/120796/os-x-base64-encode-via-command-line).

> NOTE: Please review the [iDIN Roles](#idin-roles) section above for proper understanding of the terms used by the iDIN-specific configuration parameters.

#### Your Infrastructure

To deploy an instance of the iDIN Connector service to your own server, be sure to edit the [appSettings.config](./appSettings.config) file. You can use the table in the [Azure](#azure) section above as a guide for these same settings. Please refer to the [Redis](#redis) section above for configuring your own instance of Redis.

### Auth0

To finish the integration, you then will need to create a social connection in your Auth0 account that points to the instance you've set up in Azure. If you haven't done so already, sign into the Auth0 Dashboard and install the Custom Social Connections extension in the **Extensions** tab. Then create a new connection with the following settings (`AZURE_SITE_NAME` is the name of the site you chose when you created the Azure web app):

* **Name**: `idin-connector`
* **Client ID**: Use the `Auth0IdinConnectorClientId` parameter you used in the [Azure](#azure) section above. The defaults is [here](https://github.com/auth0-samples/auth0-idin-connector/blob/master/azuredeploy.json#L89).
* **Client Secret**: Use the `Auth0IdinConnectorClientSecret` parameter you used in the [Azure](#azure) section above. The defaults is [here](https://github.com/auth0-samples/auth0-idin-connector/blob/master/azuredeploy.json#L94).
* **Fetch User Profile Script**: Something similar to the follow:  
  ```js
  function(accessToken, ctx, cb) {
    request.get({
      uri: 'https://AZURE_SITE_NAME.azurewebsites.net/oauth2/userinfo',
      headers: {
        authorization: 'bearer ' + accessToken
      },
      json: true
    }, function(err, response, body) {
      if (err) return cb(err);

      var familyName = body['urn:nl:bvn:bankid:1.0:consumer.prefferedlastname'];
      var givenName = body['urn:nl:bvn:bankid:1.0:consumer.initials'];

      cb(null, {
        user_id: body['urn:nl:bvn:bankid:1.0:consumer.bin'],
        family_name: familyName,
        given_name: givenName,
        name: givenName + ' ' + familyName,
        legallastname: body['urn:nl:bvn:bankid:1.0:consumer.legallastname'],
        dateofbirth: body['urn:nl:bvn:bankid:1.0:consumer.dateofbirth'],
        address: {
          street: body['urn:nl:bvn:bankid:1.0:consumer.street'],
          houseno: body['urn:nl:bvn:bankid:1.0:consumer.houseno'],
          postalcode: body['urn:nl:bvn:bankid:1.0:consumer.postalcode'],
          city: body['urn:nl:bvn:bankid:1.0:consumer.city'],
          country: body['urn:nl:bvn:bankid:1.0:consumer.country']
        }
      });
    });
  }
  ```
* **Authorization URL**: `https://AZURE_SITE_NAME.azurewebsites.net/oauth2/authorize`
* **Token URL**: `https://AZURE_SITE_NAME.azurewebsites.net/oauth2/token`
* **Scope**: (none)
* **Custom Headers**: (none)

You can now use this connection with any Client in Auth0. [Click here](https://auth0.com/docs/extensions/custom-social-extensions) to learn more about how to do that and everything else about custom social connections.
