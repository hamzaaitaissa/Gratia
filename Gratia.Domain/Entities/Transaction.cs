using Gratia.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Domain.Entities
{
    internal class Transaction : BaseEntity
    {
        public Guid SenderId { get; set; }
        public User Sender { get; set; }
        public Guid ReceiverId { get; set; }
        public User Receiver { get; set; }
        public int Amount { get; set; }
        public string Message { get; set; }
        public string TypeOfDonation { get; set; }


        public Transaction(Guid senderId, Guid receiverId, int amount, string message, string typeOfDonation)
        {
            SenderId = senderId;
            ReceiverId = receiverId;
            Amount = amount;
            Message = message;
            TypeOfDonation = typeOfDonation;
        }

        public void Update(Guid senderId, Guid receiverId, int amount, string message, string typeOfDonation)
        {
            SenderId = senderId;
            ReceiverId = receiverId;
            Amount = amount;
            Message = message;
            TypeOfDonation = typeOfDonation;
        }

    }
}
