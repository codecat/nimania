using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nimania.Runtime.DbModels
{
	public class LocalPlayerGroup : DbModel
	{
		public static string Tablename { get { return "groups"; } }

		public int ID;
		public int Level;
		public string Name;

		public bool IsAdmin;
		public bool IsDeveloper;
	}
}
