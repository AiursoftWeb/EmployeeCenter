using Aiursoft.EmployeeCenter.InMemory;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public class AudioAsrConcurrencyTests
{
    [TestMethod]
    public async Task ManuallySavedMinutesRejectStaleGenerationWrites()
    {
        var options = new DbContextOptionsBuilder<InMemoryContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var currentDb = new InMemoryContext(options);
        var audio = new Audio { Name = "Manual minutes", FilePath = "audio/manual.mp3" };
        currentDb.Audios.Add(audio);
        await currentDb.SaveChangesAsync();
        var currentResult = new AudioAsrResult { AudioId = audio.Id, PlainText = "Transcript" };
        currentDb.AudioAsrResults.Add(currentResult);
        await currentDb.SaveChangesAsync();

        await using var staleDb = new InMemoryContext(options);
        var staleResult = await staleDb.AudioAsrResults.SingleAsync();
        currentResult.MeetingMinutesMarkdown = "Manually imported minutes";
        await currentDb.SaveChangesAsync();
        staleResult.MeetingMinutesMarkdown = "Generated before manual save";
        await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(
            async () => await staleDb.SaveChangesAsync());
        await currentDb.Entry(currentResult).ReloadAsync();
        Assert.AreEqual("Manually imported minutes", currentResult.MeetingMinutesMarkdown);
    }

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

        Assert.IsFalse(await staleDb.Audios
            .AsNoTracking()
            .AnyAsync(audio =>
                audio.Id == staleAudio.Id &&
                audio.AsrProcessingToken == staleAudio.AsrProcessingToken));

        staleDb.Entry(staleAudio).Property(audio => audio.AsrProcessingToken).IsModified = true;
        await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(
            async () => await staleDb.SaveChangesAsync());
    }
}
