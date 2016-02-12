using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GbxRemoteNet;

namespace Nimania.Runtime
{
	public abstract class Plugin
	{
		public GbxRemote m_remote;

		public abstract void Initialize();
		public abstract void Uninitialize();
		public abstract void OnAction(string login, string action);

		public void SendChat(string s)
		{
			m_remote.Execute("ChatSendServerMessage", s);
		}

		public void SendView(string file, params string[] kvs) { SendView(file, 0, false, kvs); }
		public void SendView(string file, int timeout, params string[] kvs) { SendView(file, timeout, false, kvs); }
		public void SendView(string file, int timeout, bool clickHides, params string[] kvs)
		{
			string xml = GetView(file, kvs);
			m_remote.Execute("SendDisplayManialinkPage", xml, timeout, clickHides);
		}
		
		public void SendViewToLogin(string login, string file, params string[] kvs) { SendViewToLogin(login, file, 0, false, kvs); }
		public void SendViewToLogin(string login, string file, int timeout, params string[] kvs) { SendViewToLogin(login, file, timeout, false, kvs); }
		public void SendViewToLogin(string login, string file, int timeout, bool clickHides, params string[] kvs)
		{
			string xml = GetView(file, kvs);
			m_remote.Execute("SendDisplayManialinkPageToLogin", login, xml, timeout, clickHides);
		}

		private string GetView(string file, string[] kvs)
		{
			if (kvs.Length % 2 != 0) {
				throw new Exception("Uneven amount of strings passed to SendView!");
			}
			string xml = File.ReadAllText("Data/Views/" + file);
			for (int i = 0; i < kvs.Length; i += 2) {
				xml = xml.Replace("<?=" + kvs[i] + "?>", kvs[i + 1]);
			}
			return xml;
		}
	}
}
