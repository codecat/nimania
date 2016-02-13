using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nimania.Runtime;

namespace Nimania.Plugins
{
	public class Developer : Plugin
	{
		public override void Initialize()
		{
			SendViewToLogin("ansjh", "Developer/Bar.xml");
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
				case "Reload": Program.CurrentController.Reload(); break;
				case "Shutdown": Program.CurrentController.Shutdown(); break;
			}
		}
	}
}
