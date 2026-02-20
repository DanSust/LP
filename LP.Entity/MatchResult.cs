using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LP.Entity
{
    public class MatchResult
    {
        public Guid UserId { get; set; }
        public string Category { get; set; } = null!;
        public DateTime LastAdded { get; set; }
        public int SortOrder { get; set; }
    }
}
