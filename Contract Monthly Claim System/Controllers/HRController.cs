#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Contract_Monthly_Claim_System.Data;
using Contract_Monthly_Claim_System.Models;
using Contract_Monthly_Claim_System.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Contract_Monthly_Claim_System.Controllers
{
    [Authorize(Roles = "HR")]
    public class HRController : Controller
    {
        private readonly HRService _hrService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        static HRController()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public HRController(HRService hrService, UserManager<ApplicationUser> userManager, ApplicationDbContext db, IWebHostEnvironment env)
        {
            _hrService = hrService;
            _userManager = userManager;
            _db = db;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var vm = await _hrService.GetDashboardAsync();
            return View("~/Views/HR/Dashboard.cshtml", vm);
        }

        [HttpGet]
        public async Task<IActionResult> LecturerProfile(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest();
            var vm = await _hrService.GetLecturerProfileAsync(id);
            if (vm == null) return NotFound();
            return View("~/Views/HR/LecturerProfile.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLecturer(string id, string? newEmail, string? newPhoneNumber)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(newEmail) && !string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = newEmail;
                user.UserName = newEmail; // keep aligned with existing pattern
                user.NormalizedEmail = newEmail.ToUpperInvariant();
                user.NormalizedUserName = newEmail.ToUpperInvariant();
            }
            if (!string.IsNullOrWhiteSpace(newPhoneNumber))
            {
                user.PhoneNumber = newPhoneNumber;
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
            }
            else
            {
                TempData["Success"] = "Lecturer profile updated.";
            }

            return RedirectToAction(nameof(LecturerProfile), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> ApprovedClaimsCsv()
        {
            var rows = await _hrService.GetApprovedSummariesAsync();
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Approved Claims");

            ws.Cell("A1").Value = "Claim ID";
            ws.Cell("B1").Value = "Lecturer";
            ws.Cell("C1").Value = "Approved Date";
            ws.Cell("D1").Value = "Total Amount (R)";
            ws.Cell("E1").Value = "Status";
            ws.Range("A1:E1").Style
                .Font.SetBold()
                .Fill.SetBackgroundColor(XLColor.FromHtml("#f8f9fa"));

            var rowIndex = 2;
            foreach (var row in rows)
            {
                ws.Cell(rowIndex, 1).Value = $"CLM-{row.ClaimId:000}";
                ws.Cell(rowIndex, 2).Value = row.LecturerName;
                ws.Cell(rowIndex, 3).Value = row.ClaimDate.ToLocalTime();
                ws.Cell(rowIndex, 4).Value = row.TotalAmount;
                ws.Cell(rowIndex, 5).Value = row.Status;
                rowIndex++;
            }

            ws.Column(3).Style.DateFormat.Format = "yyyy-mm-dd HH:mm";
            ws.Column(4).Style.NumberFormat.Format = "\"R\" #,##0.00";
            ws.Columns(1, 5).AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileName = $"ApprovedClaims_{DateTime.UtcNow:yyyyMMddHHmm}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> ApprovedClaimsReport()
        {
            var rows = await _hrService.GetApprovedSummariesAsync();
            ViewData["GeneratedOn"] = DateTime.UtcNow;
            return View("~/Views/HR/Reports/ApprovedClaimsReport.cshtml", rows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int claimId)
        {
            var claim = await _db.Claims.FindAsync(claimId);
            if (claim == null) return NotFound();

            var isAjax = HttpContext?.Request?.Headers != null
                && HttpContext.Request.Headers.TryGetValue("X-Requested-With", out var requestedWith)
                && string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

            if (!string.Equals(claim.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                claim.Status = "Paid";
                await _db.SaveChangesAsync();
            }

            var message = $"Claim CLM-{claim.Id:000} marked as paid.";

            if (isAjax)
            {
                return Json(new
                {
                    success = true,
                    claimId = claim.Id,
                    message,
                    total = Math.Round((decimal)claim.Total, 2, MidpointRounding.AwayFromZero)
                });
            }

            TempData["Success"] = message;
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpGet]
        public async Task<IActionResult> ExportClaimPdf(int claimId)
        {
            var claim = await FindClaimWithLecturerAsync(claimId);
            if (claim == null) return NotFound();

            var vm = CreateInvoiceVm(claim);
            var pdfBytes = BuildInvoicePdf(vm);
            var fileName = $"{vm.InvoiceNumber}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> ExportClaimExcel(int claimId)
        {
            var claim = await FindClaimWithLecturerAsync(claimId);
            if (claim == null) return NotFound();

            var vm = CreateInvoiceVm(claim);
            var bytes = BuildInvoiceWorkbook(vm);
            var fileName = $"{vm.InvoiceNumber}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> InvoicePreview(int claimId)
        {
            var claim = await FindClaimWithLecturerAsync(claimId);
            if (claim == null) return NotFound();

            var vm = CreateInvoiceVm(claim);
            return View("~/Views/HR/InvoicePreview.cshtml", vm);
        }

        [HttpGet]
        public async Task<IActionResult> PayslipPreview(int claimId)
        {
            var claim = await FindClaimWithLecturerAsync(claimId);
            if (claim == null) return NotFound();

            var vm = CreatePayslipVm(claim);
            return View("~/Views/HR/PayslipPreview.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendPayslip(int claimId)
        {
            var claim = await FindClaimWithLecturerAsync(claimId);

            if (claim == null) return NotFound();

            var directory = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "Payslips");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var payload = BuildPayslipPayload(claim);
            var filePath = Path.Combine(directory, $"Payslip_{claim.Id:000}_{DateTime.UtcNow:yyyyMMddHHmmss}.txt");
            await System.IO.File.WriteAllTextAsync(filePath, payload);

            TempData["Success"] = $"Payslip saved and ready to email for {claim.LecturerName}.";
            return RedirectToAction(nameof(Dashboard));
        }

        private static string BuildPayslipPayload(Claim claim)
        {
            var culture = new CultureInfo("en-ZA");
            var sb = new StringBuilder();
            sb.AppendLine($"To: {claim.LecturerUser?.Email ?? "unknown"}");
            sb.AppendLine($"Subject: Payslip for claim CLM-{claim.Id:000}");
            sb.AppendLine();
            sb.AppendLine($"Dear {claim.LecturerName},");
            sb.AppendLine($"Amount due: {claim.Total.ToString("C2", culture)}");
            sb.AppendLine($"Hours: {claim.HoursWorked}");
            sb.AppendLine($"Hourly Rate: {claim.HourlyRate.ToString("C2", culture)}");
            sb.AppendLine($"Status: {claim.Status}");
            sb.AppendLine();
            sb.AppendLine("Regards,");
            sb.AppendLine("HR Department");
            return sb.ToString();
        }

        private async Task<Claim?> FindClaimWithLecturerAsync(int claimId)
        {
            return await _db.Claims
                .Include(c => c.LecturerUser)
                .Include(c => c.Documents)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == claimId);
        }

        private static ClaimInvoiceVM CreateInvoiceVm(Claim claim) => new()
        {
            Claim = claim,
            GeneratedOn = DateTime.UtcNow
        };

        private static PayslipPreviewVM CreatePayslipVm(Claim claim) => new()
        {
            Claim = claim,
            GeneratedOn = DateTime.UtcNow
        };

        private static byte[] BuildInvoicePdf(ClaimInvoiceVM vm)
        {
            var document = new ClaimInvoiceDocument(vm);
            return document.GeneratePdf();
        }

        private static byte[] BuildInvoiceWorkbook(ClaimInvoiceVM vm)
        {
            var culture = new CultureInfo("en-ZA");
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Invoice");

            ws.Cell("A1").Value = vm.CompanyName;
            ws.Range("A1:D1").Merge().Style
                .Font.SetBold()
                .Font.SetFontSize(18)
                .Font.SetFontColor(XLColor.FromHtml("#4a148c"));

            ws.Cell("A2").Value = vm.CompanyAddress;
            ws.Range("A2:D2").Merge();

            ws.Cell("A3").Value = $"{vm.CompanyEmail} | {vm.CompanyPhone}";
            ws.Range("A3:D3").Merge().Style.Font.SetFontColor(XLColor.FromHtml("#5f6368"));

            ws.Cell("A5").Value = "Invoice #";
            ws.Cell("B5").Value = vm.InvoiceNumber;
            ws.Cell("C5").Value = "Generated";
            ws.Cell("D5").Value = vm.GeneratedOn.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

            ws.Cell("A6").Value = "Lecturer";
            ws.Cell("B6").Value = vm.Claim.LecturerName;
            ws.Cell("A7").Value = "Email";
            ws.Cell("B7").Value = vm.Claim.LecturerUser?.Email ?? "-";
            ws.Cell("A8").Value = "Status";
            ws.Cell("B8").Value = vm.Claim.Status;

            var tableStart = 10;
            ws.Cell(tableStart, 1).Value = "Description";
            ws.Cell(tableStart, 2).Value = "Hours";
            ws.Cell(tableStart, 3).Value = "Rate";
            ws.Cell(tableStart, 4).Value = "Amount";
            ws.Range(tableStart, 1, tableStart, 4).Style
                .Fill.SetBackgroundColor(XLColor.FromHtml("#ede7f6"))
                .Font.SetBold();

            var row = tableStart + 1;
            var title = string.IsNullOrWhiteSpace(vm.Claim.Title) ? "Consulting Hours" : vm.Claim.Title;
            ws.Cell(row, 1).Value = title;
            ws.Cell(row, 2).Value = vm.Claim.HoursWorked;
            ws.Cell(row, 2).Style.NumberFormat.SetFormat("0.00");
            ws.Cell(row, 3).Value = vm.Claim.HourlyRate;
            ws.Cell(row, 3).Style.NumberFormat.SetFormat("\"R\" #,##0.00");
            ws.Cell(row, 4).Value = vm.Claim.Total;
            ws.Cell(row, 4).Style.NumberFormat.SetFormat("\"R\" #,##0.00");

            var notesRow = row + 2;
            ws.Cell(notesRow, 1).Value = "Notes";
            ws.Cell(notesRow, 1).Style.Font.SetBold();
            ws.Range(notesRow, 2, notesRow, 4).Merge().Value = string.IsNullOrWhiteSpace(vm.Claim.Notes) ? "-" : vm.Claim.Notes;
            ws.Range(notesRow, 1, notesRow, 4).Style.Border.SetBottomBorder(XLBorderStyleValues.Thin);

            var totalRow = notesRow + 2;
            ws.Cell(totalRow, 3).Value = "Total Due";
            ws.Cell(totalRow, 3).Style.Font.SetBold();
            ws.Cell(totalRow, 4).Value = vm.Claim.Total;
            ws.Cell(totalRow, 4).Style
                .NumberFormat.SetFormat("\"R\" #,##0.00")
                .Font.SetBold()
                .Font.SetFontSize(14);

            ws.Columns(1, 4).AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private sealed class ClaimInvoiceDocument : IDocument
        {
            private readonly ClaimInvoiceVM _vm;
            private readonly CultureInfo _culture = new("en-ZA");

            public ClaimInvoiceDocument(ClaimInvoiceVM vm) => _vm = vm;

            public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

            public DocumentSettings GetSettings() => DocumentSettings.Default;

            public void Compose(IDocumentContainer container)
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Content().Column(stack =>
                    {
                        stack.Item().Row(row =>
                        {
                            row.RelativeItem().Column(left =>
                            {
                                left.Item().Text(_vm.CompanyName).FontSize(20).SemiBold().FontColor(Colors.DeepPurple.Medium);
                                left.Item().Text(_vm.CompanyAddress);
                                left.Item().Text($"{_vm.CompanyEmail} | {_vm.CompanyPhone}");
                            });

                            row.ConstantItem(200).Column(right =>
                            {
                                right.Item().Text("Invoice").FontSize(18).SemiBold();
                                right.Item().Text($"#{_vm.InvoiceNumber}").FontColor(Colors.Grey.Darken1);
                                right.Item().Text($"Issued: {_vm.GeneratedOn.ToLocalTime():yyyy-MM-dd HH:mm}");
                                right.Item().Text($"Status: {_vm.Claim.Status}");
                            });
                        });

                        stack.Item().PaddingTop(20).BorderBottom(1).BorderColor(Colors.Grey.Lighten3);

                        stack.Item().PaddingTop(12).Text("Bill To").FontSize(13).SemiBold();
                        stack.Item().Text(_vm.Claim.LecturerName);
                        stack.Item().Text(_vm.Claim.LecturerUser?.Email ?? "-");

                        stack.Item().PaddingTop(20).Element(ComposeDetailsTable);

                        stack.Item().PaddingTop(20).AlignRight().Text($"Total Due: {_vm.Claim.Total.ToString("C2", _culture)}")
                            .FontSize(16).SemiBold();
                        if (_vm.Claim.DateOfExpense.HasValue)
                        {
                            stack.Item().AlignRight().Text($"Expense Date: {_vm.Claim.DateOfExpense:yyyy-MM-dd}");
                        }
                    });

                    page.Footer()
                        .AlignCenter()
                        .DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken1))
                        .Text(text =>
                        {
                            text.Span("Generated by Contract Monthly Claim System • ");
                            text.Span(_vm.GeneratedOn.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
                        });
                });
            }

            private void ComposeDetailsTable(IContainer container)
            {
                var description = string.IsNullOrWhiteSpace(_vm.Claim.Title) ? "Consulting Hours" : _vm.Claim.Title;
                container.Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("Description").SemiBold();
                        header.Cell().Element(CellStyle).Text("Hours").SemiBold();
                        header.Cell().Element(CellStyle).Text("Rate").SemiBold();
                        header.Cell().Element(CellStyle).Text("Amount").SemiBold();
                    });

                    table.Cell().Element(CellStyle).Text(description);
                    table.Cell().Element(CellStyle).Text(_vm.Claim.HoursWorked.ToString("0.00"));
                    table.Cell().Element(CellStyle).Text(_vm.Claim.HourlyRate.ToString("C2", _culture));
                    table.Cell().Element(CellStyle).Text(_vm.Claim.Total.ToString("C2", _culture));

                    table.Cell().ColumnSpan(3).Element(CellStyle).Text("Notes");
                    table.Cell().Element(CellStyle).Text(string.IsNullOrWhiteSpace(_vm.Claim.Notes) ? "-" : _vm.Claim.Notes);
                });
            }

            private static IContainer CellStyle(IContainer container)
            {
                return container
                    .BorderBottom(1)
                    .BorderColor(Colors.Grey.Lighten3)
                    .PaddingVertical(8)
                    .PaddingHorizontal(6);
            }
        }
    }
}
