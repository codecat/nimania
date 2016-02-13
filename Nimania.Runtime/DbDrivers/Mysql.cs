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
				return "\"" + Safe((string)o) + "\"";
			} else if (o is int) {
				return ((int)o).ToString();
			}
			throw new Exception("Unknown type to encode: " + o.GetType().Name);
		}

		public override T FindByPk<T>(int id)
		{
			return FindByAttributes<T>("ID", id);
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
			string primaryKey = PrimaryKey<T>();

			string query = QueryByAttributes<T>(attributes, null);
			var rows = Query(query);
			if (rows.Length != 1) {
				return default(T);
			}
			var newModel = (DbModel)(object)(T)Activator.CreateInstance(typeof(T));
			newModel.LoadRow(rows[0]);
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
				var newModel = (DbModel)(object)(T)Activator.CreateInstance(typeof(T));
				newModel.LoadRow(rows[0]);
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

			string query = "SELECT * FROM `" + tablename + "`" + QueryOptions(options);
			var rows = Query(query);
			var ret = new T[rows.Length];
			for (int i = 0; i < rows.Length; i++) {
				var newModel = (DbModel)(object)(T)Activator.CreateInstance(typeof(T));
				newModel.LoadRow(rows[i]);
				ret[i] = (T)(object)newModel;
			}
			return ret;
		}
	}
}
