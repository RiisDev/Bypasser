using CG.Web.MegaApiClient;
using System.Globalization;
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
                catch
                {
                    Console.WriteLine($"Failed link: {link}");
                }
            }
            return data;
        }
        
        public static async Task<List<Bypass.MediaData>> GetBunkrData(List<string>? bunkrLinks)
        {
            List<Bypass.MediaData> data = [];
            if (bunkrLinks == null || bunkrLinks.Count == 0) return data;

            foreach (string link in bunkrLinks)
            {
                try
                {
                    Bypass.MediaData? parsedData = null;

                    if (link.Contains("/a/")) parsedData = await ParseBunkrAlbum(link);
                    else if (link.Contains("/f/") || link.Contains("/v/")) parsedData = await ParseBunkrFile(link);

                    if (parsedData is null) continue;
                    data.Add(parsedData);
                }
                catch
                {
                    Console.WriteLine($"Failed Link: {link}");
                }

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
    }
}
