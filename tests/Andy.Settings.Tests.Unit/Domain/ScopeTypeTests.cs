using System.Text.Json;
using Andy.Settings.Domain.Enums;
using FluentAssertions;

namespace Andy.Settings.Tests.Unit.Domain;

public class ScopeTypeTests
{
    [Fact]
    public void ScopeType_HasCorrectPrecedenceOrder()
    {
        ((int)ScopeType.Machine).Should().Be(0);
        ((int)ScopeType.Application).Should().Be(1);
        ((int)ScopeType.Service).Should().Be(2);
        ((int)ScopeType.User).Should().Be(3);
        ((int)ScopeType.Team).Should().Be(4);
        ((int)ScopeType.Workspace).Should().Be(5);
        ((int)ScopeType.RuntimeOverride).Should().Be(6);
    }

    [Fact]
    public void ScopeType_MachineIsLowestPrecedence()
    {
        ((int)ScopeType.Machine).Should().BeLessThan((int)ScopeType.Application);
        ((int)ScopeType.Machine).Should().BeLessThan((int)ScopeType.RuntimeOverride);
    }

    [Fact]
    public void ScopeType_RuntimeOverrideIsHighestPrecedence()
    {
        ((int)ScopeType.RuntimeOverride).Should().BeGreaterThan((int)ScopeType.Machine);
        ((int)ScopeType.RuntimeOverride).Should().BeGreaterThan((int)ScopeType.Workspace);
    }

    [Fact]
    public void ScopeType_SerializesAsString()
    {
        var json = JsonSerializer.Serialize(ScopeType.RuntimeOverride);
        json.Should().Be("\"RuntimeOverride\"");
    }
}
