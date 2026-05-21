using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Phenix.Core.Reflection;

namespace Phenix.Core.Mapper.Expressions
{
    /// <summary>
    /// 运算表达式
    /// </summary>
    [Serializable]
    public sealed class OperationExpression
    {
        [Newtonsoft.Json.JsonConstructor]
        private OperationExpression(string ownerTypeAssemblyQualifiedName, string memberName,
            OperationExpression leftOperation, OperationSign sign, OperationExpression rightOperation,
            object value, bool haveValue, OperationExpression[] arguments)
        {
            _ownerTypeAssemblyQualifiedName = ownerTypeAssemblyQualifiedName;
            _memberName = memberName;
            _leftOperation = leftOperation;
            _sign = sign;
            _rightOperation = rightOperation;
            _value = value;
            _haveValue = haveValue;
            _arguments = arguments;
        }

        private OperationExpression(Type ownerType, string memberName)
        {
            _ownerTypeAssemblyQualifiedName = ownerType.AssemblyQualifiedName;
            _ownerType = ownerType;
            _memberName = memberName;
        }

        /// <summary>
        /// 运算表达式
        /// </summary>
        /// <param name="memberInfo">MemberInfo</param>
        public OperationExpression(MemberInfo memberInfo)
            : this(memberInfo.ReflectedType ?? memberInfo.DeclaringType, memberInfo.Name)
        {
            _memberInfo = memberInfo;
        }

        /// <summary>
        /// 运算表达式
        /// </summary>
        /// <param name="value">值</param>
        public OperationExpression(object value)
        {
            _value = value;
            _haveValue = true;
        }

        #region 用于构建运算表达式

        private OperationExpression(OperationExpression leftOperation, OperationSign sign, OperationExpression rightOperation, params OperationExpression[] arguments)
        {
            if (sign == OperationSign.Length ||
                sign == OperationSign.ToLower || sign == OperationSign.ToUpper ||
                sign == OperationSign.TrimStart || sign == OperationSign.TrimEnd || sign == OperationSign.Trim ||
                sign == OperationSign.Substring)
                throw new ArgumentException(String.Format("不允许'{0}'有左节点", sign), nameof(sign));

            _leftOperation = leftOperation;
            _sign = sign;
            _rightOperation = rightOperation;
            _arguments = arguments;
        }

        private OperationExpression(OperationExpression leftOperation, OperationSign sign, object value, params OperationExpression[] arguments)
        {
            if (sign == OperationSign.Length ||
                sign == OperationSign.ToLower || sign == OperationSign.ToUpper ||
                sign == OperationSign.TrimStart || sign == OperationSign.TrimEnd || sign == OperationSign.Trim ||
                sign == OperationSign.Substring)
                throw new ArgumentException(String.Format("不允许'{0}'有左节点", sign), nameof(sign));

            _leftOperation = leftOperation;
            _sign = sign;
            _value = value;
            _haveValue = true;
            _arguments = arguments;
        }

        private OperationExpression(object value, OperationSign sign, OperationExpression rightOperation, params OperationExpression[] arguments)
        {
            if (sign == OperationSign.Length ||
                sign == OperationSign.ToLower || sign == OperationSign.ToUpper ||
                sign == OperationSign.TrimStart || sign == OperationSign.TrimEnd || sign == OperationSign.Trim ||
                sign == OperationSign.Substring)
                throw new ArgumentException(String.Format("不允许'{0}'有左节点", sign), nameof(sign));

            _value = value;
            _haveValue = true;
            _sign = sign;
            _rightOperation = rightOperation;
            _arguments = arguments;
        }

        private OperationExpression(OperationSign sign, OperationExpression rightOperation, params OperationExpression[] arguments)
        {
            if (sign == OperationSign.Multiply || sign == OperationSign.Divide)
                throw new ArgumentException(String.Format("'{0}'无实际意义", sign), nameof(sign));
            if (sign == OperationSign.Length ||
                sign == OperationSign.ToLower || sign == OperationSign.ToUpper ||
                sign == OperationSign.TrimStart || sign == OperationSign.TrimEnd || sign == OperationSign.Trim ||
                sign == OperationSign.Substring)
                if (rightOperation.MemberType != typeof(string))
                    throw new ArgumentException(String.Format("'{0}'仅适用于string类型的右节点", sign), nameof(sign));

            _sign = sign;
            _rightOperation = rightOperation;
            _arguments = arguments;
        }

        #endregion

        #region Expression

        /// <summary>
        /// 运算表达式
        /// </summary>
        /// <param name="valueLambda">值 lambda 表达式</param>
        public static OperationExpression Compute<T>(Expression<Func<T, object>> valueLambda)
        {
            return valueLambda != null ? Split(valueLambda.Body) as OperationExpression : null;
        }

        internal static object Split(Expression expression)
        {
            if (expression == null)
                return null;
            switch (expression.NodeType)
            {
                case ExpressionType.Constant:
                    return Split((ConstantExpression) expression);
                case ExpressionType.MemberAccess:
                    return Split((MemberExpression) expression);
                case ExpressionType.Call:
                    return Split((MethodCallExpression) expression);
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                    return Split(((UnaryExpression) expression).Operand);
            }

            if (expression is BinaryExpression binaryExpression)
            {
                //二元算术运算
                object left = Split(binaryExpression.Left);
                object right = Split(binaryExpression.Right);
                OperationSign operationSign;
                switch (expression.NodeType)
                {
                    case ExpressionType.Add:
                    case ExpressionType.AddChecked:
                        operationSign = OperationSign.Add;
                        break;
                    case ExpressionType.Subtract:
                    case ExpressionType.SubtractChecked:
                        operationSign = OperationSign.Subtract;
                        break;
                    case ExpressionType.Multiply:
                    case ExpressionType.MultiplyChecked:
                        operationSign = OperationSign.Multiply;
                        break;
                    case ExpressionType.Divide:
                        operationSign = OperationSign.Divide;
                        break;
                    case ExpressionType.ArrayIndex:
                        if (left is Array array && right is int i)
                            return array.GetValue(i);
                        operationSign = OperationSign.None;
                        break;
                    default:
                        operationSign = OperationSign.None;
                        break;
                }

                OperationExpression leftOperation = left as OperationExpression;
                OperationExpression rightOperation = right as OperationExpression;
                if (leftOperation != null && rightOperation != null)
                    return new OperationExpression(leftOperation, operationSign, rightOperation);
                if (leftOperation != null)
                    return new OperationExpression(leftOperation, operationSign, right);
                if (rightOperation != null)
                    return new OperationExpression(left, operationSign, rightOperation);
                if (binaryExpression.Method != null)
                    return binaryExpression.Method.Invoke(null, new object[] {left, right});
                if (operationSign != OperationSign.None)
                    return new OperationExpression(new OperationExpression(left), operationSign, new OperationExpression(right));
                throw new NotImplementedException(expression.ToString());
            }

            if (expression is UnaryExpression unaryExpression)
            {
                //一元算术运算
                object operand = Split(unaryExpression.Operand);
                OperationSign operationSign;
                switch (expression.NodeType)
                {
                    case ExpressionType.Add:
                        if (operand != null && Utilities.GetUnderlyingType(operand.GetType()).IsValueType)
                            return operand;
                        operationSign = OperationSign.Add;
                        break;
                    case ExpressionType.Negate:
                        if (operand != null && Utilities.GetUnderlyingType(operand.GetType()).IsValueType)
                            return Utilities.ChangeType(-(double) Utilities.ChangeType(operand, typeof(double)), operand.GetType());
                        operationSign = OperationSign.Subtract;
                        break;
                    case ExpressionType.ArrayLength:
                        if (operand is Array array)
                            return array.Length;
                        operationSign = OperationSign.Length;
                        break;
                    default:
                        operationSign = OperationSign.None;
                        break;
                }

                if (operand is OperationExpression operation)
                    return new OperationExpression(operationSign, operation);
                if (unaryExpression.Method != null)
                    return unaryExpression.Method.Invoke(null, new object[] {operand});
                if (operationSign != OperationSign.None)
                    return new OperationExpression(operationSign, new OperationExpression(operand));
                throw new NotImplementedException(expression.ToString());
            }

            throw new NotImplementedException(expression.ToString());
        }

        private static object Split(ConstantExpression expression)
        {
            return expression.Value;
        }

        private static object Split(MemberExpression expression)
        {
            switch (expression.Member.MemberType)
            {
                case MemberTypes.Property:
                case MemberTypes.Field:
                    if (expression.Expression != null && expression.Expression.NodeType == ExpressionType.Parameter)
                        return new OperationExpression(expression.Member);

                    object obj = Split(expression.Expression);
                    if (obj is OperationExpression operation)
                    {
                        if (expression.Member.DeclaringType == typeof(string) && Utilities.GetMemberType(expression.Member) == typeof(int))
                            if (Enum.TryParse(expression.Member.Name, out OperationSign sign))
                                return new OperationExpression(sign, operation);
                        return operation;
                    }

                    return Utilities.GetMemberValue(obj, expression.Member);
                default:
                    throw new NotImplementedException(expression.ToString());
            }
        }

        private static object Split(MethodCallExpression expression)
        {
            List<object> arguments = new List<object>(expression.Arguments.Count);
            foreach (Expression item in expression.Arguments)
            {
                object argument = Split(item);
                if (argument is OperationExpression && expression.Method.IsStatic)
                    throw new NotImplementedException(expression.ToString());
                arguments.Add(argument);
            }

            object obj = Split(expression.Object);
            if (obj is OperationExpression operation)
            {
                if (expression.Method.DeclaringType == typeof(string) && expression.Method.ReturnType == typeof(string))
                    if (Enum.TryParse(expression.Method.Name, out OperationSign sign))
                    {
                        List<OperationExpression> operations = new List<OperationExpression>(arguments.Count);
                        foreach (object item in arguments)
                            operations.Add(item is OperationExpression argument ? argument : new OperationExpression(item));
                        return new OperationExpression(sign, operation, operations.ToArray());
                    }

                throw new NotImplementedException(expression.ToString());
            }

            return expression.Method.Invoke(expression.Method.IsStatic ? null : obj, arguments.ToArray());
        }

        #endregion

        #region 条件组合

        /// <summary>
        /// In
        /// </summary>
        /// <param name="insidePropertyLambda">内实体属性的 lambda 表达式</param>
        /// <param name="insideCriteriaLambda">内条件表达式</param>
        public CriteriaExpression In<TInside>(Expression<Func<TInside, object>> insidePropertyLambda, Expression<Func<TInside, bool>> insideCriteriaLambda)
        {
            return In(insidePropertyLambda, CriteriaExpression.Where(insideCriteriaLambda));
        }

        /// <summary>
        /// In
        /// </summary>
        /// <param name="insidePropertyLambda">内实体属性的 lambda 表达式</param>
        /// <param name="insideCriteriaExpression">内条件表达式</param>
        public CriteriaExpression In<TInside>(Expression<Func<TInside, object>> insidePropertyLambda, CriteriaExpression insideCriteriaExpression)
        {
            return new CriteriaExpression(this, CriteriaOperator.In,
                new CriteriaExpression(new OperationExpression(Utilities.GetPropertyInfo(insidePropertyLambda)), CriteriaOperator.None, insideCriteriaExpression));
        }

        /// <summary>
        /// NotIn
        /// </summary>
        /// <param name="insidePropertyLambda">内实体属性的 lambda 表达式</param>
        /// <param name="insideCriteriaLambda">内条件表达式</param>
        public CriteriaExpression NotIn<TInside>(Expression<Func<TInside, object>> insidePropertyLambda, Expression<Func<TInside, bool>> insideCriteriaLambda)
        {
            return NotIn(insidePropertyLambda, CriteriaExpression.Where(insideCriteriaLambda));
        }

        /// <summary>
        /// NotIn
        /// </summary>
        /// <param name="insidePropertyLambda">内实体属性的 lambda 表达式</param>
        /// <param name="insideCriteriaExpression">内条件表达式</param>
        public CriteriaExpression NotIn<TInside>(Expression<Func<TInside, object>> insidePropertyLambda, CriteriaExpression insideCriteriaExpression)
        {
            return new CriteriaExpression(this, CriteriaOperator.NotIn,
                new CriteriaExpression(new OperationExpression(Utilities.GetPropertyInfo(insidePropertyLambda)), CriteriaOperator.None, insideCriteriaExpression));
        }

        #endregion

        #region 属性

        private readonly string _ownerTypeAssemblyQualifiedName;

        /// <summary>
        /// 所属类程序集限定名
        /// </summary>
        public string OwnerTypeAssemblyQualifiedName
        {
            get { return _ownerTypeAssemblyQualifiedName; }
        }

        [NonSerialized]
        private Type _ownerType;

        /// <summary>
        /// 所属类
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Type OwnerType
        {
            get
            {
                if (_ownerType == null)
                {
                    if (_ownerTypeAssemblyQualifiedName != null)
                        _ownerType = Type.GetType(_ownerTypeAssemblyQualifiedName);
                }

                return _ownerType;
            }
        }

        private readonly string _memberName;

        /// <summary>
        /// 属性/字段名
        /// </summary>
        public string MemberName
        {
            get { return _memberName; }
        }

        [NonSerialized]
        private MemberInfo _memberInfo;

        /// <summary>
        /// 类属性/类字段
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public MemberInfo MemberInfo
        {
            get { return _memberInfo ??= Utilities.FindMemberInfo(OwnerType, MemberName); }
        }

        /// <summary>
        /// 类属性/类字段类型
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Type MemberType
        {
            get { return Utilities.GetMemberType(MemberInfo); }
        }

        #region 条件表达式节点

        private readonly OperationExpression _leftOperation;

        /// <summary>
        /// 运算表达式左
        /// </summary>
        public OperationExpression LeftOperation
        {
            get { return _leftOperation; }
        }

        private readonly OperationSign _sign;

        /// <summary>
        /// 运算符号
        /// </summary>
        public OperationSign Sign
        {
            get { return _sign; }
        }

        private readonly OperationExpression _rightOperation;

        /// <summary>
        /// 运算表达式右
        /// </summary>
        public OperationExpression RightOperation
        {
            get { return _rightOperation; }
        }

        private readonly object _value;

        /// <summary>
        /// 值
        /// </summary>
        public object Value
        {
            get { return _value; }
        }

        private readonly bool _haveValue;

        /// <summary>
        /// 是否存在值
        /// </summary>
        public bool HaveValue
        {
            get { return _haveValue; }
        }

        /// <summary>
        /// 值的类型
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Type ValueType
        {
            get
            {
                if (_value != null)
                    return _value.GetType();
                if (_leftOperation != null)
                    return _leftOperation.ValueType != null ? _leftOperation.ValueType : _leftOperation.MemberInfo != null ? _leftOperation.MemberType : null;
                if (_rightOperation != null)
                    return _rightOperation.ValueType != null ? _rightOperation.ValueType : _rightOperation.MemberInfo != null ? _rightOperation.MemberType : null;
                return null;
            }
        }

        private readonly OperationExpression[] _arguments;

        /// <summary>
        /// 参数
        /// </summary>
        public OperationExpression[] Arguments
        {
            get { return _arguments; }
        }

        #endregion

        #endregion

        #region 方法

        /// <summary>
        /// 运算表达式
        /// </summary>
        /// <param name="entity">实体</param>
        public object Compute<T>(T entity)
            where T : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return Compute(this, entity);
        }

        private static object Compute<T>(OperationExpression operation, T entity)
            where T : class
        {
            if (operation == null)
                return String.Empty;

            switch (operation.Sign)
            {
                case OperationSign.None:
                    return operation.HaveValue
                        ? operation.Value
                        : Utilities.GetMemberValue(entity, operation.MemberInfo);
                case OperationSign.Add:
                    if (operation.ValueType == typeof(string))
                        if (operation.HaveValue)
                            return operation.LeftOperation != null
                                ? ((string) Compute(operation.LeftOperation, entity) ?? String.Empty) + ((string) operation.Value ?? String.Empty)
                                : ((string) operation.Value ?? String.Empty) + ((string) Compute(operation.RightOperation, entity) ?? String.Empty);
                        else
                            return ((string) Compute(operation.LeftOperation, entity) ?? String.Empty) + ((string) Compute(operation.RightOperation, entity) ?? String.Empty);
                    else if (Utilities.GetUnderlyingType(operation.ValueType).IsValueType)
                        if (operation.HaveValue)
                            return operation.LeftOperation != null
                                ? Utilities.ChangeType((double) Utilities.ChangeType(Compute(operation.LeftOperation, entity), typeof(double)) + (double) Utilities.ChangeType(operation.Value, typeof(double)), operation.ValueType)
                                : Utilities.ChangeType((double) Utilities.ChangeType(operation.Value, typeof(double)) + (double) Utilities.ChangeType(Compute(operation.RightOperation, entity), typeof(double)), operation.ValueType);
                        else
                            return Utilities.ChangeType((double) Utilities.ChangeType(Compute(operation.LeftOperation, entity), typeof(double)) + (double) Utilities.ChangeType(Compute(operation.RightOperation, entity), typeof(double)), operation.ValueType);
                    throw new NotImplementedException(operation.ToString());
                case OperationSign.Subtract:
                    if (Utilities.GetUnderlyingType(operation.ValueType).IsValueType)
                        if (operation.HaveValue)
                            return operation.LeftOperation != null
                                ? Utilities.ChangeType((double) Utilities.ChangeType(Compute(operation.LeftOperation, entity), typeof(double)) - (double) Utilities.ChangeType(operation.Value, typeof(double)), operation.ValueType)
                                : Utilities.ChangeType((double) Utilities.ChangeType(operation.Value, typeof(double)) - (double) Utilities.ChangeType(Compute(operation.RightOperation, entity), typeof(double)), operation.ValueType);
                        else
                            return Utilities.ChangeType((double) Utilities.ChangeType(Compute(operation.LeftOperation, entity), typeof(double)) - (double) Utilities.ChangeType(Compute(operation.RightOperation, entity), typeof(double)), operation.ValueType);
                    throw new NotImplementedException(operation.ToString());
                case OperationSign.Multiply:
                    if (Utilities.GetUnderlyingType(operation.ValueType).IsValueType)
                        if (operation.HaveValue)
                            return operation.LeftOperation != null
                                ? Utilities.ChangeType((double) Utilities.ChangeType(Compute(operation.LeftOperation, entity), typeof(double)) * (double) Utilities.ChangeType(operation.Value, typeof(double)), operation.ValueType)
                                : Utilities.ChangeType((double) Utilities.ChangeType(operation.Value, typeof(double)) * (double) Utilities.ChangeType(Compute(operation.RightOperation, entity), typeof(double)), operation.ValueType);
                        else
                            return Utilities.ChangeType((double) Utilities.ChangeType(Compute(operation.LeftOperation, entity), typeof(double)) * (double) Utilities.ChangeType(Compute(operation.RightOperation, entity), typeof(double)), operation.ValueType);
                    throw new NotImplementedException(operation.ToString());
                case OperationSign.Divide:
                    if (Utilities.GetUnderlyingType(operation.ValueType).IsValueType)
                        if (operation.HaveValue)
                            return operation.LeftOperation != null
                                ? Utilities.ChangeType((double) Utilities.ChangeType(Compute(operation.LeftOperation, entity), typeof(double)) / (double) Utilities.ChangeType(operation.Value, typeof(double)), operation.ValueType)
                                : Utilities.ChangeType((double) Utilities.ChangeType(operation.Value, typeof(double)) / (double) Utilities.ChangeType(Compute(operation.RightOperation, entity), typeof(double)), operation.ValueType);
                        else
                            return Utilities.ChangeType((double) Utilities.ChangeType(Compute(operation.LeftOperation, entity), typeof(double)) / (double) Utilities.ChangeType(Compute(operation.RightOperation, entity), typeof(double)), operation.ValueType);
                    throw new NotImplementedException(operation.ToString());
                case OperationSign.Length:
                    if (operation.ValueType == typeof(string))
                        return operation.HaveValue
                            ? ((string) operation.Value ?? String.Empty).Length
                            : ((string) Compute(operation.RightOperation, entity) ?? String.Empty).Length;
                    throw new NotImplementedException(operation.ToString());
                case OperationSign.ToLower:
                    if (operation.ValueType == typeof(string))
                        return operation.HaveValue
                            ? ((string) operation.Value ?? String.Empty).ToLower()
                            : ((string) Compute(operation.RightOperation, entity) ?? String.Empty).ToLower();
                    throw new NotImplementedException(operation.ToString());
                case OperationSign.ToUpper:
                    if (operation.ValueType == typeof(string))
                        return operation.HaveValue
                            ? ((string) operation.Value ?? String.Empty).ToUpper()
                            : ((string) Compute(operation.RightOperation, entity) ?? String.Empty).ToUpper();
                    throw new NotImplementedException(operation.ToString());
                case OperationSign.TrimStart:
                    if (operation.ValueType == typeof(string))
                        return operation.HaveValue
                            ? ((string) operation.Value ?? String.Empty).TrimStart()
                            : ((string) Compute(operation.RightOperation, entity) ?? String.Empty).TrimStart();
                    throw new NotImplementedException(operation.ToString());
                case OperationSign.TrimEnd:
                    if (operation.ValueType == typeof(string))
                        return operation.HaveValue
                            ? ((string) operation.Value ?? String.Empty).TrimEnd()
                            : ((string) Compute(operation.RightOperation, entity) ?? String.Empty).TrimEnd();
                    throw new NotImplementedException(operation.ToString());
                case OperationSign.Trim:
                    if (operation.ValueType == typeof(string))
                        return operation.HaveValue
                            ? ((string) operation.Value ?? String.Empty).Trim()
                            : ((string) Compute(operation.RightOperation, entity) ?? String.Empty).Trim();
                    throw new NotImplementedException(operation.ToString());
                case OperationSign.Substring:
                    if (operation.ValueType == typeof(string))
                        if (operation.Arguments == null || operation.Arguments.Length == 0)
                            return operation.HaveValue ? operation.Value : Compute(operation.RightOperation, entity);
                        else if (operation.Arguments.Length == 1)
                            return operation.HaveValue
                                ? ((string) operation.Value ?? String.Empty).Substring((int) Utilities.ChangeType(Compute(operation.Arguments[0], entity), typeof(int)))
                                : ((string) Compute(operation.RightOperation, entity) ?? String.Empty).Substring((int) Utilities.ChangeType(Compute(operation.Arguments[0], entity), typeof(int)));
                        else if (operation.Arguments.Length == 2)
                            return operation.HaveValue
                                ? ((string) operation.Value ?? String.Empty).Substring((int) Utilities.ChangeType(Compute(operation.Arguments[0], entity), typeof(int)), (int) Utilities.ChangeType(Compute(operation.Arguments[1], entity), typeof(int)))
                                : ((string) Compute(operation.RightOperation, entity) ?? String.Empty).Substring((int) Utilities.ChangeType(Compute(operation.Arguments[0], entity), typeof(int)), (int) Utilities.ChangeType(Compute(operation.Arguments[1], entity), typeof(int)));
                        else
                            throw new InvalidOperationException(String.Format("运算表达式 {0} 不允许有 {1} 个参数", operation.Sign, operation.Arguments.Length));
                    throw new NotImplementedException(operation.ToString());
            }

            return String.Empty;
        }

        /// <summary>
        /// 比较对象
        /// </summary>
        /// <param name="obj">对象</param>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, this))
                return true;
            OperationExpression other = obj as OperationExpression;
            if (object.ReferenceEquals(other, null))
                return false;
            if (String.CompareOrdinal(_ownerTypeAssemblyQualifiedName, other._ownerTypeAssemblyQualifiedName) != 0)
                return false;
            if (String.CompareOrdinal(_memberName, other._memberName) != 0)
                return false;
            if (_leftOperation != null)
            {
                if (!_leftOperation.Equals(other._leftOperation))
                    return false;
            }
            else if (other._leftOperation != null)
                return false;
            if (_sign != other._sign)
                return false;
            if (_rightOperation != null)
            {
                if (!_rightOperation.Equals(other._rightOperation))
                    return false;
            }
            else if (other._rightOperation != null)
                return false;
            if (_value != null)
            {
                if (!_value.Equals(other._value))
                    return false;
            }
            else if (other._value != null)
                return false;
            return _haveValue == other._haveValue;
        }

        /// <summary>
        /// 取哈希值(注意字符串在32位和64位系统有不同的算法得到不同的结果) 
        /// </summary>
        public override int GetHashCode()
        {
            return (_ownerTypeAssemblyQualifiedName != null ? _ownerTypeAssemblyQualifiedName.GetHashCode() : 0) ^
                   (_memberName != null ? _memberName.GetHashCode() : 0) ^ 
                   (_leftOperation != null ? _leftOperation.GetHashCode() : 0) ^
                   _sign.GetHashCode() ^
                   (_rightOperation != null ? _rightOperation.GetHashCode() : 0) ^
                   (_value != null ? _value.GetType().FullName.GetHashCode() ^ _value.GetHashCode() : 0) ^
                   _haveValue.GetHashCode();
        }

        /// <summary>
        /// 字符串表示
        /// </summary>
        public override string ToString()
        {
            StringBuilder result = new StringBuilder();
            if (_ownerTypeAssemblyQualifiedName != null)
                result.Append(_ownerTypeAssemblyQualifiedName);
            if (_memberName != null)
                result.Append(_memberName);
            if (_leftOperation != null)
                result.Append(_leftOperation.ToString());
            result.Append(_sign.ToString());
            if (_rightOperation != null)
                result.Append(_rightOperation.ToString());
            if (_value != null)
            {
                result.Append(_value.GetType().FullName);
                result.Append(_value.ToString());
            }

            result.Append(_haveValue.ToString());
            return result.ToString();
        }

        #endregion
    }
}