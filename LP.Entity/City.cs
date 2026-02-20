using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Drawing;

namespace LP.Entity;

public class City : BaseEntity
{
    [StringLength(500)]
    public string Name { get; set; } = String.Empty;
    // Координаты: DECIMAL(9,6) — точность ~10 см
    [Column(TypeName = "decimal(9,6)")]
    public double Latitude { get; set; }
    [Column(TypeName = "decimal(9,6)")]
    public double Longitude { get; set; }
}