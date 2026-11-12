using System;
using System.Collections.Generic;

namespace Toaba
{
    /// <summary>
    /// Thrown by every SDK call that fails. Cancelled calls throw OperationCanceledException instead.
    /// </summary>
    public sealed class ToabaException : Exception
    {
        /// <summary>Toaba's error code, such as <c>invalid_code</c>, or one of <see cref="ErrorCodes"/>.</summary>
        public string Code { get; }

        /// <summary>The HTTP status, or 0 when no response arrived.</summary>
        public int Status { get; }

        /// <summary>For <c>validation_failed</c>: the messages for each field.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> FieldErrors { get; }

        public TimeSpan? RetryAfter { get; }

        public string RequestId { get; }

        internal ToabaException(
            string code,
            string message,
            int status = 0,
            IReadOnlyDictionary<string, IReadOnlyList<string>> fieldErrors = null,
            TimeSpan? retryAfter = null,
            string requestId = null)
            : base(message)
        {
            Code = code;
            Status = status;
            FieldErrors = fieldErrors ?? new Dictionary<string, IReadOnlyList<string>>();
            RetryAfter = retryAfter;
            RequestId = requestId;
        }
    }

    public static class ErrorCodes
    {
        /// <summary>No response arrived: no connection, a timeout, or an aborted request.</summary>
        public const string NetworkError = "network_error";

        /// <summary>A response the SDK couldn't read, or one without an error code.</summary>
        public const string UnexpectedResponse = "unexpected_response";

        public const string RateLimited = "rate_limited";
        public const string ValidationFailed = "validation_failed";
    }
}
