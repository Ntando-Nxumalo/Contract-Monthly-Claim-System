using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Contract_Monthly_Claim_System.Data;
using Contract_Monthly_Claim_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Contract_Monthly_Claim_System.Controllers
{
    [Authorize]
    [Route("Chat")] 
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public ChatController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpPost("Ask")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Ask([FromForm] string message, [FromForm] List<IFormFile>? files)
        {
            message = (message ?? string.Empty).Trim();
            var role = GetRole();

            try
            {
                // If files provided, prioritize document analysis
                if (files != null && files.Count > 0)
                {
                    var analysis = await AnalyzeFilesAsync(files);
                    if (!string.IsNullOrWhiteSpace(analysis))
                    {
                        return Ok(new { text = analysis });
                    }
                }

                // No files or nothing extracted: analyze the message
                var answer = await AnswerFromDataAsync(role, message);
                if (string.IsNullOrWhiteSpace(answer))
                {
                    answer = Fallback(role, message);
                }
                return Ok(new { text = answer });
            }
            catch (Exception ex)
            {
                return Ok(new { text = $"Sorry, I ran into a problem processing that. Error: {ex.Message}" });
            }
        }

        private string GetRole()
        {
            if (User.IsInRole("Academic Manager")) return "manager";
            if (User.IsInRole("Program Coordinator")) return "coordinator";
            return "lecturer";
        }

        private async Task<string> AnswerFromDataAsync(string role, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return string.Empty;
            var m = message.ToLowerInvariant();

            // Period parsing
            (DateTime? from, DateTime? to) = ParsePeriod(m);

            // Base query with role restrictions
            IQueryable<Claim> q = _db.Claims.AsNoTracking();
            if (role == "lecturer")
            {
                var uid = User?.Identity?.Name; // fallback, not used for filtering
                var userId = User.Claims.FirstOrDefault(c => c.Type.EndsWith("nameidentifier", StringComparison.OrdinalIgnoreCase))?.Value;
                if (!string.IsNullOrEmpty(userId)) q = q.Where(c => c.LecturerUserId == userId);
            }
            if (from.HasValue) q = q.Where(c => c.CreatedAt >= from.Value);
            if (to.HasValue) q = q.Where(c => c.CreatedAt <= to.Value);

            // Highest / lowest claims by lecturer (sum)
            if (Regex.IsMatch(m, @"(highest|top|max).*(claim|spend|amount)\b") || Regex.IsMatch(m, @"\bwho\b.*(highest|most).*(claim|spend)"))
            {
                var grouped = await q.GroupBy(c => new { c.LecturerUserId, c.LecturerName })
                                      .Select(g => new { g.Key.LecturerName, Total = g.Sum(x => x.Total) })
                                      .OrderByDescending(x => x.Total)
                                      .Take(5)
                                      .ToListAsync();
                if (grouped.Count == 0) return "No claims found for the selected period.";
                var top = grouped.First();
                var list = string.Join("\n", grouped.Select((x, i) => $"{i + 1}. {x.LecturerName}: R {x.Total:N2}"));
                var scope = DescribePeriod(from, to);
                return $"Highest total claims {scope}: {top.LecturerName} with R {top.Total:N2}.\nTop 5:\n{list}";
            }

            if (Regex.IsMatch(m, @"(lowest|min).*(claim|spend|amount)\b") || Regex.IsMatch(m, @"\bwho\b.*(lowest|least).*(claim|spend)"))
            {
                var grouped = await q.GroupBy(c => new { c.LecturerUserId, c.LecturerName })
                                      .Select(g => new { g.Key.LecturerName, Total = g.Sum(x => x.Total) })
                                      .OrderBy(x => x.Total)
                                      .Take(5)
                                      .ToListAsync();
                if (grouped.Count == 0) return "No claims found for the selected period.";
                var bottom = grouped.First();
                var list = string.Join("\n", grouped.Select((x, i) => $"{i + 1}. {x.LecturerName}: R {x.Total:N2}"));
                var scope = DescribePeriod(from, to);
                return $"Lowest total claims {scope}: {bottom.LecturerName} with R {bottom.Total:N2}.\nBottom 5:\n{list}";
            }

            // Status queries
            if (m.Contains("rejected") && (m.Contains("this month") || m.Contains("month")))
            {
                var (fm, tm) = ParsePeriod("this month");
                var rejected = await q.Where(c => c.Status == "Rejected")
                                       .Where(c => !fm.HasValue || c.CreatedAt >= fm.Value)
                                       .Where(c => !tm.HasValue || c.CreatedAt <= tm.Value)
                                       .OrderByDescending(c => c.CreatedAt)
                                       .Select(c => new { c.Id, c.LecturerName, c.Total, c.CreatedAt })
                                       .ToListAsync();
                if (rejected.Count == 0) return "No rejected claims found this month.";
                var rows = string.Join("\n", rejected.Select(c => $"CLM-{c.Id:D3} • {c.LecturerName} • R {c.Total:N2} • {c.CreatedAt:dd MMM}"));
                return $"Rejected claims this month ({rejected.Count}):\n{rows}";
            }

            // Totals
            if (m.Contains("total") || m.Contains("sum") || m.Contains("aggregate"))
            {
                var total = await q.SumAsync(c => (double?)c.Total) ?? 0.0;
                var scope = DescribePeriod(from, to);
                return $"Total claimed {scope}: R {total:N2}.";
            }

            // Help fallback based on role
            return string.Empty;
        }

        private static (DateTime? from, DateTime? to) ParsePeriod(string m)
        {
            m = (m ?? string.Empty).ToLowerInvariant();
            var now = DateTime.Now;
            if (m.Contains("this month"))
            {
                var from = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Local);
                var to = from.AddMonths(1).AddTicks(-1);
                return (from, to);
            }
            if (m.Contains("last month"))
            {
                var from = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Local).AddMonths(-1);
                var to = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Local).AddTicks(-1);
                return (from, to);
            }
            if (m.Contains("this year"))
            {
                var from = new DateTime(now.Year, 1, 1);
                var to = new DateTime(now.Year, 12, 31, 23, 59, 59);
                return (from, to);
            }
            // Range pattern: between 2025-01-01 and 2025-02-01
            var match = Regex.Match(m, @"(\d{4}-\d{2}-\d{2}).*(\d{4}-\d{2}-\d{2})");
            if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var f) && DateTime.TryParse(match.Groups[2].Value, out var t))
            {
                return (f, t);
            }
            return (null, null);
        }

        private static string DescribePeriod(DateTime? from, DateTime? to)
        {
            if (!from.HasValue && !to.HasValue) return "(all time)";
            return $"({from:yyyy-MM-dd} to {to:yyyy-MM-dd})";
        }

        private async Task<string> AnalyzeFilesAsync(List<IFormFile> files)
        {
            var parts = new List<string>();
            foreach (var f in files)
            {
                if (f == null || f.Length == 0) continue;
                var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
                if (ext == ".pdf")
                {
                    using var ms = new MemoryStream();
                    await f.CopyToAsync(ms);
                    ms.Position = 0;
                    var text = ExtractPdfText(ms);
                    var inv = AnalyzeInvoiceText(text);
                    parts.Add($"Invoice: {f.FileName}\n{inv}");
                }
                else
                {
                    parts.Add($"Unsupported file type for analysis: {f.FileName}");
                }
            }
            return parts.Count > 0 ? string.Join("\n\n", parts) : string.Empty;
        }

        private static string ExtractPdfText(Stream pdfStream)
        {
            try
            {
                using var doc = PdfDocument.Open(pdfStream);
                var all = new System.Text.StringBuilder();
                foreach (Page page in doc.GetPages())
                {
                    all.AppendLine(page.Text);
                }
                return all.ToString();
            }
            catch (Exception ex)
            {
                return $"[Failed to read PDF: {ex.Message}]";
            }
        }

        private static string AnalyzeInvoiceText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "No readable text in PDF.";

            // Try to find likely total amounts near keywords
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var currencyRegex = new Regex(@"(?i)(?:r\s?)([0-9]{1,3}(?:[,\s][0-9]{3})*(?:\.[0-9]{2})?|[0-9]+\.[0-9]{2})");
            var totals = new List<(string line, decimal amount, int index)>();
            for (int i = 0; i < lines.Length; i++)
            {
                var l = lines[i];
                if (l.ToLowerInvariant().Contains("total") || l.ToLowerInvariant().Contains("amount due") || l.ToLowerInvariant().Contains("balance"))
                {
                    foreach (Match m in currencyRegex.Matches(l))
                    {
                        if (TryParseCurrency(m.Value, out var amt)) totals.Add((l.Trim(), amt, i));
                    }
                }
            }
            decimal best = 0;
            string where = string.Empty;
            if (totals.Count > 0)
            {
                var pick = totals.OrderByDescending(t => t.amount).First();
                best = pick.amount; where = pick.line;
            }
            else
            {
                // Fallback: take max currency in document
                decimal max = 0; string maxLine = "";
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (Match m in currencyRegex.Matches(lines[i]))
                    {
                        if (TryParseCurrency(m.Value, out var amt) && amt > max) { max = amt; maxLine = lines[i].Trim(); }
                    }
                }
                best = max; where = maxLine;
            }

            if (best > 0)
            {
                return $"Detected total: R {best:N2}. (From: '{where}')";
            }
            return "Could not confidently extract a total from the invoice. Try a clearer PDF.";
        }

        private static bool TryParseCurrency(string input, out decimal value)
        {
            input = input.Trim();
            input = Regex.Replace(input, @"(?i)^[r]\s?", "");
            input = input.Replace(" ", string.Empty);
            return decimal.TryParse(input, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)
                || decimal.TryParse(input, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, new CultureInfo("en-ZA"), out value);
        }

        private static string Fallback(string role, string message)
        {
            if (role == "manager")
            {
                return "I can analyze uploaded invoices (PDF) to extract totals, and answer questions like:\n- Who has the highest/lowest claims this month?\n- What is the total processed this year?\nTry uploading a PDF or ask: 'highest claims this month'.";
            }
            if (role == "coordinator")
            {
                return "I can help review activity and answer queries like:\n- Show rejected claims this month\n- Total claimed between 2025-01-01 and 2025-01-31\nYou can also upload an invoice PDF for analysis.";
            }
            return "I can assist with submitting claims and tracking status, and I can analyze invoice PDFs you upload. Try: 'total claimed this month' or upload a PDF.";
        }
    }
}
