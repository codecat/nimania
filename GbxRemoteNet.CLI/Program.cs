using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GbxRemoteNet.CLI
{
	class Program
	{
		static List<string> m_commands = new List<string>();
		static string m_serverLogin = "";

		static string ResolveCommand(string s)
		{
			foreach (string command in m_commands) {
				if (command.ToLower() == s.ToLower()) {
					return command;
				}
			}
			return s;
		}

		static void Main(string[] args)
		{
			if (args.Length != 2) {
				Console.WriteLine("Usage:");
				Console.WriteLine("  GbxRemoteNet.CLI <hostname> <port>");
				return;
			}

			string hostname = args[0];
			int port = int.Parse(args[1]);

			string serverLogin = "";
			int serverAuthLevel = 0;

			var rem = new GbxRemote();
			rem.Connect(hostname, port);

			rem.Query("system.listMethods", (GbxResponse res) => {
				var methods = res.m_value.Get<ArrayList>();
				foreach (GbxValue method in methods) {
					m_commands.Add(method.Get<string>());
				}
			}).Wait();

			rem.Query("GetSystemInfo", (GbxResponse res) => {
				m_serverLogin = res.m_value.Get<string>("ServerLogin");
			}).Wait();

			while (true) {
				Console.ForegroundColor = ConsoleColor.Gray;

				Console.WriteLine();
				Console.Write(m_serverLogin + " ");
				Console.ForegroundColor = ConsoleColor.White;
				switch (serverAuthLevel) {
					case 2: Console.Write("# "); break;
					default: Console.Write("$ "); break;
				}
				Console.ForegroundColor = ConsoleColor.Gray;

				string input = Console.ReadLine();
				string[] parse = input.SplitCommandline();

				Console.ForegroundColor = ConsoleColor.White;

				if (parse[0] == "exit" || parse[0] == "quit") {
					rem.Terminate();
					break;
				} else if (parse[0] == "help") {
					if (parse.Length == 2) {
						string command = ResolveCommand(parse[1]);

						rem.Query("system.methodSignature", (GbxResponse res) => {
							if (res == null) {
								return;
							}
							var sigs = res.m_value.Get<ArrayList>();
							foreach (GbxValue sig in sigs) {
								var sigParams = sig.Get<ArrayList>();
								Console.Write(((GbxValue)sigParams[0]).Get<string>() + " " + command + "(");
								for (int i = 1; i < sigParams.Count; i++) {
									var value = (GbxValue)sigParams[i];
									var sigParam = value.Get<string>();
									Console.Write(sigParam);
									if (i != sigParams.Count - 1) {
										Console.Write(", ");
									}
								}
								Console.WriteLine(")");
							}
						}, command).Wait();

						rem.Query("system.methodHelp", (GbxResponse resHelp) => {
							if (resHelp == null) {
								return;
							}
							var help = resHelp.m_value.Get<string>();
							Console.WriteLine(help);
						}, command).Wait();
					}

					continue;
				} else if (parse[0] == "find") {
					if (parse.Length == 2) {
						foreach (string command in m_commands) {
							if (command.ToLower().Contains(parse[1].ToLower())) {
								Console.WriteLine(command);
							}
						}
					}

					continue;
				} else if (parse[0] == "local") {
					parse = new string[] { "Authenticate", "SuperAdmin", "SuperAdmin" };
					Console.WriteLine("Authenticating with \"SuperAdmin\", \"SuperAdmin\"");
				}

				var funcArgs = new List<object>();
				for (int i = 1; i < parse.Length; i++) {

					int n = 0;
					double d = 0.0;

					if (int.TryParse(parse[i], out n)) {
						funcArgs.Add(n);
					} else if (double.TryParse(parse[i], out d)) {
						funcArgs.Add(d);
					} else if (parse[i].ToLower() == "false") {
						funcArgs.Add(false);
					} else if (parse[i].ToLower() == "true") {
						funcArgs.Add(true);
					} else {
						funcArgs.Add(parse[i]);
					}
				}

				string runMethod = ResolveCommand(parse[0]);
				rem.Query(runMethod, (GbxResponse res) => {
					if (res == null) {
						return;
					}
					if (runMethod == "Authenticate" && res.m_value.Get<bool>()) {
						switch (parse[1]) {
							case "SuperAdmin": serverAuthLevel = 2; break;
							case "Admin": serverAuthLevel = 1; break;
						}
					}
					res.m_value.DumpInfo();
				}, funcArgs.ToArray()).Wait();
			}

			Console.WriteLine("Goodbye!");
		}
	}
}
