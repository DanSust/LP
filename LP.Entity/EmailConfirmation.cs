using System;
using System.ComponentModel.DataAnnotations;

namespace LP.Entity
{
    public class EmailConfirmation
    {
        [Key]
        public Guid UserId { get; set; } // PK & FK

        public virtual User User { get; set; } // Navigation property

        public bool IsConfirmed { get; set; } = false;

        public string? ConfirmationToken { get; set; }

        public DateTime? TokenExpires { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}