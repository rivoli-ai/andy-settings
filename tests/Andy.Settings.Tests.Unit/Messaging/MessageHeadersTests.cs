// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Andy.Settings.Application.Messaging;
using FluentAssertions;
using Xunit;

namespace Andy.Settings.Tests.Unit.Messaging;

public class MessageHeadersTests
{
    [Fact]
    public void NewRoot_starts_chain_at_generation_zero()
    {
        var h = MessageHeaders.NewRoot();
        h.Generation.Should().Be(0);
        h.CausationId.Should().BeNull();
        h.CorrelationId.Should().Be(h.MsgId,
            because: "a root message correlates with itself when no explicit correlation is given");
    }

    [Fact]
    public void Follow_increments_generation_and_carries_correlation()
    {
        var parent = MessageHeaders.NewRoot();
        var child = MessageHeaders.Follow(parent);

        child.Generation.Should().Be(parent.Generation + 1);
        child.CorrelationId.Should().Be(parent.CorrelationId);
        child.CausationId.Should().Be(parent.MsgId);
        child.MsgId.Should().NotBe(parent.MsgId);
    }

    [Fact]
    public void ExceedsGenerationLimit_trips_above_max()
    {
        // ADR 0001 §5 — Generation > 10 is the cycle-breaker boundary.
        new MessageHeaders(Guid.NewGuid(), Guid.NewGuid(), null, 10)
            .ExceedsGenerationLimit.Should().BeFalse(because: "exactly 10 is still in range");
        new MessageHeaders(Guid.NewGuid(), Guid.NewGuid(), null, 11)
            .ExceedsGenerationLimit.Should().BeTrue(because: "11 is one past the limit");
    }
}
