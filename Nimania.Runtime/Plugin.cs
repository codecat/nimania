using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GbxRemoteNet;
using NLog;
using Nimania.Runtime.DbModels;
using NCalc;
using Nimble.Utils;

namespace Nimania.Runtime
{
	public abstract class Plugin
	{
		protected Logger m_logger { get; private set; }
		protected Plugin()
		{
			m_logger = LogManager.GetLogger(GetType().FullName);
		}

		public ConfigFile m_config;
		public GbxRemote m_remote;
		public DbDriver m_database;

		public GameInfo m_game;

		public static Random m_random = new Random();

		public virtual void Initialize() { }
		public virtual void Uninitialize() { }

		public virtual void EverySecond() { }

		// Soft reload, typically called from developer UI, should usually only resend widgets
		public virtual void SoftReload() { }

		// Synced callbacks
		public virtual void OnAction(PlayerInfo player, string action, string[] args) { }

		public virtual void OnBeginChallenge() { }
		public virtual void OnEndChallenge() { }

		public virtual void OnPlayerConnect(PlayerInfo player) { }
		public virtual void OnPlayerDisconnect(PlayerInfo player) { }

		public virtual void OnEndRound() { }

		public virtual void OnNextMap(Map map) { }

		// Trackmania synced callbacks
		public virtual void OnPlayerBegin(PlayerInfo player) { } // called on retire and begin of 321go
		public virtual void OnPlayerCheckpoint(PlayerInfo player, int n, int time) { }
		public virtual void OnPlayerFinish(PlayerInfo player, int time, int[] checkpoints) { }

		public void SendView(string file, params object[] kvs) { SendView(file, 0, false, kvs); }
		public void SendView(string file, int timeout, params object[] kvs) { SendView(file, timeout, false, kvs); }
		public void SendView(string file, int timeout, bool clickHides, params object[] kvs)
		{
			string xml = GetResource(file, kvs);
			if (xml == "") {
				return;
			}
			m_logger.Debug("Sending view {0} to all players", file);
			m_remote.Execute("SendDisplayManialinkPage", xml, timeout, clickHides);
		}

		public void SendViewToLogin(string login, string file, params object[] kvs) { SendViewToLogin(login, file, 0, false, kvs); }
		public void SendViewToLogin(string login, string file, int timeout, params object[] kvs) { SendViewToLogin(login, file, timeout, false, kvs); }
		public void SendViewToLogin(string login, string file, int timeout, bool clickHides, params object[] kvs)
		{
			string xml = GetResource(file, kvs);
			if (xml == "") {
				return;
			}
			m_logger.Debug("Sending view {0} to {1}", file, login);
			m_remote.Execute("SendDisplayManialinkPageToLogin", login, xml, timeout, clickHides);
		}

		public void SendChat(string s)
		{
			m_remote.Execute("ChatSendServerMessage", s);
		}

		public void SendChatTo(string login, string text)
		{
			m_remote.Execute("ChatSendServerMessageToLogin", text, login);
		}

		public void SendChatTo(int id, string text)
		{
			m_remote.Execute("ChatSendServerMessageToId", text, id);
		}

		public string GetResource(string file, params object[] kvs)
		{
			if (kvs.Length % 2 != 0) {
				throw new Exception("Uneven amount of params passed to GetResource!");
			}

#if DEBUG
			string filename = "../../Data/Views/" + file;
#else
			string filename = "Data/Views/" + file;
#endif

			if (!File.Exists(filename)) {
				m_logger.Warn("Resource not found: " + file);
				return "";
			}
			var lines = File.ReadAllLines(filename);
			var ret = new StringBuilder();
			bool skipLines = false;
			for (int i = 0; i < lines.Length; i++) {
				var line = lines[i].Trim();

				if (line.StartsWith("%if")) {
					var parse = line.SplitCommandline();
					var key = parse.Get(1);
					var op = parse.Get(2, "===");
					var value = parse.Get(3, "True");

					bool found = false;
					for (int j = 0; j < kvs.Length; j++) {
						if (kvs[j].ToString() != key) {
							continue;
						}
						found = true;

						if (op == "==") {
							skipLines = !(kvs[j + 1].ToString().ToLower() == value.ToLower());
						} else if (op == "===") {
							skipLines = !(kvs[j + 1].ToString() == value);
						} else if (op == "!=") {
							skipLines = (kvs[j + 1].ToString().ToLower() == value.ToLower());
						} else if (op == "!==") {
							skipLines = (kvs[j + 1].ToString() == value);
						}
						break;
					}

					if (!found) {
						m_logger.Warn("Encountered %if with unexisting key '{0}' in resource '{1}'", key, file);
						skipLines = true;
					}

				} else if (line == "%endif") {
					skipLines = false;

				} else if (!skipLines) {
					for (int j = 0; j < kvs.Length; j += 2) {
						string find = "<?=" + kvs[j].ToString() + "?>";
						string replace = kvs[j + 1].ToString();
						line = line.Replace(find, replace);
					}

					while (true) {
						int iStart = line.IndexOf("<$=");
						if (iStart == -1) {
							break;
						}

						string expression = line.Substring(iStart + 3);
						int iEnd = expression.IndexOf("$>");
						expression = expression.Substring(0, iEnd);

						var exp = new Expression(expression);

						for (int j = 0; j < kvs.Length; j += 2) {
							exp.Parameters[kvs[j].ToString()] = kvs[j + 1];
						}

						string newLine = "";
						if (iStart > 0) {
							newLine += line.Substring(0, iStart);
						}
						try {
							newLine += exp.Evaluate().ToString();
						} catch (Exception ex) {
							m_logger.Error("Template expression evaluation error in resource '{0}' on line {1}: '{2}'", file, i + 1, ex.Message.Replace("\r\n", " "));
						}
						newLine += line.Substring(iStart + 3 + expression.Length + 2);
						line = newLine;
					}

					if (line.StartsWith("<include ")) {
						XMLTag tag = SimpleXMLReader.Parse(line.Trim());

						string fnm = tag.Attributes["src"];

						List<object> includeKvs = new List<object>(kvs);
						foreach (var kv in tag.Attributes) {
							if (kv.Key == "src") {
								continue;
							}

							string stringval = kv.Value;
							if (stringval[0] == '?') {
								includeKvs.Add(kv.Key);

								stringval = stringval.Substring(1);

								bool found = false;
								for (int k = 0; k < kvs.Length; k += 2) {
									if (kvs[k].ToString() == stringval) {
										includeKvs.Add(kvs[k + 1]);
										found = true;
										break;
									}
								}

								if (!found) {
									m_logger.Error("Include key/value pair lookup for '?{0}' was not found in %include directive!", stringval);
								}

							} else if (stringval[0] == '=') {
								stringval = stringval.Substring(1);

								bool found = false;
								for (int k = 0; k < includeKvs.Count; k += 2) {
									if (includeKvs[k].ToString() == stringval) {
										includeKvs[k] = kv.Key;
										found = true;
										break;
									}
								}

								if (!found) {
									m_logger.Error("Include key/value pair lookup for '={0}' was not found in %include directive!", stringval);
								}

							} else {
								includeKvs.Add(kv.Key);
								includeKvs.Add(kv.Value);
							}
						}

						ret.Append(GetResource(fnm, includeKvs.ToArray()) + "\n");
					}

					ret.Append(line + "\n");
				}
			}
			return ret.ToString();
		}

		public Map LoadMapInfo(GbxValue val)
		{
			string uid = val.Get<string>("UId");
			var map = m_database.FindByAttributes<Map>("UId", uid);
			if (map == null) {
				map = m_database.Create<Map>();
				map.UId = uid;
				map.Name = val.Get<string>("Name");
				map.Author = val.Get<string>("Author");
				map.FileName = val.Get<string>("FileName");
				map.Save();
			}

			// Sadly, there are times when we don't get as much info as we might want.
			val.TryGet("NbCheckpoints", ref map.m_nCheckpoints);

			val.TryGet("BronzeTime", ref map.m_timeBronze);
			val.TryGet("SilverTime", ref map.m_timeSilver);
			val.TryGet("AuthorTime", ref map.m_timeAuthor);
			val.TryGet("LapRace", ref map.m_laps);

			// But this one does make the cut. Thanks Nadeo.
			map.m_timeGold = val.Get<int>("GoldTime");
			return map;
		}
	}
}
