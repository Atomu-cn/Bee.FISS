using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Phenix.Core.Data;
using Phenix.Core.Data.Common;
using Phenix.Core.Data.Validation;
using Phenix.Core.Mapper.DataAnnotations;
using Phenix.Core.Mapper.Expressions;
using Phenix.Core.Reflection;
using Phenix.Core.Security;
using Phenix.Core.SyncCollections;

namespace Phenix.Core.Mapper.Schema
{
    /// <summary>
    /// 单子
    /// </summary>
    [Serializable]
    public abstract class Sheet
    {
        private Sheet(string name, string description)
        {
            _name = name;
            _description = description != null ? SqlHelper.ClearSpare(SqlHelper.ClearComment(description)) : String.Empty;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        protected Sheet(string name, string description, IDictionary<string, Column> columns)
            : this(name, description)
        {
            Columns = columns;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        protected Sheet(MetaData owner, string name, string description)
            : this(name, description)
        {
            _owner = owner;
        }

        #region 属性

        [NonSerialized] private MetaData _owner;

        /// <summary>
        /// 所属数据库架构
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public virtual MetaData Owner
        {
            get { return _owner; }
            protected internal set
            {
                _owner = value;

                foreach (KeyValuePair<string, Column> kvp in _columns)
                    kvp.Value.Owner = this;
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

        private readonly string _description;

        /// <summary>
        /// 注释(中英文用‘|’分隔)
        /// Thread.CurrentThread.CurrentCulture.Name为非'zh-'时返回后半截
        /// </summary>
        public string Description
        {
            get { return AppRun.SplitCulture(_description); }
        }

        private IDictionary<string, Column> _columns;

        /// <summary>
        /// 字段清单
        /// </summary>
        public IDictionary<string, Column> Columns
        {
            get { return _columns; }
            internal set { _columns = new ReadOnlyDictionary<string, Column>(new Dictionary<string, Column>(value, StringComparer.OrdinalIgnoreCase)); }
        }

        [NonSerialized] private IList<Column> _primaryKeyColumns;

        /// <summary>
        /// 主键字段清单
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public IList<Column> PrimaryKeyColumns
        {
            get
            {
                if (_primaryKeyColumns == null)
                {
                    List<Column> result = new List<Column>();
                    foreach (Column item in GetColumns(true, false))
                        if (item.TableColumn.IsPrimaryKey)
                            result.Add(item);
                    _primaryKeyColumns = result.AsReadOnly();
                }

                return _primaryKeyColumns;
            }
        }

        [NonSerialized] private IList<Column> _foreignKeyColumns;

        /// <summary>
        /// 外键字段清单
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public IList<Column> ForeignKeyColumns
        {
            get
            {
                if (_foreignKeyColumns == null)
                {
                    List<Column> result = new List<Column>();
                    foreach (KeyValuePair<string, Column> kvp in Columns)
                        if (kvp.Value.ForeignKey != null)
                            result.Add(kvp.Value);
                    _foreignKeyColumns = result.AsReadOnly();
                }

                return _foreignKeyColumns;
            }
        }

        [NonSerialized] private IList<Column> _originateTeamsColumns;

        /// <summary>
        /// 制单团体字段清单
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public IList<Column> OriginateTeamsColumns
        {
            get
            {
                if (_originateTeamsColumns == null)
                {
                    List<Column> result = new List<Column>();
                    foreach (Column item in GetColumns(true, false))
                        if (item.TableColumn.IsOriginateTeamsColumn)
                            result.Add(item);
                    _originateTeamsColumns = result.AsReadOnly();
                }

                return _originateTeamsColumns;
            }
        }

        [NonSerialized] private IList<Column> _timestampColumns;

        /// <summary>
        /// "时间戳"字段清单
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public IList<Column> TimestampColumns
        {
            get
            {
                if (_timestampColumns == null)
                {
                    List<Column> result = new List<Column>();
                    foreach (Column item in GetColumns(false, true))
                        if (item.TableColumn.IsTimestampColumn)
                            result.Add(item);
                    _timestampColumns = result.AsReadOnly();
                }

                return _timestampColumns;
            }
        }

        [NonSerialized] private IList<Column> _routeColumns;

        /// <summary>
        /// "HASH值路由增删改查数据库"字段清单
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public IList<Column> RouteColumns
        {
            get
            {
                if (_routeColumns == null)
                {
                    List<Column> result = new List<Column>();
                    foreach (Column item in GetColumns(true, false))
                        if (item.TableColumn.IsRouteColumn)
                            result.Add(item);
                    _routeColumns = result.AsReadOnly();
                }

                return _routeColumns;
            }
        }

        /// <summary>
        /// 类名
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public abstract string ClassName { get; set; }

        /// <summary>
        /// 前缀
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public abstract string Prefix { get; }

        [NonSerialized] private readonly SynchronizedDictionary<string, ReadOnlyDictionary<string, Field>> _classFieldsCache =
            new SynchronizedDictionary<string, ReadOnlyDictionary<string, Field>>(StringComparer.Ordinal);

        [NonSerialized] private readonly SynchronizedDictionary<string, ReadOnlyDictionary<string, Property>> _classPropertiesCache =
            new SynchronizedDictionary<string, ReadOnlyDictionary<string, Property>>(StringComparer.Ordinal);

        [NonSerialized] private readonly SynchronizedDictionary<string, Property> _primaryKeyPropertyCache =
            new SynchronizedDictionary<string, Property>(StringComparer.Ordinal);

        [NonSerialized] private readonly SynchronizedDictionary<string, Column[]> _columnsCache =
            new SynchronizedDictionary<string, Column[]>(StringComparer.Ordinal);

        [NonSerialized] private ReadOnlyDictionary<int, Sheet> _handles;

        /// <summary>
        /// 实际操作的单子(0为主库序号，1-N为分库序号)
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public IDictionary<int, Sheet> Handles
        {
            get
            {
                if (_handles == null)
                {
                    CheckDatabaseValidity();

                    Dictionary<int, Sheet> result = new Dictionary<int, Sheet>(Owner.Database.Handles.Count);
                    foreach (KeyValuePair<int, Database> kvp in Owner.Database.Handles)
                        result.Add(kvp.Key, MetaData.Fetch(kvp.Value).FindSheet(Name, true));
                    _handles = new ReadOnlyDictionary<int, Sheet>(result);
                }

                return _handles;
            }
        }

        #endregion

        #region 方法

        private void CheckDatabaseValidity()
        {
            if (Owner == null || Owner.Database == null)
            {
                MethodBase method = new StackTrace().GetFrame(1).GetMethod();
                throw new InvalidOperationException(String.Format("请通过数据库入口 Database 的 MetaData 属性逐级调用到 {0} 的 {1} 函数", (method.ReflectedType ?? method.DeclaringType).FullName, method.Name));
            }
        }

        /// <summary>
        /// 获取实际操作的单子
        /// </summary>
        /// <param name="routeKey">路由键</param>
        /// <returns>Handles[routeKey != null ? Math.Abs(routeKey.GetHashCode()) % (Handles.Count + 1) : 0]</returns>
        public Sheet GetHandle(object routeKey)
        {
            return Handles[routeKey != null ? Math.Abs(routeKey.GetHashCode()) % (Handles.Count + 1) : 0];
        }

        /// <summary>
        /// 获取实际操作的单子(取实体的路由字段值作为路由键)
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns>Handles[routeKey != null ? Math.Abs(routeKey.GetHashCode()) % (Handles.Count + 1) : 0]</returns>
        public Sheet GetHandle<T>(T entity)
            where T : class
        {
            return entity != null ? GetHandle(RouteColumns.Count > 0 ? RouteColumns[0].GetProperty(Utilities.LoadType(entity)).GetValue(entity) : null) : this;
        }

        #region Field

        /// <summary>
        /// 获取数据映射类字段(均映射的是遍历类时遇到的第一个符合条件的表)
        /// </summary>
        /// <param name="entityType">实体类</param>
        /// <param name="targetTable">所属表</param>
        /// <param name="isWatermarkColumn">是否仅在insert时被提交</param>
        /// <param name="overwritingOnUpdate">是否要在Update时被覆盖</param>
        /// <returns>类字段名-类字段</returns>
        public IDictionary<string, Field> GetFields(Type entityType, Sheet targetTable = null, bool? isWatermarkColumn = null, bool? overwritingOnUpdate = null)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            return targetTable == null && !isWatermarkColumn.HasValue && !overwritingOnUpdate.HasValue
                ? Field.Fetch(entityType, this)
                : _classFieldsCache.GetValue(Standards.FormatCompoundKey(entityType.FullName, targetTable != null ? targetTable.Name : null, isWatermarkColumn, overwritingOnUpdate),
                    () =>
                    {
                        Dictionary<string, Field> value = new Dictionary<string, Field>(StringComparer.Ordinal);
                        foreach (KeyValuePair<string, Field> kvp in GetFields(entityType))
                        {
                            if (kvp.Value.Column.TableColumn == null)
                                continue;
                            if (targetTable != null && !object.Equals(kvp.Value.Column.TableColumn.Owner, targetTable))
                                continue;

                            if (!isWatermarkColumn.HasValue && !overwritingOnUpdate.HasValue ||
                                isWatermarkColumn.HasValue && isWatermarkColumn.Value == kvp.Value.Column.TableColumn.IsWatermarkColumn ||
                                overwritingOnUpdate.HasValue && overwritingOnUpdate.Value == kvp.Value.Column.TableColumn.OverwritingOnUpdate)
                            {
                                if (targetTable == null)
                                    targetTable = kvp.Value.Column.TableColumn.Owner;
                                value.Add(kvp.Key, kvp.Value);
                            }
                        }

                        return new ReadOnlyDictionary<string, Field>(value);
                    }, false);
        }

        /// <summary>
        /// 获取数据映射类字段
        /// </summary>
        /// <param name="entityType">实体类</param>
        /// <param name="fieldName">字段名</param>
        /// <param name="throwIfNotFound">如果为 true, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>类字段</returns>
        public Field GetField(Type entityType, string fieldName, bool throwIfNotFound = true)
        {
            if (fieldName == null)
                throw new ArgumentNullException(nameof(fieldName));

            if (GetFields(entityType).TryGetValue(fieldName, out Field result))
                return result;

            if (throwIfNotFound)
                throw new InvalidOperationException(String.Format("类 {0} 不存在名称为 {1} 的字段", entityType.FullName, fieldName));
            return null;
        }

        #endregion

        #region Property

        /// <summary>
        /// 获取数据映射类属性(均映射的是遍历类时遇到的第一个符合条件的表)
        /// </summary>
        /// <param name="entityType">实体类</param>
        /// <param name="targetTable">所属表</param>
        /// <param name="isWatermarkColumn">是否仅在insert时被提交</param>
        /// <param name="overwritingOnUpdate">是否要在Update时被覆盖</param>
        /// <returns>类字段名-类字段</returns>
        public IDictionary<string, Property> GetProperties(Type entityType, Sheet targetTable = null, bool? isWatermarkColumn = null, bool? overwritingOnUpdate = null)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            return targetTable == null && !isWatermarkColumn.HasValue && !overwritingOnUpdate.HasValue
                ? Property.Fetch(entityType, this)
                : _classPropertiesCache.GetValue(Standards.FormatCompoundKey(entityType.FullName, targetTable != null ? targetTable.Name : null, isWatermarkColumn, overwritingOnUpdate),
                    () =>
                    {
                        Dictionary<string, Property> value = new Dictionary<string, Property>(StringComparer.Ordinal);
                        foreach (KeyValuePair<string, Property> kvp in GetProperties(entityType))
                        {
                            if (kvp.Value.Column.TableColumn == null)
                                continue;
                            if (targetTable != null && !object.Equals(kvp.Value.Column.TableColumn.Owner, targetTable))
                                continue;

                            if (!isWatermarkColumn.HasValue && !overwritingOnUpdate.HasValue ||
                                isWatermarkColumn.HasValue && isWatermarkColumn.Value == kvp.Value.Column.TableColumn.IsWatermarkColumn ||
                                overwritingOnUpdate.HasValue && overwritingOnUpdate.Value == kvp.Value.Column.TableColumn.OverwritingOnUpdate)
                            {
                                if (targetTable == null)
                                    targetTable = kvp.Value.Column.TableColumn.Owner;
                                value.Add(kvp.Key, kvp.Value);
                            }
                        }

                        return new ReadOnlyDictionary<string, Property>(value);
                    }, false);
        }

        /// <summary>
        /// 获取数据映射类属性
        /// </summary>
        /// <param name="propertyLambda">含类属性的 lambda 表达式</param>
        /// <param name="throwIfNotFound">如果为 true, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>类属性</returns>
        public Property GetProperty<T>(Expression<Func<T, object>> propertyLambda, bool throwIfNotFound = true)
            where T : class
        {
            if (propertyLambda == null)
                throw new ArgumentNullException(nameof(propertyLambda));

            PropertyInfo propertyInfo = Utilities.GetPropertyInfo(propertyLambda, throwIfNotFound);
            if (propertyInfo != null)
                return GetProperty(typeof(T), propertyInfo.Name, throwIfNotFound);

            if (throwIfNotFound)
                throw new InvalidOperationException(String.Format("{0} 应该是类 {1} 某个属性的表达式", propertyLambda.Name, typeof(T).FullName));
            return null;
        }

        /// <summary>
        /// 获取数据映射类属性
        /// </summary>
        /// <param name="entityType">实体类</param>
        /// <param name="propertyName">属性名</param>
        /// <param name="throwIfNotFound">如果为 true, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>类属性</returns>
        public Property GetProperty(Type entityType, string propertyName, bool throwIfNotFound = true)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));
            if (propertyName == null)
                throw new ArgumentNullException(nameof(propertyName));

            if (String.CompareOrdinal(propertyName, "PrimaryKeyLong") == 0)
                return GetPrimaryKeyProperty(entityType, throwIfNotFound);
            if (GetProperties(entityType).TryGetValue(propertyName, out Property result))
                return result;

            if (throwIfNotFound)
                throw new InvalidOperationException(String.Format("未能在 {0} 中检索出映射表字段的 {1} 属性", entityType.FullName, propertyName));
            return null;
        }

        /// <summary>
        /// 检索主键表字段映射类属性
        /// </summary>
        /// <param name="entityType">实体类</param>
        /// <param name="throwIfNotFound">如果为 true, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>类属性</returns>
        public Property GetPrimaryKeyProperty(Type entityType, bool throwIfNotFound = true)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            return _primaryKeyPropertyCache.GetValue(entityType.FullName,
                () =>
                {
                    SheetAttribute sheetAttribute = (SheetAttribute)Attribute.GetCustomAttribute(entityType, typeof(SheetAttribute));
                    foreach (KeyValuePair<string, Property> kvp in GetProperties(entityType))
                        if (kvp.Value.Column.IsPrimaryKey)
                            if (sheetAttribute == null || String.Compare(sheetAttribute.PrimaryKeyName, kvp.Value.Column.Name, StringComparison.OrdinalIgnoreCase) == 0)
                                return kvp.Value;


                    if (throwIfNotFound)
                        throw new InvalidOperationException(String.Format("未能在 {0} 中检索出主键", entityType.FullName));
                    return null;
                }, false);
        }

        #endregion

        #region Column

        /// <summary>
        /// 检索字段
        /// </summary>
        /// <param name="name">字段名/类属性名/类字段名</param>
        /// <param name="throwIfNotFound">如果为 true, 则会在找不到信息时引发 InvalidOperationException; 如果为 false, 则在找不到信息时返回 null</param>
        /// <returns>字段</returns>
        public Column FindColumn(string name, bool throwIfNotFound = false)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            if (Columns.TryGetValue(name, out Column result))
                return result;
            foreach (KeyValuePair<string, Column> kvp in Columns)
                if (String.Compare(kvp.Value.PropertyName, name, StringComparison.OrdinalIgnoreCase) == 0 ||
                    String.Compare(kvp.Value.FieldName, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return kvp.Value;

            if (throwIfNotFound)
                throw new InvalidOperationException(String.Format("未能在 {0} 中检索出 {1} 字段", Name, name));
            return null;
        }

        /// <summary>
        /// 获取字段清单
        /// </summary>
        /// <param name="isWatermarkColumn">是否仅在insert时被提交</param>
        /// <param name="overwritingOnUpdate">是否要在Update时被覆盖</param>
        public Column[] GetColumns(bool isWatermarkColumn, bool overwritingOnUpdate)
        {
            return _columnsCache.GetValue(Standards.FormatCompoundKey(isWatermarkColumn, overwritingOnUpdate),
                () =>
                {
                    List<Column> value = new List<Column>();
                    foreach (KeyValuePair<string, Column> kvp in Columns)
                    {
                        if (kvp.Value.TableColumn == null)
                            continue;

                        if (isWatermarkColumn && kvp.Value.TableColumn.IsWatermarkColumn ||
                            overwritingOnUpdate && kvp.Value.TableColumn.OverwritingOnUpdate)
                            value.Add(kvp.Value);
                    }

                    return value.ToArray();
                });
        }

        #endregion

        #region Validate

        /// <summary>
        /// 核对有效性
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="executeAction">执行动作</param>
        /// <param name="propertyValues">属性值队列</param>
        public void Validate<T>(T entity, ExecuteAction executeAction, IDictionary<string, object> propertyValues = null)
            where T : class
        {
            if (entity is IValidation validation)
                Validate(validation, executeAction, propertyValues);
            if (entity is IValidatableObject validationObject)
                Validate(validationObject, propertyValues);
        }

        private void Validate(IValidation entity, ExecuteAction executeAction, IDictionary<string, object> propertyValues)
        {
            Dictionary<object, object> items = null;
            if (propertyValues != null)
            {
                items = new Dictionary<object, object>();
                foreach (KeyValuePair<string, object> kvp in propertyValues)
                    items.Add(kvp.Key, kvp.Value);
            }

            System.ComponentModel.DataAnnotations.ValidationResult result = entity.Validate(executeAction, new ValidationContext(entity, items));
            if (result != null && !String.IsNullOrEmpty(result.ErrorMessage))
                throw new Phenix.Core.Data.Validation.ValidationException(null, 0, result, null, entity);
        }

        private void Validate(IValidatableObject entity, IDictionary<string, object> propertyValues)
        {
            Dictionary<object, object> items = null;
            if (propertyValues != null)
            {
                items = new Dictionary<object, object>();
                foreach (KeyValuePair<string, object> kvp in propertyValues)
                    items.Add(kvp.Key, kvp.Value);
            }

            foreach (System.ComponentModel.DataAnnotations.ValidationResult item in entity.Validate(new ValidationContext(entity, items)))
                if (item != null && !String.IsNullOrEmpty(item.ErrorMessage))
                    throw new Phenix.Core.Data.Validation.ValidationException(null, 0, item, null, entity);
        }

        #endregion

        #region FillReservedFields

        /// <summary>
        /// 填充保留字段
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="executeAction">执行动作</param>
        public T FillReservedFields<T>(T entity, ExecuteAction executeAction)
            where T : class
        {
            if (entity == null)
                return null;

            IIdentity identity = Principal.CurrentIdentity;
            bool isWatermarkColumn = executeAction == ExecuteAction.Insert;
            bool overwritingOnUpdate = executeAction == ExecuteAction.Insert || executeAction == ExecuteAction.Update;
            Type entityType = Utilities.LoadType(entity);
            foreach (KeyValuePair<string, Field> kvp in GetFields(entityType, GetPrimaryKeyProperty(entityType).Column.TableColumn.Owner, isWatermarkColumn, overwritingOnUpdate))
                if (isWatermarkColumn && kvp.Value.Column.TableColumn.IsPrimaryKey)
                {
                    if (kvp.Value.Column.TableColumn.MappingType == typeof(long) || kvp.Value.Column.TableColumn.MappingType == typeof(long?))
                    {
                        object value = kvp.Value.GetValue(entity);
                        if (value == null || (long)value == 0)
                            kvp.Value.Set(entity, Owner.Database.Sequence.Value);
                    }
                }
                else if (isWatermarkColumn && kvp.Value.Column.TableColumn.IsOriginatorColumn || kvp.Value.Column.TableColumn.IsUpdaterColumn)
                {
                    if (identity != null)
                        if (kvp.Value.Column.TableColumn.MappingType == typeof(string))
                            kvp.Value.Set(entity, identity.UserName);
                        else
                            kvp.Value.Set(entity, identity.Id);
                }
                else if (isWatermarkColumn && kvp.Value.Column.TableColumn.IsOriginateTimeColumn || kvp.Value.Column.TableColumn.IsUpdateTimeColumn)
                    kvp.Value.Set(entity, DateTime.Now);
                else if (isWatermarkColumn && kvp.Value.Column.TableColumn.IsOriginateTeamsColumn)
                {
                    if (identity != null)
                        if (kvp.Value.Column.TableColumn.MappingType == typeof(string))
                            kvp.Value.Set(entity, identity.CompanyName);
                        else
                            kvp.Value.Set(entity, identity.RootTeamsId);
                }
                else if (kvp.Value.Column.TableColumn.IsTimestampColumn)
                    kvp.Value.Set(entity, Owner.Database.Sequence.Value);

            return entity;
        }

        /// <summary>
        /// 填充保留属性
        /// </summary>
        /// <param name="entityType">实体类</param>
        /// <param name="propertyValues">属性值队列</param>
        /// <param name="executeAction">执行动作</param>
        public IDictionary<string, object> FillReservedProperties(Type entityType, IDictionary<string, object> propertyValues, ExecuteAction executeAction)
        {
            if (propertyValues == null)
                propertyValues = new Dictionary<string, object>();

            IIdentity identity = Principal.CurrentIdentity;
            bool isWatermarkColumn = executeAction == ExecuteAction.Insert;
            bool overwritingOnUpdate = executeAction == ExecuteAction.Insert || executeAction == ExecuteAction.Update;
            foreach (KeyValuePair<string, Property> kvp in GetProperties(entityType, GetPrimaryKeyProperty(entityType).Column.TableColumn.Owner, isWatermarkColumn, overwritingOnUpdate))
                if (isWatermarkColumn && kvp.Value.Column.TableColumn.IsPrimaryKey)
                {
                    if (kvp.Value.Column.TableColumn.MappingType == typeof(long) || kvp.Value.Column.TableColumn.MappingType == typeof(long?))
                    {
                        if (!propertyValues.TryGetValue(kvp.Key, out object value) || value == null || (long)value == 0)
                            propertyValues[kvp.Key] = Owner.Database.Sequence.Value;
                    }
                }
                else if (isWatermarkColumn && kvp.Value.Column.TableColumn.IsOriginatorColumn || kvp.Value.Column.TableColumn.IsUpdaterColumn)
                {
                    if (identity != null)
                        if (kvp.Value.Column.TableColumn.MappingType == typeof(string))
                            propertyValues[kvp.Key] = identity.UserName;
                        else
                            propertyValues[kvp.Key] = identity.Id;
                }
                else if (isWatermarkColumn && kvp.Value.Column.TableColumn.IsOriginateTimeColumn || kvp.Value.Column.TableColumn.IsUpdateTimeColumn)
                    propertyValues[kvp.Key] = DateTime.Now;
                else if (isWatermarkColumn && kvp.Value.Column.TableColumn.IsOriginateTeamsColumn)
                {
                    if (identity != null)
                        if (kvp.Value.Column.TableColumn.MappingType == typeof(string))
                            propertyValues[kvp.Key] = identity.CompanyName;
                        else
                            propertyValues[kvp.Key] = identity.RootTeamsId;
                }
                else if (kvp.Value.Column.TableColumn.IsTimestampColumn)
                    propertyValues[kvp.Key] = Owner.Database.Sequence.Value;

            return propertyValues;
        }

        #endregion

        #region AssembleInsertBody

        private void AssembleInsertBody(DbCommand command, List<Column> processedTableColumns, Sheet targetTable, ref StringBuilder bodyBuilder, ref StringBuilder valueBuilder)
        {
            IIdentity identity = Principal.CurrentIdentity;
            foreach (Column item in targetTable.GetColumns(true, true))
                if (!processedTableColumns.Contains(item))
                    if (item.IsPrimaryKey)
                    {
                        if (item.MappingType == typeof(long) || item.MappingType == typeof(long?))
                            AssembleInsertBody(command, item, Owner.Database.Sequence.Value, ref bodyBuilder, ref valueBuilder);
                    }
                    else if (item.IsOriginatorColumn || item.IsUpdaterColumn)
                    {
                        if (identity != null)
                            if (item.MappingType == typeof(string))
                                AssembleInsertBody(command, item, identity.UserName, ref bodyBuilder, ref valueBuilder);
                            else
                                AssembleInsertBody(command, item, identity.Id, ref bodyBuilder, ref valueBuilder);
                    }
                    else if (item.IsOriginateTimeColumn || item.IsUpdateTimeColumn)
                        AssembleInsertBody(command, item, DateTime.Now, ref bodyBuilder, ref valueBuilder);
                    else if (item.IsOriginateTeamsColumn)
                    {
                        if (identity != null)
                            if (item.MappingType == typeof(string))
                                AssembleInsertBody(command, item, identity.CompanyName, ref bodyBuilder, ref valueBuilder);
                            else
                                AssembleInsertBody(command, item, identity.RootTeamsId, ref bodyBuilder, ref valueBuilder);
                    }
                    else if (item.IsTimestampColumn)
                        AssembleInsertBody(command, item, Owner.Database.Sequence.Value, ref bodyBuilder, ref valueBuilder);
        }

        private void AssembleInsertBody(DbCommand command, Column tableColumn, object value, ref StringBuilder bodyBuilder, ref StringBuilder valueBuilder)
        {
            bodyBuilder.Append(tableColumn.Name);
            bodyBuilder.Append(',');
            DbParameter parameter = DbCommandHelper.CreateParameter(command, value);
            valueBuilder.Append(parameter.ParameterName);
            valueBuilder.Append(',');
        }

        #endregion

        #region AssembleUpdateBody

        private void AssembleUpdateBody(DbCommand command, List<Column> processedTableColumns, Sheet targetTable, ref StringBuilder bodyBuilder)
        {
            IIdentity identity = Principal.CurrentIdentity;
            foreach (Column item in targetTable.GetColumns(false, true))
                if (!processedTableColumns.Contains(item))
                    if (item.IsUpdaterColumn)
                    {
                        if (identity != null)
                            if (item.MappingType == typeof(string))
                                AssembleUpdateBody(command, item, identity.UserName, ref bodyBuilder);
                            else
                                AssembleUpdateBody(command, item, identity.Id, ref bodyBuilder);
                    }
                    else if (item.IsUpdateTimeColumn)
                        AssembleUpdateBody(command, item, DateTime.Now, ref bodyBuilder);
                    else if (item.IsTimestampColumn)
                        AssembleUpdateBody(command, item, Owner.Database.Sequence.Value, ref bodyBuilder);
        }

        private void AssembleUpdateBody(DbCommand command, Column tableColumn, OperationExpression operation, Sheet targetTable, ref StringBuilder bodyBuilder)
        {
            bodyBuilder.Insert(0, ",");
            bodyBuilder.Insert(0, AssembleComputeBody(command, operation, ExecuteAction.Update, ref targetTable, 0));
            bodyBuilder.Insert(0, "=");
            bodyBuilder.Insert(0, tableColumn.Name);
        }

        private void AssembleUpdateBody(DbCommand command, Column tableColumn, object value, ref StringBuilder bodyBuilder)
        {
            bodyBuilder.Insert(0, ",");
            DbParameter parameter = DbCommandHelper.CreateParameter(command, 0, value);
            bodyBuilder.Insert(0, parameter.ParameterName);
            bodyBuilder.Insert(0, "=");
            bodyBuilder.Insert(0, tableColumn.Name);
        }

        #endregion

        #region AssembleColumnBody

        private string AssembleColumnBody(IDictionary<string, Field> fields)
        {
            StringBuilder result = new StringBuilder();
            foreach (KeyValuePair<string, Field> kvp in fields)
            {
                result.Append(kvp.Value.Column.Name);
                result.Append(',');
            }

            return result.ToString().TrimEnd(',');
        }

        private string AssembleColumnBody(IDictionary<string, Property> properties)
        {
            StringBuilder result = new StringBuilder();
            foreach (KeyValuePair<string, Property> kvp in properties)
            {
                result.Append(kvp.Value.Column.Name);
                result.Append(',');
            }

            return result.ToString().TrimEnd(',');
        }

        #endregion

        #region AssembleOrderBody

        private string AssembleOrderBody<T>(OrderBy<T>[] orderBys, ref string uniqueColumnName)
            where T : class
        {
            if (orderBys == null || orderBys.Length == 0)
            {
                if (this is Table)
                    foreach (KeyValuePair<string, Field> kvp in GetFields(typeof(T)))
                        if (kvp.Value.Column.IsPrimaryKey || kvp.Value.Column.UniqueIndexes.Count == 1 && kvp.Value.Column.UniqueIndexes[0].ColumnNames.Length == 1)
                        {
                            uniqueColumnName = kvp.Value.Column.Name;
                            return kvp.Value.Column.Name;
                        }

                return null;
            }

            StringBuilder result = new StringBuilder();
            Column column = null;
            foreach (OrderBy<T> item in orderBys)
            {
                OrderBy orderBy = item;
                do
                {
                    column = GetProperty(typeof(T), orderBy.PropertyName).Column;
                    result.Insert(0, ",");
                    result.Insert(0, EnumKeyValue.Fetch(orderBy.Order).Key);
                    result.Insert(0, column.Name);
                    orderBy = orderBy.Prior;
                } while (orderBy != null);
            }

            if (orderBys.Length == 1 && orderBys[0].Prior == null && this is Table)
                if (column.IsPrimaryKey || column.UniqueIndexes.Count == 1 && column.UniqueIndexes[0].ColumnNames.Length == 1)
                    uniqueColumnName = column.Name;

            return result.ToString().TrimEnd(',');
        }

        #endregion

        #region AssembleWhereBody

        private string AssembleWhereBody(DbCommand command, string whereBody)
        {
            IIdentity identity = Principal.CurrentIdentity;
            if (identity != null)
                foreach (Column item in OriginateTeamsColumns)
                    whereBody = item.MappingType == typeof(string)
                        ? AssembleWhereBody(command, whereBody, item, identity.CompanyName)
                        : AssembleWhereBody(command, whereBody, item, identity.RootTeamsId);
            return whereBody;
        }

        private string AssembleWhereBody(DbCommand command, string whereBody, Sheet targetTable, Type entityType, object entity, out bool foundTimestamp)
        {
            foundTimestamp = false;
            foreach (KeyValuePair<string, Field> kvp in GetFields(entityType, targetTable))
                if (kvp.Value.Column.TableColumn.IsTimestampColumn)
                {
                    foundTimestamp = true;
                    whereBody = AssembleWhereBody(command, whereBody, kvp.Value.Column.TableColumn, kvp.Value.GetValue(entity));
                }

            return whereBody;
        }

        private string AssembleWhereBody(DbCommand command, string whereBody, Column tableColumn, object value)
        {
            DbParameter parameter = DbCommandHelper.CreateParameter(command, value);
            return String.IsNullOrEmpty(whereBody)
                ? String.Format(" {0}={1} ", tableColumn.Name, parameter.ParameterName)
                : String.Format("({0}) and {1}={2} ", whereBody, tableColumn.Name, parameter.ParameterName);
        }

        private string AssembleWhereBody(DbCommand command, CriteriaExpression criteriaExpression, object criteria, ExecuteAction executeAction, ref Sheet targetSheet)
        {
            if (criteria != null)
            {
                if (criteria is string serializedText)
                    criteria = Utilities.JsonDeserialize(serializedText);
                if (criteria is IDictionary<string, object> dictionary)
                {
                    Dictionary<string, Queue<object>> criteriaPropertyValues = new Dictionary<string, Queue<object>>();
                    foreach (KeyValuePair<string, object> kvp in dictionary)
                    {
                        string key = Standards.GetStandardPropertyName(kvp.Key);
                        if (!criteriaPropertyValues.TryGetValue(key, out Queue<object> values))
                        {
                            values = new Queue<object>();
                            criteriaPropertyValues.Add(key, values);
                        }

                        values.Enqueue(kvp.Value);
                    }

                    if (AssembleParamValues(criteriaExpression, ref criteriaPropertyValues))
                        criteriaExpression = CriteriaExpression.True;
                }
                else
                {
                    Dictionary<string, Queue<Property>> criteriaProperties = new Dictionary<string, Queue<Property>>();
                    foreach (KeyValuePair<string, Property> kvp in GetProperties(criteria.GetType()))
                    {
                        string key = Standards.GetStandardPropertyName(kvp.Key);
                        if (!criteriaProperties.TryGetValue(key, out Queue<Property> values))
                        {
                            values = new Queue<Property>();
                            criteriaProperties.Add(key, values);
                        }

                        values.Enqueue(kvp.Value);
                    }

                    if (AssembleParamValues(criteriaExpression, ref criteriaProperties, criteria))
                        criteriaExpression = CriteriaExpression.True;
                }
            }

            return AssembleWhereBody(command, criteriaExpression, executeAction, ref targetSheet);
        }

        private string AssembleWhereBody(DbCommand command, CriteriaExpression criteriaExpression, ExecuteAction executeAction, ref Sheet targetSheet)
        {
            switch (criteriaExpression.CriteriaExpressionType)
            {
                case CriteriaExpressionType.CriteriaLogical:
                    return String.Format("({0}{1}{2})",
                        criteriaExpression.Left != null ? AssembleWhereBody(command, criteriaExpression.Left, executeAction, ref targetSheet) : null,
                        criteriaExpression.Right != null ? EnumKeyValue.Fetch(criteriaExpression.Logical).Key : null,
                        criteriaExpression.Right != null ? AssembleWhereBody(command, criteriaExpression.Right, executeAction, ref targetSheet) : null);
                case CriteriaExpressionType.CriteriaOperate:
                    if (criteriaExpression.HaveValue)
                        return AssembleWhereBody(command,
                            AssembleComputeBody(command, criteriaExpression.LeftOperation, executeAction, ref targetSheet), criteriaExpression.CriteriaOperator,
                            criteriaExpression.ValueUnderlyingType, criteriaExpression.ValueCoreUnderlyingType, criteriaExpression.Value, criteriaExpression.OperateIgnoreCase);
                    else if (criteriaExpression.Right != null)
                        switch (criteriaExpression.CriteriaOperator)
                        {
                            case CriteriaOperator.In:
                            case CriteriaOperator.NotIn:
                                return String.Format("{0} {1}({2})",
                                    AssembleComputeBody(command, criteriaExpression.LeftOperation, executeAction, ref targetSheet),
                                    EnumKeyValue.Fetch(criteriaExpression.CriteriaOperator).Key,
                                    AssembleWhereBody(command, criteriaExpression.Right, executeAction, ref targetSheet));
                            default:
                                Sheet insideSheet = Owner.FindSheet(criteriaExpression.Right.OwnerType, true);
                                string insideComputeBody = insideSheet.AssembleComputeBody(command, criteriaExpression.LeftOperation, executeAction, ref targetSheet);
                                string insideSheetWhereBody = insideSheet.AssembleWhereBody(command, criteriaExpression.Right, executeAction, ref targetSheet);
                                return String.Format("select {0} from {1}{2}{3}", insideComputeBody, insideSheet.Name,
                                    !String.IsNullOrEmpty(insideSheetWhereBody) ? " where " : String.Empty, insideSheetWhereBody);
                        }
                    else
                        return AssembleWhereBody(
                            AssembleComputeBody(command, criteriaExpression.LeftOperation, executeAction, ref targetSheet), criteriaExpression.CriteriaOperator,
                            AssembleComputeBody(command, criteriaExpression.RightOperation, executeAction, ref targetSheet), criteriaExpression.OperateIgnoreCase);
                case CriteriaExpressionType.ExistsOrNotExists:
                    switch (criteriaExpression.CriteriaOperator)
                    {
                        case CriteriaOperator.Exists:
                        case CriteriaOperator.NotExists:
                            return String.Format("{0} and{1}({2})",
                                AssembleWhereBody(command, criteriaExpression.Left, executeAction, ref targetSheet),
                                EnumKeyValue.Fetch(criteriaExpression.CriteriaOperator).Key,
                                AssembleWhereBody(command, criteriaExpression.Right, executeAction, ref targetSheet));
                        default:
                            if (criteriaExpression.LeftOperation.HaveValue)
                            {
                                CriteriaExpression insideCriteriaExpression = (CriteriaExpression)criteriaExpression.LeftOperation.Value;
                                Sheet insideSheet = Owner.FindSheet(insideCriteriaExpression.OwnerType, true);
                                string insideSheetWhereBody = insideSheet.AssembleWhereBody(command, insideCriteriaExpression, executeAction, ref targetSheet);
                                return String.Format("select * from {0}{1}{2}", insideSheet.Name,
                                    !String.IsNullOrEmpty(insideSheetWhereBody) ? " where " : String.Empty, insideSheetWhereBody);
                            }
                            else
                            {
                                Table targetTable = targetSheet as Table;
                                if (targetTable == null)
                                    throw new InvalidOperationException(String.Format("执行 {0} 条件不充分", criteriaExpression.CriteriaOperator));
                                if (targetTable.PrimaryKeys.Length != 1)
                                    throw new InvalidOperationException(String.Format("仅允许主表 {0} 定义一个主键字段({1})", targetTable.Name, targetTable.PrimaryKeys.Length));
                                Column detailColumn = Owner.FindSheet(criteriaExpression.LeftOperation.OwnerType, true).GetProperty(criteriaExpression.LeftOperation.OwnerType, criteriaExpression.LeftOperation.MemberInfo.Name).Column;
                                return String.Format("select {0} from {1} where {0} = {2}", detailColumn.Name, detailColumn.Owner.Name, targetTable.PrimaryKeys[0]);
                            }
                    }
                case CriteriaExpressionType.Short:
                    return criteriaExpression.ShortValue ? "(1=1)" : "(1=0)";
            }

            return null;
        }

        private string AssembleWhereBody(DbCommand command, string computeBody, CriteriaOperator criteriaOperator, Type valueUnderlyingType, Type valueCoreUnderlyingType, object value, bool operateIgnoreCase)
        {
            switch (criteriaOperator)
            {
                case CriteriaOperator.Embed:
                    if (value == null)
                        break;
                    return value.ToString();
                case CriteriaOperator.Equal:
                case CriteriaOperator.Greater:
                case CriteriaOperator.GreaterOrEqual:
                case CriteriaOperator.Lesser:
                case CriteriaOperator.LesserOrEqual:
                    if (value != null)
                    {
                        DbParameter parameter = DbCommandHelper.CreateParameter(command, value);
                        return operateIgnoreCase && valueUnderlyingType == typeof(string)
                            ? String.Format(" upper({0}){1}upper({2}) ", computeBody, EnumKeyValue.Fetch(criteriaOperator).Key, parameter.ParameterName)
                            : String.Format(" {0}{1}{2} ", computeBody, EnumKeyValue.Fetch(criteriaOperator).Key, parameter.ParameterName);
                    }

                    if (valueUnderlyingType == null)
                        return String.Format(criteriaOperator == CriteriaOperator.Equal ? " {0} is null " : " {0} is not null ", computeBody);
                    break;
                case CriteriaOperator.Unequal:
                    if (value != null)
                    {
                        DbParameter parameter = DbCommandHelper.CreateParameter(command, value);
                        return operateIgnoreCase && valueUnderlyingType == typeof(string)
                            ? String.Format("({0} is null or upper({0}){1}upper({2}))", computeBody, EnumKeyValue.Fetch(criteriaOperator).Key, parameter.ParameterName)
                            : String.Format("({0} is null or {0}{1}{2})", computeBody, EnumKeyValue.Fetch(criteriaOperator).Key, parameter.ParameterName);
                    }

                    if (valueUnderlyingType == null)
                        return String.Format(" {0} is not null ", computeBody);
                    break;
                case CriteriaOperator.Like:
                case CriteriaOperator.LikeLeft:
                case CriteriaOperator.LikeRight:
                case CriteriaOperator.Unlike:
                    if (value != null)
                        if (valueUnderlyingType.IsEnum)
                        {
                            FlagsAttribute attribute = (FlagsAttribute)Attribute.GetCustomAttribute(valueUnderlyingType, typeof(FlagsAttribute));
                            if (attribute == null)
                                throw new InvalidOperationException(String.Format("请为枚举 {0} 打上 System.FlagsAttribute 标签才能实现 {1} 功能", valueUnderlyingType, criteriaOperator));
                            DbParameter parameter1 = DbCommandHelper.CreateParameter(command, value);
                            DbParameter parameter2 = DbCommandHelper.CreateParameter(command, value);
#if PgSQL
                            return String.Format(criteriaOperator != CriteriaOperator.Unlike ? " {0} & {1} == {2} " : " {0} & {1} <> {2} ", computeBody, parameter1.ParameterName, parameter2.ParameterName);
#endif
#if MsSQL
                            return String.Format(criteriaOperator != CriteriaOperator.Unlike ? " {0} & {1} == {2} " : " {0} & {1} <> {2} ", computeBody, parameter1.ParameterName, parameter2.ParameterName);
#endif
#if MySQL
                            return String.Format(criteriaOperator != CriteriaOperator.Unlike ? " {0} & {1} == {2} " : " {0} & {1} <> {2} ", computeBody, parameter1.ParameterName, parameter2.ParameterName);
#endif
#if ORA
                            return String.Format(criteriaOperator != CriteriaOperator.Unlike ? " BITAND({0}, {1}) == {2} " : " BITAND({0}, {1}) <> {2} ", computeBody, parameter1.ParameterName, parameter2.ParameterName);
#endif
                        }
                        else
                        {
                            string s = value.ToString();
                            if (!String.IsNullOrEmpty(s))
                            {
                                DbParameter parameter = DbCommandHelper.CreateParameter(command, s.Contains('%') ? s :
                                    criteriaOperator == CriteriaOperator.LikeLeft ? String.Format("{0}%", s) :
                                    criteriaOperator == CriteriaOperator.LikeRight ? String.Format("%{0}", s) :
                                    String.Format("%{0}%", s));
                                return String.Format(operateIgnoreCase ? " upper({0}){1}upper({2}) " : " {0}{1}{2} ", computeBody, EnumKeyValue.Fetch(criteriaOperator).Key, parameter.ParameterName);
                            }
                        }

                    if (valueUnderlyingType == null)
                        return String.Format(criteriaOperator != CriteriaOperator.Unlike ? " {0} is null " : " {0} is not null ", computeBody);
                    break;
                case CriteriaOperator.IsNull:
                    if (valueUnderlyingType != null && valueUnderlyingType == typeof(bool))
                    {
                        if (value == null)
                            break;
                        if (!(bool)value)
                            return String.Format(" {0} is not null ", computeBody);
                    }

                    return String.Format(" {0} is null ", computeBody);
                case CriteriaOperator.IsNotNull:
                    if (valueUnderlyingType != null && valueUnderlyingType == typeof(bool))
                    {
                        if (value == null)
                            break;
                        if (!(bool)value)
                            return String.Format(" {0} is null ", computeBody);
                    }

                    return String.Format(" {0} is not null ", computeBody);
                case CriteriaOperator.In:
                case CriteriaOperator.NotIn:
                    if (value == null)
                        break;
                    if (valueCoreUnderlyingType != valueUnderlyingType)
                    {
                        object[] items = ((IEnumerable)value).Cast<object>().ToArray();
                        if (items.Length > 500 || command.Parameters.Count > 500 || items.Length + command.Parameters.Count > 500) //ORA-01795: 列表中的最大表达式数为 1000
                        {
                            if (valueCoreUnderlyingType == typeof(DateTime))
#if PgSQL
                                computeBody = String.Format("to_char({0},'YYYYMMDDHH24MISS')", computeBody);
#endif
#if MsSQL
                                computeBody = String.Format("replace(replace(replace(convert(varchar, {0}, 120),'-',''),' ',''),':','')", computeBody);
#endif
#if MySQL
                                computeBody = String.Format("DATE_FORMAT({0},'%Y%m%d%H%i%s')", computeBody);
#endif
#if ORA
                                computeBody = String.Format("to_char({0},'yyyymmddhh24miss')", computeBody);
#endif
                            string result = String.Empty;
                            int i = 0;
                            string s = String.Empty;
                            foreach (object item in items)
                            {
                                if (item != null && valueCoreUnderlyingType == typeof(string))
                                    s = String.Format("{0}'{1}',", s, item);
                                else if (item != null && valueCoreUnderlyingType == typeof(DateTime))
                                    s = String.Format("{0}'{1}',", s, ((DateTime)item).ToString("yyyyMMddHHmmss"));
                                else
                                {
                                    object dbValue = Utilities.ConvertToDbValue(item);
                                    s = String.Format("{0}{1},", s, dbValue == DBNull.Value ? Standards.Null : dbValue.ToString());
                                }

                                i = i + 1;
                                if (i == 1000)
                                {
                                    result = String.Format("{0} {1}{2}({3}) {4} ", result, computeBody, EnumKeyValue.Fetch(criteriaOperator).Key, s.TrimEnd(','), criteriaOperator == CriteriaOperator.In ? "or" : "and");
                                    i = 0;
                                    s = String.Empty;
                                }
                            }

                            if (i > 0)
                                result = String.Format("{0} {1}{2}({3}) {4} ", result, computeBody, EnumKeyValue.Fetch(criteriaOperator).Key, s.TrimEnd(','), criteriaOperator == CriteriaOperator.In ? "or" : "and");
                            return String.IsNullOrEmpty(result)
                                ? String.Format("(1={0})", criteriaOperator == CriteriaOperator.In ? "0" : "1")
                                : String.Format("({0})", result.Remove(result.TrimEnd().LastIndexOf(' ')));
                        }
                        else
                        {
                            StringBuilder whereBuilder = new StringBuilder();
                            switch (criteriaOperator)
                            {
                                case CriteriaOperator.In:
                                    foreach (object item in items)
                                        if (item != null)
                                        {
                                            DbParameter parameter = DbCommandHelper.CreateParameter(command, item);
                                            whereBuilder.AppendFormat(" or {0}={1}", computeBody, parameter.ParameterName);
                                        }
                                        else
                                            whereBuilder.AppendFormat(" or {0} is null", computeBody);

                                    if (whereBuilder.Length > 0)
                                    {
                                        string result = whereBuilder.ToString().Trim();
                                        return String.Format("({0})", result.Remove(0, result.IndexOf(' ')));
                                    }

                                    return "(1=0)";
                                case CriteriaOperator.NotIn:
                                    foreach (object item in items)
                                    {
                                        if (item != null)
                                        {
                                            DbParameter parameter = DbCommandHelper.CreateParameter(command, item);
                                            whereBuilder.AppendFormat(" and {0}<>{1}", computeBody, parameter.ParameterName);
                                        }
                                        else
                                            whereBuilder.AppendFormat(" and {0} is not null", computeBody);
                                    }

                                    if (whereBuilder.Length > 0)
                                    {
                                        string result = whereBuilder.ToString().Trim();
                                        return String.Format("({0})", result.Remove(0, result.IndexOf(' ')));
                                    }

                                    return "(1=1)";
                            }
                        }
                    }
                    else
                    {
                        object dbValue = Utilities.ConvertToDbValue(value);
                        return String.Format(" {0}{1}({2}) ", computeBody, EnumKeyValue.Fetch(criteriaOperator).Key, dbValue == DBNull.Value ? Standards.Null : dbValue.ToString());
                    }

                    break;
            }

            return String.Empty;
        }

        private string AssembleWhereBody(string leftComputeBody, CriteriaOperator criteriaOperator, string rightComputeBody, bool operateIgnoreCase)
        {
            switch (criteriaOperator)
            {
                case CriteriaOperator.Equal:
                case CriteriaOperator.Greater:
                case CriteriaOperator.GreaterOrEqual:
                case CriteriaOperator.Lesser:
                case CriteriaOperator.LesserOrEqual:
                case CriteriaOperator.Unequal:
                case CriteriaOperator.Like:
                case CriteriaOperator.LikeLeft:
                case CriteriaOperator.LikeRight:
                case CriteriaOperator.Unlike:
                    return String.Format(operateIgnoreCase ? " upper({0}){1}upper({2}) " : " {0}{1}{2} ", leftComputeBody, EnumKeyValue.Fetch(criteriaOperator).Key, rightComputeBody);
                case CriteriaOperator.IsNull:
                    return String.Format(" {0} is null ", leftComputeBody);
                case CriteriaOperator.IsNotNull:
                    return String.Format(" {0} is not null ", leftComputeBody);
            }

            return String.Empty;
        }

        private string AssembleComputeBody(DbCommand command, OperationExpression operation, ExecuteAction executeAction, ref Sheet targetSheet, int? parameterIndex = null)
        {
            if (operation == null)
                return String.Empty;

            switch (operation.Sign)
            {
                case OperationSign.None:
                    if (operation.HaveValue)
                    {
                        DbParameter parameter = DbCommandHelper.CreateParameter(command, parameterIndex, operation.Value);
                        return String.Format(" {0} ", parameter.ParameterName);
                    }
                    else
                    {
                        Column column = GetProperty(operation.OwnerType, operation.MemberName).Column;
                        if (executeAction != ExecuteAction.None)
                            column = column.TableColumn;
                        if (targetSheet == null)
                            targetSheet = column.Owner;
                        else if (executeAction != ExecuteAction.None && !object.Equals(targetSheet, column.Owner))
                            throw new InvalidOperationException(String.Format("不允许同时 {0} 多个表({1},{2})的记录", executeAction, targetSheet.Name, column.Owner.Name));
                        return column.Name;
                    }

                case OperationSign.Add:
                case OperationSign.Subtract:
                case OperationSign.Multiply:
                case OperationSign.Divide:
                    string sign = operation.ValueType == typeof(string) ? " || " : EnumKeyValue.Fetch(operation.Sign).Key;
                    if (operation.HaveValue)
                    {
                        DbParameter parameter = DbCommandHelper.CreateParameter(command, parameterIndex, operation.Value);
                        return operation.LeftOperation != null
                            ? String.Format(" {0}{1}{2} ", AssembleComputeBody(command, operation.LeftOperation, executeAction, ref targetSheet, parameterIndex), sign, parameter.ParameterName)
                            : String.Format(" {0}{1}{2} ", parameter.ParameterName, sign, AssembleComputeBody(command, operation.RightOperation, executeAction, ref targetSheet, parameterIndex));
                    }
                    else
                        return String.Format(" {0}{1}{2} ", AssembleComputeBody(command, operation.LeftOperation, executeAction, ref targetSheet, parameterIndex), sign, AssembleComputeBody(command, operation.RightOperation, executeAction, ref targetSheet, parameterIndex));

                case OperationSign.Length:
                    if (operation.HaveValue)
                    {
                        DbParameter parameter = DbCommandHelper.CreateParameter(command, parameterIndex, operation.Value);
#if PgSQL
                        return String.Format(" CHAR_LENGTH({0})", parameter.ParameterName);
#endif
#if MsSQL
                        return String.Format(" LENGTH({0})", parameter.ParameterName);
#endif
#if MySQL
                        return String.Format(" CHAR_LENGTH({0})", parameter.ParameterName);
#endif
#if ORA
                        return String.Format(" LENGTH({0})", parameter.ParameterName);
#endif
                    }
                    else
#if PgSQL
                        return String.Format(" CHAR_LENGTH({0})", AssembleComputeBody(command, operation.RightOperation, executeAction, ref targetSheet, parameterIndex));
#endif
#if MsSQL
                        return String.Format(" LENGTH({0})", AssembleComputeBody(command, operation.RightOperation, executeAction, ref targetSheet, parameterIndex));
#endif
#if MySQL
                        return String.Format(" CHAR_LENGTH({0})", AssembleComputeBody(command, operation.RightOperation, executeAction, ref targetSheet, parameterIndex));
#endif
#if ORA
                        return String.Format(" LENGTH({0})", AssembleComputeBody(command, operation.RightOperation, executeAction, ref targetSheet, parameterIndex));
#endif
                case OperationSign.ToLower:
                case OperationSign.ToUpper:
                case OperationSign.TrimStart:
                case OperationSign.TrimEnd:
                case OperationSign.Trim:
                    if (operation.HaveValue)
                    {
                        DbParameter parameter = DbCommandHelper.CreateParameter(command, parameterIndex, operation.Value);
                        return String.Format("{0}({1})", EnumKeyValue.Fetch(operation.Sign).Key, parameter.ParameterName);
                    }
                    else
                        return String.Format("{0}({1})", EnumKeyValue.Fetch(operation.Sign).Key, AssembleComputeBody(command, operation.RightOperation, executeAction, ref targetSheet, parameterIndex));

                case OperationSign.Substring:
                    if (operation.Arguments == null || operation.Arguments.Length == 0)
                        if (operation.HaveValue)
                        {
                            DbParameter parameter = DbCommandHelper.CreateParameter(command, parameterIndex, operation.Value);
                            return String.Format(" {0} ", parameter.ParameterName);
                        }
                        else
                            return String.Format(" {0} ", AssembleComputeBody(command, operation.RightOperation, executeAction, ref targetSheet, parameterIndex));
                    else if (operation.Arguments.Length == 1)
                        if (operation.HaveValue)
                        {
                            DbParameter parameter = DbCommandHelper.CreateParameter(command, parameterIndex, operation.Value);
                            return String.Format("{0}({1},{2}+1)", EnumKeyValue.Fetch(operation.Sign).Key, parameter.ParameterName, AssembleComputeBody(command, operation.Arguments[0], executeAction, ref targetSheet, parameterIndex));
                        }
                        else
                            return String.Format("{0}({1},{2}+1)", EnumKeyValue.Fetch(operation.Sign).Key, AssembleComputeBody(command, operation.RightOperation, executeAction, ref targetSheet, parameterIndex), AssembleComputeBody(command, operation.Arguments[0], executeAction, ref targetSheet, parameterIndex));
                    else if (operation.Arguments.Length == 2)
                        if (operation.HaveValue)
                        {
                            DbParameter parameter = DbCommandHelper.CreateParameter(command, parameterIndex, operation.Value);
                            return String.Format("{0}({1},{2}+1,{3})", EnumKeyValue.Fetch(operation.Sign).Key, parameter.ParameterName, AssembleComputeBody(command, operation.Arguments[0], executeAction, ref targetSheet, parameterIndex), AssembleComputeBody(command, operation.Arguments[1], executeAction, ref targetSheet, parameterIndex));
                        }
                        else
                            return String.Format("{0}({1},{2}+1,{3})", EnumKeyValue.Fetch(operation.Sign).Key, AssembleComputeBody(command, operation.RightOperation, executeAction, ref targetSheet, parameterIndex), AssembleComputeBody(command, operation.Arguments[0], executeAction, ref targetSheet, parameterIndex), AssembleComputeBody(command, operation.Arguments[1], executeAction, ref targetSheet, parameterIndex));
                    else
                        throw new InvalidOperationException(String.Format("运算表达式 {0} 不允许有 {1} 个参数", operation.Sign, operation.Arguments.Length));
            }

            return String.Empty;
        }

        private bool AssembleParamValues(CriteriaExpression criteriaExpression, ref Dictionary<string, Queue<object>> criteriaPropertyValues)
        {
            if (criteriaExpression.Left != null)
                if (AssembleParamValues(criteriaExpression.Left, ref criteriaPropertyValues))
                    criteriaExpression.Left = CriteriaExpression.True;
            if (criteriaExpression.Right != null)
                if (AssembleParamValues(criteriaExpression.Right, ref criteriaPropertyValues))
                    criteriaExpression.Right = CriteriaExpression.True;

            if (criteriaExpression.RightOperation != null && criteriaExpression.RightOperation.Sign == OperationSign.None && !criteriaExpression.RightOperation.HaveValue &&
                (criteriaExpression.CriteriaOperator >= CriteriaOperator.Like && criteriaExpression.CriteriaOperator <= CriteriaOperator.NotIn ||
                 criteriaExpression.RightOperation.Equals(criteriaExpression.LeftOperation)))
                if (criteriaPropertyValues.TryGetValue(criteriaExpression.RightOperation.MemberName, out Queue<object> values) && values.Count > 0)
                {
                    object value = values.Dequeue();
                    if (value == null)
                        return true;
                    if (value is string s)
                    {
                        if (String.IsNullOrEmpty(s))
                            return true;
                        if (String.CompareOrdinal(s, Standards.IsNullSign) == 0)
                            value = null;
                    }

                    criteriaExpression.Value = Utilities.ChangeType(value, criteriaExpression.RightOperation.MemberType);
                }

            return false;
        }

        private bool AssembleParamValues(CriteriaExpression criteriaExpression, ref Dictionary<string, Queue<Property>> criteriaProperties, object criteria)
        {
            if (criteriaExpression.Left != null)
                if (AssembleParamValues(criteriaExpression.Left, ref criteriaProperties, criteria))
                    criteriaExpression.Left = CriteriaExpression.True;
            if (criteriaExpression.Right != null)
                if (AssembleParamValues(criteriaExpression.Right, ref criteriaProperties, criteria))
                    criteriaExpression.Right = CriteriaExpression.True;

            if (criteriaExpression.RightOperation != null && criteriaExpression.RightOperation.Sign == OperationSign.None && !criteriaExpression.RightOperation.HaveValue &&
                (criteriaExpression.CriteriaOperator >= CriteriaOperator.Like && criteriaExpression.CriteriaOperator <= CriteriaOperator.NotIn ||
                 criteriaExpression.RightOperation.Equals(criteriaExpression.LeftOperation)))
                if (criteriaProperties.TryGetValue(criteriaExpression.RightOperation.MemberName, out Queue<Property> values) && values.Count > 0)
                {
                    object value = values.Dequeue().GetValue(criteria);
                    if (value == null)
                        return true;
                    if (value is string s)
                    {
                        if (String.IsNullOrEmpty(s))
                            return true;
                        if (String.CompareOrdinal(s, Standards.IsNullSign) == 0)
                            value = null;
                    }

                    criteriaExpression.Value = Utilities.ChangeType(value, criteriaExpression.RightOperation.MemberType);
                }

            return false;
        }

        #endregion

        #region Apply

        /// <summary>
        /// 应用属性值
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段)</param>
        public void Apply<T>(T entity, params NameValue<T>[] propertyValues)
            where T : class
        {
            Apply(entity, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 应用属性值
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段)</param>
        public void Apply<T>(T entity, IDictionary<string, object> propertyValues)
            where T : class
        {
            if (entity == null || propertyValues == null)
                return;

            IDictionary<string, Property> properties = GetProperties(Utilities.LoadType(entity));
            foreach (KeyValuePair<string, object> kvp in propertyValues)
                if (properties.TryGetValue(kvp.Key, out Property property))
                    if (!property.Set(entity, kvp.Value, false))
                        if (property.Field != null)
                            property.Field.Set(entity, kvp.Value);
        }

        #endregion

        #region InsertOrUpdateEntity

        /// <summary>
        /// 新增记录如遇唯一键冲突则更新记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns>更新记录数</returns>
        public int InsertOrUpdateEntity<T>(T entity)
            where T : class
        {
            CheckDatabaseValidity();

            return GetHandle(entity).Owner.Database.ExecuteGet((Func<DbConnection, T, int>)InsertOrUpdateEntity, entity);
        }

        /// <summary>
        /// 新增记录如遇唯一键冲突则更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <returns>更新记录数</returns>
        public int InsertOrUpdateEntity<T>(DbConnection connection, T entity)
            where T : class
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(connection))
            {
                return InsertOrUpdateEntity(command, entity);
            }
        }

        /// <summary>
        /// 新增记录如遇唯一键冲突则更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <returns>更新记录数</returns>
        public int InsertOrUpdateEntity<T>(DbTransaction transaction, T entity)
            where T : class
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(transaction))
            {
                return InsertOrUpdateEntity(command, entity);
            }
        }

        /// <summary>
        /// 新增记录如遇唯一键冲突则更新记录
        /// </summary>
        /// <param name="command">DbCommand(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <returns>更新记录数</returns>
        public int InsertOrUpdateEntity<T>(DbCommand command, T entity)
            where T : class
        {
            try
            {
                return InsertEntity(command, entity);
            }
            catch (UniqueConstraintException)
            {
                return UpdateEntity(command, entity, null, null);
            }
        }

        #endregion

        #region InsertEntity

        /// <summary>
        /// 新增记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns>更新记录数</returns>
        public int InsertEntity<T>(T entity)
            where T : class
        {
            CheckDatabaseValidity();

            return GetHandle(entity).Owner.Database.ExecuteGet((Func<DbConnection, T, int>)InsertEntity, entity);
        }

        /// <summary>
        /// 新增记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <returns>更新记录数</returns>
        public int InsertEntity<T>(DbConnection connection, T entity)
            where T : class
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(connection))
            {
                return InsertEntity(command, entity);
            }
        }

        /// <summary>
        /// 新增记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <returns>更新记录数</returns>
        public int InsertEntity<T>(DbTransaction transaction, T entity)
            where T : class
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(transaction))
            {
                return InsertEntity(command, entity);
            }
        }

        /// <summary>
        /// 新增记录
        /// </summary>
        /// <param name="command">DbCommand(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <returns>更新记录数</returns>
        public int InsertEntity<T>(DbCommand command, T entity)
            where T : class
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            command.Parameters.Clear();

            if (entity == null)
                return 0;

            FillReservedFields(entity, ExecuteAction.Insert);
            Validate(entity, ExecuteAction.Insert);

            StringBuilder bodyBuilder = new StringBuilder();
            StringBuilder valueBuilder = new StringBuilder();
            Type entityType = Utilities.LoadType(entity);
            Sheet targetTable = GetPrimaryKeyProperty(entityType).Column.TableColumn.Owner;
            foreach (KeyValuePair<string, Property> kvp in GetProperties(entityType, targetTable))
            {
                object value = kvp.Value.Field != null ? kvp.Value.Field.GetValue(entity) : kvp.Value.GetValue(entity);
                kvp.Value.Validate(value, entity);
                AssembleInsertBody(command, kvp.Value.Column.TableColumn, value, ref bodyBuilder, ref valueBuilder);
            }

            command.CommandText = String.Format("insert into {0}({1})values({2})", targetTable.Name, bodyBuilder.ToString().TrimEnd(','), valueBuilder.ToString().TrimEnd(','));
            try
            {
                int result = DbCommandHelper.ExecuteNonQuery(command);
                if (entity is IEntity e)
                    e.SelfSheet = this;
                return result;
            }
            catch (Exception ex)
            {
                throw UniqueConstraintException.Refine(ex, this, entity);
            }
        }

        #endregion

        #region InsertRecord

        /// <summary>
        /// 新增记录(仅提交第一个属性映射的表记录)
        /// </summary>
        /// <param name="propertyValues">待更新属性值队列(仅提交第一个属性映射的表)</param>
        /// <returns>更新记录数</returns>
        public int InsertRecord<T>(params NameValue<T>[] propertyValues)
            where T : class
        {
            return InsertRecord<T>(NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 新增记录(仅提交第一个属性映射的表记录)
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="propertyValues">待更新属性值队列(仅提交第一个属性映射的表)</param>
        /// <returns>更新记录数</returns>
        public int InsertRecord<T>(DbConnection connection, params NameValue<T>[] propertyValues)
            where T : class
        {
            return InsertRecord<T>(connection, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 新增记录(仅提交第一个属性映射的表记录)
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="propertyValues">待更新属性值队列(仅提交第一个属性映射的表)</param>
        /// <returns>更新记录数</returns>
        public int InsertRecord<T>(DbTransaction transaction, params NameValue<T>[] propertyValues)
            where T : class
        {
            return InsertRecord<T>(transaction, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 新增记录(仅提交第一个属性映射的表记录)
        /// </summary>
        /// <param name="command">DbCommand(注意跨库风险未作校验)</param>
        /// <param name="propertyValues">待更新属性值队列(仅提交第一个属性映射的表)</param>
        /// <returns>更新记录数</returns>
        public int InsertRecord<T>(DbCommand command, params NameValue<T>[] propertyValues)
            where T : class
        {
            return InsertRecord<T>(command, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 新增记录(仅提交第一个属性映射的表记录)
        /// </summary>
        /// <param name="propertyValues">待更新属性值队列(仅提交第一个属性映射的表)</param>
        /// <returns>更新记录数</returns>
        public int InsertRecord<T>(IDictionary<string, object> propertyValues)
            where T : class
        {
            CheckDatabaseValidity();

            return Owner.Database.ExecuteGet((Func<DbConnection, IDictionary<string, object>, int>)InsertRecord<T>, propertyValues);
        }

        /// <summary>
        /// 新增记录(仅提交第一个属性映射的表记录)
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="propertyValues">待更新属性值队列(仅提交第一个属性映射的表)</param>
        /// <returns>更新记录数</returns>
        public int InsertRecord<T>(DbConnection connection, IDictionary<string, object> propertyValues)
            where T : class
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(connection))
            {
                return InsertRecord<T>(command, propertyValues);
            }
        }

        /// <summary>
        /// 新增记录(仅提交第一个属性映射的表记录)
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="propertyValues">待更新属性值队列(仅提交第一个属性映射的表)</param>
        /// <returns>更新记录数</returns>
        public int InsertRecord<T>(DbTransaction transaction, IDictionary<string, object> propertyValues)
            where T : class
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(transaction))
            {
                return InsertRecord<T>(command, propertyValues);
            }
        }

        /// <summary>
        /// 新增记录(仅提交第一个属性映射的表记录)
        /// </summary>
        /// <param name="command">DbCommand(注意跨库风险未作校验)</param>
        /// <param name="propertyValues">待更新属性值队列(仅提交第一个属性映射的表)</param>
        /// <returns>更新记录数</returns>
        public int InsertRecord<T>(DbCommand command, IDictionary<string, object> propertyValues)
            where T : class
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            command.Parameters.Clear();

            if (propertyValues == null || propertyValues.Count == 0)
                return 0;

            StringBuilder bodyBuilder = new StringBuilder();
            StringBuilder valueBuilder = new StringBuilder();
            Sheet targetTable = null;
            List<Column> processedTableColumns = new List<Column>(propertyValues.Count);
            IDictionary<string, Property> properties = GetProperties(typeof(T));
            foreach (KeyValuePair<string, object> kvp in propertyValues)
                if (properties.TryGetValue(kvp.Key, out Property property) && property.Column.TableColumn != null)
                {
                    if (targetTable == null)
                        targetTable = property.Column.TableColumn.Owner;
                    else if (!object.Equals(property.Column.TableColumn.Owner, targetTable))
                        continue;

                    property.Validate(kvp.Value);
                    AssembleInsertBody(command, property.Column.TableColumn, kvp.Value, ref bodyBuilder, ref valueBuilder);
                    processedTableColumns.Add(property.Column.TableColumn);
                }
                else
                    throw new InvalidOperationException(String.Format("未能在 {0} 中检索到映射 {1}.{2} 的表字段", Name, typeof(T).FullName, kvp.Key));

            AssembleInsertBody(command, processedTableColumns, targetTable, ref bodyBuilder, ref valueBuilder);
            command.CommandText = String.Format("insert into {0}({1})values({2})", targetTable.Name, bodyBuilder.ToString().TrimEnd(','), valueBuilder.ToString().TrimEnd(','));
            try
            {
                return DbCommandHelper.ExecuteNonQuery(command);
            }
            catch (Exception ex)
            {
                throw UniqueConstraintException.Refine(ex, this, typeof(T), propertyValues);
            }
        }

        #endregion

        #region UpdateEntity

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(T entity, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(entity, (CriteriaExpression)null, null, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(T entity, bool checkTimestamp = true, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(entity, (CriteriaExpression)null, null, NameValue<T>.ToDictionary(propertyValues), checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(T entity, Expression<Func<T, bool>> criteriaLambda, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(entity, CriteriaExpression.Where(criteriaLambda), null, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(T entity, Expression<Func<T, bool>> criteriaLambda, bool checkTimestamp = true, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(entity, CriteriaExpression.Where(criteriaLambda), null, NameValue<T>.ToDictionary(propertyValues), checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(T entity, Expression<Func<T, bool>> criteriaLambda, object criteria, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(entity, CriteriaExpression.Where(criteriaLambda), criteria, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(T entity, Expression<Func<T, bool>> criteriaLambda, object criteria, bool checkTimestamp = true, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(entity, CriteriaExpression.Where(criteriaLambda), criteria, NameValue<T>.ToDictionary(propertyValues), checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(T entity, CriteriaExpression criteriaExpression, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(entity, criteriaExpression, null, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(T entity, CriteriaExpression criteriaExpression, bool checkTimestamp = true, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(entity, criteriaExpression, null, NameValue<T>.ToDictionary(propertyValues), checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(T entity, CriteriaExpression criteriaExpression, object criteria, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(entity, criteriaExpression, criteria, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(T entity, CriteriaExpression criteriaExpression, object criteria, bool checkTimestamp = true, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(entity, criteriaExpression, criteria, NameValue<T>.ToDictionary(propertyValues), checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbConnection connection, T entity, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(connection, entity, (CriteriaExpression)null, null, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbConnection connection, T entity, bool checkTimestamp = true, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(connection, entity, (CriteriaExpression)null, null, NameValue<T>.ToDictionary(propertyValues), checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbConnection connection, T entity, Expression<Func<T, bool>> criteriaLambda, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(connection, entity, CriteriaExpression.Where(criteriaLambda), null, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbConnection connection, T entity, Expression<Func<T, bool>> criteriaLambda, bool checkTimestamp = true, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(connection, entity, CriteriaExpression.Where(criteriaLambda), null, NameValue<T>.ToDictionary(propertyValues), checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbConnection connection, T entity, Expression<Func<T, bool>> criteriaLambda, object criteria, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(connection, entity, CriteriaExpression.Where(criteriaLambda), criteria, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbConnection connection, T entity, Expression<Func<T, bool>> criteriaLambda, object criteria, bool checkTimestamp = true, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(connection, entity, CriteriaExpression.Where(criteriaLambda), criteria, NameValue<T>.ToDictionary(propertyValues), checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbConnection connection, T entity, CriteriaExpression criteriaExpression, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(connection, entity, criteriaExpression, null, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbConnection connection, T entity, CriteriaExpression criteriaExpression, bool checkTimestamp = true, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(connection, entity, criteriaExpression, null, NameValue<T>.ToDictionary(propertyValues), checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbConnection connection, T entity, CriteriaExpression criteriaExpression, object criteria, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(connection, entity, criteriaExpression, criteria, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbConnection connection, T entity, CriteriaExpression criteriaExpression, object criteria, bool checkTimestamp = true, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(connection, entity, criteriaExpression, criteria, NameValue<T>.ToDictionary(propertyValues), checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbTransaction transaction, T entity, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(transaction, entity, (CriteriaExpression)null, null, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbTransaction transaction, T entity, bool checkTimestamp = true, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(transaction, entity, (CriteriaExpression)null, null, NameValue<T>.ToDictionary(propertyValues), checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbTransaction transaction, T entity, Expression<Func<T, bool>> criteriaLambda, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(transaction, entity, CriteriaExpression.Where(criteriaLambda), null, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbTransaction transaction, T entity, Expression<Func<T, bool>> criteriaLambda, bool checkTimestamp = true, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(transaction, entity, CriteriaExpression.Where(criteriaLambda), null, NameValue<T>.ToDictionary(propertyValues), checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbTransaction transaction, T entity, Expression<Func<T, bool>> criteriaLambda, object criteria, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(transaction, entity, CriteriaExpression.Where(criteriaLambda), criteria, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbTransaction transaction, T entity, Expression<Func<T, bool>> criteriaLambda, object criteria, bool checkTimestamp = true, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(transaction, entity, CriteriaExpression.Where(criteriaLambda), criteria, NameValue<T>.ToDictionary(propertyValues), checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbTransaction transaction, T entity, CriteriaExpression criteriaExpression, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(transaction, entity, criteriaExpression, null, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbTransaction transaction, T entity, CriteriaExpression criteriaExpression, bool checkTimestamp = true, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(transaction, entity, criteriaExpression, null, NameValue<T>.ToDictionary(propertyValues), checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbTransaction transaction, T entity, CriteriaExpression criteriaExpression, object criteria, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(transaction, entity, criteriaExpression, criteria, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbTransaction transaction, T entity, CriteriaExpression criteriaExpression, object criteria, bool checkTimestamp = true, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(transaction, entity, criteriaExpression, criteria, NameValue<T>.ToDictionary(propertyValues), checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="command">DbCommand(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbCommand command, T entity, CriteriaExpression criteriaExpression, object criteria, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(command, entity, criteriaExpression, criteria, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="command">DbCommand(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbCommand command, T entity, CriteriaExpression criteriaExpression, object criteria, bool checkTimestamp = true, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateEntity(command, entity, criteriaExpression, criteria, NameValue<T>.ToDictionary(propertyValues), checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(T entity, IDictionary<string, object> propertyValues, bool checkTimestamp = true)
            where T : class
        {
            return UpdateEntity(entity, (CriteriaExpression)null, null, propertyValues, checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(T entity, Expression<Func<T, bool>> criteriaLambda, IDictionary<string, object> propertyValues, bool checkTimestamp = true)
            where T : class
        {
            return UpdateEntity(entity, CriteriaExpression.Where(criteriaLambda), null, propertyValues, checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(T entity, Expression<Func<T, bool>> criteriaLambda, object criteria, IDictionary<string, object> propertyValues = null, bool checkTimestamp = true)
            where T : class
        {
            return UpdateEntity(entity, CriteriaExpression.Where(criteriaLambda), criteria, propertyValues, checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(T entity, CriteriaExpression criteriaExpression, IDictionary<string, object> propertyValues = null, bool checkTimestamp = true)
            where T : class
        {
            return UpdateEntity(entity, criteriaExpression, null, propertyValues, checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(T entity, CriteriaExpression criteriaExpression, object criteria, IDictionary<string, object> propertyValues = null, bool checkTimestamp = true)
            where T : class
        {
            CheckDatabaseValidity();

            return GetHandle(entity).Owner.Database.ExecuteGet((Func<DbConnection, T, CriteriaExpression, object, IDictionary<string, object>, bool, int>)UpdateEntity, entity, criteriaExpression, criteria, propertyValues, checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbConnection connection, T entity, IDictionary<string, object> propertyValues = null, bool checkTimestamp = true)
            where T : class
        {
            return UpdateEntity(connection, entity, (CriteriaExpression)null, null, propertyValues, checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbConnection connection, T entity, Expression<Func<T, bool>> criteriaLambda, IDictionary<string, object> propertyValues = null, bool checkTimestamp = true)
            where T : class
        {
            return UpdateEntity(connection, entity, CriteriaExpression.Where(criteriaLambda), null, propertyValues, checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbConnection connection, T entity, Expression<Func<T, bool>> criteriaLambda, object criteria, IDictionary<string, object> propertyValues = null, bool checkTimestamp = true)
            where T : class
        {
            return UpdateEntity(connection, entity, CriteriaExpression.Where(criteriaLambda), criteria, propertyValues, checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbConnection connection, T entity, CriteriaExpression criteriaExpression, IDictionary<string, object> propertyValues = null, bool checkTimestamp = true)
            where T : class
        {
            return UpdateEntity(connection, entity, criteriaExpression, null, propertyValues, checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbConnection connection, T entity, CriteriaExpression criteriaExpression, object criteria, IDictionary<string, object> propertyValues = null, bool checkTimestamp = true)
            where T : class
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(connection))
            {
                return UpdateEntity(command, entity, criteriaExpression, criteria, propertyValues, checkTimestamp);
            }
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbTransaction transaction, T entity, IDictionary<string, object> propertyValues = null, bool checkTimestamp = true)
            where T : class
        {
            return UpdateEntity(transaction, entity, (CriteriaExpression)null, null, propertyValues, checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbTransaction transaction, T entity, Expression<Func<T, bool>> criteriaLambda, IDictionary<string, object> propertyValues = null, bool checkTimestamp = true)
            where T : class
        {
            return UpdateEntity(transaction, entity, CriteriaExpression.Where(criteriaLambda), null, propertyValues, checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbTransaction transaction, T entity, Expression<Func<T, bool>> criteriaLambda, object criteria, IDictionary<string, object> propertyValues = null, bool checkTimestamp = true)
            where T : class
        {
            return UpdateEntity(transaction, entity, CriteriaExpression.Where(criteriaLambda), criteria, propertyValues, checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbTransaction transaction, T entity, CriteriaExpression criteriaExpression, IDictionary<string, object> propertyValues = null, bool checkTimestamp = true)
            where T : class
        {
            return UpdateEntity(transaction, entity, criteriaExpression, null, propertyValues, checkTimestamp);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbTransaction transaction, T entity, CriteriaExpression criteriaExpression, object criteria, IDictionary<string, object> propertyValues = null, bool checkTimestamp = true)
            where T : class
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(transaction))
            {
                return UpdateEntity(command, entity, criteriaExpression, criteria, propertyValues, checkTimestamp);
            }
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="command">DbCommand(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        public int UpdateEntity<T>(DbCommand command, T entity, CriteriaExpression criteriaExpression, object criteria, IDictionary<string, object> propertyValues = null, bool checkTimestamp = true)
            where T : class
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            command.Parameters.Clear();

            if (entity == null)
                return 0;

            Type entityType = Utilities.LoadType(entity);
            propertyValues = FillReservedProperties(entityType, propertyValues, ExecuteAction.Update);
            Validate(entity, ExecuteAction.Update, propertyValues);

            Property primaryKeyProperty = GetPrimaryKeyProperty(entityType);
            Column primaryKeyTableColumn = primaryKeyProperty.Column.TableColumn;
            object primaryKeyValue = primaryKeyProperty.Field != null ? primaryKeyProperty.Field.GetValue(entity) : primaryKeyProperty.GetValue(entity);
            string whereBody = null;
            if (criteriaExpression != null)
            {
                if (criteriaExpression.OwnerType == null)
                    throw new InvalidOperationException("更新记录条件不充分");
                Sheet targetTable = primaryKeyTableColumn.Owner;
                whereBody = AssembleWhereBody(command, criteriaExpression, criteria, ExecuteAction.Update, ref targetTable);
            }

            whereBody = AssembleWhereBody(command, whereBody, primaryKeyTableColumn, primaryKeyValue);
            if (propertyValues == null || propertyValues.Count == 0)
            {
                StringBuilder bodyBuilder = new StringBuilder();
                foreach (KeyValuePair<string, Property> kvp in GetProperties(entityType, primaryKeyTableColumn.Owner, false))
                {
                    object value = kvp.Value.Field != null ? kvp.Value.Field.GetValue(entity) : kvp.Value.GetValue(entity);
                    kvp.Value.Validate(value, entity);
                    AssembleUpdateBody(command, kvp.Value.Column.TableColumn, value, ref bodyBuilder);
                }

                command.CommandText = String.Format("update {0} set {1} where {2}", primaryKeyTableColumn.Owner.Name, bodyBuilder.ToString().TrimEnd(','), whereBody);
                try
                {
                    return DbCommandHelper.ExecuteNonQuery(command);
                }
                catch (Exception ex)
                {
                    throw UniqueConstraintException.Refine(ex, this, entity);
                }
            }
            else
            {
                if (checkTimestamp)
                    whereBody = AssembleWhereBody(command, whereBody, primaryKeyTableColumn.Owner, entityType, entity, out checkTimestamp);
                StringBuilder bodyBuilder = new StringBuilder();
                IDictionary<string, Property> properties = GetProperties(entityType, primaryKeyTableColumn.Owner, false);
                foreach (KeyValuePair<string, object> kvp in propertyValues)
                    if (properties.TryGetValue(kvp.Key, out Property property))
                    {
                        property.Validate(kvp.Value, entity);
                        if (kvp.Value is OperationExpression operation)
                            AssembleUpdateBody(command, property.Column.TableColumn, operation, primaryKeyTableColumn.Owner, ref bodyBuilder);
                        else
                            AssembleUpdateBody(command, property.Column.TableColumn, kvp.Value, ref bodyBuilder);
                    }
                    else
                        throw new InvalidOperationException(String.Format("未能在 {0} 中检索到映射 {1}.{2} 的表字段", Name, entityType.FullName, kvp.Key));

                command.CommandText = String.Format("update {0} set {1} where {2}", primaryKeyTableColumn.Owner.Name, bodyBuilder.ToString().TrimEnd(','), whereBody);
                try
                {
                    int result = DbCommandHelper.ExecuteNonQuery(command);
                    if (result == 0 && checkTimestamp)
                        throw new OutdatedDataException();
                    if (result == 1)
                        Apply(entity, propertyValues);
                    return result;
                }
                catch (Exception ex)
                {
                    throw UniqueConstraintException.Refine(ex, this, entityType, propertyValues);
                }
            }
        }

        #endregion

        #region UpdateRecord

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(Expression<Func<T, bool>> criteriaLambda, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateRecord(CriteriaExpression.Where(criteriaLambda), null, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(Expression<Func<T, bool>> criteriaLambda, object criteria, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateRecord(CriteriaExpression.Where(criteriaLambda), criteria, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(CriteriaExpression criteriaExpression, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateRecord(criteriaExpression, null, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(CriteriaExpression criteriaExpression, object criteria, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateRecord(criteriaExpression, criteria, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateRecord(connection, CriteriaExpression.Where(criteriaLambda), null, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, object criteria, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateRecord(connection, CriteriaExpression.Where(criteriaLambda), criteria, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(DbConnection connection, CriteriaExpression criteriaExpression, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateRecord(connection, criteriaExpression, null, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(DbConnection connection, CriteriaExpression criteriaExpression, object criteria, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateRecord(connection, criteriaExpression, criteria, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateRecord(transaction, CriteriaExpression.Where(criteriaLambda), null, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, object criteria, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateRecord(transaction, CriteriaExpression.Where(criteriaLambda), criteria, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(DbTransaction transaction, CriteriaExpression criteriaExpression, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateRecord(transaction, criteriaExpression, null, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(DbTransaction transaction, CriteriaExpression criteriaExpression, object criteria, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateRecord(transaction, criteriaExpression, criteria, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="command">DbCommand(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(DbCommand command, CriteriaExpression criteriaExpression, object criteria, params NameValue<T>[] propertyValues)
            where T : class
        {
            return UpdateRecord(command, criteriaExpression, criteria, NameValue<T>.ToDictionary(propertyValues));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(Expression<Func<T, bool>> criteriaLambda, IDictionary<string, object> propertyValues)
            where T : class
        {
            return UpdateRecord(CriteriaExpression.Where(criteriaLambda), null, propertyValues);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(Expression<Func<T, bool>> criteriaLambda, object criteria, IDictionary<string, object> propertyValues)
            where T : class
        {
            return UpdateRecord(CriteriaExpression.Where(criteriaLambda), criteria, propertyValues);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord(CriteriaExpression criteriaExpression, IDictionary<string, object> propertyValues)
        {
            return UpdateRecord(criteriaExpression, null, propertyValues);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord(CriteriaExpression criteriaExpression, object criteria, IDictionary<string, object> propertyValues)
        {
            CheckDatabaseValidity();

            return Owner.Database.ExecuteGet((Func<DbConnection, CriteriaExpression, object, IDictionary<string, object>, int>)UpdateRecord, criteriaExpression, criteria, propertyValues);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, IDictionary<string, object> propertyValues)
            where T : class
        {
            return UpdateRecord(connection, CriteriaExpression.Where(criteriaLambda), null, propertyValues);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, object criteria, IDictionary<string, object> propertyValues)
            where T : class
        {
            return UpdateRecord(connection, CriteriaExpression.Where(criteriaLambda), criteria, propertyValues);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord(DbConnection connection, CriteriaExpression criteriaExpression, IDictionary<string, object> propertyValues)
        {
            return UpdateRecord(connection, criteriaExpression, null, propertyValues);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord(DbConnection connection, CriteriaExpression criteriaExpression, object criteria, IDictionary<string, object> propertyValues)
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(connection))
            {
                return UpdateRecord(command, criteriaExpression, criteria, propertyValues);
            }
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, IDictionary<string, object> propertyValues)
            where T : class
        {
            return UpdateRecord(transaction, CriteriaExpression.Where(criteriaLambda), null, propertyValues);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord<T>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, object criteria, IDictionary<string, object> propertyValues)
            where T : class
        {
            return UpdateRecord(transaction, CriteriaExpression.Where(criteriaLambda), criteria, propertyValues);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord(DbTransaction transaction, CriteriaExpression criteriaExpression, IDictionary<string, object> propertyValues)
        {
            return UpdateRecord(transaction, criteriaExpression, null, propertyValues);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord(DbTransaction transaction, CriteriaExpression criteriaExpression, object criteria, IDictionary<string, object> propertyValues)
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(transaction))
            {
                return UpdateRecord(command, criteriaExpression, criteria, propertyValues);
            }
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="command">DbCommand(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列</param>
        /// <returns>更新记录数</returns>
        public int UpdateRecord(DbCommand command, CriteriaExpression criteriaExpression, object criteria, IDictionary<string, object> propertyValues)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            command.Parameters.Clear();

            if (criteriaExpression == null)
                throw new ArgumentNullException(nameof(criteriaExpression));
            if (criteriaExpression.OwnerType == null)
                throw new InvalidOperationException("更新记录条件不充分");

            if (propertyValues == null || propertyValues.Count == 0)
                return 0;

            Sheet targetTable = null;
            string whereBody = AssembleWhereBody(command, criteriaExpression, criteria, ExecuteAction.Update, ref targetTable);
            if (targetTable == null)
                throw new InvalidOperationException("条件表达式与任何表都不存在映射关系");

            StringBuilder bodyBuilder = new StringBuilder();
            List<Column> processedTableColumns = new List<Column>(propertyValues.Count);
            IDictionary<string, Property> properties = GetProperties(criteriaExpression.OwnerType, targetTable, false);
            foreach (KeyValuePair<string, object> kvp in propertyValues)
                if (properties.TryGetValue(kvp.Key, out Property property))
                {
                    property.Validate(kvp.Value);
                    if (kvp.Value is OperationExpression operation)
                        AssembleUpdateBody(command, property.Column.TableColumn, operation, targetTable, ref bodyBuilder);
                    else
                        AssembleUpdateBody(command, property.Column.TableColumn, kvp.Value, ref bodyBuilder);
                    processedTableColumns.Add(property.Column.TableColumn);
                }
                else
                    throw new InvalidOperationException(String.Format("未能在 {0} 中检索到映射 {1}.{2} 的表字段", Name, criteriaExpression.OwnerType.FullName, kvp.Key));

            AssembleUpdateBody(command, processedTableColumns, targetTable, ref bodyBuilder);
            command.CommandText = String.Format("update {0} set {1} where {2}", targetTable.Name, bodyBuilder.ToString().TrimEnd(','), whereBody);
            try
            {
                return DbCommandHelper.ExecuteNonQuery(command);
            }
            catch (Exception ex)
            {
                throw UniqueConstraintException.Refine(ex, this, criteriaExpression.OwnerType, propertyValues);
            }
        }

        #endregion

        #region DeleteEntity

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        public int DeleteEntity<T>(T entity, bool cascade = false)
            where T : class
        {
            return DeleteEntity(entity, (CriteriaExpression)null, null, cascade);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        public int DeleteEntity<T>(T entity, Expression<Func<T, bool>> criteriaLambda, bool cascade = false)
            where T : class
        {
            return DeleteEntity(entity, CriteriaExpression.Where(criteriaLambda), null, cascade);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        public int DeleteEntity<T>(T entity, Expression<Func<T, bool>> criteriaLambda, object criteria, bool cascade = false)
            where T : class
        {
            return DeleteEntity(entity, CriteriaExpression.Where(criteriaLambda), criteria, cascade);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        public int DeleteEntity<T>(T entity, CriteriaExpression criteriaExpression, bool cascade = false)
            where T : class
        {
            return DeleteEntity(entity, criteriaExpression, null, cascade);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        public int DeleteEntity<T>(T entity, CriteriaExpression criteriaExpression, object criteria, bool cascade = false)
            where T : class
        {
            CheckDatabaseValidity();

            return GetHandle(entity).Owner.Database.ExecuteGet((Func<DbTransaction, T, CriteriaExpression, object, bool, int>)DeleteEntity, entity, criteriaExpression, criteria, cascade);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        public int DeleteEntity<T>(DbConnection connection, T entity, bool cascade = false)
            where T : class
        {
            return DeleteEntity(connection, entity, (CriteriaExpression)null, null, cascade);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        public int DeleteEntity<T>(DbConnection connection, T entity, Expression<Func<T, bool>> criteriaLambda, bool cascade = false)
            where T : class
        {
            return DeleteEntity(connection, entity, CriteriaExpression.Where(criteriaLambda), null, cascade);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        public int DeleteEntity<T>(DbConnection connection, T entity, Expression<Func<T, bool>> criteriaLambda, object criteria, bool cascade = false)
            where T : class
        {
            return DeleteEntity(connection, entity, CriteriaExpression.Where(criteriaLambda), criteria, cascade);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        public int DeleteEntity<T>(DbConnection connection, T entity, CriteriaExpression criteriaExpression, bool cascade = false)
            where T : class
        {
            return DeleteEntity(connection, entity, criteriaExpression, null, cascade);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        public int DeleteEntity<T>(DbConnection connection, T entity, CriteriaExpression criteriaExpression, object criteria, bool cascade = false)
            where T : class
        {
            return DbConnectionHelper.ExecuteGet(connection, (Func<DbTransaction, T, CriteriaExpression, object, bool, int>)DeleteEntity, entity, criteriaExpression, criteria, cascade);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        public int DeleteEntity<T>(DbTransaction transaction, T entity, bool cascade = false)
            where T : class
        {
            return DeleteEntity(transaction, entity, (CriteriaExpression)null, null, cascade);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        public int DeleteEntity<T>(DbTransaction transaction, T entity, Expression<Func<T, bool>> criteriaLambda, bool cascade = false)
            where T : class
        {
            return DeleteEntity(transaction, entity, CriteriaExpression.Where(criteriaLambda), null, cascade);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        public int DeleteEntity<T>(DbTransaction transaction, T entity, Expression<Func<T, bool>> criteriaLambda, object criteria, bool cascade = false)
            where T : class
        {
            return DeleteEntity(transaction, entity, CriteriaExpression.Where(criteriaLambda), criteria, cascade);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        public int DeleteEntity<T>(DbTransaction transaction, T entity, CriteriaExpression criteriaExpression, bool cascade = false)
            where T : class
        {
            return DeleteEntity(transaction, entity, criteriaExpression, null, cascade);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="entity">实体</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        public int DeleteEntity<T>(DbTransaction transaction, T entity, CriteriaExpression criteriaExpression, object criteria, bool cascade = false)
            where T : class
        {
            if (entity == null)
                return 0;

            Property primaryKeyProperty = GetPrimaryKeyProperty(Utilities.LoadType(entity));
            Column primaryKeyTableColumn = primaryKeyProperty.Column.TableColumn;
            object primaryKeyValue = primaryKeyProperty.Field != null ? primaryKeyProperty.Field.GetValue(entity) : primaryKeyProperty.GetValue(entity);
            if (cascade)
                ((Table)primaryKeyTableColumn.Owner).DeleteDepth(transaction, primaryKeyValue);

            using (DbCommand command = DbCommandHelper.CreateCommand(transaction))
            {
                Validate(entity, ExecuteAction.Delete);

                string whereBody = null;
                if (criteriaExpression != null)
                {
                    if (criteriaExpression.OwnerType == null)
                        throw new InvalidOperationException("更新记录条件不充分");
                    Sheet targetTable = primaryKeyTableColumn.Owner;
                    whereBody = AssembleWhereBody(command, criteriaExpression, criteria, ExecuteAction.Delete, ref targetTable);
                }

                command.CommandText = String.Format("delete from {0} where {1}",
                    primaryKeyTableColumn.Owner.Name, AssembleWhereBody(command, whereBody, primaryKeyTableColumn, primaryKeyValue));
                return DbCommandHelper.ExecuteNonQuery(command);
            }
        }

        #endregion

        #region DeleteRecord

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <returns>删除记录数</returns>
        public int DeleteRecord<T>(Expression<Func<T, bool>> criteriaLambda, object criteria = null)
            where T : class
        {
            return DeleteRecord(CriteriaExpression.Where(criteriaLambda), criteria);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <returns>删除记录数</returns>
        public int DeleteRecord(CriteriaExpression criteriaExpression, object criteria = null)
        {
            CheckDatabaseValidity();

            return Owner.Database.ExecuteGet((Func<DbConnection, CriteriaExpression, object, int>)DeleteRecord, criteriaExpression, criteria);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <returns>删除记录数</returns>
        public int DeleteRecord<T>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, object criteria = null)
            where T : class
        {
            return DeleteRecord(connection, CriteriaExpression.Where(criteriaLambda), criteria);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <returns>删除记录数</returns>
        public int DeleteRecord(DbConnection connection, CriteriaExpression criteriaExpression, object criteria = null)
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(connection))
            {
                return DeleteRecord(command, criteriaExpression, criteria);
            }
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <returns>删除记录数</returns>
        public int DeleteRecord<T>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, object criteria = null)
            where T : class
        {
            return DeleteRecord(transaction, CriteriaExpression.Where(criteriaLambda), criteria);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <returns>删除记录数</returns>
        public int DeleteRecord(DbTransaction transaction, CriteriaExpression criteriaExpression, object criteria = null)
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(transaction))
            {
                return DeleteRecord(command, criteriaExpression, criteria);
            }
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="command">DbCommand(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <returns>删除记录数</returns>
        public int DeleteRecord(DbCommand command, CriteriaExpression criteriaExpression, object criteria = null)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            command.Parameters.Clear();

            if (criteriaExpression == null)
                throw new ArgumentNullException(nameof(criteriaExpression));
            if (criteriaExpression.OwnerType == null)
                throw new InvalidOperationException("删除记录条件不充分");

            Sheet targetTable = null;
            string whereBody = AssembleWhereBody(command, criteriaExpression, criteria, ExecuteAction.Delete, ref targetTable);
            if (targetTable == null)
                throw new InvalidOperationException("条件表达式与任何表都不存在映射关系");

            command.CommandText = String.Format("delete from {0} where {1}", targetTable.Name, whereBody);
            return DbCommandHelper.ExecuteNonQuery(command);
        }

        #endregion

        #region SelectEntity IDictionary<TKey, T>

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="keyLambda">键 lambda 表达式</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        public IDictionary<TKey, T> SelectEntity<TKey, T>(Expression<Func<T, TKey>> keyLambda, Expression<Func<T, bool>> criteriaLambda = null, object criteria = null)
            where T : class
        {
            return SelectEntity(keyLambda, CriteriaExpression.Where(criteriaLambda), criteria);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="keyLambda">键 lambda 表达式</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        public IDictionary<TKey, T> SelectEntity<TKey, T>(Expression<Func<T, TKey>> keyLambda, CriteriaExpression criteriaExpression, object criteria = null)
            where T : class
        {
            CheckDatabaseValidity();

            return Owner.Database.ExecuteGet((Func<DbConnection, Expression<Func<T, TKey>>, CriteriaExpression, object, IDictionary<TKey, T>>)SelectEntity, keyLambda, criteriaExpression, criteria);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="keyLambda">键 lambda 表达式</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        public IDictionary<TKey, T> SelectEntity<TKey, T>(DbConnection connection, Expression<Func<T, TKey>> keyLambda, Expression<Func<T, bool>> criteriaLambda = null, object criteria = null)
            where T : class
        {
            return SelectEntity(connection, keyLambda, CriteriaExpression.Where(criteriaLambda), criteria);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="keyLambda">键 lambda 表达式</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        public IDictionary<TKey, T> SelectEntity<TKey, T>(DbConnection connection, Expression<Func<T, TKey>> keyLambda, CriteriaExpression criteriaExpression, object criteria = null)
            where T : class
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(connection))
            {
                return SelectEntity(command, keyLambda, criteriaExpression, criteria);
            }
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="keyLambda">键 lambda 表达式</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        public IDictionary<TKey, T> SelectEntity<TKey, T>(DbTransaction transaction, Expression<Func<T, TKey>> keyLambda, Expression<Func<T, bool>> criteriaLambda = null, object criteria = null)
            where T : class
        {
            return SelectEntity(transaction, keyLambda, CriteriaExpression.Where(criteriaLambda), criteria);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="keyLambda">键 lambda 表达式</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        public IDictionary<TKey, T> SelectEntity<TKey, T>(DbTransaction transaction, Expression<Func<T, TKey>> keyLambda, CriteriaExpression criteriaExpression, object criteria = null)
            where T : class
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(transaction))
            {
                return SelectEntity(command, keyLambda, criteriaExpression, criteria);
            }
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="command">DbCommand(注意跨库风险未作校验)</param>
        /// <param name="keyLambda">键 lambda 表达式</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        public IDictionary<TKey, T> SelectEntity<TKey, T>(DbCommand command, Expression<Func<T, TKey>> keyLambda, CriteriaExpression criteriaExpression = null, object criteria = null)
            where T : class
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            command.Parameters.Clear();

            Type entityType = typeof(T);
            IDictionary<string, Field> entityFields = GetFields(entityType);
            if (entityFields.Count == 0)
                throw new InvalidOperationException(String.Format("查询记录时至少类 {0} 应有一个字段", entityType.FullName));

            MemberInfo keyMemberInfo = Utilities.GetMemberInfo(keyLambda);
            PropertyInfo keyPropertyInfo = keyMemberInfo as PropertyInfo;
            FieldInfo keyFieldInfo = keyMemberInfo as FieldInfo;
            if (keyPropertyInfo == null && keyFieldInfo == null)
                throw new InvalidOperationException(String.Format("键 lambda 表达式应为类 {0} 的属性或字段", entityType.FullName));

            string whereBody = null;
            if (criteriaExpression != null)
            {
                Sheet targetSheet = this;
                whereBody = AssembleWhereBody(command, criteriaExpression, criteria, ExecuteAction.None, ref targetSheet);
            }

            whereBody = AssembleWhereBody(command, whereBody);
            command.CommandText = String.Format("select {0} from {1}{2}{3}",
                AssembleColumnBody(entityFields), Name, whereBody != null ? " where " : null, whereBody);

            Dictionary<TKey, T> result = new Dictionary<TKey, T>();
            DynamicCtorDelegate create = InstanceInfo.Fetch(entityType).Create;
            using (DataReader reader = new DataReader(command))
            {
                while (reader.Read())
                {
                    T t = (T)create();
                    int i = 0;
                    foreach (KeyValuePair<string, Field> kvp in entityFields)
                    {
                        kvp.Value.Set(t, reader.GetValue(i, kvp.Value.FieldInfo.FieldType));
                        i = i + 1;
                    }

                    if (t is IEntity e)
                        e.SelfSheet = this;
                    result.Add(Utilities.ChangeType<TKey>(keyPropertyInfo != null ? keyPropertyInfo.GetValue(t) : keyFieldInfo.GetValue(t)), t);
                }
            }

            return result;
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="reader">IDataReader</param>
        /// <param name="keyLambda">键 lambda 表达式</param>
        public IDictionary<TKey, T> SelectEntity<TKey, T>(IDataReader reader, Expression<Func<T, TKey>> keyLambda)
            where T : class
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            Type entityType = typeof(T);
            IDictionary<string, Field> entityFields = GetFields(entityType);
            if (entityFields.Count == 0)
                throw new InvalidOperationException(String.Format("查询记录时至少类 {0} 应有一个字段", entityType.FullName));

            MemberInfo keyMemberInfo = Utilities.GetMemberInfo(keyLambda);
            PropertyInfo keyPropertyInfo = keyMemberInfo as PropertyInfo;
            FieldInfo keyFieldInfo = keyMemberInfo as FieldInfo;
            if (keyPropertyInfo == null && keyFieldInfo == null)
                throw new InvalidOperationException(String.Format("键 lambda 表达式应为类 {0} 的属性或字段", entityType.FullName));

            Dictionary<TKey, T> result = new Dictionary<TKey, T>();
            DynamicCtorDelegate create = InstanceInfo.Fetch(entityType).Create;
            Dictionary<int, Field> mappingFields = null;
            while (reader.Read())
            {
                T t = (T)create();
                if (mappingFields == null)
                {
                    mappingFields = new Dictionary<int, Field>(reader.FieldCount);
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string columnName = reader.GetName(i);
                        foreach (KeyValuePair<string, Field> kvp in entityFields)
                            if (String.Compare(columnName, kvp.Value.Column.Name, StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                mappingFields.Add(i, kvp.Value);
                                kvp.Value.Set(t, reader.GetValue(i));
                                break;
                            }
                    }
                }
                else
                {
                    foreach (KeyValuePair<int, Field> kvp in mappingFields)
                        kvp.Value.Set(t, reader.GetValue(kvp.Key));
                }

                if (t is IEntity e)
                    e.SelfSheet = this;
                result.Add(Utilities.ChangeType<TKey>(keyPropertyInfo != null ? keyPropertyInfo.GetValue(t) : keyFieldInfo.GetValue(t)), t);
            }

            return result;
        }

        #endregion

        #region SelectEntity IList<T>

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectFirstEntity<T>(params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstEntity((CriteriaExpression)null, null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectFirstEntity<T>(Expression<Func<T, bool>> criteriaLambda, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstEntity(CriteriaExpression.Where(criteriaLambda), null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectFirstEntity<T>(Expression<Func<T, bool>> criteriaLambda, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstEntity(CriteriaExpression.Where(criteriaLambda), criteria, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectFirstEntity<T>(CriteriaExpression criteriaExpression, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstEntity(criteriaExpression, null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectFirstEntity<T>(CriteriaExpression criteriaExpression, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            CheckDatabaseValidity();

            return Owner.Database.ExecuteGet((Func<DbConnection, CriteriaExpression, object, OrderBy<T>[], IList<T>>)SelectFirstEntity, criteriaExpression, criteria, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity((CriteriaExpression)null, null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity((CriteriaExpression)null, null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(Expression<Func<T, bool>> criteriaLambda, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(CriteriaExpression.Where(criteriaLambda), null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(Expression<Func<T, bool>> criteriaLambda, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(CriteriaExpression.Where(criteriaLambda), null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(Expression<Func<T, bool>> criteriaLambda, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(CriteriaExpression.Where(criteriaLambda), criteria, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(Expression<Func<T, bool>> criteriaLambda, object criteria, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(CriteriaExpression.Where(criteriaLambda), criteria, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(CriteriaExpression criteriaExpression, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(criteriaExpression, null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(CriteriaExpression criteriaExpression, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(criteriaExpression, null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(CriteriaExpression criteriaExpression, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(criteriaExpression, criteria, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(CriteriaExpression criteriaExpression, object criteria, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            CheckDatabaseValidity();

            return Owner.Database.ExecuteGet((Func<DbConnection, CriteriaExpression, object, int, int, OrderBy<T>[], IList<T>>)SelectEntity, criteriaExpression, criteria, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectFirstEntity<T>(DbConnection connection, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstEntity(connection, (CriteriaExpression)null, null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectFirstEntity<T>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstEntity(connection, CriteriaExpression.Where(criteriaLambda), null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectFirstEntity<T>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstEntity(connection, CriteriaExpression.Where(criteriaLambda), criteria, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectFirstEntity<T>(DbConnection connection, CriteriaExpression criteriaExpression, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstEntity(connection, criteriaExpression, null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectFirstEntity<T>(DbConnection connection, CriteriaExpression criteriaExpression, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(connection))
            {
                return SelectEntity(command, criteriaExpression, criteria, 0, 10, true, orderBys);
            }
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbConnection connection, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(connection, (CriteriaExpression)null, null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbConnection connection, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(connection, (CriteriaExpression)null, null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(connection, CriteriaExpression.Where(criteriaLambda), null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(connection, CriteriaExpression.Where(criteriaLambda), null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(connection, CriteriaExpression.Where(criteriaLambda), criteria, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, object criteria, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(connection, CriteriaExpression.Where(criteriaLambda), criteria, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbConnection connection, CriteriaExpression criteriaExpression, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(connection, criteriaExpression, null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbConnection connection, CriteriaExpression criteriaExpression, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(connection, criteriaExpression, null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbConnection connection, CriteriaExpression criteriaExpression, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(connection, criteriaExpression, criteria, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbConnection connection, CriteriaExpression criteriaExpression, object criteria, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(connection))
            {
                return SelectEntity(command, criteriaExpression, criteria, pageNo, pageSize, false, orderBys);
            }
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectFirstEntity<T>(DbTransaction transaction, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstEntity(transaction, (CriteriaExpression)null, null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectFirstEntity<T>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstEntity(transaction, CriteriaExpression.Where(criteriaLambda), null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectFirstEntity<T>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstEntity(transaction, CriteriaExpression.Where(criteriaLambda), criteria, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectFirstEntity<T>(DbTransaction transaction, CriteriaExpression criteriaExpression, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstEntity(transaction, criteriaExpression, null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectFirstEntity<T>(DbTransaction transaction, CriteriaExpression criteriaExpression, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(transaction))
            {
                return SelectEntity(command, criteriaExpression, criteria, 0, 10, true, orderBys);
            }
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbTransaction transaction, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(transaction, (CriteriaExpression)null, null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbTransaction transaction, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(transaction, (CriteriaExpression)null, null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(transaction, CriteriaExpression.Where(criteriaLambda), null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(transaction, CriteriaExpression.Where(criteriaLambda), null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(transaction, CriteriaExpression.Where(criteriaLambda), criteria, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, object criteria, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(transaction, CriteriaExpression.Where(criteriaLambda), criteria, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbTransaction transaction, CriteriaExpression criteriaExpression, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(transaction, criteriaExpression, null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbTransaction transaction, CriteriaExpression criteriaExpression, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(transaction, criteriaExpression, null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbTransaction transaction, CriteriaExpression criteriaExpression, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectEntity(transaction, criteriaExpression, criteria, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbTransaction transaction, CriteriaExpression criteriaExpression, object criteria, int pageNo, int pageSize, params OrderBy<T>[] orderBys)
            where T : class
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(transaction))
            {
                return SelectEntity(command, criteriaExpression, criteria, pageNo, pageSize, false, orderBys);
            }
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="command">DbCommand(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="first">是否返回第一条记录</param>
        /// <param name="orderBys">排序队列</param>
        public IList<T> SelectEntity<T>(DbCommand command, CriteriaExpression criteriaExpression, object criteria, int pageNo, int pageSize = 10, bool first = false, params OrderBy<T>[] orderBys)
            where T : class
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            command.Parameters.Clear();

            Type entityType = typeof(T);
            IDictionary<string, Field> entityFields = GetFields(entityType);
            if (entityFields.Count == 0)
                throw new InvalidOperationException(String.Format("查询记录时至少类 {0} 应有一个字段", entityType.FullName));

            string whereBody = null;
            if (criteriaExpression != null)
            {
                Sheet targetSheet = this;
                whereBody = AssembleWhereBody(command, criteriaExpression, criteria, ExecuteAction.None, ref targetSheet);
            }

            whereBody = AssembleWhereBody(command, whereBody);
            string uniqueColumnName = null;
            string orderBody = AssembleOrderBody(orderBys, ref uniqueColumnName);

            if (pageNo <= 0)
                command.CommandText = String.Format("select {0} from {1}{2}{3}{4}{5}",
                    AssembleColumnBody(entityFields), Name, whereBody != null ? " where " : null, whereBody, orderBody != null ? " order by " : null, orderBody);
            else
            {
#if PgSQL
                //select * from (select US_ID from PH7_User where US_Disabled = 0 order by US_ID limit 20 offset 80) a left join PH7_User b on a.US_ID = b.US_ID
                if (uniqueColumnName != null)
                    command.CommandText = String.Format("select {0} from (select {1} from {2}{3}{4} order by {5} limit {7} offset {6}) a left join {2} b on a.{1} = b.{1}",
                        AssembleColumnBody(entityFields), uniqueColumnName, Name, whereBody != null ? " where " : null, whereBody, orderBody, (pageNo - 1) * pageSize, pageSize);
                else
                    command.CommandText = String.Format("select {0} from {1}{2}{3}{4}{5} limit {7} offset {6}",
                        AssembleColumnBody(entityFields), Name, whereBody != null ? " where " : null, whereBody, orderBody != null ? " order by " : null, orderBody, (pageNo - 1) * pageSize, pageSize);
#endif
#if MsSQL
                // select * from (select row_number() over(order by US_ID) as rownumber, PH7_User.* from PH7_User where (US_Disabled = 0)) where rownumber > 80 and rownumber <= 100
                command.CommandText = String.Format("select {0} from (select row_number() over(order by {1}) as rownumber, {0} from {2}{3}{4}) where rownumber > {5} and rownumber <= {6}",
                    AssembleColumnBody(entityFields), orderBody != null ? orderBody : PrimaryKeyColumns[0].Name, Name, whereBody != null ? " where " : null, whereBody, (pageNo - 1) * pageSize, pageNo * pageSize);
#endif
#if MySQL
                //select * from (select US_ID from PH7_User where US_Disabled = 0 order by US_ID limit 80,20) a left join PH7_User b on a.US_ID = b.US_ID
                if (uniqueColumnName != null)
                    command.CommandText = String.Format("select {0} from (select {1} from {2}{3}{4} order by {5} limit {6},{7}) a left join {2} b on a.{1} = b.{1}",
                        AssembleColumnBody(entityFields), uniqueColumnName, Name, whereBody != null ? " where " : null, whereBody, orderBody, (pageNo - 1) * pageSize, pageSize);
                else
                    command.CommandText = String.Format("select {0} from {1}{2}{3}{4}{5} limit {6},{7}",
                        AssembleColumnBody(entityFields), Name, whereBody != null ? " where " : null, whereBody, orderBody != null ? " order by " : null, orderBody, (pageNo - 1) * pageSize, pageSize);
#endif
#if ORA
                if (orderBody != null)
                    // select * from (select p.*, rownum as rownumber from (select * from PH7_User where US_Disabled = 0 order by US_ID) p where rownum <= 10) where rownumber > 5
                    command.CommandText = String.Format("select {0} from (select {0}, rownum as rownumber from (select {0} from {1}{2}{3} order by {4}) p where rownum <= {6}) where rownumber > {5}",
                        AssembleColumnBody(entityFields), Name, whereBody != null ? " where " : null, whereBody, orderBody, (pageNo - 1) * pageSize, pageNo * pageSize);
                else
                    // select * from (select p.*, rownum as rownumber from PH7_User p where (US_Disabled = 0) and rownum <= 10) where rownumber > 5
                    command.CommandText = String.Format("select {0} from (select {0}, rownum as rownumber from {1} where {2} and rownum <= {4}) where rownumber > {3}",
                        AssembleColumnBody(entityFields), Name, whereBody != null ? String.Format("({0})", whereBody) : null, (pageNo - 1) * pageSize, pageNo * pageSize);
#endif
            }

            List<T> result = new List<T>();
            DynamicCtorDelegate create = InstanceInfo.Fetch(entityType).Create;
            using (DataReader reader = new DataReader(command, first ? CommandBehavior.SingleRow : CommandBehavior.SingleResult))
            {
                while (reader.Read())
                {
                    T t = (T)create();
                    int i = 0;
                    foreach (KeyValuePair<string, Field> kvp in entityFields)
                    {
                        kvp.Value.Set(t, reader.GetValue(i, kvp.Value.FieldInfo.FieldType));
                        i = i + 1;
                    }

                    if (t is IEntity e)
                        e.SelfSheet = this;
                    result.Add(t);
                }
            }

            return result;
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="reader">IDataReader</param>
        /// <param name="first">是否返回第一条记录</param>
        public IList<T> SelectEntity<T>(IDataReader reader, bool first = false)
            where T : class
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            Type entityType = typeof(T);
            IDictionary<string, Field> entityFields = GetFields(entityType);
            if (entityFields.Count == 0)
                throw new InvalidOperationException(String.Format("查询记录时至少类 {0} 应有一个字段", entityType.FullName));

            List<T> result = new List<T>();
            DynamicCtorDelegate create = InstanceInfo.Fetch(entityType).Create;
            Dictionary<int, Field> mappingFields = null;
            while (reader.Read())
            {
                T t = (T)create();
                if (mappingFields == null)
                {
                    mappingFields = new Dictionary<int, Field>(reader.FieldCount);
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string columnName = reader.GetName(i);
                        foreach (KeyValuePair<string, Field> kvp in entityFields)
                            if (String.Compare(columnName, kvp.Value.Column.Name, StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                mappingFields.Add(i, kvp.Value);
                                kvp.Value.Set(t, reader.GetValue(i));
                                break;
                            }
                    }
                }
                else
                {
                    foreach (KeyValuePair<int, Field> kvp in mappingFields)
                        kvp.Value.Set(t, reader.GetValue(kvp.Key));
                }

                if (t is IEntity e)
                    e.SelfSheet = this;
                result.Add(t);

                if (first)
                    break;
            }

            return result;
        }

        #endregion

        #region SelectRecord

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T>(params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstRecord((CriteriaExpression)null, null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T>(Expression<Func<T, bool>> criteriaLambda, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstRecord(CriteriaExpression.Where(criteriaLambda), null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T>(Expression<Func<T, bool>> criteriaLambda, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstRecord(CriteriaExpression.Where(criteriaLambda), criteria, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T>(CriteriaExpression criteriaExpression, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstRecord(criteriaExpression, null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T>(CriteriaExpression criteriaExpression, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstRecord<T, T>(criteriaExpression, criteria, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord((CriteriaExpression)null, null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord((CriteriaExpression)null, null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(Expression<Func<T, bool>> criteriaLambda, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(CriteriaExpression.Where(criteriaLambda), null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(Expression<Func<T, bool>> criteriaLambda, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(CriteriaExpression.Where(criteriaLambda), null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(Expression<Func<T, bool>> criteriaLambda, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(CriteriaExpression.Where(criteriaLambda), criteria, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(Expression<Func<T, bool>> criteriaLambda, object criteria, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(CriteriaExpression.Where(criteriaLambda), criteria, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(CriteriaExpression criteriaExpression, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(criteriaExpression, null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(CriteriaExpression criteriaExpression, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(criteriaExpression, null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(CriteriaExpression criteriaExpression, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(criteriaExpression, criteria, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(CriteriaExpression criteriaExpression, object criteria, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord<T, T>(criteriaExpression, criteria, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T>(DbConnection connection, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstRecord(connection, (CriteriaExpression)null, null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstRecord(connection, CriteriaExpression.Where(criteriaLambda), null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstRecord(connection, CriteriaExpression.Where(criteriaLambda), criteria, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T>(DbConnection connection, CriteriaExpression criteriaExpression, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstRecord(connection, criteriaExpression, null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T>(DbConnection connection, CriteriaExpression criteriaExpression, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstRecord<T, T>(connection, criteriaExpression, criteria, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbConnection connection, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(connection, (CriteriaExpression)null, null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbConnection connection, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(connection, (CriteriaExpression)null, null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(connection, CriteriaExpression.Where(criteriaLambda), null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(connection, CriteriaExpression.Where(criteriaLambda), null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(connection, CriteriaExpression.Where(criteriaLambda), criteria, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, object criteria, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(connection, CriteriaExpression.Where(criteriaLambda), criteria, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbConnection connection, CriteriaExpression criteriaExpression, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(connection, criteriaExpression, null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbConnection connection, CriteriaExpression criteriaExpression, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(connection, criteriaExpression, null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbConnection connection, CriteriaExpression criteriaExpression, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(connection, criteriaExpression, criteria, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbConnection connection, CriteriaExpression criteriaExpression, object criteria, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord<T, T>(connection, criteriaExpression, criteria, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T>(DbTransaction transaction, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstRecord(transaction, (CriteriaExpression)null, null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstRecord(transaction, CriteriaExpression.Where(criteriaLambda), null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstRecord(transaction, CriteriaExpression.Where(criteriaLambda), criteria, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T>(DbTransaction transaction, CriteriaExpression criteriaExpression, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstRecord(transaction, criteriaExpression, null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T>(DbTransaction transaction, CriteriaExpression criteriaExpression, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectFirstRecord<T, T>(transaction, criteriaExpression, criteria, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbTransaction transaction, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(transaction, (CriteriaExpression)null, null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbTransaction transaction, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(transaction, (CriteriaExpression)null, null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(transaction, CriteriaExpression.Where(criteriaLambda), null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(transaction, CriteriaExpression.Where(criteriaLambda), null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(transaction, CriteriaExpression.Where(criteriaLambda), criteria, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, object criteria, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(transaction, CriteriaExpression.Where(criteriaLambda), criteria, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbTransaction transaction, CriteriaExpression criteriaExpression, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(transaction, criteriaExpression, null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbTransaction transaction, CriteriaExpression criteriaExpression, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(transaction, criteriaExpression, null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbTransaction transaction, CriteriaExpression criteriaExpression, object criteria, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord(transaction, criteriaExpression, criteria, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbTransaction transaction, CriteriaExpression criteriaExpression, object criteria, int pageNo, int pageSize, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord<T, T>(transaction, criteriaExpression, criteria, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="command">DbCommand(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="first">是否返回第一条记录</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T>(DbCommand command, CriteriaExpression criteriaExpression, object criteria, int pageNo, int pageSize = 10, bool first = false, params OrderBy<T>[] orderBys)
            where T : class
        {
            return SelectRecord<T, T>(command, criteriaExpression, criteria, pageNo, pageSize, first, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T, TSub>(params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectFirstRecord<T, TSub>((CriteriaExpression)null, null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T, TSub>(Expression<Func<T, bool>> criteriaLambda, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectFirstRecord<T, TSub>(CriteriaExpression.Where(criteriaLambda), null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T, TSub>(Expression<Func<T, bool>> criteriaLambda, object criteria, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectFirstRecord<T, TSub>(CriteriaExpression.Where(criteriaLambda), criteria, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T, TSub>(CriteriaExpression criteriaExpression, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectFirstRecord<T, TSub>(criteriaExpression, null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T, TSub>(CriteriaExpression criteriaExpression, object criteria, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            CheckDatabaseValidity();

            return Owner.Database.ExecuteGet((Func<DbConnection, CriteriaExpression, object, OrderBy<T>[], string>)SelectFirstRecord<T, TSub>, criteriaExpression, criteria, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>((CriteriaExpression)null, null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>((CriteriaExpression)null, null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(Expression<Func<T, bool>> criteriaLambda, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(CriteriaExpression.Where(criteriaLambda), null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(Expression<Func<T, bool>> criteriaLambda, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(CriteriaExpression.Where(criteriaLambda), null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(Expression<Func<T, bool>> criteriaLambda, object criteria, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(CriteriaExpression.Where(criteriaLambda), criteria, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(Expression<Func<T, bool>> criteriaLambda, object criteria, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(CriteriaExpression.Where(criteriaLambda), criteria, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(CriteriaExpression criteriaExpression, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(criteriaExpression, null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(CriteriaExpression criteriaExpression, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(criteriaExpression, null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(CriteriaExpression criteriaExpression, object criteria, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(criteriaExpression, criteria, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(CriteriaExpression criteriaExpression, object criteria, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            CheckDatabaseValidity();

            return Owner.Database.ExecuteGet((Func<DbConnection, CriteriaExpression, object, int, int, OrderBy<T>[], string>)SelectRecord<T, TSub>, criteriaExpression, criteria, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T, TSub>(DbConnection connection, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectFirstRecord<T, TSub>(connection, (CriteriaExpression)null, null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T, TSub>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectFirstRecord<T, TSub>(connection, CriteriaExpression.Where(criteriaLambda), null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T, TSub>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, object criteria, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectFirstRecord<T, TSub>(connection, CriteriaExpression.Where(criteriaLambda), criteria, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T, TSub>(DbConnection connection, CriteriaExpression criteriaExpression, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectFirstRecord<T, TSub>(connection, criteriaExpression, null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T, TSub>(DbConnection connection, CriteriaExpression criteriaExpression, object criteria, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(connection))
            {
                return SelectRecord<T, TSub>(command, criteriaExpression, criteria, 0, 10, true, orderBys);
            }
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbConnection connection, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(connection, (CriteriaExpression)null, null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbConnection connection, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(connection, (CriteriaExpression)null, null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(connection, CriteriaExpression.Where(criteriaLambda), null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(connection, CriteriaExpression.Where(criteriaLambda), null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, object criteria, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(connection, CriteriaExpression.Where(criteriaLambda), criteria, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, object criteria, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(connection, CriteriaExpression.Where(criteriaLambda), criteria, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbConnection connection, CriteriaExpression criteriaExpression, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(connection, criteriaExpression, null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbConnection connection, CriteriaExpression criteriaExpression, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(connection, criteriaExpression, null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbConnection connection, CriteriaExpression criteriaExpression, object criteria, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(connection, criteriaExpression, criteria, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbConnection connection, CriteriaExpression criteriaExpression, object criteria, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(connection))
            {
                return SelectRecord<T, TSub>(command, criteriaExpression, criteria, pageNo, pageSize, false, orderBys);
            }
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T, TSub>(DbTransaction transaction, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectFirstRecord<T, TSub>(transaction, (CriteriaExpression)null, null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T, TSub>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectFirstRecord<T, TSub>(transaction, CriteriaExpression.Where(criteriaLambda), null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T, TSub>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, object criteria, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectFirstRecord<T, TSub>(transaction, CriteriaExpression.Where(criteriaLambda), criteria, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T, TSub>(DbTransaction transaction, CriteriaExpression criteriaExpression, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectFirstRecord<T, TSub>(transaction, criteriaExpression, null, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectFirstRecord<T, TSub>(DbTransaction transaction, CriteriaExpression criteriaExpression, object criteria, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(transaction))
            {
                return SelectRecord<T, TSub>(command, criteriaExpression, criteria, 0, 10, true, orderBys);
            }
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbTransaction transaction, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(transaction, (CriteriaExpression)null, null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbTransaction transaction, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(transaction, (CriteriaExpression)null, null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(transaction, CriteriaExpression.Where(criteriaLambda), null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(transaction, CriteriaExpression.Where(criteriaLambda), null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, object criteria, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(transaction, CriteriaExpression.Where(criteriaLambda), criteria, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, object criteria, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(transaction, CriteriaExpression.Where(criteriaLambda), criteria, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbTransaction transaction, CriteriaExpression criteriaExpression, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(transaction, criteriaExpression, null, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbTransaction transaction, CriteriaExpression criteriaExpression, int pageNo, int pageSize = 10, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(transaction, criteriaExpression, null, pageNo, pageSize, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbTransaction transaction, CriteriaExpression criteriaExpression, object criteria, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            return SelectRecord<T, TSub>(transaction, criteriaExpression, criteria, 0, 10, orderBys);
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbTransaction transaction, CriteriaExpression criteriaExpression, object criteria, int pageNo, int pageSize, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(transaction))
            {
                return SelectRecord<T, TSub>(command, criteriaExpression, criteria, pageNo, pageSize, false, orderBys);
            }
        }

        /// <summary>
        /// 获取记录
        /// </summary>
        /// <param name="command">DbCommand(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="pageNo">页码(1..N, 0为不分页)</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="first">是否返回第一条记录</param>
        /// <param name="orderBys">排序队列</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord<T, TSub>(DbCommand command, CriteriaExpression criteriaExpression, object criteria, int pageNo, int pageSize = 10, bool first = false, params OrderBy<T>[] orderBys)
            where T : class
            where TSub : class
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            command.Parameters.Clear();

            Type subType = typeof(TSub);
            IDictionary<string, Property> properties = GetProperties(subType);
            if (properties.Count == 0)
                throw new InvalidOperationException(String.Format("查询记录时至少类/接口 {0} 应有一个属性", subType.FullName));

            string whereBody = null;
            if (criteriaExpression != null)
            {
                Sheet targetSheet = this;
                whereBody = AssembleWhereBody(command, criteriaExpression, criteria, ExecuteAction.None, ref targetSheet);
            }

            whereBody = AssembleWhereBody(command, whereBody);
            string uniqueColumnName = null;
            string orderBody = AssembleOrderBody(orderBys, ref uniqueColumnName);

            if (pageNo <= 0)
                command.CommandText = String.Format("select {0} from {1}{2}{3}{4}{5}",
                    AssembleColumnBody(properties), Name, whereBody != null ? " where " : null, whereBody, orderBody != null ? " order by " : null, orderBody);
            else
            {
#if PgSQL
                //select * from (select US_ID from PH7_User where US_Disabled = 0 order by US_ID limit 20 offset 80) a left join PH7_User b on a.US_ID = b.US_ID
                if (uniqueColumnName != null)
                    command.CommandText = String.Format("select {0} from (select {1} from {2}{3}{4} order by {5} limit {7} offset {6}) a left join {2} b on a.{1} = b.{1}",
                        AssembleColumnBody(properties), uniqueColumnName, Name, whereBody != null ? " where " : null, whereBody, orderBody, (pageNo - 1) * pageSize, pageSize);
                else
                    command.CommandText = String.Format("select {0} from {1}{2}{3}{4}{5} limit {7} offset {6}",
                        AssembleColumnBody(properties), Name, whereBody != null ? " where " : null, whereBody, orderBody != null ? " order by " : null, orderBody, (pageNo - 1) * pageSize, pageSize);
#endif
#if MsSQL
                // select * from (select row_number() over(order by US_ID) as rownumber, PH7_User.* from PH7_User where (US_Disabled = 0)) where rownumber > 80 and rownumber <= 100
                command.CommandText = String.Format("select {0} from (select row_number() over(order by {1}) as rownumber, {0} from {2}{3}{4}) where rownumber > {5} and rownumber <= {6}",
                    AssembleColumnBody(properties), orderBody != null ? orderBody : PrimaryKeyColumns[0].Name, Name, whereBody != null ? " where " : null, whereBody, (pageNo - 1) * pageSize, pageNo * pageSize);
#endif
#if MySQL
                //select * from (select US_ID from PH7_User where US_Disabled = 0 order by US_ID limit 80,20) a left join PH7_User b on a.US_ID = b.US_ID
                if (uniqueColumnName != null)
                    command.CommandText = String.Format("select {0} from (select {1} from {2}{3}{4} order by {5} limit {6},{7}) a left join {2} b on a.{1} = b.{1}",
                        AssembleColumnBody(properties), uniqueColumnName, Name, whereBody != null ? " where " : null, whereBody, orderBody, (pageNo - 1) * pageSize, pageSize);
                else
                    command.CommandText = String.Format("select {0} from {1}{2}{3}{4}{5} limit {6},{7}",
                        AssembleColumnBody(properties), Name, whereBody != null ? " where " : null, whereBody, orderBody != null ? " order by " : null, orderBody, (pageNo - 1) * pageSize, pageSize);
#endif
#if ORA
                if (orderBody != null)
                    // select * from (select p.*, rownum as rownumber from (select * from PH7_User where US_Disabled = 0 order by US_ID) p where rownum <= 10) where rownumber > 5
                    command.CommandText = String.Format("select {0} from (select {0}, rownum as rownumber from (select {0} from {1}{2}{3} order by {4}) p where rownum <= {6}) where rownumber > {5}",
                        AssembleColumnBody(properties), Name, whereBody != null ? " where " : null, whereBody, orderBody, (pageNo - 1) * pageSize, pageNo * pageSize);
                else
                    // select * from (select p.*, rownum as rownumber from PH7_User p where (US_Disabled = 0) and rownum <= 10) where rownumber > 5
                    command.CommandText = String.Format("select {0} from (select {0}, rownum as rownumber from {1} where {2} and rownum <= {4}) where rownumber > {3}",
                        AssembleColumnBody(properties), Name, whereBody != null ? String.Format("({0})", whereBody) : null, (pageNo - 1) * pageSize, pageNo * pageSize);
#endif
            }

            StringBuilder result = new StringBuilder();
            using (StringWriter stringWriter = new StringWriter(result, CultureInfo.InvariantCulture))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(stringWriter))
            {
                jsonWriter.DateFormatString = Utilities.JsonDateFormatString;
                jsonWriter.Formatting = Formatting.None;
                if (!first)
                    jsonWriter.WriteStartArray();
                using (DataReader reader = new DataReader(command, first ? CommandBehavior.SingleRow : CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        jsonWriter.WriteStartObject();
                        int i = 0;
                        foreach (KeyValuePair<string, Property> kvp in properties)
                        {
                            jsonWriter.WritePropertyName(kvp.Value.Column.PropertyName);
                            jsonWriter.WriteValue(reader.GetValue(i));
                            i = i + 1;
                        }

                        jsonWriter.WriteEndObject();
                    }
                }

                if (!first)
                    jsonWriter.WriteEndArray();
                jsonWriter.Flush();
            }

            return result.ToString();
        }

        /// <summary>
        /// 获取记录(JSON格式(仅返回匹配上本字段清单的属性值))
        /// </summary>
        /// <param name="reader">IDataReader</param>
        /// <param name="first">是否返回第一条记录</param>
        /// <returns>记录(JSON格式)</returns>
        public string SelectRecord(IDataReader reader, bool first = false)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            StringBuilder result = new StringBuilder();
            using (StringWriter stringWriter = new StringWriter(result, CultureInfo.InvariantCulture))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(stringWriter))
            {
                jsonWriter.DateFormatString = Utilities.JsonDateFormatString;
                jsonWriter.Formatting = Formatting.None;
                if (!first)
                    jsonWriter.WriteStartArray();

                Dictionary<int, Column> matchedColumns = null;
                while (reader.Read())
                {
                    jsonWriter.WriteStartObject();
                    if (matchedColumns == null)
                    {
                        matchedColumns = new Dictionary<int, Column>(reader.FieldCount);
                        for (int i = 0; i < reader.FieldCount; i++)
                            if (Columns.TryGetValue(reader.GetName(i), out Column column))
                            {
                                matchedColumns.Add(i, column);
                                jsonWriter.WritePropertyName(column.PropertyName);
                                jsonWriter.WriteValue(reader.GetValue(i));
                            }
                    }
                    else
                    {
                        foreach (KeyValuePair<int, Column> kvp in matchedColumns)
                        {
                            jsonWriter.WritePropertyName(kvp.Value.PropertyName);
                            jsonWriter.WriteValue(reader.GetValue(kvp.Key));
                        }
                    }

                    jsonWriter.WriteEndObject();
                    if (first)
                        break;
                }

                if (!first)
                    jsonWriter.WriteEndArray();
                jsonWriter.Flush();
            }

            return result.ToString();
        }

        #endregion

        #region RecordCount

        /// <summary>
        /// 获取记录数
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <returns>记录数</returns>
        public long RecordCount<T>(Expression<Func<T, bool>> criteriaLambda, object criteria = null)
            where T : class
        {
            return RecordCount(CriteriaExpression.Where(criteriaLambda), criteria);
        }

        /// <summary>
        /// 获取记录数
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <returns>记录数</returns>
        public long RecordCount(CriteriaExpression criteriaExpression = null, object criteria = null)
        {
            CheckDatabaseValidity();

            return Owner.Database.ExecuteGet((Func<DbConnection, CriteriaExpression, object, long>)RecordCount, criteriaExpression, criteria);
        }

        /// <summary>
        /// 获取记录数
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <returns>记录数</returns>
        public long RecordCount<T>(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, object criteria = null)
            where T : class
        {
            return RecordCount(connection, CriteriaExpression.Where(criteriaLambda), criteria);
        }

        /// <summary>
        /// 获取记录数
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <returns>记录数</returns>
        public long RecordCount(DbConnection connection, CriteriaExpression criteriaExpression = null, object criteria = null)
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(connection))
            {
                return RecordCount(command, criteriaExpression, criteria);
            }
        }

        /// <summary>
        /// 获取记录数
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <returns>记录数</returns>
        public long RecordCount<T>(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, object criteria = null)
            where T : class
        {
            return RecordCount(transaction, CriteriaExpression.Where(criteriaLambda), criteria);
        }

        /// <summary>
        /// 获取记录数
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <returns>记录数</returns>
        public long RecordCount(DbTransaction transaction, CriteriaExpression criteriaExpression = null, object criteria = null)
        {
            using (DbCommand command = DbCommandHelper.CreateCommand(transaction))
            {
                return RecordCount(command, criteriaExpression, criteria);
            }
        }

        /// <summary>
        /// 获取记录数
        /// </summary>
        /// <param name="command">DbCommand(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <returns>记录数</returns>
        public long RecordCount(DbCommand command, CriteriaExpression criteriaExpression = null, object criteria = null)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            command.Parameters.Clear();

            string whereBody = null;
            if (criteriaExpression != null)
            {
                Sheet targetSheet = this;
                whereBody = AssembleWhereBody(command, criteriaExpression, criteria, ExecuteAction.None, ref targetSheet);
            }

            whereBody = AssembleWhereBody(command, whereBody);
            command.CommandText = String.Format("select count(*) from {0}{1}{2}", Name, whereBody != null ? " where " : null, whereBody);
            return Convert.ToInt64(DbCommandHelper.ExecuteScalar(command));
        }

        #endregion

        /// <summary>
        /// 比较对象
        /// </summary>
        /// <param name="obj">对象</param>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, this))
                return true;
            Sheet other = obj as Sheet;
            if (object.ReferenceEquals(other, null))
                return false;
            return
                String.CompareOrdinal(_owner.Database.ConnectionString, other._owner.Database.ConnectionString) == 0 &&
                String.Compare(_name, other._name, StringComparison.OrdinalIgnoreCase) == 0;
        }

        /// <summary>
        /// 取哈希值(注意字符串在32位和64位系统有不同的算法得到不同的结果) 
        /// </summary>
        public override int GetHashCode()
        {
            return _owner.Database.ConnectionString.GetHashCode() ^
                   _name.ToUpper().GetHashCode();
        }

        #endregion
    }
}