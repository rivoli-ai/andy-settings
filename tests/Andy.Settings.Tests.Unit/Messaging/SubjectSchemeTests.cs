// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Andy.Settings.Application.Messaging.Events;
using Andy.Settings.Infrastructure.Messaging;
using FluentAssertions;
using Xunit;

namespace Andy.Settings.Tests.Unit.Messaging;

// Per AL3 the publisher-exclusive subject scheme is
// `andy.settings.events.config.{application_code}.{kind}`. These tests
// pin the wire shape so a refactor of ConfigEventOutbox can't silently
// rename a subject — consumers like mcp-gateway subscribe on the
// literal string.
public class SubjectSchemeTests
{
    [Theory]
    [InlineData("mcp-gateway", ConfigEventKind.Created, "andy.settings.events.config.mcp-gateway.created")]
    [InlineData("mcp-gateway", ConfigEventKind.Updated, "andy.settings.events.config.mcp-gateway.updated")]
    [InlineData("mcp-gateway", ConfigEventKind.Deleted, "andy.settings.events.config.mcp-gateway.deleted")]
    [InlineData("andy-containers", ConfigEventKind.Updated, "andy.settings.events.config.andy-containers.updated")]
    public void SubjectFor_emits_expected_taxonomy(
        string applicationCode, ConfigEventKind kind, string expected)
    {
        ConfigEventOutbox.SubjectFor(applicationCode, kind).Should().Be(expected);
    }

    [Fact]
    public void SubjectFor_replaces_dots_in_application_code()
    {
        // Subject tokens are dot-separated. A registration that ever
        // sneaks a dot into application_code would corrupt subject
        // parsing on the consumer side. The sanitizer collapses it to
        // a dash; the authoritative application_code stays in the
        // payload for reverse lookup.
        ConfigEventOutbox.SubjectFor("acme.products", ConfigEventKind.Updated)
            .Should().Be("andy.settings.events.config.acme-products.updated");
    }

    [Fact]
    public void SubjectFor_kind_is_past_tense_per_ADR()
    {
        // ADR 0001 §4: "Kind is past-tense. Never `create`, `start`,
        // `finish`." This is a regression guard against someone adding
        // a present-tense kind by accident.
        foreach (var kind in Enum.GetValues<ConfigEventKind>())
        {
            var subject = ConfigEventOutbox.SubjectFor("any", kind);
            subject.Should().EndWith("ed", because: $"subject {subject} must use past-tense kind suffix");
        }
    }
}
