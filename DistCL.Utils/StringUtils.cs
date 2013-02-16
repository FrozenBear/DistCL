namespace DistCL.Utils
{
	public static class StringUtils
	{
		public static string QuoteString(this string text)
		{
			return "\"" + text.Replace("\"", "\\\"") + "\"";
		}
	}
}
