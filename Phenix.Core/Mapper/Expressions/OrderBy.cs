using System;
using System.Linq.Expressions;
using System.Text;
using Phenix.Core.Data;
using Phenix.Core.Reflection;

namespace Phenix.Core.Mapper.Expressions
{
    /// <summary>
    /// 排序
    /// </summary>
    [Serializable]
    public sealed class OrderBy<T> : OrderBy
    {
        [Newtonsoft.Json.JsonConstructor]
        internal OrderBy(string propertyName, Order order, OrderBy prior)
            : base(propertyName, order, prior)
        {
        }

        #region 方法

        /// <summary>
        /// 升序
        /// </summary>
        /// <param name="propertyLambda">含类属性的 lambda 表达式</param>
        public OrderBy<T> Ascending(Expression<Func<T, object>> propertyLambda)
        {
            return Ascending(propertyLambda, this);
        }

        /// <summary>
        /// 降序
        /// </summary>
        /// <param name="propertyLambda">含类属性的 lambda 表达式</param>
        public OrderBy<T> Descending(Expression<Func<T, object>> propertyLambda)
        {
            return Descending(propertyLambda, this);
        }

        #endregion
    }

    /// <summary>
    /// 排序
    /// </summary>
    [Serializable]
    public class OrderBy
    {
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="order">顺序</param>
        /// <param name="prior">先前</param>
        [Newtonsoft.Json.JsonConstructor]
        protected OrderBy(string propertyName, Order order, OrderBy prior)
        {
            _propertyName = propertyName;
            _order = order;
            _prior = prior;
        }

        #region 工厂

        /// <summary>
        /// 升序
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="prior">先前</param>
        public static OrderBy Ascending(string propertyName, OrderBy prior = null)
        {
            return new OrderBy(propertyName, Order.Ascending, prior);
        }

        /// <summary>
        /// 降序
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="prior">先前</param>
        public static OrderBy Descending(string propertyName, OrderBy prior = null)
        {
            return new OrderBy(propertyName, Order.Descending, prior);
        }

        /// <summary>
        /// 升序
        /// </summary>
        /// <param name="propertyLambda">含类属性的 lambda 表达式</param>
        /// <param name="prior">先前</param>
        public static OrderBy<T> Ascending<T>(Expression<Func<T, object>> propertyLambda, OrderBy prior = null)
        {
            return new OrderBy<T>(Utilities.GetPropertyInfo(propertyLambda).Name, Order.Ascending, prior);
        }

        /// <summary>
        /// 降序
        /// </summary>
        /// <param name="propertyLambda">含类属性的 lambda 表达式</param>
        /// <param name="prior">先前</param>
        public static OrderBy<T> Descending<T>(Expression<Func<T, object>> propertyLambda, OrderBy prior = null)
        {
            return new OrderBy<T>(Utilities.GetPropertyInfo(propertyLambda).Name, Order.Descending, prior);
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

        private readonly Order _order;

        /// <summary>
        /// 顺序
        /// </summary>
        public Order Order
        {
            get { return _order; }
        }

        private readonly OrderBy _prior;

        /// <summary>
        /// 先前
        /// </summary>
        public OrderBy Prior
        {
            get { return _prior; }
        }

        #endregion

        #region 方法

        /// <summary>
        /// 比较对象
        /// </summary>
        /// <param name="obj">对象</param>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, this))
                return true;
            OrderBy other = obj as OrderBy;
            if (object.ReferenceEquals(other, null))
                return false;
            if (String.CompareOrdinal(_propertyName, other._propertyName) != 0)
                return false;
            if (_order != other._order)
                return false;
            if (_prior != null)
            {
                if (!_prior.Equals(other._prior))
                    return false;
            }
            else if (other._prior != null)
                return false;

            return true;
        }

        /// <summary>
        /// 取哈希值(注意字符串在32位和64位系统有不同的算法得到不同的结果) 
        /// </summary>
        public override int GetHashCode()
        {
            int result = _propertyName.GetHashCode() ^ _order.GetHashCode();
            if (_prior != null)
                result = result ^ _prior.GetHashCode();
            return result;
        }

        /// <summary>
        /// 字符串表示
        /// </summary>
        public override string ToString()
        {
            StringBuilder result = new StringBuilder();
            OrderBy orderBy = this;
            do
            {
                result.Insert(0, ",");
                result.Insert(0, EnumKeyValue.Fetch(orderBy.Order).Key);
                result.Insert(0, orderBy.PropertyName);
                orderBy = orderBy.Prior;
            } while (orderBy != null);

            return result.ToString().TrimEnd(',');
        }

        #endregion
    }
}