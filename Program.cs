using System.Diagnostics;
using Bypasser.Components;
using CG.Web.MegaApiClient;
using MudBlazor.Services;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Bypasser.Modules;
using SQLitePCL;

namespace Bypasser
{
    public class Program
    {
        public static Database Database = new();

        public static MegaApiClient MegaClient = new();

        public static HttpClient Client = new(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            UseCookies = true
        })
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestHeaders =
            {
                {"User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.6478.91 Safari/537.36 ByPassClient/1.0"},
                {"x-api-key", Environment.GetEnvironmentVariable("BYPASS_API_KEY")}
            }
        };

        public static void Main(string[] args)
        {
            _ = new EnvService();

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BYPASS_API_KEY")))
                throw new InvalidOperationException("BYPASS_API_KEY environment variable is not set.");
                
            MegaClient.ApiRequestFailed += (er, failed) =>
            {
                Debug.WriteLine(failed);
            };
            
            MegaClient.LoginAnonymousAsync().Wait();
            
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(80);
                options.ListenAnyIP(443, listenOptions =>
                {
                    listenOptions.UseHttps(LoadCertificateFromResource("Bypasser.cert.pfx", "sslpassword"));
                });
            });

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddMudServices();


            WebApplication app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseAntiforgery();

            app.MapControllers();
            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }

        private static X509Certificate2 LoadCertificateFromResource(string resourceName, string password)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream(resourceName) ?? throw new FileNotFoundException("Embedded certificate not found: " + resourceName);
            using MemoryStream ms = new();
            stream.CopyTo(ms);
            return X509CertificateLoader.LoadPkcs12(ms.ToArray(), password, X509KeyStorageFlags.Exportable);
        }
    }

    internal class EnvService
    {
        public Dictionary<string, string> Variables { get; } = new();

        internal EnvService()
        {
            string envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
            if (!File.Exists(envPath)) return;

            string[] lines = File.ReadAllLines(envPath);
            foreach (string line in lines)
            {
                string[] parts = line.Split('=', 2);
                if (parts.Length != 2) continue;

                string key = parts[0].Trim();
                string value = parts[1].Trim();

                if (string.IsNullOrEmpty(key)) continue;

                Variables[key] = value;

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
