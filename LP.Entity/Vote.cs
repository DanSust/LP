using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LP.Entity;

[PrimaryKey(nameof(Owner), nameof(Like))]
public class Vote 
{
    public Guid Owner { get; set; }
    public Guid Like { get; set; }
    public bool IsLike { get; set; } = false;
    public bool IsReject { get; set; } = false;
    public bool IsViewed { get; set; } = false;
    public DateTime Added { get; set; } = DateTime.Now;
}