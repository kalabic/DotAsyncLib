namespace DotAsync.Lock;


internal sealed class PriorityTicketHandler
{
    public static LockedTicket LinkedTickets(in LockedTicket asyncTicket, in LockedTicket priorityTicket)
    {
        var ticketPair = new PriorityTicketHandler(asyncTicket, priorityTicket);
        return new LockedTicket(priorityTicket.Ticket, ticketPair.Exit);
    }

    private readonly LockedTicket _asyncTicket;

    private readonly LockedTicket _priorityTicket;

    public PriorityTicketHandler(in LockedTicket asyncTicket, in LockedTicket priorityTicket)
    {
        _asyncTicket = asyncTicket;
        _priorityTicket = priorityTicket;
    }

    private void Exit(in LockedTicket lockedTicket)
    {
        _priorityTicket.Dispose();
        _asyncTicket.Dispose();
    }
}
