﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using Nimble.XML;
using System.Collections;
using System.Globalization;
using System.Threading.Tasks;
using NLog;

namespace GbxRemoteNet
{
	public delegate void OnGbxCallback(GbxCallback e);

	public class GbxMultiCall : GbxStruct
	{
		[GbxStructName("methodName")]
		public string m_methodName;

		[GbxStructName("params")]
		public Array m_methodParams;
	}

	public class GbxRemote
	{
		private static Logger m_logger = LogManager.GetCurrentClassLogger();

		public const string DateTimeFormat = "yyyyMMdd\tHH:mm:ss";
		public static CultureInfo Culture = new CultureInfo("en-US");

		static Mutex m_logMutex = new Mutex();
		Mutex m_writeMutex = new Mutex();

		string m_connectHost;
		int m_connectPort;

		TcpClient m_client;
		NetworkStream m_ns;
		BinaryWriter m_writer;
		BinaryReader m_reader;

		Hashtable m_callbackTable = new Hashtable();

		Thread m_readThread;
		bool m_keepReading;

		uint m_requestHandle = 0x80000000;

		Dictionary<string, List<OnGbxCallback>> m_callbacks = new Dictionary<string, List<OnGbxCallback>>();

		public void Connect(string strHost, int iPort)
		{
			m_connectHost = strHost;
			m_connectPort = iPort;

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

			m_logger.Info("Connected to server {0}:{1}", strHost, iPort);

			m_readThread = new Thread(new ThreadStart(ReadLoop));
			m_keepReading = true;
			m_readThread.Start();
		}

		private void ReadLoop()
		{
			while (m_keepReading) {
				try {
					uint size = m_reader.ReadUInt32();
					uint handle = m_reader.ReadUInt32();
					if (size == 0 || handle == 0) {
						m_logger.Fatal("Unexpected empty size or handle while reading GBX packet");
						return;
					}
					string strXml = m_reader.ReadString(size);
					m_logger.Trace("{0} bytes (handle {1:x8})", size, handle);
					XmlFile xml = new XmlFile(new MemoryStream(Encoding.UTF8.GetBytes(strXml)));
					string str = xml.Root.Children[0].Name;
					if (str == "methodResponse") {
						var response = xml["methodResponse"];
						if (response.Children[0].Name == "fault") {
							m_logger.Error("Server fault: \"{0}\"", response["fault"]["value"]["struct"].Children[1]["string"].Value);
							if (m_callbackTable.ContainsKey(handle)) {
								var failedRequest = (GbxRequest)m_callbackTable[handle];
								m_callbackTable.Remove(handle);
								var failedCallback = failedRequest.m_callback;
								Task.Factory.StartNew(() => {
									failedCallback.DynamicInvoke(new object[] { null });
									failedRequest.m_finished = true;
									failedRequest.m_reset.Set();
								});
							}
							continue;
						}
						if (!m_callbackTable.ContainsKey(handle)) {
							continue;
						}
						var request = (GbxRequest)m_callbackTable[handle];
						m_callbackTable.Remove(handle);
						var callback = request.m_callback;
						var respType = callback.GetType().GetGenericArguments()[0];
						if (respType.BaseType != typeof(GbxResponse) && respType != typeof(GbxResponse)) {
							m_logger.Fatal("Generic response type must inherit from GbxResponse!");
							continue;
						}
						var res = (GbxResponse)Activator.CreateInstance(respType);
						res.m_value = new GbxValue(response["params"].Children[0]["value"].Children[0]);
						if (m_logger.IsTraceEnabled) {
							res.m_value.DumpInfo();
						}
						Task.Factory.StartNew(() => {
							callback.DynamicInvoke(res);
							request.m_finished = true;
							request.m_reset.Set();
						});
					} else if (str == "methodCall") {
						var response = xml["methodCall"];
						var methodCall = response["methodName"].Value;
						var methodParams = response["params"];
						var ret = new GbxCallback();
						var retParams = new List<GbxValue>();
						m_logger.Trace("Callback {0} with {1} params", methodCall, methodParams.Children.Count);
						foreach (var param in methodParams.Children) {
							var v = new GbxValue(param["value"].Children[0]);
							retParams.Add(v);
							if (m_logger.IsTraceEnabled) {
								v.DumpInfo(1);
							}
						}
						ret.m_params = retParams.ToArray();
						string methodCallLower = methodCall.ToLower();
						if (m_callbacks.ContainsKey(methodCallLower)) {
							foreach (var callback in m_callbacks[methodCallLower]) {
								Task.Factory.StartNew(() => {
									callback(ret);
								});
							}
						}
					}
				} catch (Exception ex) {
					if (ex is IOException || ex is ObjectDisposedException) {
						if (m_keepReading) {
							m_logger.Error("Connection to server closed unexpectedly!");
							m_logger.Info("Reconnecting...");
							Connect(m_connectHost, m_connectPort);
						}
						return;
					}
					throw;
				}
			}
		}

		public void Terminate()
		{
			m_keepReading = false;
			if (m_client != null) {
				m_client.Close();
				m_client = null;
			}
		}

		public void EnableCallbacks(bool enable)
		{
			Execute("EnableCallbacks", enable);
		}

		public void AddCallback(string strMethod, OnGbxCallback func)
		{
			string strMethodLower = strMethod.ToLower();
			if (!m_callbacks.ContainsKey(strMethodLower)) {
				m_callbacks[strMethodLower] = new List<OnGbxCallback>();
			}
			m_callbacks[strMethodLower].Add(func);
		}

		public GbxRequest Query<T>(string strMethod, Action<T> callback, params dynamic[] args)
		{
			string strXml = GbxEncode.Encode(strMethod, args, true);
			m_logger.Trace("Query({0}) with {1} args, {2} bytes", strMethod, args.Length, strXml.Length);

			if (m_requestHandle == 0xffffffff) {
				m_requestHandle = 0x80000000;
			}
			m_requestHandle++;

			var ret = new GbxRequest(callback);

			m_callbackTable[m_requestHandle] = ret;
			WriteMessage(strXml, m_requestHandle);

			return ret;
		}

		public GbxResponse QueryWait(string strMethod, params dynamic[] args)
		{
			GbxResponse ret = null;
			Query(strMethod, (GbxResponse res) => {
				ret = res;
			}, args).Wait();
			return ret;
		}

		public GbxRequest MultiQuery(Action<GbxResponse[]> callback, params GbxMultiCall[] methods)
		{
			return Query("system.multicall", (GbxResponse res) => {
				var ret = new List<GbxResponse>();
				var results = res.m_value.Get<ArrayList>();
				foreach (GbxValue result in results) {
					if (result.m_type == GbxValueType.Struct) {
						ret.Add(null);
						continue;
					}
					ret.Add(new GbxResponse() {
						m_value = (GbxValue)result.Get<ArrayList>()[0]
					});
				}
				callback(ret.ToArray());
			}, new dynamic[] { methods });
		}

		public GbxResponse[] MultiQueryWait(params string[] methods)
		{
			GbxMultiCall[] arr = new GbxMultiCall[methods.Length];
			for (int i = 0; i < methods.Length; i++) {
				arr[i] = new GbxMultiCall() {
					m_methodName = methods[i],
					m_methodParams = new object[0]
				};
			}
			return MultiQueryWait(arr);
		}

		public GbxResponse[] MultiQueryWait(params GbxMultiCall[] methods)
		{
			var ret = new List<GbxResponse>();
			MultiQuery((GbxResponse[] results) => {
				foreach (var res in results) {
					ret.Add(res);
				}
			}, methods).Wait();
			return ret.ToArray();
		}

		public void Execute(string strMethod, params dynamic[] args)
		{
			string strXml = GbxEncode.Encode(strMethod, args, true);
			m_logger.Trace("Execute({0}) with {1} args, {2} bytes", strMethod, args.Length, strXml.Length);

			if (m_requestHandle == 0xffffffff) {
				m_requestHandle = 0x80000000;
			}
			m_requestHandle++;

			WriteMessage(strXml, m_requestHandle);
		}

		public void WriteMessage(string strXml, uint handle)
		{
			if (!m_keepReading) {
				return;
			}
			m_writeMutex.WaitOne();
			m_writer.Write((uint)strXml.Length);
			m_writer.Write(handle);
			m_writer.WriteStringBytes(strXml);
			m_writeMutex.ReleaseMutex();
		}

		public void GenerateDocumentation(string filename)
		{
			StreamWriter writer = new StreamWriter(filename);

			Query("system.listMethods", (GbxResponse res) => {
				var methods = res.m_value.Get<ArrayList>();
				foreach (GbxValue method in methods) {
					string methodName = method.Get<string>();

					Query("system.methodSignature", (GbxResponse resSignature) => {
						var sigs = resSignature.m_value.Get<ArrayList>();
						foreach (GbxValue sig in sigs) {
							var sigParams = sig.Get<ArrayList>();
							writer.Write(((GbxValue)sigParams[0]).Get<string>() + " " + methodName + "(");
							for (int i = 1; i < sigParams.Count; i++) {
								var value = (GbxValue)sigParams[i];
								var sigParam = value.Get<string>();
								writer.Write(sigParam);
								if (i != sigParams.Count - 1) {
									writer.Write(", ");
								}
							}
							writer.WriteLine(")");
						}
					}, methodName).Wait();

					writer.WriteLine();

					Query("system.methodHelp", (GbxResponse resHelp) => {
						var help = resHelp.m_value.Get<string>();
						writer.WriteLine("  Description:");
						writer.WriteLine("    " + help);
					}, methodName).Wait();

					writer.WriteLine();
					writer.WriteLine();
				}
			}).Wait();

			writer.Close();
			Console.WriteLine("Wrote documentation to: " + filename);
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
				var arr = (ArrayList)m_obj;
				foreach (GbxValue v in arr) {
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

	public class GbxRequest
	{
		public Delegate m_callback;
		public bool m_finished;
		public AutoResetEvent m_reset = new AutoResetEvent(false);

		public GbxRequest(Delegate callback)
		{
			m_callback = callback;
		}

		public void Wait()
		{
			if (m_finished) {
				return;
			}
			m_reset.WaitOne();
		}
	}

	//TODO: Remove this class and just return GbxValue's as responses?
	public class GbxResponse
	{
		public GbxValue m_value;
	}

	public class GbxCallback
	{
		public GbxValue[] m_params;
	}

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

	public abstract class GbxStruct { }

	public class GbxStructNameAttribute : Attribute
	{
		public string m_name;

		public GbxStructNameAttribute(string name)
		{
			m_name = name;
		}
	}

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
