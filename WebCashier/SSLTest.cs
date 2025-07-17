using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;

namespace WebCashier
{
    public class SSLTest
    {
        public static async Task TestSSLConnection()
        {
            Console.WriteLine("Testing SSL connection to Praxis API...");
            
            // Test with different SSL configurations
            await TestWithHandler("Default HttpClient", null);
            await TestWithHandler("TLS 1.2 Only", CreateTls12Handler());
            await TestWithHandler("SSL Bypass", CreateBypassHandler());
        }

        private static async Task TestWithHandler(string testName, HttpClientHandler handler)
        {
            Console.WriteLine($"\n--- {testName} ---");
            
            try
            {
                using var client = handler != null ? new HttpClient(handler) : new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                
                var response = await client.GetAsync("https://pci-gw-test.praxispay.com/api/direct-process");
                Console.WriteLine($"✓ Connection successful: {response.StatusCode}");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"✗ Connection failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Unexpected error: {ex.Message}");
            }
        }

        private static HttpClientHandler CreateTls12Handler()
        {
            var handler = new HttpClientHandler();
            handler.SslProtocols = SslProtocols.Tls12;
            handler.CheckCertificateRevocationList = false;
            return handler;
        }

        private static HttpClientHandler CreateBypassHandler()
        {
            var handler = new HttpClientHandler();
            handler.SslProtocols = SslProtocols.Tls12;
            handler.CheckCertificateRevocationList = false;
            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            return handler;
        }
    }
}
