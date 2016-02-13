using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nimania.Runtime
{
	public static class Utils
	{
		public static string TimeString(int tm)
		{
			int ms = tm % 1000;
			int s = (int)Math.Floor(tm / 1000.0) % 60;
			int m = (int)Math.Floor(tm / 1000.0 / 60.0) % 60;
			int h = (int)Math.Floor(tm / 1000.0 / 60.0 / 60.0);
			string ret = "";
			if (h > 0) { ret += h + ":"; }
			if (m > 0) { ret += m.ToString(h > 0 ? "00" : "0") + ":"; }
			ret += s.ToString(m > 0 ? "00" : "0") + "." + ms.ToString("000");
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
	}
}
