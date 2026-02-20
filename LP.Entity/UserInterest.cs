using System.ComponentModel.DataAnnotations;

namespace LP.Entity;

public class UserInterest : BaseEntity
{
    public required User User { get; set; }
    public Interest Interest { get; set; }
    public int Order { get; set; } = 0;
}
