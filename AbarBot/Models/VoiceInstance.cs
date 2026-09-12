using NetCord.Gateway.Voice;

namespace NetCord.Abar.Bot.Models;


public enum VoiceJobType
{
    Playing = 0,
    Recording = 1,
}


public sealed class VoiceInstance(VoiceClient client) : IDisposable
{
    private static readonly int JobTypeCount = Enum.GetValues<VoiceJobType>().Length;
    private readonly byte[] _jobStatuses = new byte[JobTypeCount];
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    public VoiceClient Client => client;


    // Represents a job that is currently being executed by the voice instance.
    public readonly record struct Job(
        VoiceInstance Instance,
        VoiceJobType JobType,
        CancellationToken CancellationToken) : IDisposable
    {
        public void Dispose()
        {
            Interlocked.Exchange(ref Instance._jobStatuses[(int)JobType], 0);
        }
    }


    // Tries to enter a job of the specified type. If the job is already being executed, it returns null.
    public Job? TryEnterJob(VoiceJobType type)
    {
        return Interlocked.CompareExchange(ref _jobStatuses[(int)type], 1, 0) is 0
            ? new(this, type, _cancellationTokenSource.Token)
            : null;
    }


    public void Dispose()
    {
        var tokenSource = _cancellationTokenSource;
        tokenSource.Cancel();
        tokenSource.Dispose();
        client.Dispose();
    }
}
