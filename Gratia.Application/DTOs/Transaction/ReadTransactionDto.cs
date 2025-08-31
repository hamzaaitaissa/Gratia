using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.DTOs.Transaction
{
    public class ReadTransactionDto
    {
        public Guid Id { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; }
        public Guid ReceiverId { get; set; }
        public string ReceiverName { get; set; }
        public Guid CompanyId { get; set; }
        public string CompanyName { get; set; }
        public int Amount { get; set; }
        public string Message { get; set; }
        public string TypeOfDonation { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
