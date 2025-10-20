namespace DotAsync.Lock;


public interface ITicketHandler
{
    void Exit(in LockedTicket lockedTicket);
}
