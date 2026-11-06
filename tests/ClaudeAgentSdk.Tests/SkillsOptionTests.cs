using System.Text.Json;
using ClaudeAgentSdk.Tests.Transport;
using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk.Tests;

public class SkillsOptionTests
{
    private const string ResultLine =
        """{"type":"result","subtype":"success","duration_ms":0,"duration_api_ms":0,"is_error":false,"num_turns":0,"session_id":"s1"}""";

    private static string AllowedToolsValue(List<string> args)
    {
        int idx = args.IndexOf("--allowedTools");
        return idx >= 0 ? args[idx + 1] : "";
    }

    private static string SettingSourcesValue(List<string> args) =>
        args[args.IndexOf("--setting-sources") + 1];

    [Test]
    public async Task SkillsList_TwoNames_AddsTwoSkillRules()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Skills = new SkillsConfigList(["pdf", "my-plugin:review"])
        });

        await Assert.That(AllowedToolsValue(args)).IsEqualTo("Skill(pdf),Skill(my-plugin:review)");
    }

    [Test]
    public async Task SkillsList_AppendsAfterAllowedTools_WithoutDuplicates()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            AllowedTools = ["Read", "Skill(pdf)"],
            Skills = new SkillsConfigList(["pdf", "docx"])
        });

        await Assert.That(AllowedToolsValue(args)).IsEqualTo("Read,Skill(pdf),Skill(docx)");
    }

    [Test]
    public async Task SkillsAll_AppendsBareSkillOnce()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            AllowedTools = ["Read"],
            Skills = new SkillsConfigAll()
        });

        await Assert.That(AllowedToolsValue(args)).IsEqualTo("Read,Skill");

        var argsWithSkill = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            AllowedTools = ["Skill"],
            Skills = new SkillsConfigAll()
        });

        await Assert.That(AllowedToolsValue(argsWithSkill)).IsEqualTo("Skill");
    }

    [Test]
    public async Task SkillsEmptyList_AddsNoAllowedTools()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Skills = new SkillsConfigList([])
        });

        await Assert.That(args).DoesNotContain("--allowedTools");
    }

    [Test]
    public async Task SkillsSet_SettingSourcesNull_DefaultsToUserAndProject()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Skills = new SkillsConfigAll()
        });

        await Assert.That(SettingSourcesValue(args)).IsEqualTo("user,project");
    }

    [Test]
    public async Task SkillsSet_SettingSourcesGiven_KeepsCallerValue()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Skills = new SkillsConfigList(["pdf"]),
            SettingSources = ["project"]
        });

        await Assert.That(SettingSourcesValue(args)).IsEqualTo("project");
    }

    [Test]
    public async Task SkillsNull_SettingSourcesUnchanged()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions());

        await Assert.That(SettingSourcesValue(args)).IsEqualTo("");
    }

    [Test]
    [Arguments("a,b")]
    [Arguments("a(b")]
    [Arguments("a)b")]
    [Arguments("a\nb")]
    [Arguments("a\u0000b")]
    [Arguments("a\u007fb")]
    [Arguments("a\u0085b")]
    [Arguments("a﻿b")]
    [Arguments("*")]
    [Arguments("plugin:*")]
    [Arguments("pdf *")]
    [Arguments("/pdf")]
    [Arguments(" pdf")]
    [Arguments("pdf ")]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("a\\\\b")]
    [Arguments("pdf\\")]
    public async Task InvalidSkillName_Build_Throws(string name)
    {
        var options = new ClaudeClientOptions { Skills = new SkillsConfigList([name]) };

        var ex = Assert.Throws<ArgumentException>(() => CliArgsBuilder.Build(options));
        await Assert.That(ex.ParamName).IsEqualTo(nameof(ClaudeClientOptions.Skills));
    }

    [Test]
    public async Task UnpairedSurrogateSkillName_Build_Throws()
    {
        string[] names = ["a" + (char)0xD800 + "b", "a" + (char)0xDC00 + "b", "end" + (char)0xD83D];
        foreach (string name in names)
        {
            var options = new ClaudeClientOptions { Skills = new SkillsConfigList([name]) };
            var ex = Assert.Throws<ArgumentException>(() => CliArgsBuilder.Build(options));
            await Assert.That(ex.ParamName).IsEqualTo(nameof(ClaudeClientOptions.Skills));
        }
    }

    [Test]
    [Arguments("pdf")]
    [Arguments("my-plugin:review")]
    [Arguments("a b")]
    [Arguments("a\\b")]
    [Arguments("emoji\U0001F600")]
    public async Task ValidSkillName_Build_AddsRule(string name)
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Skills = new SkillsConfigList([name])
        });

        await Assert.That(AllowedToolsValue(args)).IsEqualTo($"Skill({name})");
    }

    [Test]
    public async Task NullSkillName_Build_Throws()
    {
        var options = new ClaudeClientOptions { Skills = new SkillsConfigList([null!]) };

        var ex = Assert.Throws<ArgumentException>(() => CliArgsBuilder.Build(options));
        await Assert.That(ex.ParamName).IsEqualTo(nameof(ClaudeClientOptions.Skills));
    }

    [Test]
    public async Task SkillNameWithComma_ThrowsAtConnect_BeforeAnyWrite()
    {
        var transport = CreateMockWithFlow(out _);
        await using var client = new ClaudeClient(
            new ClaudeClientOptions { Skills = new SkillsConfigList(["pdf", "a,Bash"]) },
            transport);

        await Assert.That(async () =>
        {
            await foreach (var _ in client.SendAsync("test")) { }
        }).Throws<ArgumentException>();

        await Assert.That(transport.WrittenMessages).IsEmpty();
    }

    [Test]
    public async Task SkillsList_SendsSkillsInInitialize()
    {
        var transport = CreateMockWithFlow(out var initialize);
        await using var client = new ClaudeClient(
            new ClaudeClientOptions { Skills = new SkillsConfigList(["pdf", "docx"]) },
            transport);

        await foreach (var _ in client.SendAsync("test")) { }

        using var doc = JsonDocument.Parse(initialize.Value!);
        var skills = doc.RootElement.GetProperty("request").GetProperty("skills");
        await Assert.That(skills.GetArrayLength()).IsEqualTo(2);
        await Assert.That(skills[0].GetString()).IsEqualTo("pdf");
        await Assert.That(skills[1].GetString()).IsEqualTo("docx");
    }

    [Test]
    public async Task SkillsAll_OmitsSkillsFromInitialize()
    {
        var transport = CreateMockWithFlow(out var initialize);
        await using var client = new ClaudeClient(
            new ClaudeClientOptions { Skills = new SkillsConfigAll() },
            transport);

        await foreach (var _ in client.SendAsync("test")) { }

        using var doc = JsonDocument.Parse(initialize.Value!);
        await Assert.That(doc.RootElement.GetProperty("request").TryGetProperty("skills", out _)).IsFalse();
    }

    private sealed class Captured
    {
        public string? Value { get; set; }
    }

    private static MockTransport CreateMockWithFlow(out Captured initialize)
    {
        var transport = new MockTransport("");
        var captured = new Captured();
        initialize = captured;

        transport.OnWrite = json =>
        {
            if (json.Contains("\"type\":\"control_request\"") && json.Contains("initialize"))
            {
                captured.Value = json;
                using var doc = JsonDocument.Parse(json);
                string requestId = doc.RootElement.GetProperty("request_id").GetString()!;
                transport.EnqueueOutput("{\"type\":\"control_response\",\"response\":{\"subtype\":\"success\",\"request_id\":\"" + requestId + "\",\"response\":{}}}");
            }
            else if (json.Contains("\"type\":\"user\""))
            {
                transport.EnqueueOutput(ResultLine);
            }
        };

        return transport;
    }
}
