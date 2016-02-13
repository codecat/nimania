using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nimania
{
	public class Program
	{
		public static Controller CurrentController;

		public static bool Running = false;
		public static bool Shutdown = false;

		static void Main(string[] args)
		{
			while (!Shutdown) {
				Running = true;

#if DEBUG
				CurrentController = new Controller("../../Data/Config.ini");
#else
				CurrentController = new Controller("Data/Config.ini");
#endif
				CurrentController.Run();

				while (Running) {
					Thread.Sleep(1);
				}
			}
		}
	}
}
