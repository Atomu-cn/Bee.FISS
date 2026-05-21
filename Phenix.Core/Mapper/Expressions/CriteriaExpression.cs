using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Phenix.Core.Reflection;

namespace Phenix.Core.Mapper.Expressions
{
    /// <summary>
    /// 条件表达式
    /// </summary>
    [Serializable]
    public sealed class CriteriaExpression
    {
        [Newtonsoft.Json.JsonConstructor]
        private CriteriaExpression(CriteriaExpressionType criteriaExpressionType, CriteriaExpression left, CriteriaLogical logical, CriteriaExpression right,
            OperationExpression leftOperation, CriteriaOperator criteriaOperator, OperationExpression rightOperation, bool operateIgnoreCase,
            object value, bool haveValue, bool shortValue)
        {
            _criteriaExpressionType = criteriaExpressionType;
            _left = left;
            _logical = logical;
            _right = right;
            _leftOperation = leftOperation;
            _criteriaOperator = criteriaOperator;
            _rightOperation = rightOperation;
            _operateIgnoreCase = operateIgnoreCase;
            _value = value;
            _haveValue = haveValue;
            _shortValue = shortValue;
        }

        private CriteriaExpression(CriteriaExpression left, CriteriaLogical logical, CriteriaExpression right)
        {
            _criteriaExpressionType = CriteriaExpressionType.CriteriaLogical;
            _left = left;
            _logical = logical;
            _right = right;
        }

        private CriteriaExpression(CriteriaExpression left, CriteriaOperator criteriaOperator, CriteriaExpression right)
        {
            _criteriaExpressionType = criteriaOperator == CriteriaOperator.Exists || criteriaOperator == CriteriaOperator.NotExists
                ? CriteriaExpressionType.ExistsOrNotExists
                : CriteriaExpressionType.CriteriaOperate;
            _left = left;
            _criteriaOperator = criteriaOperator;
            _right = right;
        }

        private CriteriaExpression(OperationExpression leftOperation, CriteriaOperator criteriaOperator)
        {
            _criteriaExpressionType = criteriaOperator == CriteriaOperator.None 
                ? CriteriaExpressionType.ExistsOrNotExists 
                : CriteriaExpressionType.CriteriaOperate;
            _leftOperation = leftOperation;
            _criteriaOperator = criteriaOperator;
        }

        /// <summary>
        /// 条件表达式
        /// </summary>
        /// <param name="leftOperation">左侧运算并表达式</param>
        /// <param name="criteriaOperator">条件运算符</param>
        /// <param name="value">值</param>
        /// <param name="ignoreCase">条件运算忽略大小写</param>
        public CriteriaExpression(OperationExpression leftOperation, CriteriaOperator criteriaOperator, object value, bool ignoreCase = false)
        {
            _criteriaExpressionType = CriteriaExpressionType.CriteriaOperate;
            _leftOperation = leftOperation;
            _criteriaOperator = criteriaOperator;
            _operateIgnoreCase = ignoreCase;

            Value = value;
        }

        #region 短路

        private CriteriaExpression(IComparable leftValue, CriteriaOperator criteriaOperator, IComparable rightValue)
        {
            _criteriaExpressionType = CriteriaExpressionType.Short;
            switch (criteriaOperator)
            {
                case CriteriaOperator.Equal:
                    _shortValue = leftValue.CompareTo(rightValue) == 0;
                    break;
                case CriteriaOperator.Greater:
                    _shortValue = leftValue.CompareTo(rightValue) > 0;
                    break;
                case CriteriaOperator.GreaterOrEqual:
                    _shortValue = leftValue.CompareTo(rightValue) >= 0;
                    break;
                case CriteriaOperator.Lesser:
                    _shortValue = leftValue.CompareTo(rightValue) < 0;
                    break;
                case CriteriaOperator.LesserOrEqual:
                    _shortValue = leftValue.CompareTo(rightValue) <= 0;
                    break;
                case CriteriaOperator.Unequal:
                    _shortValue = leftValue.CompareTo(rightValue) != 0;
                    break;
                default:
                    throw new ArgumentException(String.Format("'{0}'无实际意义", criteriaOperator), nameof(criteriaOperator));
            }
        }

        private CriteriaExpression(bool shortValue)
        {
            _criteriaExpressionType = CriteriaExpressionType.Short;
            _shortValue = shortValue;
        }

        /// <summary>
        /// 条件表达式: True
        /// </summary>
        public static CriteriaExpression True
        {
            get { return new CriteriaExpression(true); }
        }

        /// <summary>
        /// 条件表达式: False
        /// </summary>
        public static CriteriaExpression False
        {
            get { return new CriteriaExpression(false); }
        }

        #endregion

        #region Expression

        /// <summary>
        /// 条件表达式
        /// </summary>
        /// <param name="criteriaLambda">条件表达式</param>
        public static CriteriaExpression Where<T>(Expression<Func<T, bool>> criteriaLambda = null)
        {
            return criteriaLambda != null ? Split(criteriaLambda.Body) : null;
        }

        private static CriteriaExpression Split(Expression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Constant:
                    return Split((ConstantExpression) expression);
                case ExpressionType.MemberAccess:
                    return Split((MemberExpression) expression);
                case ExpressionType.Call:
                    return Split((MethodCallExpression) expression);
                case ExpressionType.Not:
                    return Split((UnaryExpression) expression);
            }

            if (expression is BinaryExpression binaryExpression)
            {
                switch (expression.NodeType)
                {
                    //条件布尔运算
                    case ExpressionType.And:
                    case ExpressionType.AndAlso:
                        return new CriteriaExpression(Split(binaryExpression.Left), CriteriaLogical.And, Split(binaryExpression.Right));
                    case ExpressionType.Or:
                    case ExpressionType.OrElse:
                        return new CriteriaExpression(Split(binaryExpression.Left), CriteriaLogical.Or, Split(binaryExpression.Right));
                }

                object left = OperationExpression.Split(binaryExpression.Left);
                object right = OperationExpression.Split(binaryExpression.Right);
                CriteriaOperator criteriaOperator;
                switch (expression.NodeType)
                {
                    //比较运算
                    case ExpressionType.Equal:
                        criteriaOperator = CriteriaOperator.Equal;
                        break;
                    case ExpressionType.GreaterThan:
                        criteriaOperator = CriteriaOperator.Greater;
                        break;
                    case ExpressionType.GreaterThanOrEqual:
                        criteriaOperator = CriteriaOperator.GreaterOrEqual;
                        break;
                    case ExpressionType.LessThan:
                        criteriaOperator = CriteriaOperator.Lesser;
                        break;
                    case ExpressionType.LessThanOrEqual:
                        criteriaOperator = CriteriaOperator.LesserOrEqual;
                        break;
                    case ExpressionType.NotEqual:
                        criteriaOperator = CriteriaOperator.Unequal;
                        break;
                    default:
                        throw new NotImplementedException(expression.ToString());
                }

                OperationExpression leftOperation = left as OperationExpression;
                OperationExpression rightOperation = right as OperationExpression;
                if (leftOperation != null && rightOperation != null)
                    return new CriteriaExpression(leftOperation, criteriaOperator, rightOperation);
                if (leftOperation != null)
                    return new CriteriaExpression(leftOperation, criteriaOperator, right);
                if (rightOperation != null)
                {
                    //调换方向
                    switch (criteriaOperator)
                    {
                        case CriteriaOperator.Greater:
                            criteriaOperator = CriteriaOperator.Lesser;
                            break;
                        case CriteriaOperator.GreaterOrEqual:
                            criteriaOperator = CriteriaOperator.LesserOrEqual;
                            break;
                        case CriteriaOperator.Lesser:
                            criteriaOperator = CriteriaOperator.Greater;
                            break;
                        case CriteriaOperator.LesserOrEqual:
                            criteriaOperator = CriteriaOperator.GreaterOrEqual;
                            break;
                    }

                    return new CriteriaExpression(rightOperation, criteriaOperator, left);
                }

                if (left is IComparable leftValue && right is IComparable rightValue)
                    return new CriteriaExpression(leftValue, criteriaOperator, rightValue);
            }

            throw new NotImplementedException(expression.ToString());
        }

        private static CriteriaExpression Split(ConstantExpression expression)
        {
            if (Utilities.GetUnderlyingType(expression.Type) == typeof(bool))
                return new CriteriaExpression((bool) expression.Value);
            throw new NotImplementedException(expression.ToString());
        }

        private static CriteriaExpression Split(MemberExpression expression)
        {
            MemberInfo memberInfo = expression.Member;
            if (expression.Expression != null && expression.Expression.NodeType == ExpressionType.MemberAccess)
                expression = (MemberExpression) expression.Expression;
            if (expression.Expression != null && expression.Expression.NodeType == ExpressionType.Parameter)
            {
                switch (memberInfo.MemberType)
                {
                    case MemberTypes.Property:
                    case MemberTypes.Field:
                        switch (memberInfo.Name)
                        {
                            case "HasValue":
                                return new CriteriaExpression(new OperationExpression(expression.Member), CriteriaOperator.IsNotNull);
                            default:
                                if (Utilities.GetUnderlyingType(expression.Type) == typeof(bool))
                                    return new CriteriaExpression(new OperationExpression(expression.Member), CriteriaOperator.Equal, true);
                                throw new NotImplementedException(expression.ToString());
                        }
                    default:
                        throw new NotImplementedException(expression.ToString());
                }
            }

            throw new NotImplementedException(expression.ToString());
        }

        private static CriteriaExpression Split(MethodCallExpression expression)
        {
            if (expression.Method.IsStatic)
            {
                if (expression.Arguments.Count == 2 && expression.Method.ReturnType == typeof(bool))
                    return Split(expression, OperationExpression.Split(expression.Arguments[0]), OperationExpression.Split(expression.Arguments[1]));
            }
            else
            {
                if (expression.Arguments.Count == 1 && expression.Method.ReturnType == typeof(bool))
                    return Split(expression, OperationExpression.Split(expression.Object), OperationExpression.Split(expression.Arguments[0]));
            }

            throw new NotImplementedException(expression.ToString());
        }

        private static CriteriaExpression Split(UnaryExpression expression)
        {
            return new CriteriaExpression(null, CriteriaLogical.Not, Split(expression.Operand));
        }

        private static CriteriaExpression Split(MethodCallExpression expression, object callObject, object callArgument)
        {
            switch (expression.Method.Name)
            {
                case "Contains":
                    if (callArgument == null)
                        return null;
                    if (Utilities.FindListItemType(Utilities.GetUnderlyingType(callObject.GetType())) != null)
                        return new CriteriaExpression(callArgument is OperationExpression operation ? operation : new OperationExpression(callArgument),
                            CriteriaOperator.In, callObject);
                    if (callArgument is string s)
                        return new CriteriaExpression(callObject is OperationExpression operation ? operation : new OperationExpression(callObject),
                            CriteriaOperator.Like, s.Contains("%") ? s : String.Format("%{0}%", s));
                    if (callArgument is OperationExpression likeOperation && likeOperation.Sign == OperationSign.None && !likeOperation.HaveValue)
                        return new CriteriaExpression(callObject is OperationExpression leftOperation ? leftOperation : new OperationExpression(callObject),
                            CriteriaOperator.Like, likeOperation);
                    throw new NotImplementedException(expression.ToString());
                case "StartsWith":
                    if (callArgument == null)
                        return null;
                    if (callArgument is string ls)
                        return new CriteriaExpression(callObject is OperationExpression operation ? operation : new OperationExpression(callObject),
                            CriteriaOperator.LikeLeft, ls.EndsWith("%") ? ls : String.Format("{0}%", ls));
                    if (callArgument is OperationExpression likeLeftOperation && likeLeftOperation.Sign == OperationSign.None && !likeLeftOperation.HaveValue)
                        return new CriteriaExpression(callObject is OperationExpression leftOperation ? leftOperation : new OperationExpression(callObject),
                            CriteriaOperator.LikeLeft, likeLeftOperation);
                    throw new NotImplementedException(expression.ToString());
                case "EndsWith":
                    if (callArgument == null)
                        return null;
                    if (callArgument is string rs)
                        return new CriteriaExpression(callObject is OperationExpression operation ? operation : new OperationExpression(callObject),
                            CriteriaOperator.LikeRight, rs.StartsWith("%") ? rs : String.Format("%{0}", rs));
                    if (callArgument is OperationExpression likeRightOperation && likeRightOperation.Sign == OperationSign.None && !likeRightOperation.HaveValue)
                        return new CriteriaExpression(callObject is OperationExpression leftOperation ? leftOperation : new OperationExpression(callObject),
                            CriteriaOperator.LikeRight, likeRightOperation);
                    throw new NotImplementedException(expression.ToString());
                default:
                    throw new NotImplementedException(expression.ToString());
            }
        }

        #endregion

        #region 条件组合

        /// <summary>
        /// And
        /// </summary>
        public static CriteriaExpression operator &(CriteriaExpression left, CriteriaExpression right)
        {
            if (left == null)
                return right;
            if (right == null)
                return left;
            return new CriteriaExpression(left, CriteriaLogical.And, right);
        }

        /// <summary>
        /// And
        /// </summary>
        public static CriteriaExpression operator &(CriteriaExpression left, bool right)
        {
            return right ? left : False;
        }

        /// <summary>
        /// And
        /// </summary>
        public static CriteriaExpression operator &(bool left, CriteriaExpression right)
        {
            return left ? right : False;
        }

        /// <summary>
        /// Or
        /// </summary>
        public static CriteriaExpression operator |(CriteriaExpression left, CriteriaExpression right)
        {
            if (left == null)
                return right;
            if (right == null)
                return left;
            return new CriteriaExpression(left, CriteriaLogical.Or, right);
        }

        /// <summary>
        /// Or
        /// </summary>
        public static CriteriaExpression operator |(CriteriaExpression left, bool right)
        {
            return right ? True : left;
        }

        /// <summary>
        /// Or
        /// </summary>
        public static CriteriaExpression operator |(bool left, CriteriaExpression right)
        {
            return left ? True : right;
        }

        /// <summary>
        /// Not
        /// </summary>
        public static CriteriaExpression operator !(CriteriaExpression right)
        {
            if (right == null)
                throw new ArgumentNullException(nameof(right));

            return new CriteriaExpression(null, CriteriaLogical.Not, right);
        }

        /// <summary>
        /// Exists
        /// </summary>
        /// <param name="detailForeignKeyPropertyLambda">含从类虚/实外键属性的 lambda 表达式</param>
        public CriteriaExpression Exists<TDetail>(Expression<Func<TDetail, object>> detailForeignKeyPropertyLambda)
        {
            return new CriteriaExpression(this, CriteriaOperator.Exists, 
                new CriteriaExpression(new OperationExpression(Utilities.GetPropertyInfo(detailForeignKeyPropertyLambda)), CriteriaOperator.None));
        }

        /// <summary>
        /// Exists
        /// </summary>
        /// <param name="insideCriteriaLambda">内条件表达式</param>
        public CriteriaExpression Exists<TInside>(Expression<Func<TInside, bool>> insideCriteriaLambda)
        {
            if (insideCriteriaLambda == null)
                throw new ArgumentNullException(nameof(insideCriteriaLambda));

            return Exists(Where(insideCriteriaLambda));
        }

        /// <summary>
        /// Exists
        /// </summary>
        /// <param name="insideCriteriaExpression">内条件表达式</param>
        public CriteriaExpression Exists(CriteriaExpression insideCriteriaExpression)
        {
            if (insideCriteriaExpression == null)
                throw new ArgumentNullException(nameof(insideCriteriaExpression));

            return new CriteriaExpression(this, CriteriaOperator.Exists, 
                new CriteriaExpression(new OperationExpression(insideCriteriaExpression), CriteriaOperator.None));
        }

        /// <summary>
        /// NotExists
        /// </summary>
        /// <param name="detailForeignKeyPropertyLambda">含从类虚/实外键属性的 lambda 表达式</param>
        public CriteriaExpression NotExists<TDetail>(Expression<Func<TDetail, object>> detailForeignKeyPropertyLambda)
        {
            return new CriteriaExpression(this, CriteriaOperator.NotExists, 
                new CriteriaExpression(new OperationExpression(Utilities.GetPropertyInfo(detailForeignKeyPropertyLambda)), CriteriaOperator.None));
        }

        /// <summary>
        /// NotExists
        /// </summary>
        /// <param name="insideCriteriaLambda">内条件表达式</param>
        public CriteriaExpression NotExists<TInside>(Expression<Func<TInside, bool>> insideCriteriaLambda)
        {
            if (insideCriteriaLambda == null)
                throw new ArgumentNullException(nameof(insideCriteriaLambda));

            return NotExists(Where(insideCriteriaLambda));
        }

        /// <summary>
        /// NotExists
        /// </summary>
        /// <param name="insideCriteriaExpression">内条件表达式</param>
        public CriteriaExpression NotExists(CriteriaExpression insideCriteriaExpression)
        {
            if (insideCriteriaExpression == null)
                throw new ArgumentNullException(nameof(insideCriteriaExpression));

            return new CriteriaExpression(this, CriteriaOperator.NotExists, 
                new CriteriaExpression(new OperationExpression(insideCriteriaExpression), CriteriaOperator.None));
        }

        #endregion

        #region 属性

        [NonSerialized]
        private Type _ownerType;

        /// <summary>
        /// 所属类(指主体类)
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Type OwnerType
        {
            get
            {
                if (_ownerType == null)
                    switch (CriteriaExpressionType)
                    {
                        case CriteriaExpressionType.CriteriaLogical:
                            if (_left != null && _left.OwnerType != null)
                                _ownerType = _left.OwnerType;
                            else if (_right != null && _right.OwnerType != null)
                                _ownerType = _right.OwnerType;
                            break;
                        case CriteriaExpressionType.CriteriaOperate:
                            if (_leftOperation != null && _leftOperation.OwnerType != null)
                                _ownerType = _leftOperation.OwnerType;
                            else if (_rightOperation != null && _rightOperation.OwnerType != null)
                                _ownerType = _rightOperation.OwnerType;
                            break;
                        case CriteriaExpressionType.ExistsOrNotExists:
                            if (_left != null && _left.OwnerType != null)
                                _ownerType = _left.OwnerType;
                            break;
                    }

                return _ownerType;
            }
        }

        private readonly CriteriaExpressionType _criteriaExpressionType;

        /// <summary>
        /// 类型
        /// </summary>
        public CriteriaExpressionType CriteriaExpressionType
        {
            get { return _criteriaExpressionType; }
        }

        #region 条件表达式

        private CriteriaExpression _left;

        /// <summary>
        /// 条件表达式左
        /// </summary>
        public CriteriaExpression Left
        {
            get { return _left; }
            internal set { _left = value; }
        }

        /// <summary>
        /// 首个条件表达式左
        /// </summary>
        public CriteriaExpression FirstLeft
        {
            get
            {
                if (_left != null && _left.FirstLeft != null)
                    return _left.FirstLeft;
                return _left;
            }
        }

        private readonly CriteriaLogical _logical;

        /// <summary>
        /// 条件组合关系
        /// </summary>
        public CriteriaLogical Logical
        {
            get { return _logical; }
        }

        private CriteriaExpression _right;

        /// <summary>
        /// 条件表达式右
        /// </summary>
        public CriteriaExpression Right
        {
            get { return _right; }
            internal set { _right = value; }
        }

        #endregion

        #region 条件运算表达式

        private readonly OperationExpression _leftOperation;

        /// <summary>
        /// 条件运算表达式左
        /// </summary>
        public OperationExpression LeftOperation
        {
            get { return _leftOperation; }
        }

        private CriteriaOperator _criteriaOperator;

        /// <summary>
        /// 条件运算符
        /// </summary>
        public CriteriaOperator CriteriaOperator
        {
            get { return _criteriaOperator; }
        }

        private readonly bool _operateIgnoreCase;

        /// <summary>
        /// 条件运算忽略大小写
        /// 仅针对字符串类型的字段
        /// 缺省为 false
        /// </summary>
        public bool OperateIgnoreCase
        {
            get { return _operateIgnoreCase; }
        }

        private OperationExpression _rightOperation;

        /// <summary>
        /// 条件运算表达式右
        /// </summary>
        public OperationExpression RightOperation
        {
            get { return _rightOperation; }
        }

        private object _value;

        /// <summary>
        /// 值
        /// </summary>
        public object Value
        {
            get { return _value; }
            internal set
            {
                switch (_criteriaOperator)
                {
                    case CriteriaOperator.None:
                        if (value == null)
                        {
                            _right = True;
                            _rightOperation = null;
                            _value = null;
                            _haveValue = false;
                        }
                        else if (value is CriteriaExpression criteriaExpression)
                        {
                            _right = criteriaExpression;
                            _rightOperation = null;
                            _value = null;
                            _haveValue = false;
                        }
                        else
                            throw new NotImplementedException(String.Format("'{0}'运算符右节点{1}类型错误", _criteriaOperator, value));
                     
                        break;
                    case CriteriaOperator.Equal:
                    case CriteriaOperator.Like:
                    case CriteriaOperator.LikeLeft:
                    case CriteriaOperator.LikeRight:
                    case CriteriaOperator.In:
                        if (value == null)
                        {
                            _criteriaOperator = CriteriaOperator.IsNull;
                            _right = null;
                            _rightOperation = null;
                            _value = null;
                            _haveValue = false;
                        }
                        else if (value is OperationExpression operationExpression)
                        {
                            _right = null;
                            _rightOperation = operationExpression;
                            _value = null;
                            _haveValue = false;
                        }
                        else if (value is CriteriaExpression criteriaExpression)
                        {
                            _right = criteriaExpression;
                            _rightOperation = null;
                            _value = null;
                            _haveValue = false;
                        }
                        else
                        {
                            if (Utilities.FindListItemType(Utilities.GetUnderlyingType(value.GetType())) != null)
                                _criteriaOperator = CriteriaOperator.In;
                            else if (value.ToString().Contains("%"))
                                _criteriaOperator = CriteriaOperator.Like;
                            _right = null;
                            _rightOperation = null;
                            _value = value;
                            _haveValue = true;
                        }

                        break;
                    case CriteriaOperator.Greater:
                    case CriteriaOperator.GreaterOrEqual:
                    case CriteriaOperator.Lesser:
                    case CriteriaOperator.LesserOrEqual:
                    case CriteriaOperator.Unequal:
                    case CriteriaOperator.Unlike:
                    case CriteriaOperator.NotIn:
                        if (value == null)
                        {
                            _criteriaOperator = CriteriaOperator.IsNotNull;
                            _right = null;
                            _rightOperation = null;
                            _value = null;
                            _haveValue = false;
                        }
                        else if (value is OperationExpression operationExpression)
                        {
                            _right = null;
                            _rightOperation = operationExpression;
                            _value = null;
                            _haveValue = false;
                        }
                        else if (value is CriteriaExpression criteriaExpression)
                        {
                            _right = criteriaExpression;
                            _rightOperation = null;
                            _value = null;
                            _haveValue = false;
                        }
                        else
                        {
                            if (Utilities.FindListItemType(Utilities.GetUnderlyingType(value.GetType())) != null)
                                _criteriaOperator = CriteriaOperator.NotIn;
                            else if (value.ToString().Contains("%"))
                                _criteriaOperator = CriteriaOperator.Unlike;
                            _right = null;
                            _rightOperation = null;
                            _value = value;
                            _haveValue = true;
                        }

                        break;
                    default:
                        if (value != null)
                            throw new NotImplementedException(String.Format("不允许'{0}'运算符有右节点{1}", _criteriaOperator, value));
                        _value = null;
                        break;
                }
            }
        }

        private bool _haveValue;

        /// <summary>
        /// 是否存在值
        /// </summary>
        public bool HaveValue
        {
            get { return _haveValue; }
        }

        internal Type ValueUnderlyingType
        {
            get { return _value != null ? Utilities.GetUnderlyingType(_value.GetType()) : null; }
        }

        internal Type ValueCoreUnderlyingType
        {
            get { return _value != null ? Utilities.GetUnderlyingType(Utilities.GetCoreType(_value.GetType())) : null; }
        }

        #endregion

        #region 短路情况

        private readonly bool _shortValue;

        /// <summary>
        /// 短路值
        /// </summary>
        public bool ShortValue
        {
            get { return _shortValue; }
        }

        #endregion

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
            CriteriaExpression other = obj as CriteriaExpression;
            if (object.ReferenceEquals(other, null))
                return false;
            if (_criteriaExpressionType != other._criteriaExpressionType)
                return false;
            if (_criteriaExpressionType == CriteriaExpressionType.Short)
                return _shortValue == other._shortValue;
            if (_left != null)
            {
                if (!_left.Equals(other._left))
                    return false;
            }
            else if (other._left != null)
                return false;
            if (_logical != other._logical)
                return false;
            if (_right != null)
            {
                if (!_right.Equals(other._right))
                    return false;
            }
            else if (other._right != null)
                return false;
            if (_leftOperation != null)
            {
                if (!_leftOperation.Equals(other._leftOperation))
                    return false;
            }
            else if (other._leftOperation != null)
                return false;
            if (_criteriaOperator != other._criteriaOperator)
                return false;
            if (_operateIgnoreCase != other._operateIgnoreCase)
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
            return _criteriaExpressionType == CriteriaExpressionType.Short
                ? _criteriaExpressionType.ToString().GetHashCode() ^
                  _shortValue.GetHashCode()
                : _criteriaExpressionType.ToString().GetHashCode() ^
                  (_left != null ? _left.GetHashCode() : 0) ^
                  _logical.ToString().GetHashCode() ^
                  (_right != null ? _right.GetHashCode() : 0) ^
                  (_leftOperation != null ? _leftOperation.GetHashCode() : 0) ^
                  _criteriaOperator.ToString().GetHashCode() ^
                  _operateIgnoreCase.GetHashCode() ^
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
            result.Append(_criteriaExpressionType.ToString());
            if (_criteriaExpressionType == CriteriaExpressionType.Short)
                result.Append(_shortValue.ToString());
            else
            {
                if (_left != null)
                    result.Append(_left.ToString());
                result.Append(_logical.ToString());
                if (_right != null)
                    result.Append(_right.ToString());
                if (_leftOperation != null)
                    result.Append(_leftOperation.ToString());
                result.Append(_criteriaOperator.ToString());
                result.Append(_operateIgnoreCase.ToString());
                if (_rightOperation != null)
                    result.Append(_rightOperation.ToString());
                if (_value != null)
                {
                    result.Append(_value.GetType().FullName);
                    result.Append(_value.ToString());
                }
                result.Append(_haveValue.ToString());
            }

            return result.ToString();
        }

        #endregion
    }
}