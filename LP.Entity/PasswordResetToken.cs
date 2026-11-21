using System;
using System.ComponentModel.DataAnnotations;

namespace LP.Entity
{
    public class PasswordResetToken
    {
        [Key]
        public Guid UserId { get; set; } // PK & FK

        public virtual User User { get; set; }

        public string? Token { get; set; }

        public DateTime? TokenExpires { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
