using System.Threading;
using System.Windows;
using VideoGenerator.Views;
using Xunit;

namespace VideoGenerator.Tests.Views;

public sealed class ModernMessageBoxTests
{
    [Fact]
    public void ModernMessageBoxWindowInstantiatesCorrectlyOnSTA()
    {
        var thread = new Thread(() =>
        {
            var window = new ModernMessageBoxWindow(
                message: "Test Message",
                caption: "Test Caption",
                buttons: MessageBoxButton.YesNoCancel,
                icon: MessageBoxImage.Question,
                primaryButtonText: "VALIDATE ALL",
                secondaryButtonText: "KEEP UNVALIDATED",
                tertiaryButtonText: "REVIEW PENDING");

            Assert.Equal(MessageBoxResult.None, window.Result);
            Assert.Equal("TEST CAPTION", window.HeaderTitle);
            Assert.Equal("Test Caption", window.Caption);
            Assert.Equal("Test Message", window.Message);
            Assert.Equal(3, window.ButtonCount);
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
