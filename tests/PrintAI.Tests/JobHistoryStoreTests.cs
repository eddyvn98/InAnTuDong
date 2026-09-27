using PrintAI.History;
using Xunit;

namespace PrintAI.Tests;

public sealed class JobHistoryStoreTests
{
    [Fact]
    public void Append_KeepsNewestFirstAndHonorsLimit()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.json");

        try
        {
            var store = new JobHistoryStore(path, maxEntries: 2);

            store.Append(new(
                DateTimeOffset.Parse("2026-09-27T10:00:00+07:00"),
                "plan",
                "PreviewRequired"));

            store.Append(new(
                DateTimeOffset.Parse("2026-09-27T10:01:00+07:00"),
                "print",
                "Submitted"));

            store.Append(new(
                DateTimeOffset.Parse("2026-09-27T10:02:00+07:00"),
                "print",
                "Completed"));

            var items = store.Read();

            Assert.Equal(2, items.Count);
            Assert.Equal("Completed", items[0].Status);
            Assert.Equal("Submitted", items[1].Status);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public void Read_CorruptHistoryFailsClosedToEmpty()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(path, "{not-json");
            var store = new JobHistoryStore(path);

            Assert.Empty(store.Read());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
