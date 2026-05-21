using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Linq;
using Phenix.Core.Data;
using Phenix.Core.Data.Common;

namespace Phenix.Core.Mapper.Schema
{
    /// <summary>
    /// 表
    /// </summary>
    [Serializable]
    public sealed class Table : Sheet
    {
        [Newtonsoft.Json.JsonConstructor]
        private Table(string name, string description, IDictionary<string, Column> columns,
            string[] primaryKeys, IDictionary<string, ForeignKey> foreignKeys, IDictionary<string, ForeignKey> detailForeignKeys, IDictionary<string, Index> indexes)
            : base(name, description, columns)
        {
            _primaryKeys = primaryKeys;
            _foreignKeys = new ReadOnlyDictionary<string, ForeignKey>(new Dictionary<string, ForeignKey>(foreignKeys, StringComparer.OrdinalIgnoreCase));
            _detailForeignKeys = new ReadOnlyDictionary<string, ForeignKey>(new Dictionary<string, ForeignKey>(detailForeignKeys, StringComparer.OrdinalIgnoreCase));
            _indexes = new ReadOnlyDictionary<string, Index>(new Dictionary<string, Index>(indexes, StringComparer.OrdinalIgnoreCase));
        }

        internal Table(MetaData owner, string name, string description)
            : base(owner, name, description)
        {
        }

        #region 属性

        /// <summary>
        /// 所属数据库架构
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public override MetaData Owner
        {
            get { return base.Owner; }
            protected internal set
            {
                base.Owner = value;

                if (_foreignKeys != null)
                    foreach (KeyValuePair<string, ForeignKey> kvp in _foreignKeys)
                        kvp.Value.Owner = value;
                if (_detailForeignKeys != null)
                    foreach (KeyValuePair<string, ForeignKey> kvp in _detailForeignKeys)
                        kvp.Value.Owner = value;
                if (_indexes != null)
                    foreach (KeyValuePair<string, Index> kvp in _indexes)
                        kvp.Value.Owner = this;
            }
        }

        private string[] _primaryKeys;

        /// <summary>
        /// 主键清单
        /// </summary>
        public string[] PrimaryKeys
        {
            get
            {
                return _primaryKeys ??= Owner.PrimaryKeys != null && Owner.PrimaryKeys.TryGetValue(this, out string[] result)
                    ? result
                    : new string[0];
            }
        }

        private IDictionary<string, ForeignKey> _foreignKeys;

        /// <summary>
        /// 外键清单
        /// </summary>
        public IDictionary<string, ForeignKey> ForeignKeys
        {
            get
            {
                return _foreignKeys ??= Owner.ForeignKeys != null && Owner.ForeignKeys.TryGetValue(this, out IDictionary<string, ForeignKey> result)
                    ? result
                    : new ReadOnlyDictionary<string, ForeignKey>(new Dictionary<string, ForeignKey>(StringComparer.OrdinalIgnoreCase));
            }
        }

        private IDictionary<string, ForeignKey> _detailForeignKeys;

        /// <summary>
        /// 子键清单
        /// </summary>
        public IDictionary<string, ForeignKey> DetailForeignKeys
        {
            get
            {
                return _detailForeignKeys ??= Owner.DetailForeignKeys != null && Owner.DetailForeignKeys.TryGetValue(this, out IDictionary<string, ForeignKey> result)
                    ? result
                    : new ReadOnlyDictionary<string, ForeignKey>(new Dictionary<string, ForeignKey>(StringComparer.OrdinalIgnoreCase));
            }
        }

        private IDictionary<string, Index> _indexes;

        /// <summary>
        /// 索引清单
        /// </summary>
        public IDictionary<string, Index> Indexes
        {
            get
            {
                return _indexes ??= Owner.Indexes != null && Owner.Indexes.TryGetValue(this, out IDictionary<string, Index> result)
                    ? result
                    : new ReadOnlyDictionary<string, Index>(new Dictionary<string, Index>(StringComparer.OrdinalIgnoreCase));
            }
        }

        [NonSerialized]
        private string _className;

        /// <summary>
        /// 类名
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public override string ClassName
        {
            get { return _className ??= Standards.GetPascalCasingByTableName(Name, String.Compare(Prefix, "PH7", StringComparison.OrdinalIgnoreCase) == 0); }
            set { _className = value; }
        }

        [NonSerialized]
        private string _prefix;

        /// <summary>
        /// 前缀
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public override string Prefix
        {
            get { return _prefix ??= Standards.GetPrefixBySheetName(Name); }
        }

        #endregion

        #region 方法

        /// <summary>
        /// 检索唯一键索引队列
        /// </summary>
        /// <param name="columnName">字段名</param>
        /// <returns>唯一键索引队列</returns>
        public IList<Index> FindUniqueIndexes(string columnName)
        {
            List<Index> result = new List<Index>(Indexes.Count);
            foreach (KeyValuePair<string, Index> kvp in Indexes)
                if (kvp.Value.Unique)
                    if (kvp.Value.ColumnNames.Any(item => String.Compare(item, columnName, StringComparison.OrdinalIgnoreCase) == 0))
                        result.Add(kvp.Value);

            return result.AsReadOnly();
        }

        internal void DeleteDepth(DbTransaction transaction, object primaryKeyValue)
        {
            foreach (KeyValuePair<string, ForeignKey> detailForeignKey in DetailForeignKeys)
            {
                if (detailForeignKey.Value.Table.PrimaryKeys.Length != 1)
                    throw new InvalidOperationException(String.Format("表 {0} 必须有且仅有一个主键字段({1})", detailForeignKey.Value.TableName, detailForeignKey.Value.Table.PrimaryKeys.Length));

                List<object> detailPrimaryKeyValues = new List<object>();
                using (DataReader reader = new DataReader(transaction, String.Format("select {0} from {1} where {2} = :{2}",
                    detailForeignKey.Value.Table.PrimaryKeys[0], detailForeignKey.Value.TableName, detailForeignKey.Value.ColumnName)))
                {
                    reader.CreateParameter(detailForeignKey.Value.ColumnName, primaryKeyValue);
                    while (reader.Read())
                        detailPrimaryKeyValues.Add(reader.GetValue(0));
                }

                foreach (object detailPrimaryKeyValue in detailPrimaryKeyValues)
                    detailForeignKey.Value.Table.DeleteDepth(transaction, detailPrimaryKeyValue);

                using (DbCommand command = DbCommandHelper.CreateCommand(transaction, String.Format("delete from {0} where {1} = :{1}",
                    detailForeignKey.Value.TableName, detailForeignKey.Value.ColumnName)))
                {
                    DbCommandHelper.CreateParameter(command, detailForeignKey.Value.ColumnName, primaryKeyValue);
                    DbCommandHelper.ExecuteNonQuery(command);
                }
            }
        }
        
        #endregion
    }
}