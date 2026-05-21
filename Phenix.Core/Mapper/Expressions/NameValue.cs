using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Phenix.Core.Reflection;

namespace Phenix.Core.Mapper.Expressions
{
    /// <summary>
    /// 键值对
    /// </summary>
    [Serializable]
    public sealed class NameValue<T> : NameValue
    {
        [Newtonsoft.Json.JsonConstructor]
        internal NameValue(string propertyName, object value, NameValue prior)
            : base(propertyName, value, prior)
        {
        }

        #region 方法

        /// <summary>
        /// 设置键值对
        /// </summary>
        /// <param name="propertyLambda">含类属性的 lambda 表达式</param>
        /// <param name="value">值</param>
        public NameValue<T> Set(Expression<Func<T, object>> propertyLambda, object value)
        {
            return Set(propertyLambda, value, this);
        }

        /// <summary>
        /// 设置键值对
        /// </summary>
        /// <param name="propertyLambda">含类属性的 lambda 表达式</param>
        /// <param name="valueLambda">值 lambda 表达式</param>
        public NameValue<T> Set(Expression<Func<T, object>> propertyLambda, Expression<Func<T, object>> valueLambda)
        {
            return Set(propertyLambda, valueLambda, this);
        }

        /// <summary>
        /// 转换为数据字典
        /// </summary>
        /// <param name="nameValues">键值对队列</param>
        /// <returns>Name-Value</returns>
        public static IDictionary<string, object> ToDictionary(params NameValue<T>[] nameValues)
        {
            if (nameValues == null)
                return null;

            Dictionary<string, object> result = new Dictionary<string, object>(nameValues.Length);
            foreach (NameValue<T> item in nameValues)
            {
                if (item.Prior != null)
                    foreach (KeyValuePair<string, object> kvp in ToDictionary(item.Prior))
                        result.Add(kvp.Key, kvp.Value);
                result.Add(item.PropertyName, item.Value);
            }

            return result;
        }

        #endregion
    }

    /// <summary>
    /// 键值对
    /// </summary>
    [Serializable]
    public class NameValue
    {
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="value">值</param>
        /// <param name="prior">先前</param>
        [Newtonsoft.Json.JsonConstructor]
        protected NameValue(string propertyName, object value, NameValue prior)
        {
            _propertyName = propertyName;
            _value = value;
            _prior = prior;
        }

        #region 工厂

        /// <summary>
        /// 设置键值对
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="value">值</param>
        /// <param name="prior">先前</param>
        public static NameValue Set(string propertyName, object value, NameValue prior = null)
        {
            return new NameValue(propertyName, value, prior);
        }

        /// <summary>
        /// 设置键值对
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="value">值</param>
        /// <param name="prior">先前</param>
        public static NameValue<T> Set<T>(string propertyName, object value, NameValue prior = null)
        {
            return new NameValue<T>(propertyName, value, prior);
        }

        /// <summary>
        /// 设置键值对
        /// </summary>
        /// <param name="propertyLambda">含类属性的 lambda 表达式</param>
        /// <param name="value">值</param>
        /// <param name="prior">先前</param>
        public static NameValue<T> Set<T>(Expression<Func<T, object>> propertyLambda, object value, NameValue prior = null)
        {
            return new NameValue<T>(Utilities.GetPropertyInfo(propertyLambda).Name, value, prior);
        }

        /// <summary>
        /// 设置键值对
        /// </summary>
        /// <param name="propertyLambda">含类属性的 lambda 表达式</param>
        /// <param name="valueLambda">值 lambda 表达式</param>
        /// <param name="prior">先前</param>
        public static NameValue<T> Set<T>(Expression<Func<T, object>> propertyLambda, Expression<Func<T, object>> valueLambda, NameValue prior = null)
        {
            return new NameValue<T>(Utilities.GetPropertyInfo(propertyLambda).Name, OperationExpression.Compute(valueLambda), prior);
        }

        #endregion

        #region 属性

        private readonly string _propertyName;

        /// <summary>
        /// 属性名
        /// </summary>
        public string PropertyName
        {
            get { return _propertyName; }
        }

        private readonly object _value;

        /// <summary>
        /// 值/OperationExpression
        /// </summary>
        public object Value
        {
            get { return _value; }
        }

        private readonly NameValue _prior;

        /// <summary>
        /// 先前
        /// </summary>
        public NameValue Prior
        {
            get { return _prior; }
        }

        #endregion

        #region 方法

        /// <summary>
        /// 转换为数据字典
        /// </summary>
        /// <param name="nameValues">键值对队列</param>
        /// <returns>Name-Value</returns>
        public static IDictionary<string, object> ToDictionary(params NameValue[] nameValues)
        {
            if (nameValues == null)
                return null;

            Dictionary<string, object> result = new Dictionary<string, object>(nameValues.Length);
            foreach (NameValue item in nameValues)
            {
                if (item.Prior != null)
                    foreach (KeyValuePair<string, object> kvp in ToDictionary(item.Prior))
                        result.Add(kvp.Key, kvp.Value);
                result.Add(item.PropertyName, item.Value);
            }

            return result;
        }

        #endregion
    }
}