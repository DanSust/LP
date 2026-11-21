using System.ComponentModel.DataAnnotations;

namespace LP.Server.DTO
{
    public class ForgotPasswordModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
