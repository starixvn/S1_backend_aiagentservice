using System.Runtime.Serialization;
using AIAgentService.Core.Models.Enums;

namespace AIAgentService.Core.Exceptions;

public class AIAgentException : Exception, ISerializable
{
    public ErrorCodeEnum ErrorCode { get; set; }

    public AIAgentException()
    {
    }

    public AIAgentException(string message)
        : base(message)
    {
    }
    
    public AIAgentException(ErrorCodeEnum errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public AIAgentException(ErrorCodeEnum errorCode, string message, Exception inner)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }

    public AIAgentException(string message, Exception inner)
        : this(ErrorCodeEnum.SystemError, message, inner)
    {
    }
}