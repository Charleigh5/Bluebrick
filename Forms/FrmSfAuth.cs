using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Duende.IdentityModel.OidcClient.Browser;

namespace BlueBrick
{
    public partial class FrmSfAuth : Form
    {
        private BrowserResult _browserResult;
        private readonly string _startUrl;
        private readonly string _endUrl;

        public FrmSfAuth(string startUrl, string endUrl)
        {
            _startUrl = startUrl;
            _endUrl = endUrl;
            InitializeComponent();
            _ = PrepBrowser();
        }

        public BrowserResult BrowserResult => _browserResult;

        private void wvwSfLogin_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (!IsBrowserNavigatingToRedirectUri(new Uri(e.Uri))) return;
            e.Cancel = true;
            _browserResult = new BrowserResult()
            {
                ResultType = BrowserResultType.Success,
                Response = new Uri(e.Uri).AbsoluteUri
            };
            DialogResult = DialogResult.OK;
            Close();
        }

        private bool IsBrowserNavigatingToRedirectUri(Uri uri)
        {
            return uri.AbsoluteUri.StartsWith(_endUrl);
        }

        private async Task PrepBrowser()
        {
            var env = CoreWebView2Environment.CreateAsync(userDataFolder: Path.GetTempPath()).Result;
            try
            {
                await wvwSfLogin.EnsureCoreWebView2Async(env);
                wvwSfLogin.CoreWebView2.CookieManager.DeleteAllCookies();
                wvwSfLogin.CoreWebView2.Navigate(_startUrl);
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
    }
}