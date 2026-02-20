using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LP.Entity
{
    public enum LinkType
    {
        EMail = 1,
        Phone,
        WhatsApp,
        Telegram
    }
    public class Connectity: BaseEntity
    {
        [Required]
        public LinkType LinkType { get; set; }
        [StringLength(250)]
        public string Value { get; set; } = string.Empty;
    }
}
