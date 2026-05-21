using System;
using Phenix.Core.Data;

namespace Phenix.Core.Mapper.Expressions
{
    /// <summary>
    /// 条件组合关系
    /// </summary>
    [Serializable]
    public enum CriteriaLogical
    {
        /// <summary>
        /// and
        /// </summary>
        [EnumCaption("且", Key = " and ")]
        And,

        /// <summary>
        /// or
        /// </summary>
        [EnumCaption("或者", Key = " or ")]
        Or,

        /// <summary>
        /// not
        /// </summary>
        [EnumCaption("不是", Key = " not ")]
        Not
    }
}