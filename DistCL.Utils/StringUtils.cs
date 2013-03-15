using System.Diagnostics.Contracts;

namespace DistCL.Utils
{
	public static class StringUtils
	{
		public static string QuoteString(this string text)
		{
			Contract.Requires(text != null);

			return "\"" + text.Replace("\"", "\\\"") + "\"";
		}

		public static bool StartsWith(this string text, string value, int idx)
		{
			Contract.Requires(text != null);
			Contract.Requires(value != null);
			Contract.Requires(idx >= 0);

			if (idx >= text.Length)
				return false;

			return text.IndexOf(value, idx, System.StringComparison.Ordinal) == idx;
		}
	}
}
