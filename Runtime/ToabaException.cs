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

        /// <summary>The email code is wrong, expired or used. The player can try again.</summary>
        public const string InvalidCode = "invalid_code";

        /// <summary>A code was sent recently. <see cref="ToabaException.RetryAfter"/> says when to resend.</summary>
        public const string EmailCooldown = "email_cooldown";

        /// <summary>The email belongs to another player. Toaba never merges players.</summary>
        public const string EmailExists = "email_exists";

        public const string UsernameTaken = "username_taken";

        // Handled by the SDK itself.
        internal const string InvalidAccessToken = "invalid_access_token";
        internal const string SessionBusy = "session_busy";
        internal const string InvalidRefreshToken = "invalid_refresh_token";
        internal const string RefreshTokenReused = "refresh_token_reused";
        internal const string SessionExpired = "session_expired";
    }
}
