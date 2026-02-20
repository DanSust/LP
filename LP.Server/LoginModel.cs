using System.ComponentModel.DataAnnotations;

namespace LP.Server
{
    public class LoginModel
    {
        [Required] public string? Username { get; set; }
        [Required] public string? Password { get; set; }
    }
}
