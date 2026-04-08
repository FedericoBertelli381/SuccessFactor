using System;
using System.Collections.Generic;
using System.Text;

namespace SuccessFactor.Process
{
    public class ProcessTemplateDto
    {
        public string Name { get; set; } = default!;
        public int Version { get; set; } = default!;
        public Boolean IsDefault { get; set; } = default!;
    }
}
