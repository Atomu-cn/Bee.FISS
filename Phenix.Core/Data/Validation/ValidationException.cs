using System;

namespace Phenix.Core.Data.Validation
{
    /// <summary>
    /// 数据验证异常
    /// </summary>
    [Serializable]
    public class ValidationException : System.ComponentModel.DataAnnotations.ValidationException
    {
        /// <summary>
        /// 数据验证异常
        /// </summary>
        /// <param name="key">键值</param>
        /// <param name="statusCode">状态码(1000以下为保留值)</param>
        /// <param name="innerException">内嵌异常</param>
        public ValidationException(string key, int statusCode, Exception innerException = null)
            : this(key, statusCode, null, innerException)
        {
        }

        /// <summary>
        /// 数据验证异常
        /// </summary>
        /// <param name="key">键值</param>
        /// <param name="statusCode">状态码(1000以下为保留值)</param>
        /// <param name="message">错误消息</param>
        /// <param name="innerException">内嵌异常</param>
        public ValidationException(string key, int statusCode, string message, Exception innerException = null)
            : this(new ValidationMessage(key, statusCode, message), innerException)
        {
        }

        /// <summary>
        /// 数据验证异常
        /// </summary>
        /// <param name="validationMessage">数据验证消息</param>
        /// <param name="innerException">内嵌异常</param>
        public ValidationException(ValidationMessage validationMessage, Exception innerException = null)
            : base(validationMessage.Hint, innerException)
        {
            _validationMessage = validationMessage;
        }

        /// <summary>
        /// 数据验证异常
        /// </summary>
        /// <param name="key">键值</param>
        /// <param name="statusCode">状态码(1000以下为保留值)</param>
        /// <param name="validationResult">数据验证结果</param>
        /// <param name="validatingAttribute">数据验证标签</param>
        /// <param name="value">值</param>
        public ValidationException(string key, int statusCode, System.ComponentModel.DataAnnotations.ValidationResult validationResult, System.ComponentModel.DataAnnotations.ValidationAttribute validatingAttribute, object value)
            : base(validationResult, validatingAttribute, value)
        {
            _validationMessage = new ValidationMessage(key, statusCode, validationResult.ErrorMessage);
        }

        /// <summary>
        /// 数据验证异常
        /// </summary>
        /// <param name="key">键值</param>
        /// <param name="statusCode">状态码(1000以下为保留值)</param>
        /// <param name="message">错误消息</param>
        /// <param name="validatingAttribute">数据验证标签</param>
        /// <param name="value">值</param>
        public ValidationException(string key, int statusCode, string message, System.ComponentModel.DataAnnotations.ValidationAttribute validatingAttribute, object value)
            : base(message, validatingAttribute, value)
        {
            _validationMessage = new ValidationMessage(key, statusCode, message);
        }

        #region 属性

        private readonly ValidationMessage _validationMessage;

        /// <summary>
        /// 数据验证消息
        /// </summary>
        public ValidationMessage ValidationMessage
        {
            get { return _validationMessage; }
        }

        #endregion
    }
}