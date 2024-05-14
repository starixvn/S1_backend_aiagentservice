using AIAgentService.Core.Models.Enums;

namespace AIAgentService.Core.Models;

public class ApiResult
{
    public int ErrorCode { get; set; }
    public string Message { get; set; }
    public string RawErrorCode { get; set; }
    public string RawMessage { get; set; }

    public ApiResult() : this(ErrorCodeEnum.SystemError)
    {
    }

    public ApiResult(ErrorCodeEnum errorCode, string message = "", string rawErrorCode = "", string rawMessage = "")
    {
        ErrorCode = (int)errorCode;
        Message = string.IsNullOrEmpty(message) ? errorCode.ToString() : message;
        RawErrorCode = rawErrorCode;
        RawMessage = rawMessage;
    }
}

public class ApiResult<TData> : ApiResult
{
    public ApiResult() : this(ErrorCodeEnum.SystemError, default)
    {
    }
    
    public ApiResult(ErrorCodeEnum errorCode, TData data, string message = "")
        : base(errorCode, message)
    {
        Data = data;
    }

    public TData Data { get; set; }
}