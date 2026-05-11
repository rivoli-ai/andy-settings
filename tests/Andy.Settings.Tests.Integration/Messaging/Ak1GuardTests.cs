// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Andy.Settings.Tests.Integration.Messaging;

// AK1 invariant from ADR 0001: the in-memory bus is only valid in
// Development. Any other environment must set Messaging:Provider=Nats
// or the host fails fast at boot. This test boots Program.cs in a
// non-Development environment with the default config and asserts the
// guard's exact wording — drift would silently let production fall
// back to the InMemory bus.
public class Ak1GuardTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Docker")]
    [InlineData("Embedded")]
    [InlineData("Staging")]
    public void Program_throws_when_provider_is_inmemory_outside_development(string env)
    {
        // Override Messaging__Provider via env var so the read in
        // Program.cs sees "InMemory" — appsettings.json's default is
        // Nats, which would let the guard pass.
        var prior = Environment.GetEnvironmentVariable("Messaging__Provider");
        Environment.SetEnvironmentVariable("Messaging__Provider", "InMemory");

        try
        {
            using var factory = new EnvOverrideFactory(env);
            var act = () => factory.CreateClient();
            act.Should().Throw<InvalidOperationException>()
                .WithMessage($"Messaging:Provider must be 'Nats' in {env}.*");
        }
        finally
        {
            Environment.SetEnvironmentVariable("Messaging__Provider", prior);
        }
    }

    [Fact]
    public void Program_boots_when_environment_is_development()
    {
        // Sanity check: the regular CustomWebApplicationFactory uses
        // Environment=Development + Messaging:Provider=InMemory and
        // boots cleanly. That fixture is exercised by every other
        // integration test, so we just instantiate one here to keep
        // the AK1 negative tests self-contained.
        using var factory = new CustomWebApplicationFactory();
        var act = () => factory.CreateClient();
        act.Should().NotThrow();
    }

    // Minimal factory that just changes the environment name. Inherits
    // the test seeding + DbContext from CustomWebApplicationFactory.
    private sealed class EnvOverrideFactory : CustomWebApplicationFactory
    {
        private readonly string _env;
        public EnvOverrideFactory(string env) { _env = env; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseEnvironment(_env);
        }
    }
}
