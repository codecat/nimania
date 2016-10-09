using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using NLog;

namespace Nimania.Runtime.DbDrivers
{
	public class Mysql : DbDriver
	{
		private static Logger m_logger = LogManager.GetCurrentClassLogger();

		private string m_connectionString;
		private MySqlConnection m_connection;

		private List<Type> m_existingModelTables = new List<Type>();

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
					m_logger.Error("MySQL seems dead. Retrying..");
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
				m_logger.Error("Query error: \"{0}\", query was: \"{1}\"", ex.Message, qry);
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
				m_logger.Error("Query error: \"{0}\", query was: \"{1}\"", ex.Message, qry);
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

		private string FieldInfoQuery(FieldInfo fi)
		{
			string ret = "`" + fi.Name + "` ";

			int customLength = -1;
			DbFieldLengthAttribute attrib = fi.GetCustomAttribute<DbFieldLengthAttribute>();
			if (attrib != null) {
				customLength = attrib.m_length;
			}

			string colType = "";
			int colTypeLen = 0;
			string colParams = "";

			if (fi.FieldType == typeof(int) || fi.FieldType == typeof(bool)) {
				colType = "int";
				colTypeLen = 11;

			} else if (fi.FieldType == typeof(string)) {
				colType = "varchar";
				colTypeLen = 255;
				colParams = "CHARACTER SET utf8 COLLATE utf8_general_ci";

			} else if (fi.FieldType.BaseType == typeof(DbModel)) {
				// This is a relational type
				colType = "int";
				colTypeLen = 11;
			}

			if (colType == "") {
				m_logger.Error("Couldn't figure out database column type for field '{0}' with type '{1}' for table creation query!", fi.Name, fi.FieldType.FullName);
				return "";
			}
			if (customLength != -1) {
				colTypeLen = customLength;
			}

			ret += colType + "(" + colTypeLen + ") " + colParams + " NOT NULL";
			if (fi.Name == "ID") {
				ret += " AUTO_INCREMENT";
			}

			return ret;
		}

		private string ModelCreateQuery(Type type)
		{
			string tableName = Tablename(type);

			FieldInfo[] fields = type.GetFields();
			bool hasID = false;

			string ret = "CREATE TABLE `" + tableName + "` (";

			foreach (FieldInfo fi in fields) {
				if (!char.IsUpper(fi.Name[0]) || fi.Name.StartsWith("m_")) {
					continue;
				}

				if (fi.FieldType.BaseType == typeof(DbModel)) {
					EnsureExists(fi.FieldType);
				}

				string fiq = FieldInfoQuery(fi);
				if (fiq != "") {
					ret += fiq + ", ";
					if (fi.Name == "ID") {
						hasID = true;
					}
				}
			}

			if (hasID) {
				ret += "PRIMARY KEY (`ID`)";
			}
			ret += ") ENGINE=InnoDB DEFAULT CHARACTER SET=utf8 COLLATE=utf8_general_ci";
			if (hasID) {
				ret += " AUTO_INCREMENT=1";
			}
			ret += " ROW_FORMAT=COMPACT;";

			return ret;
		}

		private void EnsureExists<T>()
		{
			EnsureExists(typeof(T));
		}

		private void EnsureExists(Type type)
		{
			if (m_existingModelTables.Contains(type)) {
				return;
			}
			m_existingModelTables.Add(type);

			string tableName = Tablename(type);

			var rows = Query("SHOW TABLES");
			bool found = false;
			foreach (var row in rows) {
				if (row.Values.ElementAt(0) == tableName) {
					found = true;
					break;
				}
			}

			if (!found) {
				m_logger.Info("Creating table '{0}'", tableName);
				Query(ModelCreateQuery(type));
				return;
			}

			List<FieldInfo> addedFields = new List<FieldInfo>();
			List<string> removedFields = new List<string>();

			var columns = Query("EXPLAIN `" + tableName + "`");
			var classFields = type.GetFields();

			foreach (var col in columns) {
				removedFields.Add(col["Field"]);
			}

			foreach (var fi in classFields) {
				if (!char.IsUpper(fi.Name[0]) || fi.Name.StartsWith("m_")) {
					continue;
				}
				addedFields.Add(fi);
				removedFields.Remove(fi.Name);
			}

			foreach (var col in columns) {
				int i = addedFields.FindIndex((fi) => fi.Name == col["Field"]);
				if (i != -1) {
					addedFields.RemoveAt(i);
				}
			}

			if (addedFields.Count == 0 && removedFields.Count == 0) {
				return;
			}

			m_logger.Info("Updating table '{0}' (+{1} -{2})", tableName, addedFields.Count, removedFields.Count);

			string alterQuery = "ALTER TABLE `" + tableName + "` ";

			bool comma = false;

			foreach (var fi in removedFields) {
				if (comma) {
					alterQuery += ",";
				}
				alterQuery += "DROP COLUMN `" + fi + "`";
				comma = true;
			}

			foreach (var fi in addedFields) {
				if (comma) {
					alterQuery += ",";
				}
				alterQuery += "ADD COLUMN " + FieldInfoQuery(fi);
				comma = true;
			}

			Query(alterQuery);
		}

		public override T FindByPk<T>(int id)
		{
			EnsureExists<T>();

			string tablename = Tablename<T>();
			var cachedModel = FindCache(tablename, id);
			if (cachedModel != null) {
				return (T)(object)cachedModel;
			}

			return FindByAttributes<T>("ID", id);
		}

		public override DbModel FindByPk(int id, Type type)
		{
			EnsureExists(type);

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
			EnsureExists<T>();

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
			EnsureExists<T>();

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
			EnsureExists<T>();

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
			EnsureExists(model.GetType());

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
			EnsureExists(model.GetType());

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
