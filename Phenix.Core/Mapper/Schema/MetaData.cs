using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using Phenix.Core.Data;
using Phenix.Core.Data.Common;
using Phenix.Core.Log;
using Phenix.Core.SyncCollections;

namespace Phenix.Core.Mapper.Schema
{
    /// <summary>
    /// 元数据
    /// </summary>
    [Serializable]
    public sealed class MetaData
    {
        [Newtonsoft.Json.JsonConstructor]
        private MetaData(IDictionary<string, Table> tables, IDictionary<string, View> views)
        {
            _tables = new ReadOnlyDictionary<string, Table>(new Dictionary<string, Table>(tables, StringComparer.OrdinalIgnoreCase));
            foreach (KeyValuePair<string, Table> kvp in tables)
                kvp.Value.Owner = this;

            _views = new ReadOnlyDictionary<string, View>(new Dictionary<string, View>(views, StringComparer.OrdinalIgnoreCase));
            foreach (KeyValuePair<string, View> kvp in views)
                kvp.Value.Owner = this;
        }

        private MetaData(Database database)
        {
            _database = database;
        }

        #region 工厂

        private static MetaData _default;

        /// <summary>
        /// 缺省元数据入口
        /// </summary>
        public static MetaData Default
        {
            get { return _default ??= Fetch(); }
        }

        private static readonly SynchronizedDictionary<Database, MetaData> _cache = new SynchronizedDictionary<Database, MetaData>();

        /// <summary>
        /// 获取
        /// </summary>
        /// <param name="database">Database</param>
        /// <returns>MetaData</returns>
        public static MetaData Fetch(Database database = null)
        {
            return _cache.GetValue(database ?? Database.Default, () => new MetaData(database));
        }

        #endregion

        #region 属性

        [NonSerialized]
        private readonly Database _database;

        /// <summary>
        /// 数据库入口
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Database Database
        {
            get { return _database; }
        }

        private readonly object _lock = new object();

        private IDictionary<string, Table> _tables;

        /// <summary>
        /// 表清单
        /// </summary>
        public IDictionary<string, Table> Tables
        {
            get
            {
                if (_tables == null && Database != null)
                    lock (_lock)
                        if (_tables == null && Database != null)
                        {
                            _tables = Database.ExecuteGet(LoadTables);
                        }

                return _tables;
            }
        }

        private IDictionary<string, View> _views;

        /// <summary>
        /// 视图清单
        /// </summary>
        public IDictionary<string, View> Views
        {
            get
            {
                if (_views == null && Database != null)
                    lock (_lock)
                        if (_views == null && Database != null)
                        {
                            _views = Database.ExecuteGet(LoadViews);
                        }

                return _views;
            }
        }

        [NonSerialized]
        private IDictionary<Table, string[]> _primaryKeys;

        internal IDictionary<Table, string[]> PrimaryKeys
        {
            get
            {
                if (_primaryKeys == null && Database != null)
                    lock (_lock)
                        if (_primaryKeys == null && Database != null)
                        {
                            _primaryKeys = Database.ExecuteGet(LoadPrimaryKeys);
                        }

                return _primaryKeys;
            }
        }

        [NonSerialized]
        private IDictionary<Table, IDictionary<string, ForeignKey>> _foreignKeys;

        internal IDictionary<Table, IDictionary<string, ForeignKey>> ForeignKeys
        {
            get
            {
                if (_foreignKeys == null && Database != null)
                    lock (_lock)
                        if (_foreignKeys == null && Database != null)
                        {
                            _foreignKeys = Database.ExecuteGet(LoadForeignKeys);
                        }

                return _foreignKeys;
            }
        }

        [NonSerialized]
        private IDictionary<Table, IDictionary<string, ForeignKey>> _detailForeignKeys;

        internal IDictionary<Table, IDictionary<string, ForeignKey>> DetailForeignKeys
        {
            get
            {
                if (_detailForeignKeys == null && Database != null)
                    lock (_lock)
                        if (_detailForeignKeys == null && Database != null)
                        {
                            _detailForeignKeys = Database.ExecuteGet(LoadDetailForeignKeys);
                        }

                return _detailForeignKeys;
            }
        }

        [NonSerialized]
        private IDictionary<Table, IDictionary<string, Index>> _indexes;

        internal IDictionary<Table, IDictionary<string, Index>> Indexes
        {
            get
            {
                if (_indexes == null && Database != null)
                    lock (_lock)
                        if (_indexes == null && Database != null)
                        {
                            _indexes = Database.ExecuteGet(LoadIndexes);
                        }

                return _indexes;
            }
        }

        private readonly SynchronizedDictionary<string, Table> _classTableCache = new SynchronizedDictionary<string, Table>(StringComparer.Ordinal);
        private readonly SynchronizedDictionary<string, View> _classViewCache = new SynchronizedDictionary<string, View>(StringComparer.Ordinal);

        #endregion

        #region 方法

        /// <summary>
        /// 刷新缓存
        /// </summary>
        public void ClearTableCache()
        {
            lock (_lock)
            {
                _tables = null;
                _primaryKeys = null;
                _foreignKeys = null;
                _detailForeignKeys = null;
                _indexes = null;
                _classTableCache.Clear();
            }
        }

        /// <summary>
        /// 刷新缓存
        /// </summary>
        public void ClearViewCache()
        {
            lock (_lock)
            {
                _views = null;
                _classViewCache.Clear();
            }
        }

        /// <summary>
        /// 填充缓存
        /// </summary>
        public void FillingCache()
        {
            IDictionary<string, Table> tables = Tables;
            IDictionary<string, View> views = Views;
            IDictionary<Table, string[]> primaryKeys = PrimaryKeys;
            IDictionary<Table, IDictionary<string, ForeignKey>> foreignKeys = ForeignKeys;
            IDictionary<Table, IDictionary<string, ForeignKey>> detailForeignKeys = DetailForeignKeys;
            IDictionary<Table, IDictionary<string, Index>> indexes = Indexes;
        }

        private IDictionary<string, Table> LoadTables(DbConnection connection)
        {
            Dictionary<string, Table> result = new Dictionary<string, Table>(StringComparer.OrdinalIgnoreCase);
#if PgSQL
            using (DataReader reader = new DataReader(connection, @"
SELECT utd.description table_comments, ut.relname table_name, utcd.description column_comments, utc.column_name column_name,
  utc.is_nullable nullable, utc.udt_name data_type, utc.column_default data_default, utc.character_maximum_length data_length,
  utc.numeric_precision data_precision, utc.numeric_scale data_scale
FROM pg_class ut LEFT OUTER JOIN pg_description utd ON utd.objsubid = 0 AND ut.oid = utd.objoid,
  information_schema.columns utc left join pg_description utcd on utc.table_name::regclass = utcd.objoid and utc.ordinal_position = utcd.objsubid
WHERE UPPER(utc.table_catalog) = UPPER(@table_catalog)
  AND ut.relnamespace = (SELECT oid FROM pg_namespace WHERE nspname = 'public') AND utc.table_schema = 'public'
  AND ut.relkind = 'r'
  AND ut.relname = utc.table_name
ORDER BY ut.relname, utc.ordinal_position",
                       CommandBehavior.SingleResult))
            {
                reader.CreateParameter("@table_catalog", Database.DatabaseName);
                Table table = null;
                Dictionary<string, Column> columns = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(1), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        table.Columns = columns;
                        result.Add(table.Name, table);
                        table = null;
                    }

                    if (table == null)
                    {
                        table = new Table(this, reader.GetString(1), reader.GetNullableString(0));
                        columns = new Dictionary<string, Column>(StringComparer.OrdinalIgnoreCase);
                    }

                    columns.Add(reader.GetString(3), new Column(table, reader.GetString(3), reader.GetNullableString(2),
                        String.Compare(reader.GetNullableString(4), "YES", StringComparison.OrdinalIgnoreCase) == 0,
                        reader.GetNullableString(5), reader.GetNullableString(6), reader.GetNullableInt64(7),
                        reader.GetNullableInt32(8), reader.GetNullableInt32(9)));
                }

                if (table != null)
                {
                    table.Columns = columns;
                    result.Add(table.Name, table);
                }
            }
#endif
#if MsSQL
            using (DataReader reader = new DataReader(connection, @"
SELECT ds.value table_comments, obj.name table_name, ep.[value] column_comments, col.name column_name,
  CASE WHEN col.isnullable = 1 THEN 'Y' ELSE 'N' END nullable, t.name data_type, comm.text data_default, col.length data_length,
  COLUMNPROPERTY(col.id, col.name, 'PRECISION') data_precision, ISNULL(COLUMNPROPERTY(col.id, col.name, 'Scale'), 0) data_scale
FROM dbo.syscolumns col
  LEFT JOIN dbo.systypes t ON col.xtype = t.xusertype
  INNER JOIN dbo.sysobjects obj ON col.id = obj.id AND obj.xtype = 'U' AND obj.status >= 0
  LEFT JOIN sys.extended_properties ds ON ds.major_id = obj.id AND ds.minor_id = 0
  LEFT JOIN dbo.syscomments comm ON col.cdefault = comm.id
  LEFT JOIN sys.extended_properties ep ON col.id = ep.major_id AND col.colid = ep.minor_id AND ep.name = 'MS_Description'
  LEFT JOIN sys.extended_properties epTwo ON obj.id = epTwo.major_id AND epTwo.minor_id = 0 AND epTwo.name = 'MS_Description'
ORDER BY obj.name, col.colorder",
                       CommandBehavior.SingleResult))
            {
                Table table = null;
                Dictionary<string, Column> columns = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(1), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        table.Columns = columns;
                        result.Add(table.Name, table);
                        table = null;
                    }

                    if (table == null)
                    {
                        table = new Table(this, reader.GetString(1), reader.GetNullableString(0));
                        columns = new Dictionary<string, Column>(StringComparer.OrdinalIgnoreCase);
                    }

                    columns.Add(reader.GetString(3), new Column(table, reader.GetString(3), reader.GetNullableString(2),
                        String.Compare(reader.GetNullableString(4), "Y", StringComparison.OrdinalIgnoreCase) == 0,
                        reader.GetNullableString(5), reader.GetNullableString(6), reader.GetNullableInt64(7),
                        reader.GetNullableInt32(8), reader.GetNullableInt32(9)));
                }

                if (table != null)
                {
                    table.Columns = columns;
                    result.Add(table.Name, table);
                }
            }
#endif
#if MySQL
            using (DataReader reader = new DataReader(connection, @"
SELECT ut.table_comment table_comments, ut.table_name table_name, utc.column_comment column_comments, utc.column_name column_name,
  utc.is_nullable nullable, utc.data_type data_type, utc.column_default data_default, utc.character_maximum_length data_length,
  utc.numeric_precision data_precision, utc.numeric_scale data_scale
FROM information_schema.tables ut, information_schema.columns utc
WHERE UPPER(ut.table_schema) = UPPER(?table_schema) AND ut.table_schema = utc.table_schema
  AND ut.table_type = 'BASE TABLE'  
  AND ut.table_name = utc.table_name
ORDER BY ut.table_name, utc.ordinal_position",
                       CommandBehavior.SingleResult))
            {
                reader.CreateParameter("?table_schema", Database.DatabaseName);
                Table table = null;
                Dictionary<string, Column> columns = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(1), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        table.Columns = columns;
                        result.Add(table.Name, table);
                        table = null;
                    }

                    if (table == null)
                    {
                        table = new Table(this, reader.GetString(1), reader.GetNullableString(0));
                        columns = new Dictionary<string, Column>(StringComparer.OrdinalIgnoreCase);
                    }

                    columns.Add(reader.GetString(3), new Column(table, reader.GetString(3), reader.GetNullableString(2),
                        String.Compare(reader.GetNullableString(4), "YES", StringComparison.OrdinalIgnoreCase) == 0,
                        reader.GetNullableString(5), reader.GetNullableString(6), reader.GetNullableInt64(7),
                        reader.GetNullableInt32(8), reader.GetNullableInt32(9)));
                }

                if (table != null)
                {
                    table.Columns = columns;
                    result.Add(table.Name, table);
                }
            }
#endif
#if ORA
            using (DataReader reader = new DataReader(connection, @"
select utm.comments table_comments, ut.table_name table_name, ucm.comments column_comments, utc.column_name column_name,
  utc.nullable nullable, utc.data_type data_type, utc.data_default data_default, utc.data_length data_length,
  utc.data_precision data_precision, utc.data_scale data_scale
from user_tables ut, user_tab_comments utm, user_tab_columns utc, user_col_comments ucm
where ut.table_name = utm.table_name and ut.table_name = utc.table_name and ut.table_name = ucm.table_name
  and utc.column_name = ucm.column_name
order by ut.table_name, utc.column_id",
                       CommandBehavior.SingleResult))
            {
                Table table = null;
                Dictionary<string, Column> columns = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(1), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        table.Columns = columns;
                        result.Add(table.Name, table);
                        table = null;
                    }

                    if (table == null)
                    {
                        table = new Table(this, reader.GetString(1), reader.GetNullableString(0));
                        columns = new Dictionary<string, Column>(StringComparer.OrdinalIgnoreCase);
                    }

                    columns.Add(reader.GetString(3), new Column(table, reader.GetString(3), reader.GetNullableString(2),
                        String.Compare(reader.GetNullableString(4), "Y", StringComparison.OrdinalIgnoreCase) == 0,
                        reader.GetNullableString(5), reader.GetNullableString(6), reader.GetNullableInt64(7),
                        reader.GetNullableInt32(8), reader.GetNullableInt32(9)));
                }

                if (table != null)
                {
                    table.Columns = columns;
                    result.Add(table.Name, table);
                }
            }
#endif
            return new ReadOnlyDictionary<string, Table>(result);
        }

        private IDictionary<string, View> LoadViews(DbConnection connection)
        {
            Dictionary<string, View> result = new Dictionary<string, View>(StringComparer.OrdinalIgnoreCase);
#if PgSQL
            using (DataReader reader = new DataReader(connection, @"
SELECT utd.description view_comments, ut.relname view_name, utcd.description column_comments, utc.column_name column_name,
  utc.is_nullable nullable, utc.udt_name data_type, utc.column_default data_default, utc.character_maximum_length data_length,
  utc.numeric_precision data_precision, utc.numeric_scale data_scale, uv.definition definition
FROM pg_class ut LEFT OUTER JOIN pg_description utd ON utd.objsubid = 0 AND ut.oid = utd.objoid,
  information_schema.columns utc left join pg_description utcd on utc.table_name::regclass = utcd.objoid and utc.ordinal_position = utcd.objsubid,
  pg_views uv
WHERE UPPER(utc.table_catalog) = UPPER(@table_catalog)
  AND ut.relnamespace = (SELECT oid FROM pg_namespace WHERE nspname = 'public') AND utc.table_schema = 'public' AND utc.table_schema = uv.schemaname
  AND ut.relkind = 'v'
  AND ut.relname = utc.table_name AND ut.relname = uv.viewname  
ORDER BY ut.relname, utc.ordinal_position",
                       CommandBehavior.SingleResult))
            {
                reader.CreateParameter("@table_catalog", Database.DatabaseName);
                View view = null;
                Dictionary<string, Column> columns = null;
                while (reader.Read())
                {
                    if (view != null && String.Compare(view.Name, reader.GetString(1), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        view.Columns = columns;
                        result.Add(view.Name, view);
                        view = null;
                    }

                    if (view == null)
                    {
                        view = new View(this, reader.GetString(1), reader.GetNullableString(0), reader.GetString(10));
                        columns = new Dictionary<string, Column>(StringComparer.OrdinalIgnoreCase);
                    }

                    columns.Add(reader.GetString(3), new Column(view, reader.GetString(3), reader.GetNullableString(2),
                        String.Compare(reader.GetNullableString(4), "YES", StringComparison.OrdinalIgnoreCase) == 0,
                        reader.GetNullableString(5), reader.GetNullableString(6), reader.GetNullableInt64(7),
                        reader.GetNullableInt32(8), reader.GetNullableInt32(9)));
                }

                if (view != null)
                {
                    view.Columns = columns;
                    result.Add(view.Name, view);
                }
            }
#endif
#if MsSQL
            using (DataReader reader = new DataReader(connection, @"
SELECT ds.value view_comments, obj.name view_name, ep.[value] column_comments, col.name column_name,
  CASE WHEN col.isnullable = 1 THEN 'Y' ELSE 'N' END nullable, t.name data_type, comm.text data_default, col.length data_length,
  COLUMNPROPERTY(col.id, col.name, 'PRECISION') data_precision, ISNULL(COLUMNPROPERTY(col.id, col.name, 'Scale'), 0) data_scale, commv.text definition
FROM dbo.syscolumns col
  LEFT JOIN dbo.systypes t ON col.xtype = t.xusertype
  INNER JOIN dbo.sysobjects obj ON col.id = obj.id AND obj.xtype = 'V' AND obj.status >= 0
  LEFT JOIN sys.extended_properties ds ON ds.major_id = obj.id AND ds.minor_id = 0
  LEFT JOIN dbo.syscomments comm ON col.cdefault = comm.id
  LEFT JOIN dbo.syscomments commv ON obj.id = commv.id
  LEFT JOIN sys.extended_properties ep ON col.id = ep.major_id AND col.colid = ep.minor_id AND ep.name = 'MS_Description'
  LEFT JOIN sys.extended_properties epTwo ON obj.id = epTwo.major_id AND epTwo.minor_id = 0 AND epTwo.name = 'MS_Description'
ORDER BY obj.name, col.colorder", 
                       CommandBehavior.SingleResult))
            {
                View view = null;
                Dictionary<string, Column> columns = null;
                while (reader.Read())
                {
                    if (view != null && String.Compare(view.Name, reader.GetString(1), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        view.Columns = columns;
                        result.Add(view.Name, view);
                        view = null;
                    }

                    if (view == null)
                    {
                        view = new View(this, reader.GetString(1), reader.GetNullableString(0), reader.GetString(10));
                        columns = new Dictionary<string, Column>(StringComparer.OrdinalIgnoreCase);
                    }

                    columns.Add(reader.GetString(3), new Column(view, reader.GetString(3), reader.GetNullableString(2),
                        String.Compare(reader.GetNullableString(4), "Y", StringComparison.OrdinalIgnoreCase) == 0,
                        reader.GetNullableString(5), reader.GetNullableString(6), reader.GetNullableInt64(7),
                        reader.GetNullableInt32(8), reader.GetNullableInt32(9)));
                }

                if (view != null)
                {
                    view.Columns = columns;
                    result.Add(view.Name, view);
                }
            }
#endif
#if MySQL
            using (DataReader reader = new DataReader(connection, @"
SELECT ut.table_comment view_comments, ut.table_name view_name, utc.column_comment column_comments, utc.column_name column_name,
  utc.is_nullable nullable, utc.data_type data_type, utc.column_default data_default, utc.character_maximum_length data_length,
  utc.numeric_precision data_precision, utc.numeric_scale data_scale, uv.view_definition definition
FROM information_schema.tables ut, information_schema.columns utc, information_schema.views uv
WHERE UPPER(ut.table_schema) = UPPER(?table_schema) AND ut.table_schema = utc.table_schema AND ut.table_schema = uv.table_schema
  AND ut.table_type = 'VIEW'  
  AND ut.table_name = utc.table_name AND ut.table_name = uv.table_name
ORDER BY ut.table_name, utc.ordinal_position",
                       CommandBehavior.SingleResult))
            {
                reader.CreateParameter("?table_schema", Database.DatabaseName);
                View view = null;
                Dictionary<string, Column> columns = null;
                while (reader.Read())
                {
                    if (view != null && String.Compare(view.Name, reader.GetString(1), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        view.Columns = columns;
                        result.Add(view.Name, view);
                        view = null;
                    }

                    if (view == null)
                    {
                        view = new View(this, reader.GetString(1), reader.GetNullableString(0), reader.GetString(10));
                        columns = new Dictionary<string, Column>(StringComparer.OrdinalIgnoreCase);
                    }

                    columns.Add(reader.GetString(3), new Column(view, reader.GetString(3), reader.GetNullableString(2),
                        String.Compare(reader.GetNullableString(4), "YES", StringComparison.OrdinalIgnoreCase) == 0,
                        reader.GetNullableString(5), reader.GetNullableString(6), reader.GetNullableInt64(7),
                        reader.GetNullableInt32(8), reader.GetNullableInt32(9)));
                }

                if (view != null)
                {
                    view.Columns = columns;
                    result.Add(view.Name, view);
                }
            }
#endif
#if ORA
            using (DataReader reader = new DataReader(connection, @"
select utm.comments view_comments, uv.view_name view_name, ucm.comments column_comments, utc.column_name column_name,
  utc.nullable nullable, utc.data_type data_type, utc.data_default data_default, utc.data_length data_length,
  utc.data_precision data_precision, utc.data_scale data_scale, uv.text definition
from user_views uv, user_tab_comments utm, user_tab_columns utc, user_col_comments ucm
where uv.view_name = utm.table_name and uv.view_name = utc.table_name and uv.view_name = ucm.table_name
  and utc.column_name = ucm.column_name
order by uv.view_name, utc.column_id",
                       CommandBehavior.SingleResult))
            {
                View view = null;
                Dictionary<string, Column> columns = null;
                while (reader.Read())
                {
                    if (view != null && String.Compare(view.Name, reader.GetString(1), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        view.Columns = columns;
                        result.Add(view.Name, view);
                        view = null;
                    }

                    if (view == null)
                    {
                        view = new View(this, reader.GetString(1), reader.GetNullableString(0), reader.GetString(10));
                        columns = new Dictionary<string, Column>(StringComparer.OrdinalIgnoreCase);
                    }

                    columns.Add(reader.GetString(3), new Column(view, reader.GetString(3), reader.GetNullableString(2),
                        String.Compare(reader.GetNullableString(4), "Y", StringComparison.OrdinalIgnoreCase) == 0,
                        reader.GetNullableString(5), reader.GetNullableString(6), reader.GetNullableInt64(7),
                        reader.GetNullableInt32(8), reader.GetNullableInt32(9)));
                }

                if (view != null)
                {
                    view.Columns = columns;
                    result.Add(view.Name, view);
                }
            }
#endif
            return new ReadOnlyDictionary<string, View>(result);
        }

        private IDictionary<Table, string[]> LoadPrimaryKeys(DbConnection connection)
        {
            Dictionary<Table, string[]> result = new Dictionary<Table, string[]>(Tables.Count);
#if PgSQL
            using (DataReader reader = new DataReader(connection, @"
SELECT ucc.table_name table_name, ucf.column_name column_name
FROM information_schema.table_constraints ucc
  JOIN information_schema.key_column_usage ucf ON ucf.constraint_name = ucc.constraint_name
WHERE UPPER(ucc.table_catalog) = UPPER(@table_catalog) AND ucc.table_catalog = ucf.table_catalog
  AND ucc.table_schema = 'public' AND ucc.table_schema = ucf.table_schema
  AND ucc.constraint_type = 'PRIMARY KEY'
ORDER BY ucc.table_name",
                       CommandBehavior.SingleResult))
            {
                reader.CreateParameter("@table_catalog", Database.DatabaseName);
                Table table = null;
                List<string> columnNames = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(0), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        result.Add(table, columnNames.ToArray());
                        table = null;
                    }

                    if (table == null)
                    {
                        if (!Tables.TryGetValue(reader.GetString(0), out table))
                            continue;
                        columnNames = new List<string>();
                    }

                    columnNames.Add(reader.GetString(1));
                }

                if (table != null)
                    result.Add(table, columnNames.ToArray());
            }
#endif
#if MsSQL
            using (DataReader reader = new DataReader(connection, @"
SELECT A.Name table_name, Col.Column_Name column_name
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS Tab, INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE Col, (select NAME from dbo.sysobjects where xtype='u') AS A 
WHERE Col.Constraint_Name = Tab.Constraint_Name AND Col.Table_Name = Tab.Table_Name AND Constraint_Type = 'PRIMARY KEY' AND Col.Table_Name = A.Name 
ORDER BY A.Name", 
                       CommandBehavior.SingleResult))
            {
                Table table = null;
                List<string> columnNames = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(0), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        result.Add(table, columnNames.ToArray());
                        table = null;
                    }

                    if (table == null)
                    {
                        if (!Tables.TryGetValue(reader.GetString(0), out table))
                            continue;
                        columnNames = new List<string>();
                    }

                    columnNames.Add(reader.GetString(1));
                }

                if (table != null)
                    result.Add(table, columnNames.ToArray());
            }
#endif
#if MySQL
            using (DataReader reader = new DataReader(connection, @"
SELECT ut.table_name table_name, utc.column_name column_name
FROM information_schema.tables ut, information_schema.columns utc
WHERE UPPER(ut.table_schema) = UPPER(?table_schema) AND ut.table_schema = utc.table_schema
  AND ut.table_type = 'BASE TABLE'
  AND utc.column_key = 'PRI'  
  AND ut.table_name = utc.table_name
ORDER BY ut.table_name",
                       CommandBehavior.SingleResult))
            {
                reader.CreateParameter("?table_schema", Database.DatabaseName);
                Table table = null;
                List<string> columnNames = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(0), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        result.Add(table, columnNames.ToArray());
                        table = null;
                    }

                    if (table == null)
                    {
                        if (!Tables.TryGetValue(reader.GetString(0), out table))
                            continue;
                        columnNames = new List<string>();
                    }

                    columnNames.Add(reader.GetString(1));
                }

                if (table != null)
                    result.Add(table, columnNames.ToArray());
            }
#endif
#if ORA
            using (DataReader reader = new DataReader(connection, @"
select ucc.table_name table_name, ucc.column_name column_name
from user_constraints uc, user_cons_columns ucc
where uc.constraint_type = 'P'
  and uc.constraint_name = ucc.constraint_name
order by ucc.table_name",
                       CommandBehavior.SingleResult))
            {
                Table table = null;
                List<string> columnNames = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(0), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        result.Add(table, columnNames.ToArray());
                        table = null;
                    }

                    if (table == null)
                    {
                        if (!Tables.TryGetValue(reader.GetString(0), out table))
                            continue;
                        columnNames = new List<string>();
                    }

                    columnNames.Add(reader.GetString(1));
                }

                if (table != null)
                    result.Add(table, columnNames.ToArray());
            }
#endif
            return new ReadOnlyDictionary<Table, string[]>(result);
        }

        private IDictionary<Table, IDictionary<string, ForeignKey>> LoadForeignKeys(DbConnection connection)
        {
            Dictionary<Table, IDictionary<string, ForeignKey>> result = new Dictionary<Table, IDictionary<string, ForeignKey>>(Tables.Count);
#if PgSQL
            using (DataReader reader = new DataReader(connection, @"
SELECT ucc.constraint_name fk_name, ucc.table_name fk_table_name, ucf.column_name fk_column_name, ucp.table_name pk_table_name, ucp.column_name pk_column_name
FROM information_schema.table_constraints ucc 
  JOIN information_schema.key_column_usage ucf ON ucf.constraint_name = ucc.constraint_name
  JOIN information_schema.constraint_column_usage ucp ON ucp.constraint_name = ucc.constraint_name
WHERE UPPER(ucc.table_catalog) = UPPER(@table_catalog) AND ucc.table_catalog = ucf.table_catalog AND ucc.table_catalog = ucp.table_catalog
  AND ucc.table_schema = 'public' AND ucc.table_schema = ucf.table_schema AND ucc.table_schema = ucp.table_schema
  AND ucc.constraint_type = 'FOREIGN KEY'
ORDER BY ucc.table_name",
                       CommandBehavior.SingleResult))
            {
                reader.CreateParameter("@table_catalog", Database.DatabaseName);
                Table table = null;
                Dictionary<string, ForeignKey> foreignKeys = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(1), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        result.Add(table, new ReadOnlyDictionary<string, ForeignKey>(foreignKeys));
                        table = null;
                    }

                    if (table == null)
                    {
                        if (!Tables.TryGetValue(reader.GetString(1), out table))
                            continue;
                        foreignKeys = new Dictionary<string, ForeignKey>(StringComparer.OrdinalIgnoreCase);
                    }

                    foreignKeys.Add(reader.GetString(2), new ForeignKey(this, reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
                }

                if (table != null)
                    result.Add(table, new ReadOnlyDictionary<string, ForeignKey>(foreignKeys));
            }
#endif
#if MsSQL
            using (DataReader reader = new DataReader(connection, @"
SELECT C.CONSTRAINT_NAME fk_name, FK.TABLE_NAME fk_table_name, CU.COLUMN_NAME fk_column_name, PK.TABLE_NAME pk_table_name, PT.COLUMN_NAME pk_column_name
FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS C
  INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS FK ON C.CONSTRAINT_NAME = FK.CONSTRAINT_NAME
  INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS PK ON C.UNIQUE_CONSTRAINT_NAME = PK.CONSTRAINT_NAME
  INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE CU ON C.CONSTRAINT_NAME = CU.CONSTRAINT_NAME
  INNER JOIN (SELECT i1.TABLE_NAME, i2.COLUMN_NAME
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS i1
      INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE i2 ON i1.CONSTRAINT_NAME = i2.CONSTRAINT_NAME
    WHERE i1.CONSTRAINT_TYPE = 'PRIMARY KEY') PT ON PT.TABLE_NAME = PK.TABLE_NAME
ORDER BY FK.TABLE_NAME",
                       CommandBehavior.SingleResult))
            {
                Table table = null;
                Dictionary<string, ForeignKey> foreignKeys = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(1), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        result.Add(table, new ReadOnlyDictionary<string, ForeignKey>(foreignKeys));
                        table = null;
                    }

                    if (table == null)
                    {
                        if (!Tables.TryGetValue(reader.GetString(1), out table))
                            continue;
                        foreignKeys = new Dictionary<string, ForeignKey>(StringComparer.OrdinalIgnoreCase);
                    }

                    foreignKeys.Add(reader.GetString(2), new ForeignKey(this, reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
                }

                if (table != null)
                    result.Add(table, new ReadOnlyDictionary<string, ForeignKey>(foreignKeys));
            }
#endif
#if MySQL
            using (DataReader reader = new DataReader(connection, @"
SELECT ucc.constraint_name fk_name, ucc.table_name fk_table_name, ucc.column_name fk_column_name, ucc.referenced_table_name pk_table_name, ucc.referenced_column_name pk_column_name
FROM information_schema.key_column_usage ucc
WHERE UPPER(ucc.table_schema) = UPPER(?table_schema)
  AND ucc.referenced_table_name is not null
ORDER BY ucc.table_name",
                       CommandBehavior.SingleResult))
            {
                reader.CreateParameter("?table_schema", Database.DatabaseName);
                Table table = null;
                Dictionary<string, ForeignKey> foreignKeys = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(1), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        result.Add(table, new ReadOnlyDictionary<string, ForeignKey>(foreignKeys));
                        table = null;
                    }

                    if (table == null)
                    {
                        if (!Tables.TryGetValue(reader.GetString(1), out table))
                            continue;
                        foreignKeys = new Dictionary<string, ForeignKey>(StringComparer.OrdinalIgnoreCase);
                    }

                    foreignKeys.Add(reader.GetString(2), new ForeignKey(this, reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
                }

                if (table != null)
                    result.Add(table, new ReadOnlyDictionary<string, ForeignKey>(foreignKeys));
            }
#endif
#if ORA
            using (DataReader reader = new DataReader(connection, @"
select ucf.constraint_name fk_name, ucf.table_name fk_table_name, uccf.column_name fk_column_name, ucp.table_name pk_table_name, uccp.column_name pk_column_name
from user_constraints ucf, user_constraints ucp, user_cons_columns uccf, user_cons_columns uccp
where ucf.r_owner = ucp.owner and ucf.r_constraint_name = ucp.constraint_name
  and ucf.constraint_type = 'R' and ucf.owner = uccf.owner and ucf.constraint_Name = uccf.constraint_name and ucf.table_name = uccf.table_name
  and ucp.constraint_type = 'P' and ucp.owner = uccp.owner and ucp.constraint_Name = uccp.constraint_name and ucp.table_name = uccp.table_name
order by ucf.table_name",
                       CommandBehavior.SingleResult))
            {
                Table table = null;
                Dictionary<string, ForeignKey> foreignKeys = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(1), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        result.Add(table, new ReadOnlyDictionary<string, ForeignKey>(foreignKeys));
                        table = null;
                    }

                    if (table == null)
                    {
                        if (!Tables.TryGetValue(reader.GetString(1), out table))
                            continue;
                        foreignKeys = new Dictionary<string, ForeignKey>(StringComparer.OrdinalIgnoreCase);
                    }

                    foreignKeys.Add(reader.GetString(2), new ForeignKey(this, reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
                }

                if (table != null)
                    result.Add(table, new ReadOnlyDictionary<string, ForeignKey>(foreignKeys));
            }
#endif
            return new ReadOnlyDictionary<Table, IDictionary<string, ForeignKey>>(result);
        }

        private IDictionary<Table, IDictionary<string, ForeignKey>> LoadDetailForeignKeys(DbConnection connection)
        {
            Dictionary<Table, IDictionary<string, ForeignKey>> result = new Dictionary<Table, IDictionary<string, ForeignKey>>(Tables.Count);
#if PgSQL
            using (DataReader reader = new DataReader(connection, @"
SELECT ucc.constraint_name fk_name, ucc.table_name fk_table_name, ucf.column_name fk_column_name, ucp.table_name pk_table_name, ucp.column_name pk_column_name
FROM information_schema.table_constraints ucc 
  JOIN information_schema.key_column_usage ucf ON ucf.constraint_name = ucc.constraint_name
  JOIN information_schema.constraint_column_usage ucp ON ucp.constraint_name = ucc.constraint_name
WHERE UPPER(ucc.table_catalog) = UPPER(@table_catalog) AND ucc.table_catalog = ucf.table_catalog AND ucc.table_catalog = ucp.table_catalog
  AND ucc.table_schema = 'public' AND ucc.table_schema = ucf.table_schema AND ucc.table_schema = ucp.table_schema
  AND ucc.constraint_type = 'FOREIGN KEY'
ORDER BY ucp.table_name",
                       CommandBehavior.SingleResult))
            {
                reader.CreateParameter("@table_catalog", Database.DatabaseName);
                Table table = null;
                Dictionary<string, ForeignKey> foreignKeys = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(3), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        result.Add(table, new ReadOnlyDictionary<string, ForeignKey>(foreignKeys));
                        table = null;
                    }

                    if (table == null)
                    {
                        if (!Tables.TryGetValue(reader.GetString(3), out table))
                            continue;
                        foreignKeys = new Dictionary<string, ForeignKey>(StringComparer.OrdinalIgnoreCase);
                    }

                    foreignKeys.Add(reader.GetString(2), new ForeignKey(this, reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
                }

                if (table != null)
                    result.Add(table, new ReadOnlyDictionary<string, ForeignKey>(foreignKeys));
            }
#endif
#if MsSQL
            using (DataReader reader = new DataReader(connection, @"
SELECT C.CONSTRAINT_NAME fk_name, FK.TABLE_NAME fk_table_name, CU.COLUMN_NAME fk_column_name, PK.TABLE_NAME pk_table_name, PT.COLUMN_NAME pk_column_name
FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS C
  INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS FK ON C.CONSTRAINT_NAME = FK.CONSTRAINT_NAME
  INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS PK ON C.UNIQUE_CONSTRAINT_NAME = PK.CONSTRAINT_NAME
  INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE CU ON C.CONSTRAINT_NAME = CU.CONSTRAINT_NAME
  INNER JOIN (SELECT i1.TABLE_NAME, i2.COLUMN_NAME
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS i1
      INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE i2 ON i1.CONSTRAINT_NAME = i2.CONSTRAINT_NAME
    WHERE i1.CONSTRAINT_TYPE = 'PRIMARY KEY') PT ON PT.TABLE_NAME = PK.TABLE_NAME
ORDER BY PK.TABLE_NAME", 
                       CommandBehavior.SingleResult))
            {
                Table table = null;
                Dictionary<string, ForeignKey> foreignKeys = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(3), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        result.Add(table, new ReadOnlyDictionary<string, ForeignKey>(foreignKeys));
                        table = null;
                    }

                    if (table == null)
                    {
                        if (!Tables.TryGetValue(reader.GetString(3), out table))
                            continue;
                        foreignKeys = new Dictionary<string, ForeignKey>(StringComparer.OrdinalIgnoreCase);
                    }

                    foreignKeys.Add(reader.GetString(2), new ForeignKey(this, reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
                }

                if (table != null)
                    result.Add(table, new ReadOnlyDictionary<string, ForeignKey>(foreignKeys));
            }
#endif
#if MySQL
            using (DataReader reader = new DataReader(connection, @"
SELECT ucc.constraint_name fk_name, ucc.table_name fk_table_name, ucc.column_name fk_column_name, ucc.referenced_table_name pk_table_name, ucc.referenced_column_name pk_column_name
FROM information_schema.key_column_usage ucc
WHERE UPPER(ucc.table_schema) = UPPER(?table_schema)
  AND ucc.referenced_table_name is not null
ORDER BY ucc.referenced_table_name",
                       CommandBehavior.SingleResult))
            {
                reader.CreateParameter("?table_schema", Database.DatabaseName);
                Table table = null;
                Dictionary<string, ForeignKey> foreignKeys = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(3), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        result.Add(table, new ReadOnlyDictionary<string, ForeignKey>(foreignKeys));
                        table = null;
                    }

                    if (table == null)
                    {
                        if (!Tables.TryGetValue(reader.GetString(3), out table))
                            continue;
                        foreignKeys = new Dictionary<string, ForeignKey>(StringComparer.OrdinalIgnoreCase);
                    }

                    foreignKeys.Add(reader.GetString(2), new ForeignKey(this, reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
                }

                if (table != null)
                    result.Add(table, new ReadOnlyDictionary<string, ForeignKey>(foreignKeys));
            }
#endif
#if ORA
            using (DataReader reader = new DataReader(connection, @"
select ucf.constraint_name fk_name, ucf.table_name fk_table_name, uccf.column_name fk_column_name, ucp.table_name pk_table_name, uccp.column_name pk_column_name
from user_constraints ucf, user_constraints ucp, user_cons_columns uccf, user_cons_columns uccp
where ucf.r_owner = ucp.owner and ucf.r_constraint_name = ucp.constraint_name
  and ucf.constraint_type = 'R' and ucf.owner = uccf.owner and ucf.constraint_Name = uccf.constraint_name and ucf.table_name = uccf.table_name
  and ucp.constraint_type = 'P' and ucp.owner = uccp.owner and ucp.constraint_Name = uccp.constraint_name and ucp.table_name = uccp.table_name
order by ucp.table_name",
                       CommandBehavior.SingleResult))
            {
                Table table = null;
                Dictionary<string, ForeignKey> foreignKeys = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(3), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        result.Add(table, new ReadOnlyDictionary<string, ForeignKey>(foreignKeys));
                        table = null;
                    }

                    if (table == null)
                    {
                        if (!Tables.TryGetValue(reader.GetString(3), out table))
                            continue;
                        foreignKeys = new Dictionary<string, ForeignKey>(StringComparer.OrdinalIgnoreCase);
                    }

                    foreignKeys.Add(reader.GetString(2), new ForeignKey(this, reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
                }

                if (table != null)
                    result.Add(table, new ReadOnlyDictionary<string, ForeignKey>(foreignKeys));
            }
#endif
            return new ReadOnlyDictionary<Table, IDictionary<string, ForeignKey>>(result);
        }

        private IDictionary<Table, IDictionary<string, Index>> LoadIndexes(DbConnection connection)
        {
            Dictionary<Table, IDictionary<string, Index>> result = new Dictionary<Table, IDictionary<string, Index>>(Tables.Count);
#if PgSQL
            using (DataReader reader = new DataReader(connection, @"
SELECT pg_indexes.indexname, pg_index.indisunique, pg_indexes.tablename, pg_indexes.indexdef
FROM pg_indexes, pg_class, pg_index
WHERE pg_indexes.schemaname = 'public'
  AND pg_indexes.indexname = pg_class.relname AND pg_class.relkind = 'i' AND pg_class.oid = pg_index.indexrelid
ORDER BY pg_indexes.tablename, pg_indexes.indexname",
                       CommandBehavior.SingleResult))
            {
                Table table = null;
                Dictionary<string, Index> indices = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(2), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        result.Add(table, new ReadOnlyDictionary<string, Index>(indices));
                        table = null;
                    }

                    if (table == null)
                    {
                        if (!Tables.TryGetValue(reader.GetString(2), out table))
                            continue;
                        indices = new Dictionary<string, Index>(StringComparer.OrdinalIgnoreCase);
                    }

                    Index index = new Index(table, reader.GetString(0), reader.GetBoolean(1));
                    string columnNames = reader.GetString(3);
                    columnNames = columnNames.Substring(columnNames.IndexOf('(') + 1);
                    columnNames = columnNames.Remove(columnNames.IndexOf(')'));
                    index.ColumnNames = columnNames.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    indices.Add(index.Name, index);
                }

                if (table != null)
                    result.Add(table, new ReadOnlyDictionary<string, Index>(indices));
            }
#endif
#if MsSQL
            using (DataReader reader = new DataReader(connection, @"
SELECT A.NAME index_name, A.is_unique uniqueness, C.NAME table_name, D.NAME column_name
FROM SYS.INDEXES A   
  JOIN SYS.INDEX_COLUMNS B ON A.object_id = B.object_id AND A.index_id = B.index_id   
  JOIN SYS.TABLES C ON A.object_id = C.object_id   
  JOIN SYS.COLUMNS D ON A.object_id = D.object_id AND B.column_id = D.column_id
ORDER BY C.NAME, A.NAME", 
                       CommandBehavior.SingleResult))
            {
                Table table = null;
                Dictionary<string, Index> indices = null;
                Index index = null;
                List<string> columnNames = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(2), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        result.Add(table, new ReadOnlyDictionary<string, Index>(indices));
                        table = null;
                    }

                    if (table == null)
                    {
                        if (!Tables.TryGetValue(reader.GetString(2), out table))
                            continue;
                        indices = new Dictionary<string, Index>(StringComparer.OrdinalIgnoreCase);
                    }

                    if (index != null && String.Compare(index.Name, reader.GetString(0), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        index.ColumnNames = columnNames.ToArray();
                        index = null;
                    }

                    if (index == null)
                    {
                        index = new Index(table, reader.GetString(0), reader.GetBoolean(1));
                        indices.Add(index.Name, index);
                        columnNames = new List<string>();
                    }

                    columnNames.Add(reader.GetString(3));
                }

                if (table != null)
                {
                    index.ColumnNames = columnNames.ToArray();
                    result.Add(table, new ReadOnlyDictionary<string, Index>(indices));
                }
            }
#endif
#if MySQL
            using (DataReader reader = new DataReader(connection, @"
SELECT ui.index_name index_name, ui.non_unique uniqueness, ui.table_name table_name, ui.column_name column_name
FROM information_schema.statistics ui
WHERE UPPER(ui.table_schema) = UPPER(?table_schema)
ORDER BY ui.table_name, ui.index_name",
                       CommandBehavior.SingleResult))
            {
                reader.CreateParameter("?table_schema", Database.DatabaseName);
                Table table = null;
                Dictionary<string, Index> indices = null;
                Index index = null;
                List<string> columnNames = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(2), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        result.Add(table, new ReadOnlyDictionary<string, Index>(indices));
                        table = null;
                    }

                    if (table == null)
                    {
                        if (!Tables.TryGetValue(reader.GetString(2), out table))
                            continue;
                        indices = new Dictionary<string, Index>(StringComparer.OrdinalIgnoreCase);
                    }

                    if (index != null && String.Compare(index.Name, reader.GetString(0), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        index.ColumnNames = columnNames.ToArray();
                        index = null;
                    }

                    if (index == null)
                    {
                        index = new Index(table, reader.GetString(0), reader.GetDecimal(1) == 0);
                        indices.Add(index.Name, index);
                        columnNames = new List<string>();
                    }

                    columnNames.Add(reader.GetString(3));
                }

                if (table != null)
                {
                    index.ColumnNames = columnNames.ToArray();
                    result.Add(table, new ReadOnlyDictionary<string, Index>(indices));
                }
            }
#endif
#if ORA
            using (DataReader reader = new DataReader(connection, @"
select ui.index_name index_name, ui.uniqueness uniqueness, uic.table_name table_name, uic.column_name column_name
from user_indexes ui, user_ind_columns uic
where ui.index_name = uic.index_name
order by uic.table_name, ui.index_name",
                       CommandBehavior.SingleResult))
            {
                Table table = null;
                Dictionary<string, Index> indices = null;
                Index index = null;
                List<string> columnNames = null;
                while (reader.Read())
                {
                    if (table != null && String.Compare(table.Name, reader.GetString(2), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        result.Add(table, new ReadOnlyDictionary<string, Index>(indices));
                        table = null;
                    }

                    if (table == null)
                    {
                        if (!Tables.TryGetValue(reader.GetString(2), out table))
                            continue;
                        indices = new Dictionary<string, Index>(StringComparer.OrdinalIgnoreCase);
                    }

                    if (index != null && String.Compare(index.Name, reader.GetString(0), StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        index.ColumnNames = columnNames.ToArray();
                        index = null;
                    }

                    if (index == null)
                    {
                        index = new Index(table, reader.GetString(0), String.Compare(reader.GetNullableString(1), "UNIQUE", StringComparison.OrdinalIgnoreCase) == 0);
                        indices.Add(index.Name, index);
                        columnNames = new List<string>();
                    }

                    columnNames.Add(reader.GetString(3));
                }

                if (table != null)
                {
                    index.ColumnNames = columnNames.ToArray();
                    result.Add(table, new ReadOnlyDictionary<string, Index>(indices));
                }
            }
#endif
            return new ReadOnlyDictionary<Table, IDictionary<string, Index>>(result);
        }

        /// <summary>
        /// 检索表/视图
        /// </summary>
        /// <param name="name">表/视图名</param>
        /// <param name="throwIfNotFound">如果为 true, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>表</returns>
        public Sheet FindSheet(string name, bool throwIfNotFound = false)
        {
            return FindSheet(name, null, throwIfNotFound);
        }

        /// <summary>
        /// 检索表/视图
        /// </summary>
        /// <param name="name">表/视图名</param>
        /// <param name="doCreate">如果没有找到表/视图则调用本函数新增</param>
        /// <param name="throwIfNotFound">如果为 true 且 doCreate 为 null, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>表</returns>
        public Sheet FindSheet(string name, Action<Database> doCreate, bool throwIfNotFound = false)
        {
            try
            {
                Sheet result = FindView(name, doCreate);
                if (result != null)
                    return result;
            }
            catch (Exception)
            {
                // ignored
            }

            return FindTable(name, doCreate, throwIfNotFound);
        }

        /// <summary>
        /// 检索表/视图
        /// </summary>
        /// <param name="throwIfNotFound">如果为 true, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>表/视图</returns>
        public Sheet FindSheet<T>(bool throwIfNotFound = false)
            where T : class
        {
            return FindSheet(typeof(T), null, throwIfNotFound);
        }

        /// <summary>
        /// 检索表/视图
        /// </summary>
        /// <param name="entityType">实体类</param>
        /// <param name="throwIfNotFound">如果为 true, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>表/视图</returns>
        public Sheet FindSheet(Type entityType, bool throwIfNotFound = false)
        {
            return FindSheet(entityType, null, throwIfNotFound);
        }

        /// <summary>
        /// 检索表/视图
        /// </summary>
        /// <param name="doCreate">如果没有映射的表/视图则调用本函数新增</param>
        /// <param name="throwIfNotFound">如果为 true 且 doCreate 为 null, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>表/视图</returns>
        public Sheet FindSheet<T>(Action<Database> doCreate, bool throwIfNotFound = false)
            where T : class
        {
            return FindSheet(typeof(T), doCreate, throwIfNotFound);
        }

        /// <summary>
        /// 检索表/视图
        /// </summary>
        /// <param name="entityType">实体类</param>
        /// <param name="doCreate">如果没有映射的表/视图则调用本函数新增</param>
        /// <param name="throwIfNotFound">如果为 true 且 doCreate 为 null, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>表/视图</returns>
        public Sheet FindSheet(Type entityType, Action<Database> doCreate, bool throwIfNotFound = false)
        {
            try
            {
                Sheet result = FindView(entityType, doCreate);
                if (result != null)
                    return result;
            }
            catch (Exception)
            {
                // ignored
            }

            return FindTable(entityType, doCreate, throwIfNotFound);
        }

        /// <summary>
        /// 检索表
        /// </summary>
        /// <param name="throwIfNotFound">如果为 true, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>表</returns>
        public Table FindTable<T>(bool throwIfNotFound = false)
            where T : class
        {
            return FindTable(typeof(T), null, throwIfNotFound);
        }

        /// <summary>
        /// 检索表
        /// </summary>
        /// <param name="entityType">实体类</param>
        /// <param name="throwIfNotFound">如果为 true, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>表</returns>
        public Table FindTable(Type entityType, bool throwIfNotFound = false)
        {
            return FindTable(entityType, null, throwIfNotFound);
        }

        /// <summary>
        /// 检索表
        /// </summary>
        /// <param name="doCreate">如果没有映射的表则调用本函数新增</param>
        /// <param name="throwIfNotFound">如果为 true 且 doCreate 为 null, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>表</returns>
        public Table FindTable<T>(Action<Database> doCreate, bool throwIfNotFound = false)
            where T : class
        {
            return FindTable(typeof(T), doCreate, throwIfNotFound);
        }

        /// <summary>
        /// 检索表
        /// </summary>
        /// <param name="entityType">实体类</param>
        /// <param name="doCreate">如果没有映射的表则调用本函数新增</param>
        /// <param name="throwIfNotFound">如果为 true 且 doCreate 为 null, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>表</returns>
        public Table FindTable(Type entityType, Action<Database> doCreate, bool throwIfNotFound = false)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));
            if (!entityType.IsClass || entityType.IsAbstract || entityType.IsGenericType)
                throw new InvalidOperationException(String.Format("仅允许非抽象、非泛型、可实例化的类映射表"));

            return _classTableCache.GetValue(entityType.Name, () =>
            {
                Label:
                SheetAttribute sheetAttribute = (SheetAttribute)Attribute.GetCustomAttribute(entityType, typeof(SheetAttribute));
                if (sheetAttribute != null && Tables.TryGetValue(sheetAttribute.Name, out Table result))
                    return result;
                foreach (KeyValuePair<string, Table> kvp in Tables)
                    if (String.Compare(kvp.Value.ClassName, entityType.Name, StringComparison.OrdinalIgnoreCase) == 0)
                        return kvp.Value;

                if (doCreate == null)
                {
                    if (throwIfNotFound)
                        throw new InvalidOperationException(String.Format("未发现 {0} 映射的表, 请检查类名是否与表名匹配", entityType.FullName));
                    return null;
                }

                try
                {
                    doCreate(Database);
                }
                catch (Exception ex)
                {
                    LogHelper.Error(ex, String.Format("create {0} mapping table", entityType.FullName));
                }

                doCreate = null;
                ClearTableCache();
                goto Label;
            });
        }

        /// <summary>
        /// 检索表
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="throwIfNotFound">如果为 true, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>表</returns>
        public Table FindTable(string tableName, bool throwIfNotFound = false)
        {
            return FindTable(tableName, null, throwIfNotFound);
        }

        /// <summary>
        /// 检索表
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="doCreate">如果没有找到表则调用本函数新增</param>
        /// <param name="throwIfNotFound">如果为 true 且 doCreate 为 null, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>表</returns>
        public Table FindTable(string tableName, Action<Database> doCreate, bool throwIfNotFound = false)
        {
            if (String.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));

            Label:
            if (Tables.TryGetValue(tableName, out Table result))
                return result;

            if (doCreate == null)
            {
                if (throwIfNotFound)
                    throw new InvalidOperationException(String.Format("表 {0} 不存在", tableName));
                return null;
            }

            try
            {
                doCreate(Database);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, String.Format("create {0} table", tableName));
            }

            doCreate = null;
            ClearTableCache();
            goto Label;
        }

        /// <summary>
        /// 检索视图
        /// </summary>
        /// <param name="throwIfNotFound">如果为 true, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>视图</returns>
        public View FindView<T>(bool throwIfNotFound = false)
            where T : class
        {
            return FindView(typeof(T), null, throwIfNotFound);
        }

        /// <summary>
        /// 检索视图
        /// </summary>
        /// <param name="entityType">实体类</param>
        /// <param name="throwIfNotFound">如果为 true, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>视图</returns>
        public View FindView(Type entityType, bool throwIfNotFound = false)
        {
            return FindView(entityType, null, throwIfNotFound);
        }

        /// <summary>
        /// 检索视图
        /// </summary>
        /// <param name="doCreate">如果没有映射的视图则调用本函数新增</param>
        /// <param name="throwIfNotFound">如果为 true 且 doCreate 为 null, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>视图</returns>
        public View FindView<T>(Action<Database> doCreate, bool throwIfNotFound = false)
            where T : class
        {
            return FindView(typeof(T), doCreate, throwIfNotFound);
        }

        /// <summary>
        /// 检索视图
        /// </summary>
        /// <param name="entityType">实体类</param>
        /// <param name="doCreate">如果没有映射的视图则调用本函数新增</param>
        /// <param name="throwIfNotFound">如果为 true 且 doCreate 为 null, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>视图</returns>
        public View FindView(Type entityType, Action<Database> doCreate, bool throwIfNotFound = false)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));
            if (!entityType.IsClass || entityType.IsAbstract || entityType.IsGenericType)
                throw new InvalidOperationException(String.Format("仅允许非抽象、非泛型、可实例化的类映射视图"));

            return _classViewCache.GetValue(entityType.Name, () =>
            {
                Label:
                SheetAttribute sheetAttribute = (SheetAttribute)Attribute.GetCustomAttribute(entityType, typeof(SheetAttribute));
                if (sheetAttribute != null && Views.TryGetValue(sheetAttribute.Name, out View result))
                    return result;
                foreach (KeyValuePair<string, View> kvp in Views)
                    if (String.Compare(kvp.Value.ClassName, entityType.Name, StringComparison.OrdinalIgnoreCase) == 0)
                        return kvp.Value;

                if (doCreate == null)
                {
                    if (throwIfNotFound)
                        throw new InvalidOperationException(String.Format("未发现 {0} 映射的视图, 请检查类名是否与视图名匹配", entityType.FullName));
                    return null;
                }

                try
                {
                    doCreate(Database);
                }
                catch (Exception ex)
                {
                    LogHelper.Error(ex, String.Format("create {0} mapping view", entityType.FullName));
                }

                doCreate = null;
                ClearViewCache();
                goto Label;
            });
        }

        /// <summary>
        /// 检索视图
        /// </summary>
        /// <param name="viewName">视图名</param>
        /// <param name="throwIfNotFound">如果为 true, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>视图</returns>
        public View FindView(string viewName, bool throwIfNotFound = false)
        {
            return FindView(viewName, null, throwIfNotFound);
        }

        /// <summary>
        /// 检索视图
        /// </summary>
        /// <param name="viewName">视图名</param>
        /// <param name="doCreate">如果没有找到视图则调用本函数新增</param>
        /// <param name="throwIfNotFound">如果为 true 且 doCreate 为 null, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>视图</returns>
        public View FindView(string viewName, Action<Database> doCreate, bool throwIfNotFound = false)
        {
            if (String.IsNullOrEmpty(viewName))
                throw new ArgumentNullException(nameof(viewName));

            Label:
            if (Views.TryGetValue(viewName, out View result))
                return result;

            if (doCreate == null)
            {
                if (throwIfNotFound)
                    throw new InvalidOperationException(String.Format("视图 {0} 不存在", viewName));
                return null;
            }

            try
            {
                doCreate(Database);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, String.Format("create {0} view", viewName));
            }

            doCreate = null;
            ClearViewCache();
            goto Label;
        }

        /// <summary>
        /// 提取视图中表队列
        /// </summary>
        /// <param name="viewText">视图文本</param>
        /// <returns>表队列</returns>
        public IDictionary<string, Table> ExtractViewTables(string viewText)
        {
            Dictionary<string, Table> result = new Dictionary<string, Table>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, List<string>> kvp in SqlHelper.GetSourceBody(viewText))
                if (!result.ContainsKey(kvp.Key))
                {
                    Table table = FindTable(kvp.Key);
                    if (table != null)
                        result[kvp.Key] = table;
                    else
                    {
                        View view = FindView(kvp.Key);
                        if (view != null)
                            foreach (Table viewTable in view.Tables.Values)
                                result[viewTable.Name] = viewTable;
                    }
                }

            return new ReadOnlyDictionary<string, Table>(result);
        }

        #endregion
    }
}