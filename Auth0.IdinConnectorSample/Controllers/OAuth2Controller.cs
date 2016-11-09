using Auth0.IdinConnectorSample.Models;
using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using StackExchange.Redis;
using Newtonsoft.Json;
using BankId.Merchant.Library;

namespace Auth0.IdinConnectorSample.Controllers
{
    public class OAuth2Controller : Controller
    {
        private string _auth0IdinConnectorClientId = ConfigurationManager.AppSettings["Auth0IdinConnectorClientId"]; 
        private string _auth0IdinConnectorClientSecret = ConfigurationManager.AppSettings["Auth0IdinConnectorClientSecret"];
        private string _auth0IdinConnectorClientAllowedCallbackUrl = ConfigurationManager.AppSettings["Auth0IdinConnectorClientAllowedCallbackUrl"];

        private static Lazy<IConnectionMultiplexer> _lazyConnection = new Lazy<IConnectionMultiplexer>(() =>
        {
            return ConnectionMultiplexer.Connect(ConfigurationManager.AppSettings["RedisConnectionString"]);
        });
        private static IConnectionMultiplexer _connection
        {
            get { return _lazyConnection.Value; }
        }

        private static string BuildAuthorizeWithIssuerUrl(HttpRequestBase request, string issuerId)
        {
            var uri = new UriBuilder(request.Url);

            var query = HttpUtility.ParseQueryString(uri.Query);
            query["issuer_id"] = issuerId;
            uri.Query = query.ToString();

            return uri.Path + uri.Query;
        }

        private RedirectResult OAuth2AuthorizationError(string redirectUrl, string state, string error, string errorDescription)
        {
            var uri = new UriBuilder(redirectUrl);

            var query = HttpUtility.ParseQueryString(uri.Query);
            if (!string.IsNullOrEmpty(state))
                query["state"] = state;
            query["error"] = error;
            query["error_description"] = errorDescription;
            uri.Query = query.ToString();

            return Redirect(uri.ToString());
        }

        private JsonResult OAuth2TokenError(string error, string errorDescription)
        {
            Response.TrySkipIisCustomErrors = true;
            Response.StatusCode = 400;
            return Json(new
            {
                error,
                error_description = errorDescription
            });
        }

        // GET: /oauth2/authorize
        [HttpGet]
        public async Task<ActionResult> Authorize()
        {
            // Redirect URI
            var redirectUri = Request.QueryString["redirect_uri"];
            if (string.IsNullOrEmpty(redirectUri))
            {
                redirectUri = _auth0IdinConnectorClientAllowedCallbackUrl;
            }
            else
            {
                // make sure redirect uri is allowed
                var uri = new Uri(redirectUri);
                var allowedUri = new Uri(_auth0IdinConnectorClientAllowedCallbackUrl);
                if (uri.Scheme != allowedUri.Scheme ||
                    uri.Authority != allowedUri.Authority ||
                    uri.AbsolutePath != allowedUri.AbsolutePath ||
                    uri.Port != allowedUri.Port)
                {
                    return new HttpStatusCodeResult(400, "The redirect_uri is now allowed: " + redirectUri);
                }
            }

            // State
            var state = Request.QueryString["state"];

            // Client ID
            var clientId = Request.QueryString["client_id"];
            if (string.IsNullOrEmpty(clientId))
            {
                return OAuth2AuthorizationError(redirectUri, state, "invalid_client", "Missing required parameter: client_id");
            }
            if (clientId != _auth0IdinConnectorClientId)
            {
                return OAuth2AuthorizationError(redirectUri, state, "invalid_client", "Unknown client_id: " + clientId);
            }

            // Response Type
            var responseType = Request.QueryString["response_type"];
            if (string.IsNullOrEmpty(responseType))
            {
                return OAuth2AuthorizationError(redirectUri, state, "invalid_request", "Missing required parameter: response_type");
            }
            if (responseType != "code")
            {
                return OAuth2AuthorizationError(redirectUri, state, "unsupported_response_type", "Unsupported response_type: " + responseType);
            }
                
            var communicator = new Communicator();

            // Issuer ID
            var issuerId = this.Request.QueryString["issuer_id"];
            if (string.IsNullOrEmpty(issuerId))
            {
                // get list of IDIN issuers and display them to the users
                var directoryResponse = await Task.Run(() => communicator.GetDirectory());
                if (directoryResponse.IsError)
                {
                    return OAuth2AuthorizationError(redirectUri, state, "server_error", "Error getting IDIN directory: " + directoryResponse.Error.ErrorMessage);
                }

                return View(new AuthorizeModel
                {
                    Issuers = directoryResponse.Issuers.Select(i => new IssuerModel
                    {
                        Name = i.Name,
                        Country = i.Country,
                        AuthorizeUrl = BuildAuthorizeWithIssuerUrl(Request, i.Id)
                    })
                });
            }

            var cache = _connection.GetDatabase();

            // create merchant reference (that always starts with a letter)
            var entranceCode = String.Concat(Guid.NewGuid().ToString("N").Select(c => (char)(c + 17)));
            
            // perform IDIN authentication request
            var authenticationRequest = new AuthenticationRequest(
                entranceCode,
                ServiceIds.DateOfBirth | ServiceIds.Address | ServiceIds.Name | ServiceIds.ConsumerBin, 
                issuerId);

            var authenticationResponse = await Task.Run(() => communicator.NewAuthenticationRequest(authenticationRequest));
            if (authenticationResponse.IsError)
            {
                return OAuth2AuthorizationError(redirectUri, state, "server_error", "Error performing IDIN authentication transaction: " + authenticationResponse.Error.ErrorMessage);
            }

            // save state and redirect URI to cache
            var authorizeJson = JsonConvert.SerializeObject(new AuthorizeCacheModel
            {
                State = state,
                RedirectUri = redirectUri
            });
            await cache.StringSetAsync("ec." + entranceCode, authorizeJson, TimeSpan.FromMinutes(5));

            return Redirect(authenticationResponse.IssuerAuthenticationUrl.ToString());
        }

        // GET: /oauth2/callback
        [HttpGet]
        public async Task<ActionResult> Callback()
        {
            var cache = _connection.GetDatabase();

            // Entrance Code
            var entranceCode = Request.QueryString["ec"];
            if (string.IsNullOrEmpty(entranceCode))
            {
                return new HttpStatusCodeResult(400, "Missing required parameter: ec (entrance code)");
            }
            var entranceCodeKey = "ec." + entranceCode;
            string authorizeJson = await cache.StringGetAsync(entranceCodeKey);
            if (authorizeJson == null)
            {
                return new HttpStatusCodeResult(400, "Invalid entrance code: " + entranceCode);
            }
            // Delete entrance code because it's single-use
            await cache.KeyDeleteAsync(entranceCodeKey);

            // AuthorizeCache (state & redirect_uri)
            var authorizeCache = JsonConvert.DeserializeObject<AuthorizeCacheModel>(authorizeJson);

            // Transaction ID
            var transactionId = Request.QueryString["trxid"];
            if (string.IsNullOrEmpty(transactionId))
            {
                return new HttpStatusCodeResult(400, "Missing required parameter: trxid");
            }

            // Attempt to retrieve transaction status
            var communicator = new Communicator();
            var statusRequest = new StatusRequest(transactionId);

            // attempt to get IDIN response with retries
            StatusResponse statusResponse = null;
            var retries = 0;
            var done = false;
            while (!done)
            {
                statusResponse = await Task.Run(() => communicator.GetResponse(statusRequest));
                if (statusResponse.IsError)
                {
                    return OAuth2AuthorizationError(authorizeCache.RedirectUri, authorizeCache.State, "server_error", "Error obtaining IDIN authentication transaction status: " + statusResponse.Error.ErrorMessage);
                }
                if (statusResponse.Status == "Open")
                {
                    retries++;
                    if (retries == 5)
                    {
                        // max retries met
                        return OAuth2AuthorizationError(authorizeCache.RedirectUri, authorizeCache.State, "access_denied", "IDIN transaction did not return a successful status, even after performing " + retries + " retries.");
                    }
                    else
                    {
                        // deley before next retry
                        await Task.Delay(retries * 250);
                    }
                }
                else if (statusResponse.Status == "Success")
                {
                    done = true;
                }
                else
                {
                    return OAuth2AuthorizationError(authorizeCache.RedirectUri, authorizeCache.State, "access_denied", "IDIN transaction did not return a successful status. Status = " + statusResponse.Status);
                }
            }

            // Create code and save user profile and state to cache
            var code = Guid.NewGuid().ToString("N");
            var callbackJson = JsonConvert.SerializeObject(new CallbackCacheModel
            {
                RedirectUri = authorizeCache.RedirectUri,
                Profile = statusResponse.SamlResponse.AttributeStatements.ToDictionary(a => a.Name, a => a.Value)
            });
            await cache.StringSetAsync("code." + code, callbackJson, TimeSpan.FromHours(24));

            // Redirect to client with code and state
            var uri = new UriBuilder(authorizeCache.RedirectUri);
            var query = HttpUtility.ParseQueryString(uri.Query);
            query["code"] = code;
            if (!String.IsNullOrEmpty(authorizeCache.State))
            {
                query["state"] = authorizeCache.State;
            }
            uri.Query = query.ToString();
            return Redirect(uri.ToString());
        }

        // POST: /oauth2/token
        [HttpPost]
        public async Task<ActionResult> Token(TokenModel model)
        {
            var cache = _connection.GetDatabase();

            // Validate client_id, client_secret, code
            if (model.client_id != _auth0IdinConnectorClientId || model.client_secret != _auth0IdinConnectorClientSecret)
            {
                return OAuth2TokenError("invalid_client", "Bad client_id or client_secret");
            }
            if (model.grant_type != "authorization_code")
            {
                return OAuth2TokenError("unsupported_grant_type", "Bad client_id or client_secret");
            }
            if (string.IsNullOrEmpty(model.code))
            {
                return OAuth2TokenError("invalid_request", "Missing required parameter: code");
            }

            // Fetch callback cache (state & profile)
            var codeKey = "code." + model.code;
            string callbackJson = await cache.StringGetAsync(codeKey);
            if (callbackJson == null)
            {
                return OAuth2TokenError("invalid_grant", "Invalid authorization code");
            }
            
            // Delete authorization code because it's single-use
            await cache.KeyDeleteAsync(codeKey);

            var callbackCache = JsonConvert.DeserializeObject<CallbackCacheModel>(callbackJson);

            // Validate redirect_uri
            if (model.redirect_uri != callbackCache.RedirectUri)
            {
                return OAuth2TokenError("invalid_grant", "Invalid redirect_uri: " + model.redirect_uri);
            }

            // Create access_token and save profile to cache
            var accessToken = Guid.NewGuid().ToString("N");
            var profileJson = JsonConvert.SerializeObject(callbackCache.Profile);
            await cache.StringSetAsync("token." + accessToken, profileJson, TimeSpan.FromHours(24));

            // return JSON response
            return Json(new {
                access_token = accessToken,
                token_type = "bearer"
            });
        }

        // GET: /oauth2/userinfo
        [HttpGet]
        public async Task<ActionResult> UserInfo()
        {
            var authorizationHeaderValues = Request.Headers.GetValues("authorization");
            if (authorizationHeaderValues.Length != 1)
            {
                return new HttpStatusCodeResult(400, "Missing required header: authorization");
            }
            var authorizationHeaderParts = authorizationHeaderValues.Single().Split(' ');
            if (authorizationHeaderParts.Length != 2 || authorizationHeaderParts[0] != "bearer")
            {
                return new HttpStatusCodeResult(400, "Invalid authorization header format");
            }

            // Access Token
            var accessToken = authorizationHeaderParts[1];

            var cache = _connection.GetDatabase();

            // Fetch and return user info
            string profileJson = await cache.StringGetAsync("token." + accessToken);
            if (profileJson == null)
            {
                return new HttpStatusCodeResult(404, "User profile not found");
            }

            return Content(profileJson, "application/json");
        }
    }
}