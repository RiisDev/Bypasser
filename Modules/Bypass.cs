using System.Diagnostics;
using System.Web;

namespace Bypasser.Modules
{
    public static class Bypass
    {
        public record MediaData(string MegaTitle, string DirectorySize, string ImageCount, string VideoCount, string Url);

        private static async Task<string?> CallApi(string url)
        {
            try
            {
                HttpResponseMessage response = await Program.Client.GetAsync($"https://api.bypass.vip/premium/bypass?url={HttpUtility.UrlEncode(url)}");

                string data = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"Bypass API Response: {data}");
                if (response.IsSuccessStatusCode)
                {
                    Dictionary<string, string>? json = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                    if (json != null && json.TryGetValue("result", out string? result)) return result;
                }
            }
            catch {/**/}

            return null;
        }

        public static async Task<string?> GetBypassData(string host, string? query = "", string? path = "")
        {
            string fullUrl = $"https://{host}/{path ?? ""}";
            if (!string.IsNullOrEmpty(query)) fullUrl += query;
            if (!Program.UriToWatch.Any(host.Contains)) return null;
            //
            return await CallApi(fullUrl);
        }
    }
}
