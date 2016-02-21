using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GbxRemoteNet
{
	public static class GbxEncode
	{
		public static string Encode(string strMethod, dynamic[] args, bool bEscape = true)
		{
			string ret = "<?xml version=\"1.0\" encoding=\"utf-8\"?><methodCall><methodName>" + strMethod + "</methodName>";
			if (args.Length == 0) {
				return ret + "<params/></methodCall>";
			}
			ret += "<params>";
			foreach (var arg in args) {
				ret += "<param><value>" + EncodeValue(arg, bEscape) + "</value></param>";
			}
			return ret + "</params></methodCall>";
		}

		public static string EncodeValue(dynamic arg, bool bEscape = true)
		{
			if (arg is bool) {
				return "<boolean>" + ((bool)arg ? "1" : "0") + "</boolean>";
			} else if (arg is int) {
				return "<int>" + (int)arg + "</int>";
			} else if (arg is double) {
				return "<double>" + arg.ToString(GbxRemote.Culture) + "</double>";
			} else if (arg is string) {
				return "<string>" + Escape((string)arg, bEscape) + "</string>";
			} else if (arg is byte[]) {
				if (arg.m_str64 == "") {
					return "<base64/>";
				} else {
					return "<base64>" + Convert.ToBase64String((byte[])arg) + "</base64>";
				}
			} else if (arg is DateTime) {
				return "<dateTime.iso8601>" + ((DateTime)arg).ToString(GbxRemote.DateTimeFormat, GbxRemote.Culture) + "</dateTime.iso8601>";
			} else if (arg is Array) {
				var array = (Array)arg;
				if (array.Length == 0) {
					return "<array><data/></array>";
				}
				string ret = "<array><data>";
				foreach (dynamic v in array) {
					ret += "<value>" + EncodeValue(v, bEscape) + "</value>";
				}
				return ret + "</data></array>";
			} else if (arg is GbxStruct) {
				string ret = "<struct>";
				Type type = arg.GetType();
				var fields = type.GetFields();
				foreach (var field in fields) {
					string name = field.Name;
					var attrs = field.GetCustomAttributes(typeof(GbxStructNameAttribute), false);
					if (attrs.Length > 0) {
						var attr = (GbxStructNameAttribute)attrs[0];
						name = attr.m_name;
					}
					ret += "<member><name>" + Escape(name, bEscape) + "</name><value>" + EncodeValue(field.GetValue(arg)) + "</value></member>";
				}
				return ret + "</struct>";
			}

			System.Diagnostics.Debug.Assert(false);
			return "";
		}

		public static string Escape(string arg, bool bEscape)
		{
			if (bEscape) {
				return XmlEntities(arg);
			}
			return arg;
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
