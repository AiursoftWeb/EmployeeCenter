using Aiursoft.EmployeeCenter.InMemory;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class AudioAsrConcurrencyTests
{
    [TestMethod]
    public async Task ProcessingTokenRejectsStaleAsrWrites()
    {
        var options = new DbContextOptionsBuilder<InMemoryContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using (var seedDb = new InMemoryContext(options))
        {
            seedDb.Audios.Add(new Audio
            {
                Name = "Concurrency Test",
                FilePath = "audio/concurrency-test.mp3",
                AsrProcessingToken = Guid.NewGuid().ToString("N")
            });
            await seedDb.SaveChangesAsync();
        }

        await using var staleDb = new InMemoryContext(options);
        await using var currentDb = new InMemoryContext(options);
        var staleAudio = await staleDb.Audios.SingleAsync();
        var currentAudio = await currentDb.Audios.SingleAsync();

        currentAudio.AsrProcessingToken = Guid.NewGuid().ToString("N");
        await currentDb.SaveChangesAsync();

        staleDb.Entry(staleAudio).Property(audio => audio.AsrProcessingToken).IsModified = true;
        await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(
            async () => await staleDb.SaveChangesAsync());
    }
}
