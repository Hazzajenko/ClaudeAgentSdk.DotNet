using System.Text.Json;
using ClaudeAgentSdk.Control;
using ClaudeAgentSdk.Hooks;
using ClaudeAgentSdk.Permissions;
using ClaudeAgentSdk.Transport;
using NSubstitute;

namespace ClaudeAgentSdk.Tests.Control;

public class ControlProtocolHandlerTests
{
    [Test]
    public async Task HandleControlResponse_RoutesToPendingRequest()
    {
        var transport = Substitute.For<ITransport>();
        var writtenJson = new List<string>();
        transport.WriteAsync(Arg.Do<string>(s => writtenJson.Add(s)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);

        // Start a control request (won't complete until response received)
        using var doc = JsonDocument.Parse("""{"subtype":"interrupt"}""");
        var requestTask = handler.SendControlRequestAsync(doc.RootElement.Clone());

        // Give it a moment to send
        await Task.Delay(50);

        // Extract the request_id from what was written
        await Assert.That(writtenJson.Count).IsEqualTo(1);
        using var sentDoc = JsonDocument.Parse(writtenJson[0]);
        string requestId = sentDoc.RootElement.GetProperty("request_id").GetString()!;

        // Simulate CLI response
        using var responseDoc = JsonDocument.Parse($$"""
            {
                "type": "control_response",
                "response": {
                    "subtype": "success",
                    "request_id": "{{requestId}}",
                    "response": {"status": "ok"}
                }
            }
            """);
        handler.HandleControlResponse(responseDoc.RootElement);

        // Request should now complete
        var result = await requestTask;
        await Assert.That(result.GetProperty("status").GetString()).IsEqualTo("ok");
    }

    [Test]
    public async Task HandleControlResponse_ErrorResponse_ThrowsException()
    {
        var transport = Substitute.For<ITransport>();
        var writtenJson = new List<string>();
        transport.WriteAsync(Arg.Do<string>(s => writtenJson.Add(s)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);

        using var doc = JsonDocument.Parse("""{"subtype":"interrupt"}""");
        var requestTask = handler.SendControlRequestAsync(doc.RootElement.Clone());

        await Task.Delay(50);
        using var sentDoc = JsonDocument.Parse(writtenJson[0]);
        string requestId = sentDoc.RootElement.GetProperty("request_id").GetString()!;

        using var responseDoc = JsonDocument.Parse($$"""
            {
                "type": "control_response",
                "response": {
                    "subtype": "error",
                    "request_id": "{{requestId}}",
                    "error": "Something went wrong"
                }
            }
            """);
        handler.HandleControlResponse(responseDoc.RootElement);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await requestTask);
    }

    [Test]
    public async Task SendControlRequest_WritesCorrectJsonFormat()
    {
        var transport = Substitute.For<ITransport>();
        var writtenJson = new List<string>();
        transport.WriteAsync(Arg.Do<string>(s => writtenJson.Add(s)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);

        using var doc = JsonDocument.Parse("""{"subtype":"set_model","model":"claude-sonnet-4-5"}""");
        // Don't await — we just want to verify what was sent
        _ = handler.SendControlRequestAsync(doc.RootElement.Clone(), TimeSpan.FromMilliseconds(100));

        await Task.Delay(50);

        await Assert.That(writtenJson.Count).IsEqualTo(1);
        using var sentDoc = JsonDocument.Parse(writtenJson[0]);
        var root = sentDoc.RootElement;
        await Assert.That(root.GetProperty("type").GetString()).IsEqualTo("control_request");
        await Assert.That(root.TryGetProperty("request_id", out _)).IsTrue();
        await Assert.That(root.GetProperty("request").GetProperty("subtype").GetString()).IsEqualTo("set_model");
    }

    [Test]
    public async Task HandleControlRequestAsync_HookCallback_DeserializesTypedInput()
    {
        var transport = Substitute.For<ITransport>();
        transport.WriteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);

        string? receivedCallbackId = null;
        HookInput? receivedInput = null;
        handler.OnHookCallback = (callbackId, input, ct) =>
        {
            receivedCallbackId = callbackId;
            receivedInput = input;
            return Task.FromResult(HookOutput.Empty);
        };

        using var requestDoc = JsonDocument.Parse("""
            {
                "request_id": "req_1",
                "request": {
                    "subtype": "hook_callback",
                    "callback_id": "hook_0",
                    "input": {
                        "hook_event_name": "PreToolUse",
                        "session_id": "sess-1",
                        "transcript_path": "/tmp/t.jsonl",
                        "cwd": "/home/user",
                        "tool_name": "Bash",
                        "tool_input": {"command": "echo hello"},
                        "tool_use_id": "toolu_01"
                    }
                }
            }
            """);

        await handler.HandleControlRequestAsync(requestDoc.RootElement, CancellationToken.None);

        await Assert.That(receivedCallbackId).IsEqualTo("hook_0");
        await Assert.That(receivedInput).IsTypeOf<PreToolUseInput>();
        var pre = (PreToolUseInput)receivedInput!;
        await Assert.That(pre.ToolName).IsEqualTo("Bash");
        await Assert.That(pre.ToolUseId).IsEqualTo("toolu_01");
        await Assert.That(pre.SessionId).IsEqualTo("sess-1");
    }

    [Test]
    public async Task HandleControlRequestAsync_HookCallback_SerializesOutputToCamelCase()
    {
        var transport = Substitute.For<ITransport>();
        var writtenJson = new List<string>();
        transport.WriteAsync(Arg.Do<string>(s => writtenJson.Add(s)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);

        handler.OnHookCallback = (callbackId, input, ct) =>
        {
            return Task.FromResult(new HookOutput
            {
                HookSpecificOutput = new PreToolUseOutput
                {
                    PermissionDecision = "deny",
                    PermissionDecisionReason = "blocked"
                }
            });
        };

        using var requestDoc = JsonDocument.Parse("""
            {
                "request_id": "req_2",
                "request": {
                    "subtype": "hook_callback",
                    "callback_id": "hook_0",
                    "input": {
                        "hook_event_name": "PreToolUse",
                        "session_id": "s",
                        "transcript_path": "/t",
                        "cwd": "/c",
                        "tool_name": "Bash",
                        "tool_input": {},
                        "tool_use_id": "t1"
                    }
                }
            }
            """);

        await handler.HandleControlRequestAsync(requestDoc.RootElement, CancellationToken.None);

        await Assert.That(writtenJson.Count).IsEqualTo(1);
        using var sentDoc = JsonDocument.Parse(writtenJson[0]);
        var responseData = sentDoc.RootElement.GetProperty("response").GetProperty("response");
        // camelCase: hookSpecificOutput, permissionDecision
        var hookSpecific = responseData.GetProperty("hookSpecificOutput");
        await Assert.That(hookSpecific.GetProperty("permissionDecision").GetString()).IsEqualTo("deny");
        await Assert.That(hookSpecific.GetProperty("permissionDecisionReason").GetString()).IsEqualTo("blocked");
    }

    [Test]
    public async Task HandleControlRequestAsync_HookCallback_MalformedInput_WritesError()
    {
        var transport = Substitute.For<ITransport>();
        var writtenJson = new List<string>();
        transport.WriteAsync(Arg.Do<string>(s => writtenJson.Add(s)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);
        handler.OnHookCallback = (callbackId, input, ct) => Task.FromResult(HookOutput.Empty);

        // Missing hook_event_name — deserialization should fail
        using var requestDoc = JsonDocument.Parse("""
            {
                "request_id": "req_3",
                "request": {
                    "subtype": "hook_callback",
                    "callback_id": "hook_0",
                    "input": {"not_a_valid_field": true}
                }
            }
            """);

        await handler.HandleControlRequestAsync(requestDoc.RootElement, CancellationToken.None);

        await Assert.That(writtenJson.Count).IsEqualTo(1);
        using var sentDoc = JsonDocument.Parse(writtenJson[0]);
        var response = sentDoc.RootElement.GetProperty("response");
        await Assert.That(response.GetProperty("subtype").GetString()).IsEqualTo("error");
    }

    [Test]
    public async Task HandleControlRequestAsync_McpMessage_InvokesOnMcpMessage()
    {
        var transport = Substitute.For<ITransport>();
        var writtenJson = new List<string>();
        transport.WriteAsync(Arg.Do<string>(s => writtenJson.Add(s)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);

        string? receivedServerName = null;
        handler.OnMcpMessage = async (serverName, message, ct) =>
        {
            receivedServerName = serverName;
            // Return a mock JSONRPC response
            using var doc = JsonDocument.Parse("""{"jsonrpc":"2.0","id":1,"result":{"tools":[]}}""");
            return doc.RootElement.Clone();
        };

        using var requestDoc = JsonDocument.Parse("""
            {
                "request_id": "req_mcp_1",
                "request": {
                    "subtype": "mcp_message",
                    "server_name": "math",
                    "message": {"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}
                }
            }
            """);

        await handler.HandleControlRequestAsync(requestDoc.RootElement, CancellationToken.None);

        await Assert.That(receivedServerName).IsEqualTo("math");

        // Verify response was written with mcp_response wrapper
        await Assert.That(writtenJson.Count).IsEqualTo(1);
        using var sentDoc = JsonDocument.Parse(writtenJson[0]);
        var response = sentDoc.RootElement.GetProperty("response");
        await Assert.That(response.GetProperty("subtype").GetString()).IsEqualTo("success");

        var responseData = response.GetProperty("response");
        await Assert.That(responseData.TryGetProperty("mcp_response", out _)).IsTrue();
    }

    [Test]
    public async Task HandleControlRequestAsync_CanUseTool_SuggestionsDeserializedAsTypedPermissionUpdates()
    {
        var transport = Substitute.For<ITransport>();
        transport.WriteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);

        ToolPermissionContext? receivedContext = null;
        handler.OnCanUseTool = (toolName, input, context, ct) =>
        {
            receivedContext = context;
            return Task.FromResult<PermissionResult>(new PermissionResultAllow());
        };

        using var requestDoc = JsonDocument.Parse("""
            {
                "request_id": "req_sug_1",
                "request": {
                    "subtype": "can_use_tool",
                    "tool_name": "Bash",
                    "input": {"command": "ls"},
                    "permission_suggestions": [
                        {
                            "type": "addRules",
                            "rules": [
                                {"tool_name": "Bash", "rule_content": "allow ls", "behavior": "allow"}
                            ],
                            "destination": "session"
                        },
                        {
                            "type": "setMode",
                            "mode": "acceptEdits",
                            "destination": "session"
                        }
                    ]
                }
            }
            """);

        await handler.HandleControlRequestAsync(requestDoc.RootElement, CancellationToken.None);

        await Assert.That(receivedContext).IsNotNull();
        await Assert.That(receivedContext!.Suggestions).IsNotNull();
        await Assert.That(receivedContext.Suggestions!.Count).IsEqualTo(2);

        var addRules = receivedContext.Suggestions[0];
        await Assert.That(addRules.Type).IsEqualTo(PermissionUpdateType.AddRules);
        await Assert.That(addRules.Rules).IsNotNull();
        await Assert.That(addRules.Rules!.Count).IsEqualTo(1);
        await Assert.That(addRules.Rules[0].ToolName).IsEqualTo("Bash");
        await Assert.That(addRules.Rules[0].RuleContent).IsEqualTo("allow ls");
        await Assert.That(addRules.Rules[0].Behavior).IsEqualTo("allow");
        await Assert.That(addRules.Destination).IsEqualTo("session");

        var setMode = receivedContext.Suggestions[1];
        await Assert.That(setMode.Type).IsEqualTo(PermissionUpdateType.SetMode);
        await Assert.That(setMode.Mode).IsEqualTo("acceptEdits");
    }

    [Test]
    public async Task HandleControlRequestAsync_CanUseTool_AllowWithUpdatedPermissions_WritesPermissionsInResponse()
    {
        var transport = Substitute.For<ITransport>();
        var writtenJson = new List<string>();
        transport.WriteAsync(Arg.Do<string>(s => writtenJson.Add(s)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);

        handler.OnCanUseTool = (toolName, input, context, ct) =>
        {
            return Task.FromResult<PermissionResult>(new PermissionResultAllow
            {
                UpdatedPermissions =
                [
                    new PermissionUpdate
                    {
                        Type = PermissionUpdateType.AddRules,
                        Rules =
                        [
                            new PermissionRuleValue
                            {
                                ToolName = "Bash",
                                RuleContent = "allow ls",
                                Behavior = "allow"
                            }
                        ],
                        Destination = "session"
                    },
                    new PermissionUpdate
                    {
                        Type = PermissionUpdateType.AddDirectories,
                        Directories = ["/tmp/extra"],
                        Destination = "session"
                    }
                ]
            });
        };

        using var requestDoc = JsonDocument.Parse("""
            {
                "request_id": "req_perm_up_1",
                "request": {
                    "subtype": "can_use_tool",
                    "tool_name": "Bash",
                    "input": {"command": "ls"},
                    "permission_suggestions": []
                }
            }
            """);

        await handler.HandleControlRequestAsync(requestDoc.RootElement, CancellationToken.None);

        await Assert.That(writtenJson.Count).IsEqualTo(1);
        using var sentDoc = JsonDocument.Parse(writtenJson[0]);
        var responseData = sentDoc.RootElement.GetProperty("response").GetProperty("response");
        await Assert.That(responseData.GetProperty("behavior").GetString()).IsEqualTo("allow");

        // updatedPermissions array present
        var perms = responseData.GetProperty("updatedPermissions");
        await Assert.That(perms.ValueKind).IsEqualTo(JsonValueKind.Array);
        await Assert.That(perms.GetArrayLength()).IsEqualTo(2);

        // First: addRules
        var first = perms[0];
        await Assert.That(first.GetProperty("type").GetString()).IsEqualTo("addRules");
        await Assert.That(first.GetProperty("destination").GetString()).IsEqualTo("session");
        var rules = first.GetProperty("rules");
        await Assert.That(rules.GetArrayLength()).IsEqualTo(1);
        await Assert.That(rules[0].GetProperty("tool_name").GetString()).IsEqualTo("Bash");
        await Assert.That(rules[0].GetProperty("rule_content").GetString()).IsEqualTo("allow ls");
        await Assert.That(rules[0].GetProperty("behavior").GetString()).IsEqualTo("allow");

        // Second: addDirectories
        var second = perms[1];
        await Assert.That(second.GetProperty("type").GetString()).IsEqualTo("addDirectories");
        var dirs = second.GetProperty("directories");
        await Assert.That(dirs[0].GetString()).IsEqualTo("/tmp/extra");
    }

    [Test]
    public async Task HandleControlRequestAsync_CanUseTool_AllowWithoutUpdatedPermissions_OmitsField()
    {
        var transport = Substitute.For<ITransport>();
        var writtenJson = new List<string>();
        transport.WriteAsync(Arg.Do<string>(s => writtenJson.Add(s)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);

        handler.OnCanUseTool = (toolName, input, context, ct) =>
        {
            return Task.FromResult<PermissionResult>(new PermissionResultAllow());
        };

        using var requestDoc = JsonDocument.Parse("""
            {
                "request_id": "req_perm_no_up",
                "request": {
                    "subtype": "can_use_tool",
                    "tool_name": "Read",
                    "input": {"file_path": "/tmp/test.txt"},
                    "permission_suggestions": []
                }
            }
            """);

        await handler.HandleControlRequestAsync(requestDoc.RootElement, CancellationToken.None);

        await Assert.That(writtenJson.Count).IsEqualTo(1);
        using var sentDoc = JsonDocument.Parse(writtenJson[0]);
        var responseData = sentDoc.RootElement.GetProperty("response").GetProperty("response");
        await Assert.That(responseData.GetProperty("behavior").GetString()).IsEqualTo("allow");
        // No updatedPermissions field when not set
        await Assert.That(responseData.TryGetProperty("updatedPermissions", out _)).IsFalse();
    }

    [Test]
    public async Task HandleControlRequestAsync_CanUseTool_Allow_WritesCorrectResponse()
    {
        var transport = Substitute.For<ITransport>();
        var writtenJson = new List<string>();
        transport.WriteAsync(Arg.Do<string>(s => writtenJson.Add(s)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);

        string? receivedToolName = null;
        handler.OnCanUseTool = (toolName, input, context, ct) =>
        {
            receivedToolName = toolName;
            return Task.FromResult<PermissionResult>(new PermissionResultAllow
            {
                UpdatedInput = input
            });
        };

        using var requestDoc = JsonDocument.Parse("""
            {
                "request_id": "req_perm_1",
                "request": {
                    "subtype": "can_use_tool",
                    "tool_name": "Bash",
                    "input": {"command": "ls"},
                    "permission_suggestions": []
                }
            }
            """);

        await handler.HandleControlRequestAsync(requestDoc.RootElement, CancellationToken.None);

        await Assert.That(receivedToolName).IsEqualTo("Bash");
        await Assert.That(writtenJson.Count).IsEqualTo(1);
        using var sentDoc = JsonDocument.Parse(writtenJson[0]);
        var responseData = sentDoc.RootElement.GetProperty("response").GetProperty("response");
        await Assert.That(responseData.GetProperty("behavior").GetString()).IsEqualTo("allow");
        await Assert.That(responseData.GetProperty("updatedInput").GetProperty("command").GetString()).IsEqualTo("ls");
    }

    [Test]
    public async Task HandleControlRequestAsync_CanUseTool_Deny_WritesCorrectResponse()
    {
        var transport = Substitute.For<ITransport>();
        var writtenJson = new List<string>();
        transport.WriteAsync(Arg.Do<string>(s => writtenJson.Add(s)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);

        handler.OnCanUseTool = (toolName, input, context, ct) =>
        {
            return Task.FromResult<PermissionResult>(new PermissionResultDeny
            {
                Message = "Blocked",
                Interrupt = true
            });
        };

        using var requestDoc = JsonDocument.Parse("""
            {
                "request_id": "req_perm_2",
                "request": {
                    "subtype": "can_use_tool",
                    "tool_name": "Bash",
                    "input": {"command": "rm -rf /"},
                    "permission_suggestions": []
                }
            }
            """);

        await handler.HandleControlRequestAsync(requestDoc.RootElement, CancellationToken.None);

        await Assert.That(writtenJson.Count).IsEqualTo(1);
        using var sentDoc = JsonDocument.Parse(writtenJson[0]);
        var responseData = sentDoc.RootElement.GetProperty("response").GetProperty("response");
        await Assert.That(responseData.GetProperty("behavior").GetString()).IsEqualTo("deny");
        await Assert.That(responseData.GetProperty("message").GetString()).IsEqualTo("Blocked");
        await Assert.That(responseData.GetProperty("interrupt").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task HandleControlRequestAsync_CanUseTool_Allow_EchoesOriginalInput_WhenNoOverride()
    {
        var transport = Substitute.For<ITransport>();
        var writtenJson = new List<string>();
        transport.WriteAsync(Arg.Do<string>(s => writtenJson.Add(s)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);

        handler.OnCanUseTool = (toolName, input, context, ct) =>
        {
            // No UpdatedInput — should echo original
            return Task.FromResult<PermissionResult>(new PermissionResultAllow());
        };

        using var requestDoc = JsonDocument.Parse("""
            {
                "request_id": "req_perm_3",
                "request": {
                    "subtype": "can_use_tool",
                    "tool_name": "Read",
                    "input": {"file_path": "/tmp/test.txt"}
                }
            }
            """);

        await handler.HandleControlRequestAsync(requestDoc.RootElement, CancellationToken.None);

        await Assert.That(writtenJson.Count).IsEqualTo(1);
        using var sentDoc = JsonDocument.Parse(writtenJson[0]);
        var responseData = sentDoc.RootElement.GetProperty("response").GetProperty("response");
        await Assert.That(responseData.GetProperty("behavior").GetString()).IsEqualTo("allow");
        // Original input echoed back
        await Assert.That(responseData.GetProperty("updatedInput").GetProperty("file_path").GetString()).IsEqualTo("/tmp/test.txt");
    }

    [Test]
    public async Task FailAllPending_CancelsOutstandingRequests()
    {
        var transport = Substitute.For<ITransport>();
        transport.WriteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);

        using var doc = JsonDocument.Parse("""{"subtype":"interrupt"}""");
        var task = handler.SendControlRequestAsync(doc.RootElement.Clone());

        await Task.Delay(50);
        handler.FailAllPending(new OperationCanceledException("Shutdown"));

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
    }
}
