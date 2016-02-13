using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nimania.Runtime.DbModels
{
	public class LocalTime : DbModel
	{
		public static string Tablename { get { return "locals"; } }

		public int ID;
		public Map Map;
		public Player Player;
		public int Time;
		public string Checkpoints;
	}
}
