using System.Text.RegularExpressions;
using static System.Text.RegularExpressions.Regex;

namespace Bypasser.Modules
{
    public static class PageParsers
    {
        public static async Task<IReadOnlyList<string>> ExtractLinks(string url)
        {
	        List<string> outputLinks = [];

	        IReadOnlyList<string> linkInputs = ExtractLinksFromString(url);

	        foreach (string input in linkInputs)
	        {
		        if (input.Contains("rentry", StringComparison.OrdinalIgnoreCase)) outputLinks.AddRange(await ExtractRentryLinks(input));
		        else if (input.Contains("mega.nz")) outputLinks.Add(input);
				else if (input.Contains("graph.org")) outputLinks.AddRange(await ExtractGraphOrgLinks(input));
	        }

	        return outputLinks.AsReadOnly();
        }

        private static async Task<IReadOnlyList<string>> ExtractRentryLinks(string url)
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

        private static async Task<IReadOnlyList<string>> ExtractGraphOrgLinks(string url)
		{
			string pageData = await Program.Client.GetStringAsync(url);

			int articleStartIndex = pageData.IndexOf("<article", StringComparison.OrdinalIgnoreCase);
			if (articleStartIndex == -1) throw new InvalidOperationException("No <article tag found in the page data.");
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

        private static IReadOnlyList<string> ExtractLinksFromString(string input)
        {
	        List<string> links = [];

	        int index = 0;

			while (index < input.Length)
			{
				int start = input.IndexOf("http", index, StringComparison.OrdinalIgnoreCase);
				if (start == -1)
					break;

				int next = input.IndexOf("http", start + 4, StringComparison.OrdinalIgnoreCase);

				if (next == -1)
				{
					links.Add(input[start..].Trim());
					break;
				}

				links.Add(input[start..next].Trim());
				index = next;
			}

			return links.AsReadOnly();
        }
	}
}
