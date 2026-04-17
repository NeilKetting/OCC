using Moq;
using Xunit;
using OCC.WpfClient.Features.AuthHub.ViewModels;
using System.Threading.Tasks;

namespace OCC.Tests.ViewModels
{
    public class LoginViewModelTests
    {
        [Fact]
        public async Task LoginAsync_IsVirtual_AndCanBeMocked()
        {
            // Arrange
            var mockVm = new Mock<LoginViewModel>();
            
            mockVm.Setup(vm => vm.LoginAsync()).Returns(Task.CompletedTask).Verifiable();

            // Act
            await mockVm.Object.LoginAsync();

            // Assert
            mockVm.Verify(vm => vm.LoginAsync(), Times.Once);
        }
    }
}
