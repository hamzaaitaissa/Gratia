namespace Gratia.Application.Interfaces
{
    // Invitation emails go out in the background (#55, SendInvitationEmailsCommand). Call Notify after committing a
    // queued email, so the sender runs at once rather than at its next check. Nothing is lost if it isn't called, or if
    // no sender runs in this process: the queue is in the database.
    public interface IInvitationEmailQueue
    {
        void Notify();
    }
}
