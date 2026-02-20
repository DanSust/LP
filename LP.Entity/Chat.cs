using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LP.Entity
{
    public class Chat : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public Guid Owner { get; set; }
        public Guid UserId { get; set; }
        public bool OwnerUsedAI  { get; set; } = false;
        public bool UserUsedAI { get; set; } = false;
        public DateTime Time { get; set; } = DateTime.UtcNow;
    }
}
