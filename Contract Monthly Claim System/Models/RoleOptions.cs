#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Contract_Monthly_Claim_System.Models
{
    public static class RoleOptions
    {
        private static readonly string[] _supportedRoles = new[]
        {
            "Lecturer",
            "Program Coordinator",
            "Academic Manager",
            "HR"
        };

        public static IReadOnlyList<string> SupportedRoles => _supportedRoles;

        public static string Normalize(string? role)
        {
            if (string.IsNullOrWhiteSpace(role)) return _supportedRoles[0];
            var match = _supportedRoles.FirstOrDefault(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
            return match ?? _supportedRoles[0];
        }
    }
}

