using System;
using Phenix.Core.Data.Validation;

namespace Phenix.Core.Mapper.DataAnnotations
{
    /// <summary>
    /// 关联数据异常
    /// </summary>
    [Serializable]
    public class AssociationDataException : ValidationException
    {
        /// <summary>
        /// 关联数据异常
        /// </summary>
        public AssociationDataException(Exception innerException = null)
            : this(AppSettings.GetValue("提交的数据已被使用, 不允许删除!"), innerException)
        {
        }

        /// <summary>
        /// 关联数据异常
        /// </summary>
        public AssociationDataException(string message, Exception innerException = null)
            : base(null, 3, message, innerException)
        {
        }
    }
}