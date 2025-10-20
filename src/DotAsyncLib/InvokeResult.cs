using System.Diagnostics.CodeAnalysis;

namespace DotAsync;


/// <summary>
/// 
/// The type returned by various 'InvokeSync' functions, wrapped in a <see cref="ValueTask"/>
/// most of the time. Contains <see cref="InvokeStatus"/>, telling if 'InvokeSync' call was
/// successful or not.
/// 
/// </summary>
public readonly struct InvokeResult
    : IEquatable<InvokeResult>
{
    //-------------------------------------------------------------------------
    //
    // Public properties.
    //
    //-------------------------------------------------------------------------

    public readonly long Value;

    public readonly InvokeStatus Status;


    //-------------------------------------------------------------------------
    //
    // Implementation.
    //
    //-------------------------------------------------------------------------

    public InvokeResult(bool value)
    {
        Value = value ? 1 : 0;
        Status = InvokeStatus.SUCCESS;
    }

    public InvokeResult(short value)
    {
        Value = value;
        Status = InvokeStatus.SUCCESS;
    }

    public InvokeResult(int value)
    {
        Value = value;
        Status = InvokeStatus.SUCCESS;
    }

    public InvokeResult(long value)
    {
        Value = value;
        Status = InvokeStatus.SUCCESS;
    }

    public InvokeResult(ulong value)
    {
        Value = (long)value;
        Status = InvokeStatus.SUCCESS;
    }

    public InvokeResult(InvokeStatus status)
    {
        Value = 0;
        Status = status;
    }

    public bool AsBool()
    {
        return Value == 1;
    }

    public short AsShort()
    {
        return (short)Value;
    }

    public int AsInt()
    {
        return (int)Value;
    }

    public long AsLong()
    {
        return Value;
    }

    public ulong AsULong()
    {
        return (ulong)Value;
    }

    public ValueTask<InvokeResult> AsValueTask()
    {
        return ValueTask.FromResult(this);
    }

    public bool Equals(InvokeResult other)
        => other.Status == Status && other.Value == Value;

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is InvokeResult other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Status, Value);

    public override string ToString()
        => Status == InvokeStatus.SUCCESS ? Value.ToString() : Status.ToString();


    // Operators >>

    public static bool operator ==(InvokeResult first, InvokeResult second)
    {
        return (first.Status == second.Status) && (first.Value == second.Value);
    }

    public static bool operator !=(InvokeResult first, InvokeResult second)
    {
        return (first.Status != second.Status) || (first.Value != second.Value);
    }


    //-------------------------------------------------------------------------
    //
    // Static properties.
    //
    //-------------------------------------------------------------------------

    public static readonly InvokeResult SUCCESS = new(InvokeStatus.SUCCESS);

    public static readonly InvokeResult FAILED = new(InvokeStatus.FAILED);

    public static readonly InvokeResult CANCELED = new(InvokeStatus.CANCELED);

    public static readonly InvokeResult EXCEPTION = new(InvokeStatus.EXCEPTION);

    public static readonly InvokeResult TIMEOUT = new(InvokeStatus.TIMEOUT);

    public static readonly InvokeResult DISPOSED = new(InvokeStatus.DISPOSED);

    public static readonly InvokeResult NOT_FOUND = new(InvokeStatus.NOT_FOUND);

    public static readonly InvokeResult BAD_STATE = new(InvokeStatus.BAD_STATE);

    public static readonly InvokeResult BAD_MESSAGE = new(InvokeStatus.BAD_MESSAGE);


    public static implicit operator bool(InvokeResult other)
    {
        return other.Status == InvokeStatus.SUCCESS;
    }

    public static implicit operator InvokeResult(bool other) 
    { 
        return other ? SUCCESS : FAILED;
    }

    public static implicit operator InvokeResult(InvokeStatus other)
    {
        switch (other)
        {
            case InvokeStatus.SUCCESS:
                return SUCCESS;

            case InvokeStatus.FAILED:
                return FAILED;

            case InvokeStatus.CANCELED:
                return CANCELED;

            case InvokeStatus.EXCEPTION:
                return EXCEPTION;

            case InvokeStatus.TIMEOUT:
                return TIMEOUT;

            case InvokeStatus.DISPOSED:
                return DISPOSED;

            case InvokeStatus.NOT_FOUND:
                return NOT_FOUND;

            case InvokeStatus.BAD_STATE:
                return BAD_STATE;

            case InvokeStatus.BAD_MESSAGE:
                return BAD_MESSAGE;

            default:
                return FAILED;
        }
    }
}
