using System;
using Phenix.Core.Data;

namespace Phenix.Core.Mapper.Expressions
{
    /// <summary>
    /// 顺序
    /// </summary>
    [Serializable]
    public enum Order
    {
        /// <summary>
        /// 升序
        /// </summary>
        [EnumCaption("升序", Key = "")]
        Ascending,

        /// <summary>
        /// 降序
        /// </summary>
        [EnumCaption("降序", Key = " DESC ")]
        Descending
    }
}