using ItemTool.Application.DTOs;
using ItemTool.Domain.Validation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ItemTool.Application.Validation
{
    public sealed class ItemValidator
    {
        public IReadOnlyList<ValidationIssue> Validate(ItemDto item, IEnumerable<ItemDto> allItems)
        {
            List<ValidationIssue> issues = new();

            if (string.IsNullOrWhiteSpace(item.ItemName))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = "Name es obligatorio."
                });
            }

            if (string.IsNullOrWhiteSpace(item.IconPath))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = "Icon es obligatorio."
                });
            }

            if (item.MaxStack < 1)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = "Max Stack debe ser >= 1."
                });
            }

            if (item.SlotDimension.Width < 1)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = "Width debe ser >= 1."
                });
            }

            if (item.SlotDimension.Height < 1)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = "Height debe ser >= 1."
                });
            }

            if (!string.IsNullOrWhiteSpace(item.ItemName))
            {
                int duplicates = allItems.Count(x =>
                    !ReferenceEquals(x, item) &&
                    !string.IsNullOrWhiteSpace(x.ItemName) &&
                    string.Equals(x.ItemName.Trim(), item.ItemName.Trim(), StringComparison.OrdinalIgnoreCase));

                if (duplicates > 0)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = $"itemName duplicado: \"{item.ItemName}\"."
                    });
                }
            }

            return issues;
        }
    }
}
