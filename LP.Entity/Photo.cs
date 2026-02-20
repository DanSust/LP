using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LP.Entity
{
    public class Photo : BaseEntity
    {
        public required User User { get; set; }
        public string? Path { get; set; } = string.Empty;
    }

    [Table("PhotoMain")]
    public class PhotoMain()
    {
        [Key]                       // PK == FK
        public Guid PhotoId { get; set; }
        public required User User { get; set; }
    }
}
