using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DistCL.Utils
{
	public class StringUtils
	{
		public static string QuoteString(this string text)
		{
			if (!(text.StartsWith("\"") && text.EndsWith("\"")))
			{
				return "\"" + text + "\"";
			}
			else
			{
				return text;
			}
		}
	}
}
