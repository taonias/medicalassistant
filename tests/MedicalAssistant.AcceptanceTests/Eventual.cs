namespace MedicalAssistant.AcceptanceTests;

internal static class Eventual
{
    public static Task<T> UntilNotNullAsync<T>(
        Func<CancellationToken, Task<T?>> observe,
        TimeSpan timeout,
        CancellationToken cancellationToken) where T : class =>
        UntilAsync(observe, timeout, cancellationToken);

    public static async Task<T> UntilAsync<T>(
        Func<CancellationToken, Task<T?>> observe,
        TimeSpan timeout,
        CancellationToken cancellationToken) where T : class
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            while (true)
            {
                if (await observe(deadline.Token) is { } result)
                    return result;
                await Task.Delay(TimeSpan.FromMilliseconds(100), deadline.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The service outcome was not visible within {timeout}.");
        }
    }
}
