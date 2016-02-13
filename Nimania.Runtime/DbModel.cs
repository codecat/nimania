﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nimania.Runtime
{
	public abstract class DbModel
	{
		internal DbDriver m_database;
		internal Dictionary<string, object> m_originalData = new Dictionary<string, object>();

		public bool m_loaded = false;

		internal void LoadRow(Dictionary<string, string> row)
		{
			m_originalData.Clear();
			foreach (var columnName in row.Keys) {
				var field = GetType().GetField(columnName);
				if (field == null) {
					continue;
				}
				object v = null;
				if (field.FieldType == typeof(int)) {
					v = int.Parse(row[columnName]);
				} else if (field.FieldType == typeof(string)) {
					v = row[columnName];
				} else {
					throw new Exception("Unknown column/field type: " + field.FieldType.Name + " for column '" + columnName + "'");
				}
				field.SetValue(this, v);
				m_originalData[columnName] = v;
			}
			m_loaded = true;
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
				if (!m_originalData[key].Equals(field.GetValue(this))) {
					ret.Add(key);
				}
			}
			return ret.ToArray();
		}

		public bool IsDirty()
		{
			var type = GetType();
			foreach (var key in m_originalData.Keys) {
				var field = type.GetField(key);
				if (!m_originalData[key].Equals(field.GetValue(this))) {
					return true;
				}
			}
			return false;
		}

		public virtual void Save() { m_database.Save(this); }
		public virtual void Insert() { m_database.Insert(this); }
	}
}
