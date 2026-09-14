using FluentAssertions;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Sessions;

namespace UsageNotch.Core.Tests.Hooks;

public class HookEventParserTests
{
    [Fact]
    public void Reads_every_field_claude_code_provides()
    {
        var body = """
        { "session_id": "s-1", "cwd": "C:\\src\\proj", "prompt": "fais X", "message": "Autoriser ?",
          "tool_name": "Bash", "tool_input": { "command": "dotnet build" }, "model": "claude-opus-5" }
        """;

        var ev = HookEventParser.Parse(HookEvent.Running, 1234, body);

        ev.Should().Be(new HookEvent(HookEvent.Running, "s-1", 1234, @"C:\src\proj", "fais X", "Autoriser ?", "Bash", "dotnet build", "claude-opus-5"));
    }

    [Fact]
    public void Missing_fields_become_empty_strings_and_unknown_session_id()
    {
        var ev = HookEventParser.Parse(HookEvent.Done, 0, "{}");
        ev.SessionId.Should().Be("unknown");
        ev.Cwd.Should().BeEmpty();
        ev.ToolCommand.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public void Unreadable_bodies_do_not_throw(string body)
    {
        var ev = HookEventParser.Parse(HookEvent.Attention, 7, body);
        ev.Kind.Should().Be(HookEvent.Attention);
        ev.ParentPid.Should().Be(7);
        ev.SessionId.Should().Be("unknown");
    }

    [Fact]
    public void Tool_input_without_command_gives_an_empty_command()
    {
        var ev = HookEventParser.Parse(HookEvent.Running, 0, """{ "tool_name": "Read", "tool_input": { "file_path": "x" } }""");
        ev.ToolName.Should().Be("Read");
        ev.ToolCommand.Should().BeEmpty();
    }

    [Fact]
    public void A_truncated_body_keeps_the_fields_read_before_the_cut()
    {
        var body = "{\"session_id\":\"s-9\",\"cwd\":\"C:\\\\p\",\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"x\",\"content\":\""
            + new string('a', 1000);

        var ev = HookEventParser.Parse(HookEvent.Running, 0, body);

        ev.SessionId.Should().Be("s-9");
        ev.Cwd.Should().Be(@"C:\p");
        ev.ToolName.Should().Be("Write");
    }

    [Fact]
    public void A_body_cut_inside_the_session_id_string_gives_unknown()
    {
        var ev = HookEventParser.Parse(HookEvent.Running, 0, """{"session_id":"s-""");

        ev.SessionId.Should().Be("unknown");
    }
}
