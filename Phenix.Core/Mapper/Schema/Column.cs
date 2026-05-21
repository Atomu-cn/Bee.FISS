using System;
using System.Collections.Generic;
using System.Linq;
using Phenix.Core;
using Phenix.Core.Data;
using Phenix.Core.Data.Common;
using Phenix.Core.SyncCollections;

namespace Phenix.Core.Mapper.Schema
{
    /// <summary>
    /// 字段
    /// </summary>
    [Serializable]
    public sealed class Column
    {
        [Newtonsoft.Json.JsonConstructor]
        private Column(string name, string description, bool? nullable, string dataType, string dataDefault, long? dataLength, int? dataPrecision, int? dataScale)
        {
            _name = name;
            _description = description;
            _nullable = nullable;
            _dataType = dataType;
            if (dataDefault != null)
                _dataDefault = dataDefault.Trim('\n').Replace('\'', '"');
            _dataLength = dataLength;
            _dataPrecision = dataPrecision;
            _dataScale = dataScale;
        }

        internal Column(Sheet owner, string name, string description, bool? nullable, string dataType, string dataDefault, long? dataLength, int? dataPrecision, int? dataScale)
            : this(name, description, nullable, dataType, dataDefault, dataLength, dataPrecision, dataScale)
        {
            _owner = owner;
        }

        #region 属性

        #region 配置项

        private static bool? _fieldNameByTrimColumnName;

        /// <summary>
        /// 字段名是否取自被整理的字段名(如果第1-PrefixCount位后是"_"则剔去其及之前的字符)
        /// 默认：true
        /// </summary>
        public static bool FieldNameByTrimColumnName
        {
            get { return AppSettings.GetLocalProperty(ref _fieldNameByTrimColumnName, true); }
            set { AppSettings.SetLocalProperty(ref _fieldNameByTrimColumnName, value); }
        }

        private static int? _prefixCount;

        /// <summary>
        /// 前缀字符数
        /// 默认：1-3
        /// </summary>
        public static int PrefixCount
        {
            get { return AppSettings.GetLocalProperty(ref _prefixCount, 3); }
            set { AppSettings.SetLocalProperty(ref _prefixCount, value); }
        }

        #endregion

        [NonSerialized]
        private Sheet _owner;

        /// <summary>
        /// Owner
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Sheet Owner
        {
            get { return _owner; }
            internal set { _owner = value; }
        }

        [NonSerialized]
        private Column _tableColumn;

        /// <summary>
        /// 表字段
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Column TableColumn
        {
            get
            {
                if (_tableColumn == null)
                    if (Owner is Table)
                        _tableColumn = this;
                    else if (Owner is View view)
                        _tableColumn = view.FindTableColumn(_name);
                return _tableColumn;
            }
        }

        private readonly string _name;

        /// <summary>
        /// 名称
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        [NonSerialized]
        private string _fullName;

        /// <summary>
        /// 完整字段名(SheetName.Name)
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string FullName
        {
            get { return _fullName ??= SqlHelper.AssembleFullTableColumnName(Owner.Name, _name); }
        }

        private string _description;

        /// <summary>
        /// 注释(中英文用‘|’分隔)
        /// Thread.CurrentThread.CurrentCulture.Name为非'zh-'时返回后半截
        /// </summary>
        public string Description
        {
            get
            {
                if (String.IsNullOrEmpty(_description))
                {
                    if (TableColumn != null && TableColumn != this)
                        _description = TableColumn.Description;
                }
                
                return AppRun.SplitCulture(_description);
            }
        }

        private bool? _nullable;

        /// <summary>
        /// Nullable
        /// </summary>
        public bool Nullable
        {
            get
            {
                if (!_nullable.HasValue)
                    _nullable = TableColumn != null && TableColumn != this ? TableColumn.Nullable : true;
                return _nullable.Value;
            }
        }

        private string _dataType;

        /// <summary>
        /// DataType
        /// 不存在则返回 String.Empty
        /// </summary>
        public string DataType
        {
            get { return _dataType ??= TableColumn != null && TableColumn != this 
                ? TableColumn.DataType
                : String.Empty; }
        }

        [NonSerialized]
        private Type _mappingType;

        /// <summary>
        /// 映射类型
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Type MappingType
        {
            get
            {
                if (_mappingType == null)
                {
                    Type result;
                    switch (DataType.ToUpper())
                    {
                        case "CHAR": //Oracle/MySQ/MsSqlL/PgSQL
                        case "CHARACTER": //MySQL/PgSQL
                        case "VARCHAR": //Oracle/MySQL/MsSql/PgSQL
                        case "CHARACTER VARYING": //PgSQL
                        case "VARCHAR2": //Oracle
                        case "BINARY": //MySQL/MsSql
                        case "VARBINARY": //MySQL/MsSql
                        case "NCHAR": //Oracle/MsSql
                        case "NVARCHAR": //Oracle/MsSql
                        case "NVARCHAR2": //Oracle
                        case "LONG": //Oracle
                        case "CLOB": //Oracle
                        case "TINYTEXT": //MySQL
                        case "TEXT": //MySQL/MsSql/PgSQL
                        case "NTEXT": //MsSql
                        case "MEDIUMTEXT": //MySQL
                        case "LONGTEXT": //MySQL
                            result = typeof(string);
                            break;
                        case "NUMERIC": //Oracle/MySQL/MsSql/PgSQL
                        case "NUMBER": //Oracle
                        case "DECIMAL": //Oracle/MySQL/MsSql/PgSQL
                        case "DEC": //Oracle/MySQL
                        case "MONEY": //MsSql/PgSQL
                        case "SMALLMONEY": //MsSql
                            if (DataScale > 0)
                            {
                                result = Nullable ? typeof(decimal?) : typeof(decimal);
                                break;
                            }

                            if (DataPrecision == 1 && Standards.IsDefaultEnumColumnName(_name))
                            {
                                result = Nullable ? typeof(bool?) : typeof(bool);
                                break;
                            }

                            if (DataPrecision == 2 && Standards.IsDefaultEnumColumnName(_name))
                            {
                                result = null;
                                break;
                            }

                            result = Nullable
                                ? DataPrecision >= 10 ? typeof(long?) : DataPrecision >= 5 ? typeof(int?) : typeof(short?)
                                : DataPrecision >= 10 ? typeof(long) : DataPrecision >= 5 ? typeof(int) : typeof(short);
                            break;
                        case "TINYINT": //MySQL/MsSql
                            result = Nullable ? typeof(sbyte?) : typeof(sbyte);
                            break;
                        case "TINYINT UNSIGNED": //MySQL
                            result = Nullable ? typeof(byte?) : typeof(byte);
                            break;
                        case "SMALLINT": //Oracle/MySQL/MsSql/PgSQL
                        case "INT2": //PgSQL
                            result = Nullable ? typeof(short?) : typeof(short);
                            break;
                        case "SMALLINT UNSIGNED": //MySQL
                        case "SMALLSERIAL": //PgSQL
                            result = Nullable ? typeof(ushort?) : typeof(ushort);
                            break;
                        case "INT": //Oracle/MySQL/MsSql
                        case "INTEGER": //Oracle/MySQL/MsSql/PgSQL
                        case "INT4": //PgSQL
                            result = Nullable ? typeof(int?) : typeof(int);
                            break;
                        case "INT UNSIGNED": //MySQL
                        case "INTEGER UNSIGNED": //MySQL
                        case "SERIAL": //PgSQL
                            result = Nullable ? typeof(uint?) : typeof(uint);
                            break;
                        case "BIGINT": //MySQL/PgSQL
                        case "INT8": //PgSQL
                            result = Nullable ? typeof(long?) : typeof(long);
                            break;
                        case "BIGINT UNSIGNED": //MySQL
                        case "BIGSERIAL": //PgSQL
                            result = Nullable ? typeof(ulong?) : typeof(ulong);
                            break;
                        case "DATE": //Oracle/MySQL/PgSQL
                        case "DATETIME": //MySQL/MsSql
                        case "TIME": //MySQL/PgSQL
                        case "TIMESTAMP": //MsSql/PgSQL
                            result = Nullable ? typeof(DateTime?) : typeof(DateTime);
                            break;
                        case "BOOL": //MySQL
                        case "BIT": //MsSql
                        case "BOOLEAN": //PgSQL
                            result = Nullable ? typeof(bool?) : typeof(bool);
                            break;
                        case "BINARY_FLOAT": //Oracle
                        case "FLOAT": //Oracle/MySQL/MsSql/PgSQL
                        case "REAL": //MySQL/MsSql/PgSQL
                            result = Nullable ? typeof(float?) : typeof(float);
                            break;
                        case "BINARY_DOUBLE": //Oracle
                        case "DOUBLE": //MySQL
                        case "DOUBLE PRECISION": //Oracle/MySQL/PgSQL
                            result = Nullable ? typeof(double?) : typeof(double);
                            break;
                        case "RAW": //Oracle
                        case "LONG RAW": //Oracle
                        case "BFILE": //Oracle
                        case "TINYBLOB": //MySQL
                        case "BLOB": //Oracle/MySQL
                        case "MEDIUMBLOB": //MySQL
                        case "LONGBLOB": //MySQL
                        case "IMAGE": //MsSql
                            result = typeof(byte[]);
                            break;
                        case "ROWID": //Oracle
                            result = typeof(string);
                            break;
                        default:
                            result = null;
                            break;
                    }
                    
                    _mappingType = result;
                }

                return _mappingType;
            }
        }

        [NonSerialized]
        private string _mappingTypeName;

        /// <summary>
        /// 映射类型名
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string MappingTypeName
        {
            get
            {
                if (_mappingTypeName == null)
                {
                    string result;
                    switch (DataType.ToUpper())
                    {
                        case "CHAR": //Oracle/MySQL/MsSql/PgSQL
                        case "CHARACTER": //MySQL/PgSQL
                        case "VARCHAR": //Oracle/MySQL/MsSql/PgSQL
                        case "CHARACTER VARYING": //PgSQL
                        case "VARCHAR2": //Oracle
                        case "BINARY": //MySQL/MsSql
                        case "VARBINARY": //MySQL/MsSql
                        case "NCHAR": //Oracle/MsSql
                        case "NVARCHAR": //Oracle/MsSql
                        case "NVARCHAR2": //Oracle
                        case "LONG": //Oracle
                        case "CLOB": //Oracle
                        case "TINYTEXT": //MySQL
                        case "TEXT": //MySQL/MsSql/PgSQL
                        case "NTEXT": //MsSql
                        case "MEDIUMTEXT": //MySQL
                        case "LONGTEXT": //MySQL
                            result = "string";
                            break;
                        case "NUMERIC": //Oracle/MySQL/MsSql/PgSQL
                        case "NUMBER": //Oracle
                        case "DECIMAL": //Oracle/MySQL/MsSql/PgSQL
                        case "DEC": //Oracle/MySQL
                        case "MONEY": //MsSql/PgSQL
                        case "SMALLMONEY": //MsSql
                            if (DataScale > 0)
                            {
                                result = Nullable ? "decimal?" : "decimal";
                                break;
                            }

                            if (DataPrecision == 1 && Standards.IsDefaultEnumColumnName(_name))
                            {
                                result = Nullable ? "bool?" : "bool";
                                break;
                            }

                            if (DataPrecision == 2 && Standards.IsDefaultEnumColumnName(_name))
                            {
                                result = PropertyName;
                                break;
                            }

                            result = Nullable
                                ? DataPrecision >= 10 ? "long?" : DataPrecision >= 5 ? "int?" : "short?"
                                : DataPrecision >= 10 ? "long" : DataPrecision >= 5 ? "int" : "short";
                            break;
                        case "TINYINT": //MySQL/MsSql
                            result = Nullable ? "sbyte?" : "sbyte";
                            break;
                        case "TINYINT UNSIGNED": //MySQL
                            result = Nullable ? "byte?" : "byte";
                            break;
                        case "SMALLINT": //Oracle/MySQL/MsSql/PgSQL
                        case "INT2": //PgSQL
                            result = Nullable ? "short?" : "short";
                            break;
                        case "SMALLINT UNSIGNED": //MySQL
                        case "SMALLSERIAL": //PgSQL
                            result = Nullable ? "ushort?" : "ushort";
                            break;
                        case "INT": //Oracle/MySQL/MsSql
                        case "INTEGER": //Oracle/MySQL/MsSql/PgSQL
                        case "INT4": //PgSQL
                            result = Nullable ? "int?" : "int";
                            break;
                        case "INT UNSIGNED": //MySQL
                        case "INTEGER UNSIGNED": //MySQL
                        case "SERIAL": //PgSQL
                            result = Nullable ? "uint?" : "uint";
                            break;
                        case "BIGINT": //MySQL/PgSQL
                        case "INT8": //PgSQL
                            result = Nullable ? "long?" : "long";
                            break;
                        case "BIGINT UNSIGNED": //MySQL
                        case "BIGSERIAL": //PgSQL
                            result = Nullable ? "ulong?" : "ulong";
                            break;
                        case "DATE": //Oracle/MySQL/PgSQL
                        case "DATETIME": //MySQL/MsSql
                        case "TIME": //MySQL/PgSQL
                        case "TIMESTAMP": //MsSql/PgSQL
                            result = Nullable ? "DateTime?" : "DateTime";
                            break;
                        case "BOOL": //MySQL
                        case "BIT": //MsSql
                        case "BOOLEAN": //PgSQL
                            result = Nullable ? "bool?" : "bool";
                            break;
                        case "BINARY_FLOAT": //Oracle
                        case "FLOAT": //Oracle/MySQL/MsSql/PgSQL
                        case "REAL": //MySQL/MsSql/PgSQL
                            result = Nullable ? "float?" : "float";
                            break;
                        case "BINARY_DOUBLE": //Oracle
                        case "DOUBLE": //MySQL
                        case "DOUBLE PRECISION": //Oracle/MySQL/PgSQL
                            result = Nullable ? "double?" : "double)";
                            break;
                        case "RAW": //Oracle
                        case "LONG RAW": //Oracle
                        case "BFILE": //Oracle
                        case "TINYBLOB": //MySQL
                        case "BLOB": //Oracle/MySQL
                        case "MEDIUMBLOB": //MySQL
                        case "LONGBLOB": //MySQL
                        case "IMAGE": //MsSql
                            result = "byte[]";
                            break;
                        case "ROWID": //Oracle
                            result = "string";
                            break;
                        default:
                            result = PropertyName;
                            break;
                    }

                    _mappingTypeName = result;
                }

                return _mappingTypeName;
            }
        }

        [NonSerialized]
        private bool? _isUnicode;

        /// <summary>
        /// IsUnicode
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool IsUnicode
        {
            get
            {
                if (!_isUnicode.HasValue)
                    switch (DataType.ToUpper())
                    {
                        case "NVARCHAR":
                        case "NVARCHAR2": //Oracle
                        case "NCHAR":
                        case "NTEXT": //MsSql
                            _isUnicode = true;
                            break;
                        default:
                            _isUnicode = false;
                            break;
                    }

                return _isUnicode.Value;
            }
        }

        [NonSerialized]
        private bool? _isLongText;

        /// <summary>
        /// IsLongText
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool IsLongText
        {
            get
            {
                if (!_isLongText.HasValue)
                    switch (DataType.ToUpper())
                    {
                        case "LONG": //Oracle
                        case "CLOB": //Oracle
                        case "TINYTEXT": //MySQL
                        case "TEXT": //MySQL/MsSql/PgSQL
                        case "NTEXT": //MsSql
                        case "MEDIUMTEXT": //MySQL
                        case "LONGTEXT": //MySQL
                            _isLongText = true;
                            break;
                        default:
                            _isLongText = false;
                            break;
                    }

                return _isLongText.Value;
            }
        }

        private string _dataDefault;

        /// <summary>
        /// DataDefault
        /// 不存在则返回 String.Empty
        /// </summary>
        public string DataDefault
        {
            get { return _dataDefault ??= TableColumn != null && TableColumn != this 
                ? TableColumn.DataDefault 
                : String.Empty; }
        }

        private long? _dataLength;

        /// <summary>
        /// DataLength
        /// </summary>
        public long DataLength
        {
            get
            {
                if (!_dataLength.HasValue)
                    _dataLength = TableColumn != null && TableColumn != this ? TableColumn.DataLength : 0;
                return _dataLength.Value;
            }
        }

        private int? _dataPrecision;

        /// <summary>
        /// DataPrecision
        /// </summary>
        public int DataPrecision
        {
            get
            {
                if (!_dataPrecision.HasValue)
                    _dataPrecision = TableColumn != null && TableColumn != this ? TableColumn.DataPrecision : 0;
                return _dataPrecision.Value;
            }
        }

        private int? _dataScale;

        /// <summary>
        /// DataScale
        /// </summary>
        public int DataScale
        {
            get
            {
                if (!_dataScale.HasValue)
                    _dataScale = TableColumn != null && TableColumn != this ? TableColumn.DataScale : 0;
                return _dataScale.Value;
            }
        }

        [NonSerialized]
        private bool? _isPrimaryKey;

        /// <summary>
        /// 是否主键（字段类型需是长整型15位以上精度，新增记录时自动填充Sequence.Default.Value）
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool IsPrimaryKey
        {
            get
            {
                if (!_isPrimaryKey.HasValue)
                    _isPrimaryKey = TableColumn != null && TableColumn != this 
                        ? TableColumn.IsPrimaryKey
                        : Owner is Table owner && owner.PrimaryKeys.Any(item => String.Compare(item, _name, StringComparison.OrdinalIgnoreCase) == 0);
                return _isPrimaryKey.Value;
            }
        }

        [NonSerialized]
        private bool _foreignKeyChecked;

        [NonSerialized]
        private ForeignKey _foreignKey;

        /// <summary>
        /// 外键
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public ForeignKey ForeignKey
        {
            get
            {
                if (!_foreignKeyChecked)
                {
                    if (TableColumn != null && TableColumn != this)
                        _foreignKey = TableColumn.ForeignKey;
                    else if (Owner is Table owner)
                        owner.ForeignKeys.TryGetValue(_name, out _foreignKey);
                    _foreignKeyChecked = true;
                }

                return _foreignKey;
            }
        }

        [NonSerialized]
        private bool _uniqueIndexesChecked;

        [NonSerialized]
        private IList<Index> _uniqueIndexes;

        /// <summary>
        /// 唯一键索引队列
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public IList<Index> UniqueIndexes
        {
            get
            {
                if (!_uniqueIndexesChecked)
                {
                    if (TableColumn != null && TableColumn != this)
                        _uniqueIndexes = TableColumn.UniqueIndexes;
                    else if (Owner is Table owner)
                        _uniqueIndexes = owner.FindUniqueIndexes(_name);
                    _uniqueIndexesChecked = true;
                }

                return _uniqueIndexes;
            }
        }

        [NonSerialized]
        private bool? _isOriginatorColumn;

        /// <summary>
        /// 是否"制单人"字段（字段类型需是字符串/长整型15位以上精度，新增记录时自动填充Identity.CurrentIdentity.User.Name/Id）
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool IsOriginatorColumn
        {
            get
            {
                if (!_isOriginatorColumn.HasValue)
                    _isOriginatorColumn = TableColumn != null && TableColumn != this
                        ? TableColumn.IsOriginatorColumn 
                        : (MappingType == typeof(string) || MappingType == typeof(long) || MappingType == typeof(long?)) && Standards.IsDefaultOriginatorColumnName(_name);
                return _isOriginatorColumn.Value;
            }
        }

        [NonSerialized]
        private bool? _isOriginateTimeColumn;

        /// <summary>
        /// 是否"制单时间"字段（字段类型需是DateTime，新增记录时自动填充当前时间）
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool IsOriginateTimeColumn
        {
            get
            {
                if (!_isOriginateTimeColumn.HasValue)
                    _isOriginateTimeColumn = TableColumn != null && TableColumn != this 
                        ? TableColumn.IsOriginateTimeColumn
                        : (MappingType == typeof(DateTime) || MappingType == typeof(DateTime?)) && Standards.IsDefaultOriginateTimeColumnName(_name);
                return _isOriginateTimeColumn.Value;
            }
        }

        [NonSerialized]
        private bool? _isOriginateTeamsColumn;

        /// <summary>
        /// 是否"制单团体"字段（字段类型需是字符串/长整型15位以上精度，新增记录时自动填充Identity.CurrentIdentity.User.RootTeams.Name/Id）
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool IsOriginateTeamsColumn
        {
            get
            {
                if (!_isOriginateTeamsColumn.HasValue)
                    _isOriginateTeamsColumn = TableColumn != null && TableColumn != this 
                        ? TableColumn.IsOriginateTeamsColumn
                        : (MappingType == typeof(string) || MappingType == typeof(long) || MappingType == typeof(long?)) && Standards.IsDefaultOriginateTeamsColumnName(_name);
                return _isOriginateTeamsColumn.Value;
            }
        }

        [NonSerialized]
        private bool? _isUpdaterColumn;

        /// <summary>
        /// 是否"更新人"字段（字段类型需是字符串/长整型15位以上精度，更新记录时自动填充Identity.CurrentIdentity.User.Name/Id）
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool IsUpdaterColumn
        {
            get
            {
                if (!_isUpdaterColumn.HasValue)
                    _isUpdaterColumn = TableColumn != null && TableColumn != this
                        ? TableColumn.IsUpdaterColumn
                        : (MappingType == typeof(string) || MappingType == typeof(long) || MappingType == typeof(long?)) && Standards.IsDefaultUpdaterColumnName(_name);
                return _isUpdaterColumn.Value;
            }
        }

        [NonSerialized]
        private bool? _isUpdateTimeColumn;

        /// <summary>
        /// 是否"更新时间"字段（字段类型需是DateTime，更新记录时自动填充当前时间）
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool IsUpdateTimeColumn
        {
            get
            {
                if (!_isUpdateTimeColumn.HasValue)
                    _isUpdateTimeColumn = TableColumn != null && TableColumn != this
                        ? TableColumn.IsUpdateTimeColumn
                        : (MappingType == typeof(DateTime) || MappingType == typeof(DateTime?)) && Standards.IsDefaultUpdateTimeColumnName(_name);
                return _isUpdateTimeColumn.Value;
            }
        }

        [NonSerialized]
        private bool? _isTimestampColumn;

        /// <summary>
        /// 是否"时间戳"字段（字段类型需是长整型15位以上精度，更新记录时自动填充Sequence.Default.Value）
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool IsTimestampColumn
        {
            get
            {
                if (!_isTimestampColumn.HasValue)
                    _isTimestampColumn = TableColumn != null && TableColumn != this
                        ? TableColumn.IsTimestampColumn
                        : (MappingType == typeof(string) && DataLength >= 15 || MappingType == typeof(long) || MappingType == typeof(long?)) && Standards.IsDefaultTimestampColumnName(_name);
                return _isTimestampColumn.Value;
            }
        }

        [NonSerialized]
        private bool? _isRouteColumn;

        /// <summary>
        /// 是否HASH值路由增删改查数据库
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool IsRouteColumn
        {
            get
            {
                if (!_isRouteColumn.HasValue)
                    _isRouteColumn = TableColumn != null && TableColumn != this
                        ? TableColumn.IsRouteColumn
                        : Standards.IsDefaultRouteColumnName(_name);
                return _isRouteColumn.Value;
            }
        }

        [NonSerialized]
        private bool? _isWatermarkColumn;

        /// <summary>
        /// 是否仅在insert时被提交
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool IsWatermarkColumn
        {
            get
            {
                if (!_isWatermarkColumn.HasValue)
                    _isWatermarkColumn = TableColumn != null && TableColumn != this
                        ? TableColumn.IsWatermarkColumn
                        : Standards.IsDefaultWatermarkColumnName(_name) || IsPrimaryKey || IsOriginatorColumn || IsOriginateTimeColumn || IsOriginateTeamsColumn || IsRouteColumn;
                return _isWatermarkColumn.Value;
            }
        }

        [NonSerialized]
        private bool? _overwritingOnUpdate;

        /// <summary>
        /// 是否要在Update时被覆盖
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool OverwritingOnUpdate
        {
            get
            {
                if (!_overwritingOnUpdate.HasValue)
                    _overwritingOnUpdate = TableColumn != null && TableColumn != this
                        ? TableColumn.OverwritingOnUpdate
                        : IsUpdaterColumn || IsUpdateTimeColumn || IsTimestampColumn;
                return _overwritingOnUpdate.Value;
            }
        }

        [NonSerialized]
        private string _propertyName;

        /// <summary>
        /// 类属性名
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string PropertyName
        {
            get { return _propertyName ??= Standards.GetPascalCasingByColumnName(Name, FieldNameByTrimColumnName || String.Compare(Owner.Prefix, "PH7", StringComparison.OrdinalIgnoreCase) == 0, PrefixCount); }
            set { _propertyName = value; }
        }

        [NonSerialized]
        private string _fieldName;

        /// <summary>
        /// 类字段名
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string FieldName
        {
            get { return _fieldName ??= Standards.GetFieldNameByPropertyName(PropertyName); }
            set { _fieldName = value; }
        }

        [NonSerialized]
        private string _parameterName;

        /// <summary>
        /// 参数名
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string ParameterName
        {
            get { return _parameterName ??= Standards.GetParameterNameByPropertyName(PropertyName); }
            set { _parameterName = value; }
        }
        
        [NonSerialized]
        private readonly SynchronizedDictionary<string, Property> _propertyCache =
            new SynchronizedDictionary<string, Property>(StringComparer.Ordinal);

        #endregion

        #region 方法

        /// <summary>
        /// 获取字段映射类属性
        /// </summary>
        /// <param name="entityType">实体类</param>
        /// <param name="throwIfNotFound">如果为 true, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>类属性</returns>
        public Property GetProperty(Type entityType, bool throwIfNotFound = true)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            return _propertyCache.GetValue(entityType.FullName,
                () =>
                {
                    foreach (KeyValuePair<string, Property> kvp in Owner.GetProperties(entityType))
                        if (String.Compare(kvp.Value.Column.Name, _name, StringComparison.OrdinalIgnoreCase) == 0)
                            return kvp.Value;

                    if (throwIfNotFound)
                        throw new InvalidOperationException(String.Format("未能在 {0} 中检索出 {1}", entityType.FullName, _name));
                    return null;
                }, false);
        }

        #endregion
    }
}