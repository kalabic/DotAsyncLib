namespace DotAsync.Lock;


internal class PriorityTicketHandler 
    : ITicketHandler
{
    private readonly LockedTicket _asyncTicket;

    private readonly LockedTicket _priorityTicket;

    public PriorityTicketHandler(in LockedTicket asyncTicket, in LockedTicket priorityTicket)
    {
        _asyncTicket = asyncTicket;
        _priorityTicket = priorityTicket;
    }

    public void Exit(in LockedTicket lockedTicket)
    {
        _priorityTicket.Dispose();
        _asyncTicket.Dispose();
    }
}
