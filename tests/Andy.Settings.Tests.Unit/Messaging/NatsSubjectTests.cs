// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Andy.Settings.Infrastructure.Messaging.Nats;
using FluentAssertions;
using Xunit;

namespace Andy.Settings.Tests.Unit.Messaging;

// Subject subsumption drives whether the provisioner adds a subject to the
// SHARED ANDY_DOMAIN stream. Getting it wrong in one direction wedges startup
// (JetStream refuses overlapping subjects); getting it wrong in the other
// swallows another service's traffic. rivoli-ai/andy-settings#149.
public class NatsSubjectTests
{
    [Theory]
    // Identical.
    [InlineData("andy.settings.events.>", "andy.settings.events.>")]
    // '>' absorbs everything below it.
    [InlineData("andy.>", "andy.settings.events.>")]
    [InlineData("andy.settings.>", "andy.settings.events.config.x.updated")]
    // '*' covers exactly one token, literal or wildcard.
    [InlineData("andy.*.dlq.>", "andy.settings.dlq.>")]
    [InlineData("andy.*.events.>", "andy.tasks.events.>")]
    [InlineData("andy.*.dlq.>", "andy.*.dlq.>")]
    // Concrete subject fully inside the pattern.
    [InlineData("andy.settings.events.*", "andy.settings.events.updated")]
    public void Covers_WhenPatternSubsumesCandidate_ReturnsTrue(string pattern, string candidate)
    {
        NatsSubject.Covers(pattern, candidate).Should().BeTrue();
    }

    [Theory]
    // Different literals.
    [InlineData("andy.tasks.events.>", "andy.settings.events.>")]
    [InlineData("andy.*.dlq.>", "andy.settings.events.>")]
    // The candidate reaches further than the pattern permits.
    [InlineData("andy.settings.events.*", "andy.settings.events.>")]
    [InlineData("andy.settings.events", "andy.settings.events.>")]
    // A '*' cannot be covered by a literal.
    [InlineData("andy.settings.dlq.>", "andy.*.dlq.>")]
    // Candidate is shorter than the pattern.
    [InlineData("andy.settings.events.>", "andy.settings")]
    // Different depth, no wildcard.
    [InlineData("andy.settings", "andy.settings.events")]
    public void Covers_WhenPatternDoesNotSubsumeCandidate_ReturnsFalse(string pattern, string candidate)
    {
        NatsSubject.Covers(pattern, candidate).Should().BeFalse();
    }

    [Fact]
    public void Overlaps_IsSymmetric()
    {
        NatsSubject.Overlaps("andy.*.dlq.>", "andy.settings.dlq.>").Should().BeTrue();
        NatsSubject.Overlaps("andy.settings.dlq.>", "andy.*.dlq.>").Should().BeTrue();

        NatsSubject.Overlaps("andy.tasks.events.>", "andy.settings.events.>").Should().BeFalse();
        NatsSubject.Overlaps("andy.settings.events.>", "andy.tasks.events.>").Should().BeFalse();
    }

    // The exact production shape: the live shared stream against what this
    // service needs.
    [Fact]
    public void LiveSharedStream_OnlyEventSubjectIsMissing()
    {
        string[] existing =
        [
            "andy.tasks.events.>", "andy.issues.events.>", "andy.agents.events.>",
            "andy.rbac.events.policy.>", "andy.*.dlq.>",
        ];
        string[] required = ["andy.settings.events.>", "andy.settings.dlq.>"];

        var missing = required
            .Where(s => !existing.Any(e => NatsSubject.Covers(e, s)))
            .ToArray();

        // The DLQ subject is already covered by `andy.*.dlq.>`; adding it would
        // make JetStream reject the whole update.
        missing.Should().BeEquivalentTo(["andy.settings.events.>"]);
    }

    [Fact]
    public void LiveSharedStream_OurSubjectsDoNotSubsumeAnyExisting()
    {
        string[] existing =
        [
            "andy.tasks.events.>", "andy.issues.events.>", "andy.agents.events.>",
            "andy.rbac.events.policy.>", "andy.*.dlq.>",
        ];
        string[] required = ["andy.settings.events.>", "andy.settings.dlq.>"];

        foreach (var ours in required)
            existing.Should().NotContain(e => NatsSubject.Covers(ours, e));
    }
}
