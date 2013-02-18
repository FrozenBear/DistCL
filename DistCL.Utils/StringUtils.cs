namespace DistCL.Utils
{
	public static class StringUtils
	{
		public static string QuoteString(this string text)
		{
			return "\"" + text.Replace("\"", "\\\"") + "\"";
		}

		public static bool StartsWith(this string text, string value, int idx)
		{
			if (idx >= text.Length)
				return false;

			return text.IndexOf(value, idx) == idx;
		}
	}
}
