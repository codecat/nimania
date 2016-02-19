using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nimania.Runtime.DbModels
{
	public class MapVote : DbModel
	{
		public static string Tablename { get { return "mapvotes"; } }

		public int ID;
		public string Login;
		public Map Map;
		public int Value;
	}
}
