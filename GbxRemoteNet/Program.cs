﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Nimble.XML;

namespace GbxRemoteNet
{
	public class Program
	{
		static void Main(string[] args)
		{
			GbxRemote rem = new GbxRemote();
			rem.ReportDebug = true;
			rem.Connect("localhost", 5000);
			rem.Query("Authenticate", (GbxResponse res) => {
			}, new string[] { "SuperAdmin", "SuperAdmin" });

			Console.WriteLine("Done");
			Console.ReadKey();
		}
	}
}
