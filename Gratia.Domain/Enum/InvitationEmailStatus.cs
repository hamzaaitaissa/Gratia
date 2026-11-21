namespace Gratia.Domain.Enum
{
    // Where an invitation's email is (#55). Invitations from admins are sent in the background; a founding invitation's
    // setup email is sent by signup itself, so it never waits. Sent is 0 so that invitations from before the queue, all
    // sent at once, count as sent.
    public enum InvitationEmailStatus
    {
        Sent = 0,
        Pending = 1,
        Failed = 2
    }
}
