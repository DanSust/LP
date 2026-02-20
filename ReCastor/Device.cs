using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReCastor
{
    public enum DeviceState
    {
        Ready, Idly, Busy, Broken
    }
    public class Device : BaseNamedEntity
    {
        public DeviceState State { get; set; }
        public string StateDescription { get; set; } = String.Empty;
        public required DeviceType DeviceType;
    }
}
