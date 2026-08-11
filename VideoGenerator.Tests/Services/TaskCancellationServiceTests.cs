using System.Threading;
using Xunit;

using VideoGenerator.Services;

namespace VideoGenerator.Tests;

public sealed class TaskCancellationServiceTests
{
    [Fact]
    public void CreatingANewTokenCancelsThePreviousOperation()
    {
        var service = new TaskCancellationService();

        CancellationToken previousToken = service.CreateNewToken();
        CancellationToken currentToken = service.CreateNewToken();

        Assert.True(previousToken.IsCancellationRequested);
        Assert.False(currentToken.IsCancellationRequested);
        Assert.False(service.IsCancellationRequested);
    }

    [Fact]
    public void CancelCancelsTheCurrentOperation()
    {
        var service = new TaskCancellationService();
        service.CreateNewToken();

        service.Cancel();

        Assert.True(service.IsCancellationRequested);
    }
}
