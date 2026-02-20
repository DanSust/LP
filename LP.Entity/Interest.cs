using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LP.Entity
{
    public class Interest : BaseEntity
    {
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;
        [StringLength(150)]
        public string Path { get; set; } = string.Empty;
        // 1 - family
        // 2 - Character
        // 3 - Finance
        // 4 - Sexuality
        // 5 - Social
        // 6 - Religion
        // 7 - Valuation
        public int Group { get; set; } = 1;
    }
}
