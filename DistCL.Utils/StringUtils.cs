using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
