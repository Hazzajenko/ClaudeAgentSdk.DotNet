using ClaudeAgentSdk.Hooks;

namespace ClaudeAgentSdk.Tests.Hooks;

public class HookMatcherTests
{
    [Test]
    public async Task Create_AsyncCallback_HooksListHasOneEntry()
    {
        HookCallback callback = (_, _) => Task.FromResult(new HookOutput { Decision = "allow" });

        var matcher = HookMatcher.Create("Bash", callback);

        await Assert.That(matcher.Hooks.Count).IsEqualTo(1);

        var result = await matcher.Hooks[0](null!, CancellationToken.None);
        await Assert.That(result.Decision).IsEqualTo("allow");
    }

    [Test]
    public async Task Create_SyncCallback_WrapsCorrectly()
    {
        var matcher = HookMatcher.Create("Bash", _ => new HookOutput { Decision = "deny" });

        await Assert.That(matcher.Hooks.Count).IsEqualTo(1);

        var result = await matcher.Hooks[0](null!, CancellationToken.None);
        await Assert.That(result.Decision).IsEqualTo("deny");
    }

    [Test]
    public async Task Create_MatcherStringSet()
    {
        var matcher = HookMatcher.Create("Read", (_, _) => Task.FromResult(HookOutput.Empty));

        await Assert.That(matcher.Matcher).IsEqualTo("Read");
    }

    [Test]
    public async Task Create_NullMatcher_Works()
    {
        var matcher = HookMatcher.Create(null, _ => HookOutput.Empty);

        await Assert.That(matcher.Matcher).IsNull();
    }

    [Test]
    public async Task Create_WithTimeout_TimeoutSet()
    {
        var matcher = HookMatcher.Create("Bash", _ => HookOutput.Empty, timeoutSeconds: 5.0);

        await Assert.That(matcher.TimeoutSeconds).IsEqualTo(5.0);
    }
}
