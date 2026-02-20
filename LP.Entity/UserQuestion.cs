using System.ComponentModel.DataAnnotations;

namespace LP.Entity;

public class UserQuestion : BaseEntity
{
    [StringLength(50)]
    public required User User { get; set; }
    public string Question { get; set; }
    public int Order { get; set; } = 0;
}