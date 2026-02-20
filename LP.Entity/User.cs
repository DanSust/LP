using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LP.Entity
{
    public class User : BaseEntity
    {
        [StringLength(50)]
        public string Caption { get; set; } = string.Empty;
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;
        [StringLength(250)]
        public byte[]? PasswordHash { get; set; }
        [StringLength(150)]
        public DateOnly Birthday { get; set; }
        public Boolean? Sex { get; set; } = null;
        public DateTime? Created { get; set; } = null;
        public string? Provider { get; set; } = string.Empty;
        public string? ProviderId { get; set; } = string.Empty;
        public string? Email { get; set; } = string.Empty;
        public DateTime? LastLogin { get; set; } = null;
        public DateTime? EventsSeen { get; set; } = null;
        public bool IsPaused { get; set; } = false;
        public virtual EmailConfirmation? EmailConfirmation { get; set; }
    }
}