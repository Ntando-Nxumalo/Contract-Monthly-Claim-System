using Contract_Monthly_Claim_System.Controllers;
using Contract_Monthly_Claim_System.Data;
using Contract_Monthly_Claim_System.Hubs;
using Contract_Monthly_Claim_System.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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

            var claim = new Claim { HoursWorked = 5, HourlyRate = 100, Status = "Pending", Total = 500, LecturerName = "X" };
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

            var claim = new Claim { HoursWorked = 5, HourlyRate = 100, Status = "Pending", Total = 500, LecturerName = "X" };
            db.Claims.Add(claim);
            await db.SaveChangesAsync();

            var result = await controller.Reject(claim.Id);

            var updated = await db.Claims.FindAsync(claim.Id);
            Assert.NotNull(updated);
            Assert.Equal("Rejected", updated!.Status);
        }
    }
}