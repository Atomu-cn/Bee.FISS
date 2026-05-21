using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Phenix.Core.Mapper.Schema;

namespace Phenix.Core.Data.Common
{
    /// <summary>
    /// 数据读取器扩展
    /// </summary>
    public static class DataReaderExtension
    {
        #region Select

        /// <summary>
        /// 读取Entity记录
        /// </summary>
        /// <param name="dataReader">DataReader</param>
        /// <param name="keyLambda">键 lambda 表达式</param>
        public static IDictionary<TKey, T> ReadEntity<TKey, T>(this DataReader dataReader, Expression<Func<T, TKey>> keyLambda)
            where T : class
        {
            if (dataReader == null)
                throw new ArgumentNullException(nameof(dataReader));

            return MetaData.Fetch(dataReader.Database).FindSheet<T>(true).SelectEntity(dataReader, keyLambda);
        }

        /// <summary>
        /// 读取Entity记录
        /// </summary>
        /// <param name="dataReader">DataReader</param>
        /// <param name="first">是否返回第一条记录</param>
        public static IList<T> ReadEntity<T>(this DataReader dataReader, bool first = false)
            where T : class
        {
            if (dataReader == null)
                throw new ArgumentNullException(nameof(dataReader));

            return MetaData.Fetch(dataReader.Database).FindSheet<T>(true).SelectEntity<T>(dataReader, first);
        }

        #endregion
    }
}