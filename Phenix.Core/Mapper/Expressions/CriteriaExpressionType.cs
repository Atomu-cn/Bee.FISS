using System;

namespace Phenix.Core.Mapper.Expressions
{
    /// <summary>
    /// 条件表达式类型
    /// </summary>
    [Serializable]
    public enum CriteriaExpressionType
    {
        /// <summary>
        /// 组合
        /// </summary>
        CriteriaLogical,

        /// <summary>
        /// 运算
        /// </summary>
        CriteriaOperate,

        /// <summary>
        /// 子条件
        /// </summary>
        ExistsOrNotExists,

        /// <summary>
        /// 短路
        /// </summary>
        Short
    }
}