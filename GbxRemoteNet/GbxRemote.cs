using System;
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
	public delegate void OnGbxCallback(GbxValue[] values);
	public delegate void OnGbxResponse(GbxValue value);

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
									failedCallback(null);
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
						var res = new GbxValue(response["params"].Children[0]["value"].Children[0]);
						if (m_logger.IsTraceEnabled) {
							res.DumpInfo();
						}
						Task.Factory.StartNew(() => {
							callback(res);
							request.m_finished = true;
							request.m_reset.Set();
						});
					} else if (str == "methodCall") {
						var response = xml["methodCall"];
						var methodCall = response["methodName"].Value;
						var methodParams = response["params"];
						var ret = new List<GbxValue>();
						m_logger.Trace("Callback {0} with {1} params", methodCall, methodParams.Children.Count);
						foreach (var param in methodParams.Children) {
							var v = new GbxValue(param["value"].Children[0]);
							ret.Add(v);
							if (m_logger.IsTraceEnabled) {
								v.DumpInfo(1);
							}
						}
						string methodCallLower = methodCall.ToLower();
						if (m_callbacks.ContainsKey(methodCallLower)) {
							foreach (var callback in m_callbacks[methodCallLower]) {
								Task.Factory.StartNew(() => {
									callback(ret.ToArray());
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

		public GbxRequest Query(string strMethod, OnGbxResponse callback, params dynamic[] args)
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

		public GbxValue QueryWait(string strMethod, params dynamic[] args)
		{
			GbxValue ret = null;
			Query(strMethod, (GbxValue res) => {
				ret = res;
			}, args).Wait();
			return ret;
		}

		public GbxRequest MultiQuery(Action<GbxValue[]> callback, params GbxMultiCall[] methods)
		{
			return Query("system.multicall", (GbxValue res) => {
				var results = res.Get<ArrayList>();
				var ret = new List<GbxValue>();
				foreach (GbxValue result in results) {
					if (result.m_type == GbxValueType.Struct) {
						ret.Add(null);
						continue;
					}
					ret.Add((GbxValue)result.Get<ArrayList>()[0]);
				}
				callback(ret.ToArray());
			}, new dynamic[] { methods });
		}

		public GbxRequest MultiQuery(OnGbxResponse[] callbacks, params GbxMultiCall[] methods)
		{
			return Query("system.multicall", (GbxValue res) => {
				var results = res.Get<ArrayList>();
				for (int i = 0; i < results.Count; i++) {
					var result = (GbxValue)results[i];
					if (result.m_type == GbxValueType.Struct) {
						callbacks[i](null);
						continue;
					}
					callbacks[i]((GbxValue)result.Get<ArrayList>()[0]);
				}
			}, new dynamic[] { methods });
		}

		public GbxValue[] MultiQueryWait(params string[] methods)
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

		public GbxValue[] MultiQueryWait(params GbxMultiCall[] methods)
		{
			var ret = new List<GbxValue>();
			MultiQuery((GbxValue[] results) => {
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

			Query("system.listMethods", (GbxValue res) => {
				var methods = res.Get<ArrayList>();
				foreach (GbxValue method in methods) {
					string methodName = method.Get<string>();

					Query("system.methodSignature", (GbxValue resSignature) => {
						var sigs = resSignature.Get<ArrayList>();
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

					Query("system.methodHelp", (GbxValue resHelp) => {
						var help = resHelp.Get<string>();
						writer.WriteLine("  Description:");
						writer.WriteLine("    " + help);
					}, methodName).Wait();

					writer.WriteLine();
					writer.WriteLine();
				}
			}).Wait();

			writer.Close();
			m_logger.Info("Wrote documentation to: " + filename);
		}
	}
}
