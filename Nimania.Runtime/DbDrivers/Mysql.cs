using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace Nimania.Runtime.DbDrivers
{
	public class Mysql : DbDriver
	{
		private string m_connectionString;
		private MySqlConnection m_connection;

		Mutex m_threadLock = new Mutex();

		public Mysql(ConfigFile config)
		{
			m_connectionString = string.Format("Server={0};Database={1};User ID={2};Password={3};Pooling=false;CharSet=utf8;",
				config["Database.Hostname"], config["Database.Database"], config["Database.Username"], config["Database.Password"]);
			m_modelCacheTime = config.GetInt("Database.ModelCacheTime");
			Connect();
		}

		private void Connect()
		{
			m_connection = new MySqlConnection(m_connectionString);
			while (true) {
				try {
					m_connection.Open();
					break;
				} catch {
					Console.WriteLine("MySQL seems dead. Retrying..");
				}
			}
		}

		private Dictionary<string, string>[] Query(string qry)
		{
			m_threadLock.WaitOne();

			IDbCommand dbcmd = m_connection.CreateCommand();
			dbcmd.CommandText = qry;
			IDataReader dr = null;
			try {
				dr = dbcmd.ExecuteReader();
			} catch (Exception ex) {
				Console.WriteLine("\n********************************************\nQUERY ERROR:\n" +
					ex.Message + "\n\nQUERY WAS:\n" + qry + "\n********************************************");
			}

			if (dr == null && m_connection.State != ConnectionState.Connecting) {
				m_connection.Close();
				m_connection = new MySqlConnection(m_connectionString);
				while (true) {
					try {
						m_connection.Open();
						break;
					} catch { }
				}

				m_threadLock.ReleaseMutex();
				return Query(qry);
			}

			return Rows(dr);
		}

		private int QueryScalarInt(string qry)
		{
			m_threadLock.WaitOne();

			IDbCommand dbcmd = m_connection.CreateCommand();
			dbcmd.CommandText = qry;
			object ret = null;
			try {
				ret = dbcmd.ExecuteScalar();
			} catch (Exception ex) {
				Console.WriteLine("\n********************************************\nQUERY ERROR:\n" +
					ex.Message + "\n\nQUERY WAS:\n" + qry + "\n********************************************");
			}
			m_threadLock.ReleaseMutex();
			return Convert.ToInt32(ret);
		}

		private Dictionary<string, string>[] Rows(IDataReader rowdata)
		{
			var rows = new List<Dictionary<string, string>>();

			while (rowdata.Read()) {
				var nr = new Dictionary<string, string>();

				for (int i = 0; i < rowdata.FieldCount; i++)
					nr.Add(rowdata.GetName(i), rowdata.GetValue(i).ToString());

				rows.Add(nr);
			}

			rowdata.Close();
			rowdata.Dispose();
			rowdata = null;

			m_threadLock.ReleaseMutex();

			return rows.ToArray();
		}

		private string Safe(string str)
		{
			return str.Replace("\\", "\\\\").Replace("'", "\\'").Replace("`", "\\`");
		}

		private string Encode(object o)
		{
			if (o is string) {
				return "'" + Safe((string)o) + "'";
			} else if (o is int) {
				return ((int)o).ToString();
			} else if (o is DbModel) {
				var type = o.GetType();
				string primaryKey = PrimaryKey(type);
				var fieldPk = type.GetField(primaryKey);
				if (fieldPk == null) {
					throw new Exception("Field for primary key '" + primaryKey + "' does not exist in model " + type.Name);
				}
				return ((int)fieldPk.GetValue(o)).ToString();
			}
			throw new Exception("Unknown type to encode: " + o.GetType().Name);
		}

		public override T FindByPk<T>(int id)
		{
			string tablename = Tablename<T>();
			var cachedModel = FindCache(tablename, id);
			if (cachedModel != null) {
				return (T)(object)cachedModel;
			}

			return FindByAttributes<T>("ID", id);
		}

		public override DbModel FindByPk(int id, Type type)
		{
			string tablename = Tablename(type);
			var cachedModel = FindCache(tablename, id);
			if (cachedModel != null) {
				return cachedModel;
			}

			string primaryKey = PrimaryKey(type);

			string query = "SELECT * FROM `" + tablename + "` WHERE `" + primaryKey + "`=" + id;
			var rows = Query(query);
			if (rows.Length != 1) {
				return null;
			}
			var row = rows[0];
			DbModel ret = Create(type);
			ret.LoadRow(row);
			AddCache(tablename, id, ret);
			ret.LoadRelations(row);
			return ret;
		}

		private string QueryOptions(DbQueryOptions options)
		{
			string query = "";

			if (options != null) {
				if (options.m_sort) {
					query += " ORDER BY `" + options.m_sortKey + "`";
					switch (options.m_sortDir) {
						case DbSortDir.Ascending: query += " ASC"; break;
						case DbSortDir.Descending: query += " DESC"; break;
					}
				}

				if (options.m_range) {
					query += " LIMIT " + options.m_rangeFrom + "," + options.m_rangeTo;
				}
			}

			return query;
		}

		private string QueryByAttributes<T>(dynamic[] attributes, DbQueryOptions options)
		{
			if (attributes.Length % 2 != 0) {
				throw new Exception("Must be an even amount of attributes");
			}

			string tablename = Tablename<T>();

			string query = "SELECT * FROM `" + tablename + "` WHERE ";
			for (int i = 0; i < attributes.Length; i += 2) {
				if (i > 0) {
					query += " AND ";
				}
				query += attributes[i] + "=" + Encode(attributes[i + 1]);
			}

			return query + QueryOptions(options);
		}

		public override T FindByAttributes<T>(params dynamic[] attributes)
		{
			string tablename = Tablename<T>();

			string query = QueryByAttributes<T>(attributes, null);
			var rows = Query(query);
			if (rows.Length != 1) {
				return default(T);
			}
			var row = rows[0];

			string primaryKey = PrimaryKey<T>();
			int pk = int.Parse(row[primaryKey]);
			var cachedModel = FindCache(tablename, pk);
			if (cachedModel != null) {
				return (T)(object)cachedModel;
			}

			var newModel = (DbModel)(object)Create<T>();
			newModel.LoadRow(row);
			AddCache(tablename, pk, newModel);
			newModel.LoadRelations(row);
			return (T)(object)newModel;
		}

		public override T[] FindAllByAttributes<T>(params dynamic[] attributes)
		{
			return FindAllByAttributes<T>(attributes, null);
		}

		public override T[] FindAllByAttributes<T>(dynamic[] attributes, DbQueryOptions options)
		{
			string tablename = Tablename<T>();
			string primaryKey = PrimaryKey<T>();

			string query = QueryByAttributes<T>(attributes, options);
			var rows = Query(query);
			var ret = new T[rows.Length];
			for (int i = 0; i < rows.Length; i++) {
				var row = rows[i];

				int pk = int.Parse(row[primaryKey]);
				var cachedModel = FindCache(tablename, pk);
				if (cachedModel != null) {
					ret[i] = (T)(object)cachedModel;
					continue;
				}

				var newModel = (DbModel)(object)Create<T>();
				newModel.LoadRow(row);
				AddCache(tablename, pk, newModel);
				newModel.LoadRelations(row);
				ret[i] = (T)(object)newModel;
			}
			return ret;
		}

		public override T[] FindAll<T>()
		{
			return FindAll<T>(null);
		}

		public override T[] FindAll<T>(DbQueryOptions options)
		{
			string tablename = Tablename<T>();
			string primaryKey = PrimaryKey<T>();

			string query = "SELECT * FROM `" + tablename + "`" + QueryOptions(options);
			var rows = Query(query);
			var ret = new T[rows.Length];
			for (int i = 0; i < rows.Length; i++) {
				var row = rows[i];

				int pk = int.Parse(row[primaryKey]);
				var cachedModel = FindCache(tablename, pk);
				if (cachedModel != null) {
					ret[i] = (T)(object)cachedModel;
					continue;
				}

				var newModel = (DbModel)(object)Create<T>();
				newModel.LoadRow(row);
				AddCache(tablename, pk, newModel);
				newModel.LoadRelations(row);
				ret[i] = (T)(object)newModel;
			}
			return ret;
		}

		public override void Save(DbModel model)
		{
			if (!model.m_loaded) {
				Insert(model);
				return;
			}

			var dirtyKeys = model.DirtyKeys();
			if (dirtyKeys.Length == 0) {
				// nothing to do here..
				return;
			}

			var type = model.GetType();
			var tableName = Tablename(type);
			var primaryKey = PrimaryKey(type);

			string query = "UPDATE `" + tableName + "` SET ";
			for (int i = 0; i < dirtyKeys.Length; i++) {
				if (i > 0) {
					query += ",";
				}
				var field = type.GetField(dirtyKeys[i]);
				query += "`" + dirtyKeys[i] + "`=" + Encode(field.GetValue(model));
			}

			var fieldPk = type.GetField(primaryKey);
			query += " WHERE `" + primaryKey + "`=" + (int)fieldPk.GetValue(model);

			Query(query);
			model.ResetDirty();
		}

		public override void Insert(DbModel model)
		{
			var type = model.GetType();
			var tableName = Tablename(type);
			var primaryKey = PrimaryKey(type);

			string query = "INSERT INTO `" + tableName + "` (";
			string queryValues = "";

			var fields = type.GetFields();
			var i = 0;
			foreach (var field in fields) {
				if (field.Name.StartsWith("m_")) {
					continue;
				}
				if (field.Name == primaryKey) {
					continue;
				}
				object v = field.GetValue(model);
				if (v != null) {
					if (i > 0) {
						query += ",";
						queryValues += ",";
					}

					query += "`" + field.Name + "`";
					queryValues += Encode(v);
					i++;
				}

				model.m_originalData[field.Name] = v;
			}

			query += ") VALUES(" + queryValues + ");SELECT LAST_INSERT_ID();";

			int newPk = QueryScalarInt(query);

			AddCache(tableName, newPk, model);

			var fieldPk = type.GetField(primaryKey);
			if (fieldPk != null) {
				fieldPk.SetValue(model, newPk);
			}

			model.m_loaded = true;
		}
	}
}
