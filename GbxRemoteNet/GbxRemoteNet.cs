using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using Nimble.XML;
using System.Collections;
using System.Globalization;

namespace GbxRemoteNet
{
	public delegate void OnGbxCallback(GbxCallback e);

	public class GbxRemote
	{
		public const string DateTimeFormat = "yyyyMMdd\tHH:mm:ss";
		public static CultureInfo Culture = new CultureInfo("en-US");

		TcpClient m_client;
		NetworkStream m_ns;
		BinaryWriter m_writer;
		BinaryReader m_reader;

		Hashtable m_callbackTable = new Hashtable();

		Thread m_readThread;
		bool m_keepReading;

		uint m_requestHandle = 0x80000000;

		public bool ReportDebug = false;
		public bool ReportError = true;

		Dictionary<string, List<OnGbxCallback>> m_callbacks = new Dictionary<string, List<OnGbxCallback>>();

		public GbxRemote()
		{
		}

		public void Connect(string strHost, int iPort)
		{
			m_client = new TcpClient();
			m_client.Connect(strHost, iPort);
			m_ns = m_client.GetStream();
			m_writer = new BinaryWriter(m_ns);
			m_reader = new BinaryReader(m_ns);

			uint size = m_reader.ReadUInt32();
			string protocol = m_reader.ReadString(size);
			if (protocol != "GBXRemote 2") {
				throw new Exception("Unexpected protocol version: " + protocol);
			}

			m_readThread = new Thread(new ThreadStart(ReadLoop));
			m_keepReading = true;
			m_readThread.Start();
		}

		private void FatalError(string str)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("[FATAL] " + str);
			Console.ForegroundColor = ConsoleColor.Gray;
		}

		private void PrintError(string str)
		{
			if (!ReportError) {
				return;
			}
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("[ERR] " + str);
			Console.ForegroundColor = ConsoleColor.Gray;
		}

		private void PrintDebug(string str)
		{
			if (!ReportDebug) {
				return;
			}
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine("[DBG] " + str);
			Console.ForegroundColor = ConsoleColor.Gray;
		}

		private void ReadLoop()
		{
			while (m_keepReading) {
				uint size = m_reader.ReadUInt32();
				uint handle = m_reader.ReadUInt32();
				if (size == 0 || handle == 0) {
					FatalError("Unexpected empty size or handle while reading GBX packet");
					return;
				}
				string strXml = m_reader.ReadString(size);
				PrintDebug(size + " bytes (handle " + handle.ToString("x8") + "): \"" + strXml + "\"");
				XmlFile xml = new XmlFile(new MemoryStream(Encoding.UTF8.GetBytes(strXml)));
				string str = xml.Root.Children[0].Name;
				if (str == "methodResponse") {
					var response = xml["methodResponse"];
					if (response.Children[0].Name == "fault") {
						PrintError(response["fault"]["value"]["struct"].Children[1]["string"].Value);
						continue;
					}
					if (!m_callbackTable.ContainsKey(handle)) {
						continue;
					}
					var callback = m_callbackTable[handle];
					m_callbackTable.Remove(handle);
					var respType = callback.GetType().GetGenericArguments()[0];
					if (respType.BaseType != typeof(GbxResponse) && respType != typeof(GbxResponse)) {
						PrintError("Generic response type must inherit from GbxResponse!");
						continue;
					}
					var res = (GbxResponse)Activator.CreateInstance(respType);
					res.m_value = new GbxValue(response["params"].Children[0]["value"].Children[0]);
					((Delegate)callback).DynamicInvoke(res);
				} else if (str == "methodCall") {
					var response = xml["methodCall"];
					var methodCall = response["methodName"].Value;
					var methodParams = response["params"];
					var ret = new GbxCallback();
					var retParams = new List<GbxValue>();
					foreach (var param in methodParams.Children) {
						retParams.Add(new GbxValue(param["value"].Children[0]));
					}
					ret.m_params = retParams.ToArray();
					string methodCallLower = methodCall.ToLower();
					if (m_callbacks.ContainsKey(methodCallLower)) {
						foreach (var cb in m_callbacks[methodCallLower]) {
							cb(ret);
						}
					}
				}
			}
		}

		public void Terminate()
		{
			if (m_client != null) {
				m_client.Close();
				m_client = null;
			}
		}

		public void AddCallback(string strMethod, OnGbxCallback func)
		{
			string strMethodLower = strMethod.ToLower();
			if (!m_callbacks.ContainsKey(strMethodLower)) {
				m_callbacks[strMethodLower] = new List<OnGbxCallback>();
			}
			m_callbacks[strMethodLower].Add(func);
		}

		public void Query<T>(string strMethod, Action<T> callback, params dynamic[] args)
		{
			string strXml = GbxRequest.Encode(strMethod, args, true);

			//TODO: MAX_REQUEST_SIZE and "multicall".. I'm lazy tonight :)

			if (m_requestHandle == 0xffffffff) {
				m_requestHandle = 0x80000000;
			}
			m_requestHandle++;

			m_callbackTable[m_requestHandle] = callback;
			WriteMessage(strXml, m_requestHandle);
		}

		public void Execute(string strMethod, params dynamic[] args)
		{
			string strXml = GbxRequest.Encode(strMethod, args, true);

			//TODO: MAX_REQUEST_SIZE and "multicall".. I'm lazy tonight :)

			if (m_requestHandle == 0xffffffff) {
				m_requestHandle = 0x80000000;
			}
			m_requestHandle++;

			WriteMessage(strXml, m_requestHandle);
		}

		public void WriteMessage(string strXml, uint handle)
		{
			m_writer.Write((uint)strXml.Length);
			m_writer.Write(handle);
			m_writer.WriteStringBytes(strXml);
		}

		public void GenerateDocumentation(string filename)
		{
			StreamWriter writer = new StreamWriter(filename);

			Query("system.listMethods", (GbxResponse res) => {
				var methods = res.m_value.Get<ArrayList>();
				foreach (GbxValue method in methods) {
					string methodName = method.Get<string>();
					writer.WriteLine(methodName + ":");

					Query("system.methodSignature", (GbxResponse resSignature) => {
						var sigs = resSignature.m_value.Get<ArrayList>();
						foreach (GbxValue sig in sigs) {
							var sigParams = sig.Get<ArrayList>();
							for (int i = 0; i < sigParams.Count; i++) {
								var value = (GbxValue)sigParams[i];
								var sigParam = value.Get<string>();
								writer.Write(sigParam);
								if (i != sigParams.Count - 1) {
									writer.Write(", ");
								}
							}
						}
						return;
					}, methodName);
				}
			});
		}
	}

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
				m_obj = new Base64String(tag.Value, true);
			} else if (tag.Name == "dateTime.iso8601") {
				DateTime dt = new DateTime();
				if (DateTime.TryParseExact(tag.Value, GbxRemote.DateTimeFormat, GbxRemote.Culture, DateTimeStyles.None, out dt)) {
					m_type = GbxValueType.DateTime;
					m_obj = dt;
				}
			} else if (tag.Name == "array") {
				var array = new ArrayList();
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
	}

	public class GbxResponse
	{
		public GbxValue m_value;
	}

	public class GbxCallback
	{
		public GbxValue[] m_params;
	}

	public static class GbxRequest
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
			} else if (arg is Base64String) {
				if (arg.m_str64 == "") {
					return "<base64/>";
				} else {
					return "<base64>" + ((Base64String)arg).m_str64 + "</base64>";
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
				var props = type.GetProperties();
				foreach (var prop in props) {
					ret += "<member><name>" + Escape(prop.Name, bEscape) + "</name><value>" + EncodeValue(prop.GetValue(arg, null)) + "</value></member>";
				}
				return ret + "</struct>";
			}

			System.Diagnostics.Debug.Assert(false);
			return "";
		}

		public static string Escape(string arg, bool bEscape)
		{
			//TODO: Is this even safe? Took this directly from Nadeo's PHP code..
			if (bEscape) {
				return "<![CDATA[" + arg.Replace("]]>", "]]]]><![CDATA[>") + "]]>";
			}
			return arg;
		}
	}

	public abstract class GbxStruct { }

	public class Base64String
	{
		public string m_str;
		public string m_str64;

		public Base64String(string str, bool strIs64 = false)
		{
			if (strIs64) {
				m_str = Encoding.UTF8.GetString(Convert.FromBase64String(str));
				m_str64 = str;
			} else {
				m_str = str;
				m_str64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(str));
			}
		}
	}

	public static class BinaryStreamExtensions
	{
		public static void WriteStringBytes(this BinaryWriter writer, string str)
		{
			writer.WriteStringBytes(str, Encoding.UTF8);
		}

		public static void WriteStringBytes(this BinaryWriter writer, string str, Encoding encoding)
		{
			writer.Write(encoding.GetBytes(str));
		}

		public static string ReadString(this BinaryReader reader, uint n)
		{
			return reader.ReadString(n, Encoding.UTF8);
		}

		public static string ReadString(this BinaryReader reader, uint n, Encoding encoding)
		{
			if (n == 0) {
				return "";
			}
			var buffer = reader.ReadBytes((int)n);
			return encoding.GetString(buffer);
		}
	}
}
