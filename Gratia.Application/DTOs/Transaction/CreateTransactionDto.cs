using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.DTOs.Transaction
{
    public class CreateReadTransactionDto
    {
        [Required]
        public Guid ReceiverId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public int Amount { get; set; }

        [Required]
        [StringLength(500, ErrorMessage = "Message cannot exceed 500 characters")]
        public string Message { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "Type of donation cannot exceed 100 characters")]
        public string TypeOfDonation { get; set; }
    }
}
