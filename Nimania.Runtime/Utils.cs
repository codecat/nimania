using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Nimania.Runtime
{
	public static class Utils
	{
		private static Logger m_logger = LogManager.GetCurrentClassLogger();

		public static string TimeString(int tm)
		{
			int ms = tm % 1000;
			int s = (tm / 1000) % 60;
			int m = (tm / 1000 / 60) % 60;
			int h = (tm / 1000 / 60 / 60);
			string ret = "";
			if (h > 0) { ret += h + ":"; }
			if (h > 0 || m > 0) { ret += m.ToString(h > 0 ? "00" : "0") + ":"; }
			ret += s.ToString(m > 0 ? "00" : "0") + "." + ms.ToString("000");
			return ret;
		}

		public static string TimeStringHMS(int tm)
		{
			int s = tm % 60;
			int m = (tm / 60) % 60;
			int h = (tm / 60 / 60);
			string ret = "";
			if (h > 0) { ret += h + "h "; }
			if (h > 0 || m > 0) { ret += m + "m "; }
			ret += s + "s";
			return ret;
		}

		public static string XmlEntities(string s)
		{
			var sb = new StringBuilder();
			int len = s.Length;
			for (int i = 0; i < len; i++) {
				char c = s[i];
				switch (c) {
					case '<': sb.Append("&lt;"); break;
					case '>': sb.Append("&gt;"); break;
					case '&': sb.Append("&amp;"); break;
					case '"': sb.Append("&quot;"); break;
					default:
						if (c > 159) {
							sb.Append("&#x");
							sb.Append(((int)c).ToString("x"));
							sb.Append(';');
						} else {
							sb.Append(c);
						}
						break;
				}
			}
			return sb.ToString();
		}

		public static string StripFormatCodes(string s)
		{
			return Regex.Replace(s, "\\$([0-9a-f]{3}|[ionwsz<>]|[lh](\\[[^\\]]+\\])?)", "");
		}

		public static string StripLinkCodes(string s)
		{
			return Regex.Replace(s, "\\$([lh](\\[[^\\]]+\\])?)", "");
		}

		public static void Assert(bool b, string desc = "")
		{
			if (b) {
				return;
			}
			m_logger.Error("Failed assertion: {0}", desc);
			Debug.Assert(false);
		}
	}
}
