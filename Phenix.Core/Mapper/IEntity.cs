using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq.Expressions;
using Phenix.Core.Data;
using Phenix.Core.Mapper.Expressions;
using Phenix.Core.Mapper.Schema;

namespace Phenix.Core.Mapper
{
    /// <summary>
    /// 实体接口
    /// </summary>
    public interface IEntity<T> : IEntity
    {
        #region 方法

        #region InsertOrUpdateSelf

        /// <summary>
        /// 新增自己如遇唯一键冲突则更新记录
        /// </summary>
        /// <returns>更新记录数</returns>
        int InsertOrUpdateSelf();

        /// <summary>
        /// 新增自己如遇唯一键冲突则更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <returns>更新记录数</returns>
        int InsertOrUpdateSelf(DbConnection connection);

        /// <summary>
        /// 新增自己如遇唯一键冲突则更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <returns>更新记录数</returns>
        int InsertOrUpdateSelf(DbTransaction transaction);

        #endregion

        #region InsertSelf

        /// <summary>
        /// 新增自己
        /// </summary>
        /// <returns>更新记录数</returns>
        int InsertSelf();

        /// <summary>
        /// 新增自己
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <returns>更新记录数</returns>
        int InsertSelf(DbConnection connection);

        /// <summary>
        /// 新增自己
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <returns>更新记录数</returns>
        int InsertSelf(DbTransaction transaction);

        #endregion

        #region UpdateSelf

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(bool checkTimestamp = true, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(Expression<Func<T, bool>> criteriaLambda, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(Expression<Func<T, bool>> criteriaLambda, bool checkTimestamp = true, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(Expression<Func<T, bool>> criteriaLambda, object criteria, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(Expression<Func<T, bool>> criteriaLambda, object criteria, bool checkTimestamp = true, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(CriteriaExpression criteriaExpression, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(CriteriaExpression criteriaExpression, bool checkTimestamp = true, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(CriteriaExpression criteriaExpression, object criteria, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(CriteriaExpression criteriaExpression, object criteria, bool checkTimestamp = true, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbConnection connection, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbConnection connection, bool checkTimestamp = true, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, bool checkTimestamp = true, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, object criteria, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, object criteria, bool checkTimestamp = true, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbConnection connection, CriteriaExpression criteriaExpression, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbConnection connection, CriteriaExpression criteriaExpression, bool checkTimestamp = true, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbConnection connection, CriteriaExpression criteriaExpression, object criteria, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbConnection connection, CriteriaExpression criteriaExpression, object criteria, bool checkTimestamp = true, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbTransaction transaction, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbTransaction transaction, bool checkTimestamp = true, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, bool checkTimestamp = true, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, object criteria, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, object criteria, bool checkTimestamp = true, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbTransaction transaction, CriteriaExpression criteriaExpression, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbTransaction transaction, CriteriaExpression criteriaExpression, bool checkTimestamp = true, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbTransaction transaction, CriteriaExpression criteriaExpression, object criteria, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbTransaction transaction, CriteriaExpression criteriaExpression, object criteria, bool checkTimestamp = true, params NameValue<T>[] propertyValues);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(T source, bool checkTimestamp = true);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(IDictionary<string, object> propertyValues, bool checkTimestamp = true);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(Expression<Func<T, bool>> criteriaLambda, IDictionary<string, object> propertyValues, bool checkTimestamp = true);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(Expression<Func<T, bool>> criteriaLambda, object criteria, IDictionary<string, object> propertyValues, bool checkTimestamp = true);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(CriteriaExpression criteriaExpression, IDictionary<string, object> propertyValues, bool checkTimestamp = true);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(CriteriaExpression criteriaExpression, object criteria, IDictionary<string, object> propertyValues, bool checkTimestamp = true);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbConnection connection, IDictionary<string, object> propertyValues, bool checkTimestamp = true);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, IDictionary<string, object> propertyValues, bool checkTimestamp = true);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, object criteria, IDictionary<string, object> propertyValues, bool checkTimestamp = true);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbConnection connection, CriteriaExpression criteriaExpression, IDictionary<string, object> propertyValues, bool checkTimestamp = true);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbConnection connection, CriteriaExpression criteriaExpression, object criteria, IDictionary<string, object> propertyValues, bool checkTimestamp = true);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbTransaction transaction, IDictionary<string, object> propertyValues, bool checkTimestamp = true);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, IDictionary<string, object> propertyValues, bool checkTimestamp = true);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, object criteria, IDictionary<string, object> propertyValues, bool checkTimestamp = true);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbTransaction transaction, CriteriaExpression criteriaExpression, IDictionary<string, object> propertyValues, bool checkTimestamp = true);

        /// <summary>
        /// 更新记录
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="propertyValues">待更新属性值队列(如果没有set语句的话就直接更新字段，null代表提交的是实体本身)</param>
        /// <param name="checkTimestamp">是否检查时间戳（不一致时抛出Phenix.Core.Mapper.DataAnnotations.OutdatedDataException）</param>
        /// <returns>更新记录数</returns>
        int UpdateSelf(DbTransaction transaction, CriteriaExpression criteriaExpression, object criteria, IDictionary<string, object> propertyValues, bool checkTimestamp = true);

        #endregion

        #region DeleteSelf

        /// <summary>
        /// 删除自己
        /// </summary>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        int DeleteSelf(bool cascade = false);

        /// <summary>
        /// 删除自己
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        int DeleteSelf(Expression<Func<T, bool>> criteriaLambda, bool cascade = false);

        /// <summary>
        /// 删除自己
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        int DeleteSelf(Expression<Func<T, bool>> criteriaLambda, object criteria, bool cascade = false);

        /// <summary>
        /// 删除自己
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        int DeleteSelf(CriteriaExpression criteriaExpression, bool cascade = false);

        /// <summary>
        /// 删除自己
        /// </summary>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        int DeleteSelf(CriteriaExpression criteriaExpression, object criteria, bool cascade = false);

        /// <summary>
        /// 删除自己
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        int DeleteSelf(DbConnection connection, bool cascade = false);

        /// <summary>
        /// 删除自己
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        int DeleteSelf(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, bool cascade = false);

        /// <summary>
        /// 删除自己
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        int DeleteSelf(DbConnection connection, Expression<Func<T, bool>> criteriaLambda, object criteria, bool cascade = false);

        /// <summary>
        /// 删除自己
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        int DeleteSelf(DbConnection connection, CriteriaExpression criteriaExpression, bool cascade = false);

        /// <summary>
        /// 删除自己
        /// </summary>
        /// <param name="connection">DbConnection(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        int DeleteSelf(DbConnection connection, CriteriaExpression criteriaExpression, object criteria, bool cascade = false);

        /// <summary>
        /// 删除自己
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        int DeleteSelf(DbTransaction transaction, bool cascade = false);

        /// <summary>
        /// 删除自己
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        int DeleteSelf(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, bool cascade = false);

        /// <summary>
        /// 删除自己
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaLambda">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        int DeleteSelf(DbTransaction transaction, Expression<Func<T, bool>> criteriaLambda, object criteria, bool cascade = false);

        /// <summary>
        /// 删除自己
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        int DeleteSelf(DbTransaction transaction, CriteriaExpression criteriaExpression, bool cascade = false);

        /// <summary>
        /// 删除自己
        /// </summary>
        /// <param name="transaction">DbTransaction(注意跨库风险未作校验)</param>
        /// <param name="criteriaExpression">条件表达式</param>
        /// <param name="criteria">条件对象/JSON格式字符串/属性值队列</param>
        /// <param name="cascade">是否级联</param>
        /// <returns>删除记录数</returns>
        int DeleteSelf(DbTransaction transaction, CriteriaExpression criteriaExpression, object criteria, bool cascade = false);

        #endregion

        #endregion
    }

    /// <summary>
    /// 实体接口
    /// </summary>
    public interface IEntity
    {
        #region 属性

        /// <summary>
        /// 数据库入口
        /// </summary>
        public Database Database { get; set; }

        /// <summary>
        /// 操作单子
        /// </summary>
        public Sheet SelfSheet { get; set; }

        /// <summary>
        /// 根实体
        /// </summary>
        IEntity Root { get; }

        /// <summary>
        /// 主实体
        /// </summary>
        IEntity Master { get; }

        #endregion
    }
}
