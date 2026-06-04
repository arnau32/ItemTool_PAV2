using System;
using System.Collections.Generic;
using System.Text;

namespace ItemTool.Domain.Validation
{
    public sealed class ValidationIssue
    {
        public ValidationSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
