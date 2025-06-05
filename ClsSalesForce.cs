using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Browser;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Windows.Forms;
using System.Security.Claims;
using System.Net.Http;
using System.Net.Http.Headers;
using EPDM.Interop.epdm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlueBrick
{
    internal class ClsSalesForce
    {
        private bool _loggedIn;
        private readonly ClsSettings _clsSettings;

        private const string ApiEndpoint = @"https://virainsight.my.salesforce.com/services/data/v62.0/";

        internal ClsSalesForce(ref ClsSettings settings)
        {
            //get logged in state
            _clsSettings = settings;
            var chk = bool.TryParse(_clsSettings.GetSetting("sfLoggedIn"), out var val);
            _loggedIn = !chk || val;
        }

        public bool Login()
        {
            //check if already logged in
            if (_loggedIn) return true;

            //set tls1.2 for sf
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            //init sf api options
            var options = new OidcClientOptions
            {
                Authority = @"https://virainsight.my.salesforce.com/",
                ClientId = @"3MVG9dZJodJWITSuBuCvzSXGEXK7wa47mSU1SLqJXN.6fFhtb40dRlqP2MjFw8dlnLPJHdtsDczVtjl2banNu",
                Scope = @"full refresh_token offline_access",
                RedirectUri = @"http://localhost/winforms.client"
            };

            var oidcClient = new OidcClient(options);

            //catch out of date assembly refs
            var domain = AppDomain.CurrentDomain;
            domain.AssemblyResolve += ResolveAsyFail;

            //open form and get login result
            BrowserResult browserResult;
            var state = oidcClient.PrepareLoginAsync().Result;
            using (var login = new FrmSfAuth(state.StartUrl, oidcClient.Options.RedirectUri))
            {
                var result = login.ShowDialog();
                if (result != DialogResult.OK)
                    return false;
                browserResult = login.BrowserResult;
            }

            //process result from webview browser
            var loginResult = oidcClient.ProcessResponseAsync(browserResult.Response, state).Result;

            //assign tokens
            if (!string.IsNullOrWhiteSpace(loginResult.RefreshToken))
            {
                _clsSettings.SetSetting("sfRefresh", loginResult.RefreshToken, true);
            }

            if (!string.IsNullOrWhiteSpace(loginResult.AccessToken))
            {
                _clsSettings.SetSetting("sfAccess", loginResult.AccessToken, true);
            }

            //assign properties from claims
            foreach (var claim in loginResult.User.Claims)
            {
                switch (claim.Type)
                {
                    case "name":
                        _clsSettings.SetSetting("sfName", claim.Value);
                        break;
                    case "email":
                        _clsSettings.SetSetting("sfEmail", claim.Value);
                        break;
                    case "picture":
                        _clsSettings.SetSetting("sfPicUrl", claim.Value);
                        break;
                }
            }

            //stash values
            _clsSettings.SetSetting("sfLoggedIn", "true");
            _clsSettings.WriteSettings();

            //finish
            _loggedIn = true;
            domain.AssemblyResolve -= ResolveAsyFail;
            return _loggedIn;
        }

        #region GetTaskInfo

        //todo: soql query to get task
        //todo: deserialize class for above

        #endregion GetTaskInfo

        internal Opportunity GetOpp(string oppNum)
        {
            try
            {
                //generate query
                var query =
                    $@"SELECT Id, Opportunity_Description__c, Account.Name, Owner.Email FROM Opportunity WHERE Opportunity_Number__c = '{oppNum}'";
                var data = GetJson("query", query, out var msg, out var success);
                if (!success) return null;
                
                //deserialize and decode html strings
                var results = JsonConvert.DeserializeObject<Opportunity>(data);
                results.records[0].Opportunity_Description__c = WebUtility.HtmlDecode(results.records[0].Opportunity_Description__c);
                return results;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        internal void GetTasks(string oppNum)
        {
            try
            {
                //todo: query for tasks
                //generate query
                //var query = $@"SELECT Id, Account.Name, Owner.Name, Owner.UserName, Opportunity_Description__c FROM Opportunity WHERE Opportunity_Number__c = '{oppNum}'";

                //get opp data
                //var data = GetJson("query", query, out var msg, out var success);
                //if (!success) return null;
                //var results = JsonConvert.DeserializeObject<Opportunity>(data);
                //return results;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                //return null;
            }
        }

        //make api call
        private string GetJson(string type, string key, out string msg, out bool success)
        {
            //validate inputs
            if (string.IsNullOrEmpty(type))
            {
                msg = @"No type specified.";
                success = false;
                return null;
            }

            if (string.IsNullOrEmpty(key))
            {
                msg = @"No object id or query string specified.";
                success = false;
                return null;
            }

            //create api call
            var apiCall = ApiEndpoint;
            if (type == "query")
            {
                key = key.Replace(" ", "+");
                apiCall += $"query/?q={key}";
            }
            else
            {
                apiCall += $"sobjects/{type}/{key}";
            }

            //validate token
            var accToken = _clsSettings.GetSetting("sfAccess", true);
            if (string.IsNullOrEmpty(accToken))
            {
                msg = @"No access token.";
                success = false;
                return null;
            }

            //set tls1.2 for sf
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            //send query
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accToken);
            var response = client.GetAsync(apiCall).Result;

            //validate response
            if (!response.IsSuccessStatusCode)
            {
                var test = response.Content.ReadAsStringAsync();
                msg = @"Failed to retrieve data: " + (int)response.StatusCode + " - " + response.ReasonPhrase;
                msg += @"Result: " + test.Result;
                success = false;
                return null;
            }

            //return data
            var data = response.Content.ReadAsStringAsync().Result;
            msg = @"Successfully retrieved data.";
            success = true;
            return data;
        }

        //forced binding redirect for oidc lib requiring version of system.text.json.dll with vulnerabilities
        //sources:
        //https://blog.slaks.net/2013-12-25/redirecting-assembly-loads-at-runtime/
        //https://gist.github.com/markvincze/10148fbeb41a57c841c7
        private static Assembly ResolveAsyFail(object sender, ResolveEventArgs args)
        {
            var requestedAssembly = new AssemblyName(args.Name);

            //values for all outdated refs
            switch (requestedAssembly.Name)
            {
                case "System.Text.Json":
                    requestedAssembly.Version = new Version(8, 0, 0, 5);
                    requestedAssembly.SetPublicKeyToken(
                        new AssemblyName("x, PublicKeyToken=cc7b13ffcd2ddd51").GetPublicKeyToken());
                    break;
                case "System.Runtime.CompilerServices.Unsafe":
                    requestedAssembly.Version = new Version(6, 0, 0, 0);
                    requestedAssembly.SetPublicKeyToken(
                        new AssemblyName("x, PublicKeyToken=b03f5f7f11d50a3a").GetPublicKeyToken());
                    break;
                case "System.Text.Encodings.Web":
                case "Microsoft.Bcl.AsyncInterfaces":
                    requestedAssembly.Version = new Version(9, 0, 0, 2);
                    requestedAssembly.SetPublicKeyToken(
                        new AssemblyName("x, PublicKeyToken=cc7b13ffcd2ddd51").GetPublicKeyToken());
                    break;
                default:
                    return null;
            }

            requestedAssembly.CultureInfo = CultureInfo.InvariantCulture;

            //check for already loaded
            var alreadyLoadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == requestedAssembly.Name);
            return alreadyLoadedAssembly != null ? alreadyLoadedAssembly : Assembly.Load(requestedAssembly);
        }

        // ReSharper disable All

        #region Deserialization Classes

        // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);

        public class Account
        {
            public Attributes attributes { get; set; }
            public string Name { get; set; }
        }

        public class Attributes
        {
            public string type { get; set; }
            public string url { get; set; }
        }

        public class Owner
        {
            public Attributes attributes { get; set; }
            public string Email { get; set; }
        }

        public class Record
        {
            public Attributes attributes { get; set; }
            public string Id { get; set; }
            public string Opportunity_Description__c { get; set; }
            public Account Account { get; set; }
            public Owner Owner { get; set; }
        }

        public class Opportunity
        {
            public int totalSize { get; set; }
            public bool done { get; set; }
            public List<Record> records { get; set; }
        }

        #endregion Deserialization Classes
    }
}