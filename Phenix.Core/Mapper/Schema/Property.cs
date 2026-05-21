using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Phenix.Core.Data;
using Phenix.Core.Mapper.Expressions;
using Phenix.Core.Reflection;
using Phenix.Core.SyncCollections;

namespace Phenix.Core.Mapper.Schema
{
    /// <summary>
    /// 数据映射属性信息
    /// </summary>
    public sealed class Property
    {
        private Property(Type ownerType, PropertyInfo propertyInfo, Sheet ownerSheet)
        {
            _ownerType = ownerType;
            _propertyInfo = propertyInfo;
            _ownerSheet = ownerSheet;

            _field = ownerSheet != null
                ? ownerSheet.GetField(ownerType, Standards.GetFieldNameByPropertyName(propertyInfo.Name), false)
                : null;

            ColumnAttribute columnAttribute = (ColumnAttribute)Attribute.GetCustomAttribute(ownerType, typeof(ColumnAttribute));
            _columnAttribute = columnAttribute;

            Column column = ownerSheet != null
                ? ownerSheet.FindColumn(columnAttribute != null ? columnAttribute.Name : propertyInfo.Name)
                : null;
            _column = column;

            DisplayAttribute displayAttribute = (DisplayAttribute)Attribute.GetCustomAttributes(propertyInfo, typeof(DisplayAttribute)).SingleOrDefault();
            if (displayAttribute != null && !String.IsNullOrEmpty(displayAttribute.Description))
                _description = AppRun.SplitCulture(displayAttribute.Description);
            else if (column != null && !String.IsNullOrEmpty(column.Description))
                _description = AppRun.SplitCulture(column.Description);
            else
                _description = AppRun.SplitCulture(propertyInfo.Name);

            List<ValidationAttribute> validationAttributes = new List<ValidationAttribute>((ValidationAttribute[])Attribute.GetCustomAttributes(propertyInfo, typeof(ValidationAttribute)));
            if (column != null)
            {
                bool foundRequiredAttribute = false;
                bool foundStringLengthAttribute = false;
                foreach (ValidationAttribute item in validationAttributes)
                {
                    if (String.CompareOrdinal(item.GetType().FullName, typeof(RequiredAttribute).FullName) == 0) //主要用于IDE环境
                        foundRequiredAttribute = true;
                    if (String.CompareOrdinal(item.GetType().FullName, typeof(StringLengthAttribute).FullName) == 0) //主要用于IDE环境
                        foundStringLengthAttribute = true;
                }

                if (!foundRequiredAttribute && !column.Nullable)
                {
                    RequiredAttribute attribute = new RequiredAttribute();
                    attribute.ErrorMessage = String.Format(AppSettings.GetValue(attribute.GetType().Name, "请填写完整: 属性 {0} 值不允许为空!"), Description);
                    validationAttributes.Add(attribute);
                }

                if (!foundStringLengthAttribute && column.MappingType == typeof(string) && column.DataLength <= Int32.MaxValue)
                {
                    StringLengthAttribute attribute = new StringLengthAttribute((int)column.DataLength);
                    attribute.ErrorMessage = String.Format(AppSettings.GetValue(attribute.GetType().Name, "请精简内容: {0} 的字数不得超过 {1} 个!"), Description, Column.DataLength);
                    validationAttributes.Add(attribute);
                }
            }

            _validationAttributes = new ReadOnlyCollection<ValidationAttribute>(validationAttributes);

            _getValue = DynamicInstanceFactory.CreatePropertyGetter(propertyInfo);
            _setValue = DynamicInstanceFactory.CreatePropertySetter(propertyInfo);
        }

        #region 工厂

        private static readonly SynchronizedDictionary<string, ReadOnlyDictionary<string, Property>> _classPropertiesCache =
            new SynchronizedDictionary<string, ReadOnlyDictionary<string, Property>>(StringComparer.Ordinal);

        internal static IDictionary<string, Property> Fetch(Type entityType, Sheet ownerSheet = null)
        {
            entityType = Utilities.LoadType(entityType); //主要用于IDE环境、typeof(T)

            return _classPropertiesCache.GetValue(Standards.FormatCompoundKey(entityType.FullName, ownerSheet != null ? ownerSheet.Name : null), () =>
            {
                Dictionary<string, Property> result = new Dictionary<string, Property>(StringComparer.Ordinal);
                Type type = entityType;
                while (!Utilities.IsNotApplicationType(type))
                {
                    foreach (PropertyInfo item in type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        if (!result.ContainsKey(item.Name))
                        {
                            Property property = new Property(entityType, item, ownerSheet);
                            if (ownerSheet == null || property.Column != null)
                                result.Add(item.Name, property);
                        }

                    type = type.BaseType;
                }

                return new ReadOnlyDictionary<string, Property>(result);
            });
        }

        #endregion

        #region 属性

        private readonly Type _ownerType;

        /// <summary>
        /// 所属类
        /// </summary>
        public Type OwnerType
        {
            get { return _ownerType; }
        }

        private readonly PropertyInfo _propertyInfo;

        /// <summary>
        /// 属性信息
        /// </summary>
        public PropertyInfo PropertyInfo
        {
            get { return _propertyInfo; }
        }

        private readonly Sheet _ownerSheet;

        /// <summary>
        /// 所属单子
        /// </summary>
        public Sheet OwnerSheet
        {
            get { return _ownerSheet; }
        }

        private readonly Field _field;

        /// <summary>
        /// 数据映射字段信息
        /// </summary>
        public Field Field
        {
            get { return _field; }
        }

        private readonly ColumnAttribute _columnAttribute;

        /// <summary>
        /// 字段映射标签
        /// </summary>
        public ColumnAttribute ColumnAttribute
        {
            get { return _columnAttribute; }
        }

        private readonly Column _column;

        /// <summary>
        /// 字段
        /// </summary>
        public Column Column
        {
            get { return _column; }
        }

        private readonly string _description;

        /// <summary>
        /// 优先取 DisplayAttribute.Description(中英文用‘|’分隔)
        /// 其次取 Column.Description
        /// 最后取 PropertyName
        /// </summary>
        public string Description
        {
            get { return _description; }
        }

        private readonly ReadOnlyCollection<ValidationAttribute> _validationAttributes;

        /// <summary>
        /// 校验规则标签
        /// </summary>
        public IList<ValidationAttribute> ValidationAttributes
        {
            get { return _validationAttributes; }
        }

        private readonly DynamicMemberGetDelegate _getValue;

        /// <summary>
        /// 动态执行get函数的委托函数
        /// </summary>
        public DynamicMemberGetDelegate GetValue
        {
            get { return _getValue; }
        }

        private readonly DynamicMemberSetDelegate _setValue;

        private DynamicMemberSetDelegate SetValue
        {
            get { return _setValue; }
        }

        #endregion

        #region 方法

        /// <summary>
        /// 设置值
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="valueLambda">值 lambda 表达式</param>
        /// <param name="throwIfNotFound">如果为 true, 则会在找不到Set函数时引发 InvalidOperationException; 如果为 false, 则在找不到Set函数时返回 false</param>
        /// <returns>是否成功</returns>
        public bool Set<T>(T entity, Expression<Func<T, object>> valueLambda, bool throwIfNotFound = true)
            where T : class
        {
            return Set(entity, OperationExpression.Compute(valueLambda), throwIfNotFound);
        }

        /// <summary>
        /// 设置值
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="value">值/OperationExpression</param>
        /// <param name="throwIfNotFound">如果为 true, 则会在找不到Set函数时引发 InvalidOperationException; 如果为 false, 则在找不到Set函数时返回 false</param>
        /// <returns>是否成功</returns>
        public bool Set<T>(T entity, object value, bool throwIfNotFound = true)
            where T : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (_setValue != null)
            {
                if (value is OperationExpression operationExpression)
                    value = operationExpression.Compute(entity);
                value = Utilities.ChangeType(value, _propertyInfo.PropertyType);
                _setValue(entity, value);
                return true;
            }

            if (throwIfNotFound)
                throw new InvalidOperationException(String.Format("类 {0} 的 {1} 属性应包含set语句{2}",
                    _ownerType.FullName, _propertyInfo.Name, Column != null ? String.Format("或改名以不映射 {0}.{1} 表字段", Column.Owner.Name, Column.Name) : null));
            return false;
        }

        /// <summary>
        /// 核对值的有效性
        /// </summary>
        /// <param name="value">值</param>
        public void Validate(object value)
        {
            if (ValidationAttributes.Count > 0)
            {
                if (value is OperationExpression)
                    return;
                if (Column != null)
                    value = Utilities.ChangeType(value, Column.MappingType);
                foreach (ValidationAttribute item in ValidationAttributes)
                    if (!item.IsValid(value))
                        throw new ValidationException(new Phenix.Core.Data.Validation.ValidationResult(null, value, String.Format("{0}({1}): {2}", Description, value, item.ErrorMessage)), item, value);
            }
        }

        /// <summary>
        /// 核对值的有效性
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="entity">实体</param>
        public void Validate<T>(object value, T entity)
            where T : class
        {
            if (ValidationAttributes.Count > 0)
            {
                if (value is OperationExpression)
                    return;
                if (Column != null)
                    value = Utilities.ChangeType(value, Column.MappingType);
                ValidationContext validationContext = new ValidationContext(entity);
                validationContext.MemberName = _propertyInfo.Name;
                validationContext.DisplayName = Description;
                foreach (ValidationAttribute item in ValidationAttributes)
                    item.Validate(value, validationContext);
            }
        }

        /// <summary>
        /// 比较对象
        /// </summary>
        /// <param name="obj">对象</param>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, this))
                return true;
            Property other = obj as Property;
            if (object.ReferenceEquals(other, null))
                return false;
            return String.CompareOrdinal(_ownerType.FullName, other._ownerType.FullName) == 0 &&
                   String.CompareOrdinal(_propertyInfo.Name, other._propertyInfo.Name) == 0 &&
                   object.Equals(_ownerSheet, other._ownerSheet);
        }

        /// <summary>
        /// 取哈希值(注意字符串在32位和64位系统有不同的算法得到不同的结果) 
        /// </summary>
        public override int GetHashCode()
        {
            return _ownerType.FullName.GetHashCode() ^
                   _propertyInfo.Name.GetHashCode() ^
                   (_ownerSheet != null ? _ownerSheet.GetHashCode() : 0);
        }

        #endregion
    }
}