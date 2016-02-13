using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nimania.Runtime
{
	public enum DbSortDir
	{
		Ascending,
		Descending,
	}

	public class DbQueryOptions
	{
		public bool m_sort = false;
		public string m_sortKey;
		public DbSortDir m_sortDir = DbSortDir.Ascending;

		public bool m_range = false;
		public int m_rangeFrom = 0;
		public int m_rangeTo;
	}

	public abstract class DbDriver
	{
		public string Tablename<T>()
		{
			var propTableName = typeof(T).GetProperty("Tablename");
			if (propTableName == null) {
				throw new Exception("Static 'Tablename' property not defined!");
			}
			return (string)propTableName.GetValue(null);
		}

		public string PrimaryKey<T>()
		{
			var propPrimaryKey = typeof(T).GetProperty("PrimaryKey");
			if (propPrimaryKey != null) {
				return (string)propPrimaryKey.GetValue(null);
			}
			return "ID";
		}

		public abstract T FindByPk<T>(int id);

		public abstract T FindByAttributes<T>(params dynamic[] attributes);

		public abstract T[] FindAllByAttributes<T>(params dynamic[] attributes);
		public abstract T[] FindAllByAttributes<T>(dynamic[] attributes, DbQueryOptions options);

		public abstract T[] FindAll<T>();
		public abstract T[] FindAll<T>(DbQueryOptions options);
	}
}
