using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nimania.Runtime;

namespace Nimania.Plugins
{
	public class Admin : Plugin
	{
		public override void Initialize()
		{
			Console.WriteLine("Initializing Admin plugin");
			SendView("Admin/Bar.xml");
		}

		public override void Uninitialize()
		{
		}

		public override void OnAction(string login, string action)
		{
			//TODO: Tie to database of sorts
			if (login != "ansjh") {
				return;
			}
			switch (action) {
				case "RestartMap": m_remote.Execute("RestartMap"); break;
				case "ForceEndRound": m_remote.Execute("ForceEndRound"); break;
				case "NextMap": m_remote.Execute("NextMap"); break;
			}
		}
	}
}
