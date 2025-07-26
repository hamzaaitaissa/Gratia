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
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public User User { get; set; }
        public int Amount { get; set; }
        public string Message { get; set; }
        public string TypeOfdonation { get; set; }


    }
}
