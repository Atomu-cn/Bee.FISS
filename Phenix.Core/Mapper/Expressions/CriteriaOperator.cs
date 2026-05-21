using System;
using Phenix.Core.Data;

namespace Phenix.Core.Mapper.Expressions
{
    /// <summary>
    /// 条件运算符
    /// </summary>
    [Serializable]
    public enum CriteriaOperator
    {
        /// <summary>
        /// 无(不参与SQL的拼装但参与参数定义和赋值)
        /// </summary>
        None,

        /// <summary>
        /// 嵌入  
        /// </summary>
        Embed,

        /// <summary>
        /// 等于  
        /// </summary>
        [EnumCaption("=", Key = "=")]
        Equal,

        /// <summary>
        /// 大于
        /// </summary>
        [EnumCaption(">", Key = ">")]
        Greater,

        /// <summary>
        /// 大于等于
        /// </summary>
        [EnumCaption(">=", Key = ">=")]
        GreaterOrEqual,

        /// <summary>
        /// 小于
        /// </summary>
        [EnumCaption("<", Key = "<")]
        Lesser,

        /// <summary>
        /// 小于等于
        /// </summary>
        [EnumCaption("<=", Key = "<=")]
        LesserOrEqual,

        /// <summary>
        /// 不等于
        /// </summary>
        [EnumCaption("<>", Key = "<>")]
        Unequal,

        /// <summary>
        /// 像
        /// </summary>
        [EnumCaption("像", Key = " like ")]
        Like,

        /// <summary>
        /// 像左侧
        /// </summary>
        [EnumCaption("像左侧", Key = " like ")]
        LikeLeft,

        /// <summary>
        /// 像右侧
        /// </summary>
        [EnumCaption("像右侧", Key = " like ")]
        LikeRight,

        /// <summary>
        /// 不像
        /// </summary>
        [EnumCaption("不像", Key = " not like ")]
        Unlike,

        /// <summary>
        /// 是空值
        /// </summary>
        [EnumCaption("是空值", Key = " is null ")]
        IsNull,

        /// <summary>
        /// 非空值
        /// </summary>
        [EnumCaption("非空值", Key = " is not null ")]
        IsNotNull,

        /// <summary>
        /// 包含(适用于关联关系也支持Array)
        /// </summary>
        [EnumCaption("包含", Key = " in ")]
        In,

        /// <summary>
        /// 不包含(适用于关联关系也支持Array)
        /// </summary>
        [EnumCaption("不包含", Key = " not in ")]
        NotIn,

        /// <summary>
        /// 存在于(适用于关联关系)
        /// </summary>
        [EnumCaption("存在于", Key = " exists ")]
        Exists,

        /// <summary>
        /// 不存在于(适用于关联关系)
        /// </summary>
        [EnumCaption("不存在于", Key = " not exists ")]
        NotExists
    }
}