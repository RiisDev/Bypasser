global using static Bypasser.Logging;
using Bypasser.Components;
using Bypasser.Modules;
using CG.Web.MegaApiClient;
using Microsoft.AspNetCore.Connections;
using MudBlazor.Services;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Bypasser
{
	public static class Logging
	{
		private static readonly Lock LogLock = new();

		public enum State
		{
            Error,
            Warning,
            Info
		}

		public static void Log(string message, State state = State.Error, [CallerMemberName] string caller = "", [CallerFilePath] string callerFilePath = "")
		{
			lock (LogLock)
			{
				string className = Path.GetFileNameWithoutExtension(callerFilePath);
				string data = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{state}] [{className}.{caller}] {message}\n";
                Debug.WriteLine(data);
                File.AppendAllText($"{AppDomain.CurrentDomain.BaseDirectory}\\log.txt", data);

                Console.ForegroundColor = state switch
                {
	                State.Error => ConsoleColor.Red,
	                State.Warning => ConsoleColor.Yellow,
	                State.Info => ConsoleColor.Green,
	                _ => Console.ForegroundColor
                };
                Console.WriteLine(data);
                Console.ResetColor();
			}
		}
	}

    public class Program
    {
        public static Database Database = new();
		
        public static MegaApiClient? MegaClient { get; set; }

        public static HttpClient Client = new(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            UseCookies = true,
            UseProxy = false,
            Proxy = null
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

            Log("Starting...", State.Info);

            Log("Error Handlers...", State.Info);
            AppDomain.CurrentDomain.UnhandledException += (_, exceptionEventArgs) => Log(exceptionEventArgs.ExceptionObject.ToString() ?? "Unknown error");
			TaskScheduler.UnobservedTaskException += (_, eventArgs) => Log(eventArgs.Exception.ToString(), State.Error, eventArgs.Exception.Source ?? "");

            Log("Client Bindings...", State.Info);
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BYPASS_API_KEY"))) { Log("Missing BYPASS_API_KEY from ENV"); return; }

			try
			{
				MegaApiClient tempClient = new ();
				tempClient.ApiRequestFailed += (_, failed) => Log(failed.ResponseJson);

				Log("Logging in...", State.Info);

				Task loginTask = tempClient.LoginAnonymousAsync();
				Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(15));

				Task completedTask = Task.WhenAny(loginTask, timeoutTask).GetAwaiter().GetResult();

				if (completedTask == loginTask) { loginTask.GetAwaiter().GetResult(); MegaClient = tempClient; Log("Logged in to MEGA successfully.", State.Info); }
				else { Log("MEGA login timed out after 30 seconds.", State.Warning); MegaClient = null; }
			}
			catch (Exception ex)
			{
				Log(ex.ToString());
				MegaClient = null;
			}

			Log("WebAppBuilder...", State.Info);
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


            Log("App Building...", State.Info);
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

            Log("App Running...", State.Info);
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
