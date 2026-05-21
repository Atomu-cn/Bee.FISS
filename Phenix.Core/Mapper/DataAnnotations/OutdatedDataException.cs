using System;
using Phenix.Core.Data.Validation;

namespace Phenix.Core.Mapper.DataAnnotations
{
    /// <summary>
    /// 数据过时异常
    /// </summary>
    [Serializable]
    public class OutdatedDataException : ValidationException
    {
        /// <summary>
        /// 数据过时异常
        /// </summary>
        public OutdatedDataException(Exception innerException = null)
            : this(AppSettings.GetValue("提交的数据已过时，请获取最新数据，编辑后再提交!"), innerException)
        {
        }

        /// <summary>
        /// 数据过时异常
        /// </summary>
        public OutdatedDataException(string message, Exception innerException = null)
            : base(null, 2, message, innerException)
        {
        }
    }
}