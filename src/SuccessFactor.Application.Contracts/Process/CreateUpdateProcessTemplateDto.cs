using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SuccessFactor.Process
{
    public class CreateUpdateProcessTemplateDto
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = default!;
        public int Version { get; set; } = default!;
        public Boolean IsDefault { get; set; } = default!;
    }
}
