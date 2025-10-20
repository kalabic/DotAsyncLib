namespace DotAsync.Model;


public interface IAsyncValueReader<TValue> 
{
    public bool IsCancelled { get; }

    public bool IsSet { get; }

    public TValue Value { get; }
}
