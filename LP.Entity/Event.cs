using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace LP.Entity;

public class Event : BaseEntity
{
    [StringLength(200)]
    public string Title { get; set; }
    [StringLength(1500)]
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsActive { get; set; } = true; // Можно отключать старые события
}