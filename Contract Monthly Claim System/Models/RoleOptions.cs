#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace ContractMonthlyClaimSystem.Models
{
    public static class RoleOptions
    {
        public const string Admin = "Admin";
        public const string HR = "HR";
        public const string Lecturer = "Lecturer";

        public static IReadOnlyList<string> AllRoles { get; } = new[] { Admin, HR, Lecturer };

        public static bool IsValid(string? role) =>
            !string.IsNullOrEmpty(role) && AllRoles.Contains(role);
    }
}

