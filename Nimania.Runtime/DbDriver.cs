using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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

	public class DbCache
	{
		private DateTime m_timeAdded;
		public DbModel m_model;

		public DbCache(DbModel model)
		{
			m_timeAdded = DateTime.Now;
			m_model = model;
		}

		public int TimePassed()
		{
			return (int)(DateTime.Now - m_timeAdded).TotalSeconds;
		}
	}

	public abstract class DbDriver
	{
		//TODO: Make an update loop that clears the cache of outdated rows (it only does it on access right now, which still leaks!)
		protected Dictionary<string, Dictionary<int, DbCache>> m_modelCache = new Dictionary<string, Dictionary<int, DbCache>>();
		protected int m_modelCacheTime = 0;

		public string Tablename<T>()
		{
			return Tablename(typeof(T).GetTypeInfo());
		}

		internal string Tablename(TypeInfo t)
		{
			var propTableName = t.GetDeclaredProperty("Tablename");
			if (propTableName == null) {
				throw new Exception("Static 'Tablename' property not defined!");
			}
			return (string)propTableName.GetValue(null);
		}

		public string PrimaryKey<T>()
		{
			return PrimaryKey(typeof(T).GetTypeInfo());
		}

		internal string PrimaryKey(TypeInfo t)
		{
			var propPrimaryKey = t.GetDeclaredProperty("PrimaryKey");
			if (propPrimaryKey != null) {
				return (string)propPrimaryKey.GetValue(null);
			}
			return "ID";
		}

		public T Create<T>()
		{
			return (T)(object)Create(typeof(T));
		}

		protected DbModel FindCache(string tablename, int pk)
		{
			if (m_modelCacheTime == 0) {
				return null;
			}
			if (!m_modelCache.ContainsKey(tablename)) {
				return null;
			}
			if (!m_modelCache[tablename].ContainsKey(pk)) {
				return null;
			}
			var cache = m_modelCache[tablename][pk];
			if (cache.TimePassed() > m_modelCacheTime) {
				m_modelCache[tablename].Remove(pk);
				return null;
			}
			return cache.m_model;
		}

		protected void AddCache(string tablename, int pk, DbModel model)
		{
			if (m_modelCacheTime == 0) {
				return;
			}
			if (!m_modelCache.ContainsKey(tablename)) {
				m_modelCache.Add(tablename, new Dictionary<int, DbCache>());
			}
			Utils.Assert(!m_modelCache[tablename].ContainsKey(pk));
			m_modelCache[tablename].Add(pk, new DbCache(model));
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
