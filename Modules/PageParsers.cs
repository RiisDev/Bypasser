using System.Text.RegularExpressions;
using static System.Text.RegularExpressions.Regex;

namespace Bypasser.Modules
{
    public static class PageParsers
    {
        public static async Task<IReadOnlyList<string>> ExtractRentryLinks(string url)
        {
            string pageData = await Program.Client.GetStringAsync(url);

            int articleStartIndex = pageData.IndexOf("<article>", StringComparison.OrdinalIgnoreCase);
            if (articleStartIndex == -1) throw new InvalidOperationException("No <article> tag found in the page data.");
            int articleEndIndex = pageData.IndexOf("</article>", articleStartIndex, StringComparison.OrdinalIgnoreCase);
            if (articleEndIndex == -1) throw new InvalidOperationException("No </article> tag found in the page data.");

            string articleContent = pageData.Substring(articleStartIndex, articleEndIndex - articleStartIndex + "</article>".Length);

            List<string> links = [];

            MatchCollection matches = Matches(articleContent, @"(http|ftp|https):\/\/([\w_-]+(?:(?:\.[\w_-]+)+))([\w.,@?^=%&:\/~+#-]*[\w@?^=%&\/~+#-])");

            foreach (Match match in matches)
            {
                string link = match.Value;
                if (!links.Contains(link))
                {
                    links.Add(link);
                }
            }

            return links;
        }
    }
}
