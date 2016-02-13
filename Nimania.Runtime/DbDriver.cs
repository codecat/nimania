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
			return Tablename(typeof(T));
		}

		internal string Tablename(Type t)
		{
			var propTableName = t.GetProperty("Tablename");
			if (propTableName == null) {
				throw new Exception("Static 'Tablename' property not defined!");
			}
			return (string)propTableName.GetValue(null);
		}

		public string PrimaryKey<T>()
		{
			return PrimaryKey(typeof(T));
		}

		internal string PrimaryKey(Type t)
		{
			var propPrimaryKey = t.GetProperty("PrimaryKey");
			if (propPrimaryKey != null) {
				return (string)propPrimaryKey.GetValue(null);
			}
			return "ID";
		}

		public T Create<T>()
		{
			return (T)(object)Create(typeof(T));
		}

		internal DbModel Create(Type t)
		{
			var newModel = (DbModel)Activator.CreateInstance(t);
			newModel.m_database = this;
			return newModel;
		}

		public abstract T FindByPk<T>(int id);
		public abstract DbModel FindByPk(int id, Type type);

		public abstract T FindByAttributes<T>(params dynamic[] attributes);

		public abstract T[] FindAllByAttributes<T>(params dynamic[] attributes);
		public abstract T[] FindAllByAttributes<T>(dynamic[] attributes, DbQueryOptions options);

		public abstract T[] FindAll<T>();
		public abstract T[] FindAll<T>(DbQueryOptions options);

		public abstract void Save(DbModel model);
		public abstract void Insert(DbModel model);
	}
}
