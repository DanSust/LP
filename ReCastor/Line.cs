using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReCastor
{
    public class Line: BaseNamedEntity
    {
        public bool IsActive { get; set; }
        public IList<Tuple<int, Device>>? Devices { get; set; }
    }
}
