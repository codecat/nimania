using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nimble.XML;
using NLog;

namespace GbxRemoteNet
{
	public enum GbxValueType
	{
		None,
		Boolean,
		Integer,
		Double,
		String,
		Base64,
		DateTime,
		Array,
		Struct
	}

	public class GbxValue
	{
		private static Logger m_logger = LogManager.GetCurrentClassLogger();

		public object m_obj;
		public GbxValueType m_type;

		public GbxValue(XmlTag tag)
		{
			if (tag.Name == "boolean") {
				m_type = GbxValueType.Boolean;
				m_obj = (tag.Value == "1" ? true : false);
			} else if (tag.Name == "int" || tag.Name == "i4") {
				int n = 0;
				if (int.TryParse(tag.Value, out n)) {
					m_type = GbxValueType.Integer;
					m_obj = n;
				}
			} else if (tag.Name == "double") {
				double d = 0.0;
				if (double.TryParse(tag.Value, out d)) {
					m_type = GbxValueType.Double;
					m_obj = d;
				}
			} else if (tag.Name == "string") {
				m_type = GbxValueType.String;
				m_obj = tag.Value;
			} else if (tag.Name == "base64") {
				m_type = GbxValueType.Base64;
				m_obj = Convert.FromBase64String(tag.Value);
			} else if (tag.Name == "dateTime.iso8601") {
				DateTime dt = new DateTime();
				if (DateTime.TryParseExact(tag.Value, GbxRemote.DateTimeFormat, GbxRemote.Culture, DateTimeStyles.None, out dt)) {
					m_type = GbxValueType.DateTime;
					m_obj = dt;
				}
			} else if (tag.Name == "array") {
				var array = new List<GbxValue>();
				var tagData = tag["data"];
				foreach (var tagDataValue in tagData.Children) {
					array.Add(new GbxValue(tagDataValue.Children[0]));
				}
				m_type = GbxValueType.Array;
				m_obj = array;
			} else if (tag.Name == "struct") {
				var table = new Dictionary<string, GbxValue>();
				foreach (var tagMember in tag.Children) {
					var name = tagMember["name"].Value;
					var value = new GbxValue(tagMember["value"].Children[0]);
					System.Diagnostics.Debug.Assert(!table.ContainsKey(name));
					table.Add(name, value);
				}
				m_type = GbxValueType.Struct;
				m_obj = table;
			} else {
				throw new Exception("Unexpected tag name: " + tag.Name);
			}
		}

		public T Get<T>()
		{
			System.Diagnostics.Debug.Assert(m_obj.GetType() == typeof(T));
			return (T)m_obj;
		}

		public T Get<T>(string key)
		{
			System.Diagnostics.Debug.Assert(m_type == GbxValueType.Struct);
			var table = (Dictionary<string, GbxValue>)m_obj;
			System.Diagnostics.Debug.Assert(table.ContainsKey(key));
			return (T)table[key].m_obj;
		}

		public bool TryGet<T>(string key, ref T v)
		{
			System.Diagnostics.Debug.Assert(m_type == GbxValueType.Struct);
			var table = (Dictionary<string, GbxValue>)m_obj;
			if (!table.ContainsKey(key)) {
				return false;
			}
			v = (T)table[key].m_obj;
			return true;
		}

		public void DumpInfo(int startDepth = 0)
		{
			DumpInfoInternal(startDepth);
		}

		internal void DumpInfoInternal(int depth, string structKey = "")
		{
			string indent = "";
			for (int i = 0; i < depth; i++) {
				indent += "  ";
			}
			string keyInfo = "";
			if (structKey != "") {
				keyInfo = "[" + structKey + "]: ";
			}
			if (m_type == GbxValueType.Boolean) {
				m_logger.Trace(indent + keyInfo + "(boolean) " + ((bool)m_obj));
			} else if (m_type == GbxValueType.Integer) {
				m_logger.Trace(indent + keyInfo + "(int) " + ((int)m_obj));
			} else if (m_type == GbxValueType.Double) {
				m_logger.Trace(indent + keyInfo + "(double) " + ((double)m_obj));
			} else if (m_type == GbxValueType.String) {
				m_logger.Trace(indent + keyInfo + "(string) \"" + ((string)m_obj) + "\"");
			} else if (m_type == GbxValueType.Base64) {
				m_logger.Trace(indent + keyInfo + "(base64) " + ((byte[])m_obj).Length + " bytes");
			} else if (m_type == GbxValueType.DateTime) {
				m_logger.Trace(indent + keyInfo + "(datetime) " + ((DateTime)m_obj));
			} else if (m_type == GbxValueType.Array) {
				m_logger.Trace(indent + keyInfo + "(array) [");
				var arr = (List<GbxValue>)m_obj;
				foreach (var v in arr) {
					v.DumpInfoInternal(depth + 1);
				}
				m_logger.Trace(indent + "]");
			} else if (m_type == GbxValueType.Struct) {
				m_logger.Trace(indent + keyInfo + "(struct) {");
				var dic = (Dictionary<string, GbxValue>)m_obj;
				foreach (var key in dic.Keys) {
					dic[key].DumpInfoInternal(depth + 1, key);
				}
				m_logger.Trace(indent + "}");
			} else {
				m_logger.Trace(indent + keyInfo + "(unknown)");
			}
		}
	}
}
