using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nimania.Runtime
{
	public abstract class DbModel
	{
		public bool m_loaded = false;

		internal void LoadRow(Dictionary<string, string> row)
		{
			foreach (var columnName in row.Keys) {
				var field = GetType().GetField(columnName);
				if (field == null) {
					continue;
				}
				if (field.FieldType == typeof(int)) {
					field.SetValue(this, int.Parse(row[columnName]));
				} else if (field.FieldType == typeof(string)) {
					field.SetValue(this, row[columnName]);
				} else {
					throw new Exception("Unknown column/field type: " + field.FieldType.Name + " for column '" + columnName + "'");
				}
			}
			m_loaded = true;
		}

		public void Save()
		{
			//TODO
		}

		public void Insert()
		{
			//TODO
		}
	}
}
