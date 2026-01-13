using CG.Web.MegaApiClient;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Bypasser.Modules
{
    public static class DownloadParser
    {

        private static readonly List<string> VideoExtensions =
        [
            ".mp4",
            ".mov",
            ".wmv",
            ".flv",
            ".avi",
            ".mkv",
            ".webm",
            ".mpeg",
            ".mpg",
            ".3gp",
            ".m4v",
            ".ts",
            ".ogv",
            ".divx"
        ];

        private static readonly List<string> ImageExtensions =
        [
            ".jpg",
            ".jpeg",
            ".png",
            ".gif",
            ".bmp",
            ".tiff",
            ".tif",
            ".webp",
            ".heic",
            ".svg",
            ".ico",
            ".raw",
            ".cr2",
            ".nef",
            ".orf",
            ".arw"
        ];
        
        public static async Task<List<Bypass.MediaData>> GetMegaData(List<string>? megaLinks)
        {
            List<Bypass.MediaData> data = [];
            if (megaLinks == null || megaLinks.Count == 0) return data;

            if (Program.MegaClient is null)
            {
                data.AddRange(megaLinks.Select(x=> new Bypass.MediaData("Error", "-1", "-1", "-1", x)));
	            return data;
            }

            foreach (string link in megaLinks)
            {
                try
                {
                    long size = 0;
                    int imageCount = 0;
                    int videoCount = 0;

                    Uri folderUri = new(link);


                    IEnumerable<INode>? nodes = await Program.MegaClient.GetNodesFromLinkAsync(folderUri);
                    IEnumerable<INode> enumerable = nodes.ToList();
                    if (!enumerable.Any()) continue;

                    string rootNode = enumerable.FirstOrDefault(x => x.Type == NodeType.Root)?.Name ?? "Unknown Folder";

                    foreach (INode node in enumerable)
                    {
                        string nodeName = node.Name.ToLowerInvariant(); // Make comparison case-insensitive

                        if (VideoExtensions.Any(nodeName.EndsWith)) videoCount++;
                        else if (ImageExtensions.Any(nodeName.EndsWith)) imageCount++;

                        size += node.Size;
                    }

                    data.Add(new Bypass.MediaData(rootNode, Formatting.FormatBytes(double.Parse(size.ToString())), imageCount.ToString(), videoCount.ToString(), link));

                }
                catch (Exception ex) { Log($"Failed Link: {link} |||| {ex}"); }
            }
            return data;
        }
        
        public static async Task<List<Bypass.MediaData>> GetBunkrData(List<string>? bunkrLinks)
        {
            List<Bypass.MediaData> data = [];
            try
            {
                if (bunkrLinks == null || bunkrLinks.Count == 0) return data;

                foreach (string link in bunkrLinks)
                {
                    try
                    {
                        Bypass.MediaData? parsedData = null;

                        // Black is protected by Cloudflare, so we replace it with the CR version
                        string linkData = link.Replace("bunkr.black", "bunkr.cr");

                        if (linkData.Contains("/a/")) parsedData = await ParseBunkrAlbum(linkData);
                        else if (linkData.Contains("/f/") || linkData.Contains("/v/"))
                            parsedData = await ParseBunkrFile(linkData);

                        if (parsedData is null) continue;
                        data.Add(parsedData);
                    }
                    catch
                    {
                        Log($"Failed Link: {link}");
					}

                }
            }
            catch (Exception ex)
            {
                Log($"Error while parsing Bunkr data: {ex.Message}");
            }

            return data;
        }

        private static async Task<Bypass.MediaData?> ParseBunkrFile(string link)
        {
            string pageData = await Program.Client.GetStringAsync(link);

            Match match = Regex.Match(pageData, """<meta property="og:title" content="(.*?)">""");

            string fileName = match.Groups[1].Value.Trim();

            match = Regex.Match(pageData, """<p\b[^>]*>([^<]+?\b(?:MB|GB|KB|TB|PB)\b)\W*<span id="downloadCount""");
            string fileSize = match.Groups[1].Value.Trim();

            double fileSizeDouble = Formatting.ParseFormattedBytes(fileSize);
            bool videoFound = VideoExtensions.Any(fileName.ToLowerInvariant().EndsWith);

            return new Bypass.MediaData(fileName, Formatting.FormatBytes(fileSizeDouble), (videoFound ? 0 : 1).ToString(), (videoFound ? 1 : 0).ToString(), link);
        }

        private static async Task<Bypass.MediaData?> ParseBunkrAlbum(string link)
        {
            long size = 0;
            int imageCount = 0;
            int videoCount = 0;

            string pageData = await Program.Client.GetStringAsync(link);

            MatchCollection matches = Regex.Matches(pageData, """<p style="display:none;"*>(.*?)<\/p>""");

            string[] fileNames = matches.Select(m => m.Groups[1].Value.Trim()).ToArray();
            if (fileNames.Length == 0) return null;

            string pageTitle = Regex.Match(pageData, @"<title>(.*?)<\/title>").Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(pageTitle)) pageTitle = "Unknown Folder";

            matches = Regex.Matches(pageData,
                """<p\b[^>]*\bclass\s*=\s*["'][^"']*\btheSize\b[^"']*["'][^>]*>(.*?)<\/p>""");
            string[] fileSizes = matches.Select(m => m.Groups[1].Value.Trim()).ToArray();

            foreach (string fileName in fileNames)
            {
                string lowerFileName = fileName.ToLowerInvariant();
                if (VideoExtensions.Any(lowerFileName.EndsWith)) videoCount++;
                else if (ImageExtensions.Any(lowerFileName.EndsWith)) imageCount++;
            }

            foreach (string fileSize in fileSizes)
            {
                if (double.TryParse(Formatting.ParseFormattedBytes(fileSize).ToString(CultureInfo.InvariantCulture),
                        out double sizeValue))
                {
                    size += (long)sizeValue;
                }
            }

            return new Bypass.MediaData(pageTitle, Formatting.FormatBytes(size), imageCount.ToString(), videoCount.ToString(), link);
        }

        private static async Task<GoFileData?> GetGoFileData(string link, string wToken, string authToken)
        {
            HttpRequestMessage request = new(HttpMethod.Get, $"https://api.gofile.io/contents/{link[(link.LastIndexOf('/') + 1)..]}?wt={wToken}");
            request.Headers.TryAddWithoutValidation("Cookie", $"accountToken:{authToken}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            HttpResponseMessage fileResponse = await Program.Client.SendAsync(request);

            if (!fileResponse.IsSuccessStatusCode)
            {
                Log($"Failed to retrieve data for: {link}\nStatus: {fileResponse.StatusCode}\n{await fileResponse.Content.ReadAsStringAsync()}");
                return null;
            }

            GoFileData? goFileData = await fileResponse.Content.ReadFromJsonAsync<GoFileData>();

            if (goFileData is not null && goFileData.Status == "ok") return goFileData;

            Log($"Failed to parse GoFile data for link: {link}\n{await fileResponse.Content.ReadAsStringAsync()}");
            return null;
        }

        private static async Task<List<Bypass.MediaData>> ScrapeGoFileRecursively(string link, string wToken, string authToken)
        {
            List<Bypass.MediaData> mediaList = [];

            GoFileData? goFileData = await GetGoFileData(link, wToken, authToken);
            if (goFileData is null) return mediaList;

            int imageCount = 0;
            int videoCount = 0;

            IEnumerable<KeyValuePair<string, Child>> files = goFileData.Data.Children.Where(x => x.Value.Type == "file");
            IEnumerable<KeyValuePair<string, Child>> folders = goFileData.Data.Children.Where(x => x.Value.Type == "folder");

            foreach (KeyValuePair<string, Child> file in files)
            {
                if (file.Value.Mimetype is null) continue;

                bool isVideo = file.Value.Mimetype.StartsWith("video");
                bool isImage = file.Value.Mimetype.StartsWith("image");

                if (isVideo) videoCount++;
                else if (isImage) imageCount++;
            }

            mediaList.Add(new Bypass.MediaData(
                goFileData.Data.Name,
                Formatting.FormatBytes(double.Parse(goFileData.Data.TotalSize?.ToString() ?? "0")),
                imageCount.ToString(),
                videoCount.ToString(),
                link));

            foreach (KeyValuePair<string, Child> folder in folders)
            {
                string folderLink = $"https://gofile.io/d/{folder.Value.Id}";
                List<Bypass.MediaData> nestedMedia = await ScrapeGoFileRecursively(folderLink, wToken, authToken);
                mediaList.AddRange(nestedMedia);
            }

            return mediaList;
        }

        public static async Task<List<Bypass.MediaData>> ParseGoFileData(List<string>? goFileLinks)
        {
            List<Bypass.MediaData> data = [];
            try
            {
                if (goFileLinks == null || goFileLinks.Count == 0) return data;

                HttpResponseMessage response =
                    await Program.Client.SendAsync(new HttpRequestMessage(HttpMethod.Post,
                        "https://api.gofile.io/accounts"));

                GoFileAuth? apiData;
                try
                {
                    apiData = await response.Content.ReadFromJsonAsync<GoFileAuth>();
                }
                catch
                {
                    return data;
                }

                if (apiData is null || apiData.Status != "ok") { Log("Failed to authenticate with GoFile API."); return data; }

                string authToken = apiData.Data.Token;
                if (string.IsNullOrEmpty(authToken)) { Log("Authentication token is empty."); return data; }

                string globalJs = await Program.Client.GetStringAsync("https://gofile.io/dist/js/global.js");

                if (string.IsNullOrEmpty(globalJs)) 
                { Log("Failed to retrieve global.js from GoFile."); return data; }

                Match wTokenMatch = Regex.Match(globalJs, @"appdata\.wt\s*=\s*[""']([^""']+)[""']");

                if (!wTokenMatch.Success || wTokenMatch.Groups.Count != 2)
                { Log("Failed to find wToken in global.js."); return data; }

                string wToken = wTokenMatch.Groups[1].Value;

                foreach (string link in goFileLinks)
                    data.AddRange(await ScrapeGoFileRecursively(link, wToken, authToken));
            }
            catch (Exception ex)
            {
                Log($"Error while parsing GoFile data: {ex.Message}");
            }

            return data;
        }

        #region GoFileRecords

        public record AuthData(
            [property: JsonPropertyName("id")] string Id,
            [property: JsonPropertyName("rootFolder")] string RootFolder,
            [property: JsonPropertyName("tier")] string Tier,
            [property: JsonPropertyName("token")] string Token
        );

        public record GoFileAuth(
            [property: JsonPropertyName("status")] string Status,
            [property: JsonPropertyName("data")] AuthData Data
        );

        public record Child(
            [property: JsonPropertyName("canAccess")] bool? CanAccess,
            [property: JsonPropertyName("id")] string Id,
            [property: JsonPropertyName("parentFolder")] string? ParentFolder,
            [property: JsonPropertyName("type")] string Type,
            [property: JsonPropertyName("name")] string Name,
            [property: JsonPropertyName("createTime")] long? CreateTime,
            [property: JsonPropertyName("modTime")] long? ModTime,
            [property: JsonPropertyName("size")] long? Size,
            [property: JsonPropertyName("downloadCount")] long? DownloadCount,
            [property: JsonPropertyName("md5")] string? Md5,
            [property: JsonPropertyName("mimetype")] string? Mimetype,
            [property: JsonPropertyName("servers")] IReadOnlyList<string>? Servers,
            [property: JsonPropertyName("serverSelected")] string? ServerSelected,
            [property: JsonPropertyName("link")] string? Link,
            [property: JsonPropertyName("thumbnail")] string? Thumbnail,
            [property: JsonPropertyName("code")] string? Code,
            [property: JsonPropertyName("public")] bool? Public,
            [property: JsonPropertyName("totalDownloadCount")] long? TotalDownloadCount,
            [property: JsonPropertyName("totalSize")] long? TotalSize,
            [property: JsonPropertyName("childrenCount")] long? ChildrenCount
        );

        public record BodyData(
            [property: JsonPropertyName("canAccess")] bool? CanAccess,
            [property: JsonPropertyName("id")] string Id,
            [property: JsonPropertyName("type")] string Type,
            [property: JsonPropertyName("name")] string Name,
            [property: JsonPropertyName("createTime")] long? CreateTime,
            [property: JsonPropertyName("modTime")] long? ModTime,
            [property: JsonPropertyName("code")] string Code,
            [property: JsonPropertyName("isRoot")] bool? IsRoot,
            [property: JsonPropertyName("public")] bool? Public,
            [property: JsonPropertyName("totalDownloadCount")] long? TotalDownloadCount,
            [property: JsonPropertyName("totalSize")] long? TotalSize,
            [property: JsonPropertyName("childrenCount")] long? ChildrenCount, 
            [property: JsonPropertyName("children")] Dictionary<string, Child> Children
        );

        public record GoFileData(
            [property: JsonPropertyName("status")] string Status,
            [property: JsonPropertyName("data")] BodyData Data,
            [property: JsonPropertyName("metadata")] object Metadata
        );


        #endregion
    }
}
