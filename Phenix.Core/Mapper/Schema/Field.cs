using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
using Phenix.Core.Data;
using Phenix.Core.Mapper.Expressions;
using Phenix.Core.Reflection;
using Phenix.Core.SyncCollections;

namespace Phenix.Core.Mapper.Schema
{
    /// <summary>
    /// 数据映射字段信息
    /// </summary>
    public sealed class Field
    {
        private Field(Type ownerType, FieldInfo fieldInfo, Sheet ownerSheet)
        {
            _ownerType = ownerType;
            _fieldInfo = fieldInfo;
            _ownerSheet = ownerSheet;

            Property property = ownerSheet != null
                ? ownerSheet.GetProperty(ownerType, Standards.GetPropertyNameByFieldName(fieldInfo.Name), false)
                : null;
            _property = property;

            _column = property != null
                ? property.Column
                : ownerSheet != null
                    ? ownerSheet.FindColumn(fieldInfo.Name)
                    : null;

            _getValue = DynamicInstanceFactory.CreateFieldGetter(fieldInfo);
            _setValue = DynamicInstanceFactory.CreateFieldSetter(fieldInfo);
        }

        #region 工厂

        private static readonly SynchronizedDictionary<string, ReadOnlyDictionary<string, Field>> _classFieldsCache =
            new SynchronizedDictionary<string, ReadOnlyDictionary<string, Field>>(StringComparer.Ordinal);

        internal static IDictionary<string, Field> Fetch(Type entityType, Sheet ownerSheet = null)
        {
            entityType = Utilities.LoadType(entityType); //主要用于IDE环境、typeof(T)

            return _classFieldsCache.GetValue(Standards.FormatCompoundKey(entityType.FullName, ownerSheet != null ? ownerSheet.Name : null), () =>
            {
                Dictionary<string, Field> result = new Dictionary<string, Field>(StringComparer.Ordinal);
                Type type = entityType;
                while (!Utilities.IsNotApplicationType(type))
                {
                    foreach (FieldInfo item in type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.NonPublic))
                        if (!result.ContainsKey(item.Name))
                        {
                            Field field = new Field(entityType, item, ownerSheet);
                            if (ownerSheet == null || field.Column != null)
                                result.Add(item.Name, field);
                        }

                    type = type.BaseType;
                }

                return new ReadOnlyDictionary<string, Field>(result);
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

        private readonly FieldInfo _fieldInfo;

        /// <summary>
        /// 字段信息
        /// </summary>
        public FieldInfo FieldInfo
        {
            get { return _fieldInfo; }
        }

        private readonly Sheet _ownerSheet;

        /// <summary>
        /// 所属单子
        /// </summary>
        public Sheet OwnerSheet
        {
            get { return _ownerSheet; }
        }

        private readonly Property _property;

        /// <summary>
        /// 数据映射属性信息
        /// </summary>
        public Property Property
        {
            get { return _property; }
        }

        private readonly Column _column;

        /// <summary>
        /// 字段
        /// </summary>
        public Column Column
        {
            get { return _column; }
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
        public bool Set<T>(T entity, object value, bool throwIfNotFound = true)
            where T : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (_setValue != null)
            {
                if (value is OperationExpression operationExpression)
                    value = operationExpression.Compute(entity);
                value = Utilities.ChangeType(value, _fieldInfo.FieldType);
                _setValue(entity, value);
                return true;
            }

            if (throwIfNotFound)
                throw new InvalidOperationException(String.Format("类 {0} 的 {1} 字段应去除 readonly 标记", _ownerType.FullName, _fieldInfo.Name));
            return false;
        }

        /// <summary>
        /// 比较对象
        /// </summary>
        /// <param name="obj">对象</param>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, this))
                return true;
            Field other = obj as Field;
            if (object.ReferenceEquals(other, null))
                return false;
            return String.CompareOrdinal(_ownerType.FullName, other._ownerType.FullName) == 0 &&
                   String.CompareOrdinal(_fieldInfo.Name, other._fieldInfo.Name) == 0 &&
                   object.Equals(_ownerSheet, other._ownerSheet);
        }

        /// <summary>
        /// 取哈希值(注意字符串在32位和64位系统有不同的算法得到不同的结果) 
        /// </summary>
        public override int GetHashCode()
        {
            return _ownerType.FullName.GetHashCode() ^
                   _fieldInfo.Name.GetHashCode() ^
                   (_ownerSheet != null ? _ownerSheet.GetHashCode() : 0);
        }

        #endregion
    }
}