using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Nimania.Runtime
{
	public abstract class DbModel
	{
		internal DbDriver m_database;
		internal Dictionary<string, object> m_originalData = new Dictionary<string, object>();

		public bool m_loaded = false;
		public bool m_loadedRelations = false;

		internal void LoadRow(Dictionary<string, string> row)
		{
			m_originalData.Clear();
			var type = GetType().GetTypeInfo();
			foreach (var columnName in row.Keys) {
				var field = type.GetDeclaredField(columnName);
				if (field == null) {
					continue;
				}
				object v = null;
				if (field.FieldType == typeof(int)) {
					v = int.Parse(row[columnName]);
				} else if (field.FieldType == typeof(string)) {
					v = row[columnName];
				} else if (field.FieldType == typeof(bool)) {
					v = (row[columnName] != "0");
				} else {
					TypeInfo ti = field.FieldType.GetTypeInfo();
					if (ti.BaseType != typeof(DbModel)) {
						throw new Exception("Unknown column/field type: " + field.FieldType.Name + " for column '" + columnName + "'");
					}
				}
				field.SetValue(this, v);
				m_originalData[columnName] = v;
			}
			m_loaded = true;
		}

		internal void LoadRelations(Dictionary<string, string> row)
		{
			var type = GetType();
			foreach (var columnName in row.Keys) {
				var field = type.GetField(columnName);
				if (field == null) {
					continue;
				}
				var ti = field.FieldType.GetTypeInfo();
				if (ti.BaseType != typeof(DbModel)) {
					continue;
				}
				var model = m_database.FindByPk(int.Parse(row[columnName]), field.FieldType);
				field.SetValue(this, model);
				m_originalData[columnName] = model;
			}
			m_loadedRelations = true;
		}

		internal void ResetDirty()
		{
			var type = GetType();
			var keys = m_originalData.Keys.ToArray();
			foreach (var key in keys) {
				var field = type.GetField(key);
				m_originalData[key] = field.GetValue(this);
			}
		}

		internal string[] DirtyKeys()
		{
			var type = GetType();
			var ret = new List<string>();
			foreach (var key in m_originalData.Keys) {
				var field = type.GetField(key);
				if (m_originalData[key] == null) {
					if (field.GetValue(this) != null) {
						ret.Add(key);
					}
				} else {
					if (!m_originalData[key].Equals(field.GetValue(this))) {
						ret.Add(key);
					}
				}
			}
			return ret.ToArray();
		}

		public bool IsDirty()
		{
			if (!m_loaded) {
				return true;
			}
			var type = GetType();
			foreach (var key in m_originalData.Keys) {
				var field = type.GetField(key);
				if (m_originalData[key] == null) {
					if (field.GetValue(this) != null) {
						return true;
					}
				} else {
					if (!m_originalData[key].Equals(field.GetValue(this))) {
						return true;
					}
				}
			}
			return false;
		}

		public virtual void Save() { m_database.Save(this); }
		public virtual void Insert() { m_database.Insert(this); }
	}
}
