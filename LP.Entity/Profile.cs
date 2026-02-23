using System.ComponentModel.DataAnnotations;

namespace LP.Entity;

// 0 - без обязательств, 1 - позже решу, 2 - все серьезно
public enum Aim {aimFree = 0, aimLater = 1, aimSerios = 2}


public class Profile: BaseEntity
{
    public Guid UserId { get; set; }
    [StringLength(500)]
    public string Description { get; set; } = String.Empty;

    public int Weight { get; set; } = 100;
    public int Height { get; set; } = 100;

    public int AgeFrom { get; set; } = 18;
    public int AgeTo { get; set; } = 100;

    public Guid CityId { get; set; }
    public Aim Aim { get; set; } = Aim.aimLater;
    public bool SendEmail { get; set; } = true;
    public bool SendTelegram { get; set; } = true;
    public bool WithPhoto { get; set; } = true;
    public bool WithEmail { get; set; } = true;
    public bool WithLikes { get; set; } = false;
}