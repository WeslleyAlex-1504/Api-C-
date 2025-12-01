using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;

namespace api.Hubs
{
    public class MyHub : Hub
    {
        public async IAsyncEnumerable<DateTime> Streaming(int count, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (var i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(1000, cancellationToken);
                yield return DateTime.Now;
            }
        }
    }
}