using MangaMesh.Peer.ClientApi.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MangaMesh.Peer.Tests.ClientApi.Services;

public class InMemoryLoggerProviderTests
{
    [Fact]
    public void CreateLogger_ReturnsInMemoryLogger()
    {
        var provider = new InMemoryLoggerProvider();
        var logger = provider.CreateLogger("TestCategory");
        Assert.IsType<InMemoryLogger>(logger);
    }

    [Fact]
    public void AddLog_SingleEntry_IsRetrievable()
    {
        var provider = new InMemoryLoggerProvider();
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = LogLevel.Information,
            Category = "Test",
            Message = "Hello"
        };

        provider.AddLog(entry);

        var logs = provider.GetLogs().ToList();
        Assert.Single(logs);
        Assert.Equal("Hello", logs[0].Message);
    }

    [Fact]
    public void AddLog_ExceedsMaxLogs_OldestRemoved()
    {
        var provider = new InMemoryLoggerProvider();

        // Add 1001 entries (max is 1000)
        for (int i = 0; i < 1001; i++)
        {
            provider.AddLog(new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = LogLevel.Debug,
                Category = "Test",
                Message = $"Message {i}"
            });
        }

        var logs = provider.GetLogs().ToList();
        Assert.True(logs.Count <= 1000);
    }

    [Fact]
    public void Clear_RemovesAllLogs()
    {
        var provider = new InMemoryLoggerProvider();
        provider.AddLog(new LogEntry { Message = "a", Category = "c", Level = LogLevel.Debug });
        provider.AddLog(new LogEntry { Message = "b", Category = "c", Level = LogLevel.Debug });

        provider.Clear();

        Assert.Empty(provider.GetLogs());
    }

    [Fact]
    public void GetLogs_EmptyProvider_ReturnsEmpty()
    {
        var provider = new InMemoryLoggerProvider();
        Assert.Empty(provider.GetLogs());
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var provider = new InMemoryLoggerProvider();
        var ex = Record.Exception(() => provider.Dispose());
        Assert.Null(ex);
    }
}

public class InMemoryLoggerTests
{
    private readonly InMemoryLoggerProvider _provider;
    private readonly InMemoryLogger _sut;

    public InMemoryLoggerTests()
    {
        _provider = new InMemoryLoggerProvider();
        _sut = new InMemoryLogger("TestCategory", _provider);
    }

    [Fact]
    public void IsEnabled_AnyLevel_ReturnsTrue()
    {
        Assert.True(_sut.IsEnabled(LogLevel.Trace));
        Assert.True(_sut.IsEnabled(LogLevel.Debug));
        Assert.True(_sut.IsEnabled(LogLevel.Information));
        Assert.True(_sut.IsEnabled(LogLevel.Warning));
        Assert.True(_sut.IsEnabled(LogLevel.Error));
        Assert.True(_sut.IsEnabled(LogLevel.Critical));
    }

    [Fact]
    public void BeginScope_ReturnsNull()
    {
        var scope = _sut.BeginScope("state");
        Assert.Null(scope);
    }

    [Fact]
    public void Log_Information_IsStoredInProvider()
    {
        _sut.Log(LogLevel.Information, new EventId(1), "test state", null,
            (state, _) => state.ToString()!);

        var logs = _provider.GetLogs().ToList();
        Assert.Single(logs);
        Assert.Equal(LogLevel.Information, logs[0].Level);
        Assert.Equal("test state", logs[0].Message);
        Assert.Equal("TestCategory", logs[0].Category);
    }

    [Fact]
    public void Log_WithException_StoresExceptionString()
    {
        var ex = new InvalidOperationException("oops");
        _sut.Log(LogLevel.Error, new EventId(0), "error msg", ex,
            (state, e) => $"{state}: {e?.Message}");

        var logs = _provider.GetLogs().ToList();
        Assert.Single(logs);
        Assert.NotNull(logs[0].Exception);
        Assert.Contains("oops", logs[0].Exception);
    }

    [Fact]
    public void Log_MultipleEntries_AllStoredInOrder()
    {
        _sut.Log(LogLevel.Debug, default, "first", null, (s, _) => s!);
        _sut.Log(LogLevel.Warning, default, "second", null, (s, _) => s!);
        _sut.Log(LogLevel.Error, default, "third", null, (s, _) => s!);

        var logs = _provider.GetLogs().ToList();
        Assert.Equal(3, logs.Count);
    }

    [Fact]
    public void Log_TimestampIsSetToUtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        _sut.Log(LogLevel.Information, default, "msg", null, (s, _) => s!);
        var after = DateTime.UtcNow.AddSeconds(1);

        var entry = _provider.GetLogs().Single();
        Assert.True(entry.Timestamp >= before && entry.Timestamp <= after);
    }
}
