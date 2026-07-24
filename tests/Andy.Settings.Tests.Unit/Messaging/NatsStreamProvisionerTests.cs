// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Andy.Settings.Infrastructure.Messaging.Nats;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Andy.Settings.Tests.Unit.Messaging;

// rivoli-ai/andy-settings#149. Under the shipped configuration the provisioner
// asked NATS for a stream whose subject list contained the same subject twice,
// which NATS rejects with "duplicate subjects detected". The exception escaped
// IHostedService.StartAsync, so the whole host died on boot — and because the
// AK1 guard forces Messaging:Provider=Nats outside Development, the service
// could not start in ANY non-Development environment.
public class NatsStreamProvisionerTests
{
    // The root cause, pinned directly: the .NET configuration binder APPENDS
    // bound array elements to whatever the property already holds. A non-empty
    // default therefore duplicates any value appsettings.json also supplies.
    [Fact]
    public void Binding_ConfiguredSubjectMatchingDefault_DoesNotDuplicate()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:Nats:StreamName"] = "ANDY_DOMAIN",
                // Exactly what appsettings.json ships.
                ["Messaging:Nats:StreamSubjects:0"] = "andy.settings.events.>",
            })
            .Build();

        var opts = new NatsOptions();
        config.GetSection(NatsOptions.SectionName).Bind(opts);

        opts.StreamSubjects.Should().ContainSingle()
            .Which.Should().Be("andy.settings.events.>");
    }

    [Fact]
    public void RequiredSubjects_NeverContainsDuplicates()
    {
        var opts = new NatsOptions
        {
            // Simulate the binder's append behaviour reaching us anyway.
            StreamSubjects = ["andy.settings.events.>", "andy.settings.events.>"],
            DlqPrefix = "andy.settings.dlq",
        };

        var subjects = NatsStreamProvisioner.RequiredSubjects(opts);

        subjects.Should().OnlyHaveUniqueItems();
    }

    // NatsMessageBus publishes dead letters to {DlqPrefix}.{originalSubject},
    // but the DLQ prefix was never in the stream's subject list. It only worked
    // because the shared ANDY_DOMAIN happened to carry `andy.*.dlq.>`.
    [Fact]
    public void RequiredSubjects_IncludesDlqPrefix()
    {
        var opts = new NatsOptions
        {
            StreamSubjects = ["andy.settings.events.>"],
            DlqPrefix = "andy.settings.dlq",
        };

        NatsStreamProvisioner.RequiredSubjects(opts)
            .Should().Contain("andy.settings.dlq.>");
    }

    [Fact]
    public void RequiredSubjects_NoConfiguredSubjects_FallsBackToDefault()
    {
        var opts = new NatsOptions { StreamSubjects = [], DlqPrefix = "" };

        NatsStreamProvisioner.RequiredSubjects(opts)
            .Should().BeEquivalentTo(NatsOptions.DefaultStreamSubjects);
    }

    [Fact]
    public void RequiredSubjects_ConfiguredSubjects_OverrideDefault()
    {
        var opts = new NatsOptions { StreamSubjects = ["andy.custom.events.>"], DlqPrefix = "" };

        NatsStreamProvisioner.RequiredSubjects(opts)
            .Should().BeEquivalentTo(["andy.custom.events.>"]);
    }

    // ANDY_DOMAIN is shared. StreamConfig.Subjects is the stream's COMPLETE
    // subject list, so provisioning must union rather than replace — otherwise
    // andy-settings deletes andy-tasks', andy-issues', andy-agents' and
    // andy-rbac's routing the moment it boots successfully.
    [Fact]
    public void MergeSemantics_PreserveOtherServicesSubjects()
    {
        string[] existing =
        [
            "andy.tasks.events.>", "andy.issues.events.>", "andy.agents.events.>",
            "andy.rbac.events.policy.>", "andy.*.dlq.>",
        ];
        var required = NatsStreamProvisioner.RequiredSubjects(new NatsOptions
        {
            StreamSubjects = ["andy.settings.events.>"],
            DlqPrefix = "andy.settings.dlq",
        });

        var missing = required.Where(s => !existing.Any(e => NatsSubject.Covers(e, s))).ToArray();
        var merged = existing.Concat(missing).ToArray();

        merged.Should().Contain(existing, "no other service's subjects may be dropped");
        merged.Should().Contain("andy.settings.events.>");
        merged.Should().OnlyHaveUniqueItems();

        // The DLQ subject must NOT be added: `andy.*.dlq.>` already covers it,
        // and JetStream rejects a subject list containing overlapping patterns.
        merged.Should().NotContain("andy.settings.dlq.>");
    }

    [Fact]
    public void MergeSemantics_AlreadyCovered_RequiresNoUpdate()
    {
        string[] existing = ["andy.settings.events.>", "andy.*.dlq.>", "andy.tasks.events.>"];
        var required = NatsStreamProvisioner.RequiredSubjects(new NatsOptions
        {
            StreamSubjects = ["andy.settings.events.>"],
            DlqPrefix = "andy.settings.dlq",
        });

        required.Where(s => !existing.Any(e => NatsSubject.Covers(e, s))).Should().BeEmpty();
    }
}
