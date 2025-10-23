using Contract_Monthly_Claim_System.Controllers;
using Contract_Monthly_Claim_System.Data;
using Contract_Monthly_Claim_System.Hubs;
using Contract_Monthly_Claim_System.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Contract_Monthly_Claim_System.Tests
{
    public class ClaimsControllerTests
    {
        private static ApplicationDbContext GetDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task Submit_ComputesTotalPrecisely_RoundsAwayFromZero()
        {
            var db = GetDbContext(nameof(Submit_ComputesTotalPrecisely_RoundsAwayFromZero));
            var userManagerMock = GetUserManagerMock();
            var hubMock = GetHubContextMock();
            var envMock = GetEnvMock();

            var controller = new ClaimsController(db, envMock.Object, hubMock.Object, userManagerMock.Object);

            var testUser = new ApplicationUser { UserName = "lect@example.com", Email = "lect@example.com", FullName = "Lecturer", Id = "lect-1" };
            userManagerMock.Setup(um => um.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            // Rate 2.005 should round to 2.01 when multiplied by 1.00 and rounded to 2dp away from zero
            var model = new ClaimsController.ClaimSubmitModel
            {
                HoursWorked = 1.0,
                HourlyRate = 2.005,
                Notes = "rounding"
            };

            var result = await controller.Submit(model);

            var saved = await db.Claims.FirstOrDefaultAsync();
            Assert.NotNull(saved);
            Assert.Equal(2.01, saved!.Total, 3);
        }

        [Fact]
        public async Task Submit_RateZero_RedirectsWithError()
        {
            var db = GetDbContext(nameof(Submit_RateZero_RedirectsWithError));
            var userManagerMock = GetUserManagerMock();
            var hubMock = GetHubContextMock();
            var envMock = GetEnvMock();

            var controller = new ClaimsController(db, envMock.Object, hubMock.Object, userManagerMock.Object);

            var testUser = new ApplicationUser { UserName = "lect@example.com", Email = "lect@example.com", FullName = "Lecturer", Id = "lect-1" };
            userManagerMock.Setup(um => um.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var model = new ClaimsController.ClaimSubmitModel
            {
                HoursWorked = 2,
                HourlyRate = 0,
                Notes = "invalid"
            };

            var result = await controller.Submit(model);
            Assert.IsType<RedirectToActionResult>(result);
        }

        [Fact]
        public async Task Submit_MultipleDocuments_SavesAllowedAndSkipsUnsupported()
        {
            var db = GetDbContext(nameof(Submit_MultipleDocuments_SavesAllowedAndSkipsUnsupported));
            var userManagerMock = GetUserManagerMock();
            var hubMock = GetHubContextMock();
            var envMock = GetEnvMock();

            var controller = new ClaimsController(db, envMock.Object, hubMock.Object, userManagerMock.Object);

            var testUser = new ApplicationUser { UserName = "lect@example.com", Email = "lect@example.com", FullName = "Lecturer", Id = "lect-1" };
            userManagerMock.Setup(um => um.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var pdf = CreateFormFile("a.pdf", "pdf");
            var docx = CreateFormFile("b.docx", "docx");
            var exe = CreateFormFile("c.exe", "exe");

            var model = new ClaimsController.ClaimSubmitModel
            {
                HoursWorked = 1,
                HourlyRate = 100,
                Documents = new System.Collections.Generic.List<IFormFile> { pdf, docx, exe }
            };

            var result = await controller.Submit(model);

            var saved = await db.Claims.Include(c => c.Documents).FirstOrDefaultAsync();
            Assert.NotNull(saved);
            Assert.Equal(2, saved!.Documents.Count); // pdf + docx
            Assert.All(saved.Documents, d => Assert.True(d.FilePath.EndsWith(".pdf") || d.FilePath.EndsWith(".docx")));
        }

        [Fact]
        public async Task DownloadDocument_ForbidForDifferentLecturer()
        {
            var db = GetDbContext(nameof(DownloadDocument_ForbidForDifferentLecturer));
            var userManagerMock = GetUserManagerMock();
            var hubMock = GetHubContextMock();
            var envMock = GetEnvMock();

            var controller = new ClaimsController(db, envMock.Object, hubMock.Object, userManagerMock.Object);

            var claim = new Claim { LecturerUserId = "lect-1", LecturerName = "L", HoursWorked = 1, HourlyRate = 100, Total = 100, Status = "Pending" };
            db.Claims.Add(claim);
            await db.SaveChangesAsync();

            var root = envMock.Object.WebRootPath;
            var docs = System.IO.Path.Combine(root, "Documents");
            if (!Directory.Exists(docs)) Directory.CreateDirectory(docs);
            var fileName = Guid.NewGuid().ToString() + ".pdf";
            var physical = System.IO.Path.Combine(docs, fileName);
            await System.IO.File.WriteAllTextAsync(physical, "data");

            var doc = new ClaimDocument { ClaimId = claim.Id, FileName = "file.pdf", FilePath = "/Documents/" + fileName };
            db.ClaimDocuments.Add(doc);
            await db.SaveChangesAsync();

            var otherLecturer = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "lect-2"),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Lecturer")
            }, IdentityConstants.ApplicationScheme));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = otherLecturer }
            };

            var result = await controller.DownloadDocument(doc.Id);
            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task Row_Returns_PartialView_WithClaim()
        {
            var db = GetDbContext(nameof(Row_Returns_PartialView_WithClaim));
            var userManagerMock = GetUserManagerMock();
            var hubMock = GetHubContextMock();
            var envMock = GetEnvMock();

            var controller = new ClaimsController(db, envMock.Object, hubMock.Object, userManagerMock.Object);

            var claim = new Claim { LecturerUserId = "lect-1", LecturerName = "L", HoursWorked = 1, HourlyRate = 100, Total = 100, Status = "Pending" };
            db.Claims.Add(claim);
            await db.SaveChangesAsync();

            var result = await controller.Row(claim.Id) as PartialViewResult;
            Assert.NotNull(result);
            Assert.Contains("_ClaimRow.cshtml", result!.ViewName);
            Assert.IsType<Claim>(result.Model);
        }

        private static Mock<UserManager<ApplicationUser>> GetUserManagerMock()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(store.Object,
                null, null, null, null, null, null, null, null);
        }

        private static Mock<IHubContext<ClaimHub>> GetHubContextMock()
        {
            var mockClients = new Mock<IHubClients>();
            var mockClientProxy = new Mock<IClientProxy>();

            // Ensure Group(...) returns the mock client proxy
            mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);

            var hubMock = new Mock<IHubContext<ClaimHub>>();
            hubMock.Setup(h => h.Clients).Returns(mockClients.Object);
            return hubMock;
        }

        private static Mock<IWebHostEnvironment> GetEnvMock()
        {
            var envMock = new Mock<IWebHostEnvironment>();
            envMock.Setup(e => e.WebRootPath).Returns(Directory.GetCurrentDirectory());
            return envMock;
        }

        [Fact]
        public async Task Create_ValidClaim_SavesAndRedirect()
        {
            // Arrange
            var db = GetDbContext(nameof(Create_ValidClaim_SavesAndRedirect));
            var userManagerMock = GetUserManagerMock();
            var hubMock = GetHubContextMock();
            var envMock = GetEnvMock();

            // Create controller - correct ctor order
            var controller = new ClaimsController(db, envMock.Object, hubMock.Object, userManagerMock.Object);

            // Setup user
            var testUser = new ApplicationUser { UserName = "test@example.com", Email = "test@example.com", FullName = "Test User", Id = "test-user-id" };
            userManagerMock.Setup(um => um.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            // Mock file
            var content = "Fake PDF content";
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.OpenReadStream()).Returns(ms);
            fileMock.Setup(f => f.FileName).Returns("test.pdf");
            fileMock.Setup(f => f.Length).Returns(ms.Length);
            fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), default)).Returns((Stream s, System.Threading.CancellationToken t) =>
            {
                ms.Position = 0;
                ms.CopyTo(s);
                return Task.CompletedTask;
            });

            // Prepare submit model matching ClaimsController.ClaimSubmitModel
            var submitModel = new
            {
                HoursWorked = 5.0,
                HourlyRate = 100.0,
                Notes = "Test claim",
                Document = fileMock.Object as IFormFile
            };

            // Because ClaimSubmitModel is a nested class, construct via dynamic-like mapping:
            var model = new ClaimsController.ClaimSubmitModel
            {
                HoursWorked = submitModel.HoursWorked,
                HourlyRate = submitModel.HourlyRate,
                Notes = submitModel.Notes,
                Document = submitModel.Document
            };

            // Act
            var result = await controller.Submit(model);

            // Assert - Db saved
            var saved = await db.Claims.FirstOrDefaultAsync();
            Assert.NotNull(saved);
            Assert.Equal(5, saved!.HoursWorked);
            Assert.Equal(100, saved.HourlyRate);
            Assert.Equal(500, saved.Total);
            Assert.Equal("Pending", saved.Status);
        }

        [Fact]
        public async Task Approve_SetsStatusToApproved()
        {
            var db = GetDbContext(nameof(Approve_SetsStatusToApproved));
            var userManagerMock = GetUserManagerMock();
            var hubMock = GetHubContextMock();
            var envMock = GetEnvMock();

            var controller = new ClaimsController(db, envMock.Object, hubMock.Object, userManagerMock.Object);

            var claim = new Claim { LecturerUserId = "lect-1", HoursWorked = 5, HourlyRate = 100, Status = "Pending", Total = 500, LecturerName = "X" };
            db.Claims.Add(claim);
            await db.SaveChangesAsync();

            var result = await controller.Approve(claim.Id);

            var updated = await db.Claims.FindAsync(claim.Id);
            Assert.NotNull(updated);
            Assert.Equal("Approved", updated!.Status);
        }

        [Fact]
        public async Task Reject_SetsStatusToRejected()
        {
            var db = GetDbContext(nameof(Reject_SetsStatusToRejected));
            var userManagerMock = GetUserManagerMock();
            var hubMock = GetHubContextMock();
            var envMock = GetEnvMock();

            var controller = new ClaimsController(db, envMock.Object, hubMock.Object, userManagerMock.Object);

            var claim = new Claim { LecturerUserId = "lect-1", HoursWorked = 5, HourlyRate = 100, Status = "Pending", Total = 500, LecturerName = "X" };
            db.Claims.Add(claim);
            await db.SaveChangesAsync();

            var result = await controller.Reject(claim.Id);

            var updated = await db.Claims.FindAsync(claim.Id);
            Assert.NotNull(updated);
            Assert.Equal("Rejected", updated!.Status);
        }

        [Fact]
        public async Task Submit_InvalidModel_RedirectsWithError()
        {
            var db = GetDbContext(nameof(Submit_InvalidModel_RedirectsWithError));
            var userManagerMock = GetUserManagerMock();
            var hubMock = GetHubContextMock();
            var envMock = GetEnvMock();

            var controller = new ClaimsController(db, envMock.Object, hubMock.Object, userManagerMock.Object);

            var testUser = new ApplicationUser { UserName = "lect@example.com", Email = "lect@example.com", FullName = "Lecturer", Id = "lect-1" };
            userManagerMock.Setup(um => um.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var model = new ClaimsController.ClaimSubmitModel
            {
                HoursWorked = 0,
                HourlyRate = 100,
                Notes = "x"
            };

            var result = await controller.Submit(model);

            Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(result);
        }

        [Fact]
        public async Task Submit_DocumentFiltering_SavesOnlyAllowed()
        {
            var db = GetDbContext(nameof(Submit_DocumentFiltering_SavesOnlyAllowed));
            var userManagerMock = GetUserManagerMock();
            var hubMock = GetHubContextMock();
            var envMock = GetEnvMock();

            var controller = new ClaimsController(db, envMock.Object, hubMock.Object, userManagerMock.Object);

            var testUser = new ApplicationUser { UserName = "lect@example.com", Email = "lect@example.com", FullName = "Lecturer", Id = "lect-1" };
            userManagerMock.Setup(um => um.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            var goodPdf = CreateFormFile("allowed.pdf", "pdf-data");
            var badExe = CreateFormFile("notallowed.exe", "exe-data");

            var model = new ClaimsController.ClaimSubmitModel
            {
                HoursWorked = 2,
                HourlyRate = 100,
                Notes = "docs",
                Documents = new System.Collections.Generic.List<IFormFile> { goodPdf, badExe }
            };

            var result = await controller.Submit(model);

            var saved = await db.Claims.Include(c => c.Documents).FirstOrDefaultAsync();
            Assert.NotNull(saved);
            Assert.Single(saved!.Documents);
            Assert.EndsWith(".pdf", saved.Documents.First().FilePath);
        }

        [Fact]
        public async Task DownloadDocument_AllowsOwnerAndCoordinator()
        {
            var db = GetDbContext(nameof(DownloadDocument_AllowsOwnerAndCoordinator));
            var userManagerMock = GetUserManagerMock();
            var hubMock = GetHubContextMock();
            var envMock = GetEnvMock();

            var controller = new ClaimsController(db, envMock.Object, hubMock.Object, userManagerMock.Object);

            var claim = new Claim { LecturerUserId = "lect-1", LecturerName = "L", HoursWorked = 1, HourlyRate = 100, Total = 100, Status = "Pending" };
            db.Claims.Add(claim);
            await db.SaveChangesAsync();

            var root = envMock.Object.WebRootPath;
            var docs = System.IO.Path.Combine(root, "Documents");
            if (!Directory.Exists(docs)) Directory.CreateDirectory(docs);
            var fileName = Guid.NewGuid().ToString() + ".pdf";
            var physical = System.IO.Path.Combine(docs, fileName);
            await System.IO.File.WriteAllTextAsync(physical, "data");

            var doc = new ClaimDocument { ClaimId = claim.Id, FileName = "file.pdf", FilePath = "/Documents/" + fileName };
            db.ClaimDocuments.Add(doc);
            await db.SaveChangesAsync();

            var ownerUser = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "lect-1"),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Lecturer")
            }, IdentityConstants.ApplicationScheme));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = ownerUser }
            };

            var fileResult = await controller.DownloadDocument(doc.Id);
            Assert.IsType<FileContentResult>(fileResult);

            var coordUser = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "coord-1"),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Program Coordinator")
            }, IdentityConstants.ApplicationScheme));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = coordUser }
            };

            var fileResult2 = await controller.DownloadDocument(doc.Id);
            Assert.IsType<FileContentResult>(fileResult2);
        }

        private static IFormFile CreateFormFile(string name, string content)
        {
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.OpenReadStream()).Returns(ms);
            fileMock.Setup(f => f.FileName).Returns(name);
            fileMock.Setup(f => f.Length).Returns(ms.Length);
            fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), default)).Returns((Stream s, System.Threading.CancellationToken t) =>
            {
                ms.Position = 0;
                ms.CopyTo(s);
                return Task.CompletedTask;
            });
            return fileMock.Object;
        }
    }
}