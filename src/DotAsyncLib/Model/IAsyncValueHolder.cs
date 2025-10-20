namespace DotAsync.Model;


public interface IAsyncValueHolder
{
    IAsyncValueWaiter GetWaiter();
}
