// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Andy.Settings.Infrastructure.Messaging.Nats;

/// <summary>
/// NATS subject-pattern arithmetic.
/// </summary>
/// <remarks>
/// JetStream refuses a stream whose subject list contains redundant entries —
/// not only exact duplicates ("duplicate subjects detected") but any overlap
/// ("subject \"andy.*.dlq.&gt;\" overlaps with \"andy.settings.dlq.&gt;\"").
/// Provisioning a subject onto a SHARED stream therefore has to ask whether an
/// existing pattern already covers it, which exact string comparison cannot
/// answer: the shared ANDY_DOMAIN carries <c>andy.*.dlq.&gt;</c>, which already
/// covers this service's <c>andy.settings.dlq.&gt;</c>
/// (rivoli-ai/andy-settings#149).
/// </remarks>
internal static class NatsSubject
{
    private const string MatchOneToken = "*";
    private const string MatchAllRemaining = ">";

    /// <summary>
    /// True when every concrete subject matched by <paramref name="candidate"/>
    /// is also matched by <paramref name="pattern"/> — i.e. adding
    /// <paramref name="candidate"/> alongside <paramref name="pattern"/> would
    /// be redundant.
    /// </summary>
    public static bool Covers(string pattern, string candidate)
    {
        var patternTokens = pattern.Split('.');
        var candidateTokens = candidate.Split('.');

        var p = 0;
        var c = 0;

        while (p < patternTokens.Length && c < candidateTokens.Length)
        {
            var patternToken = patternTokens[p];
            var candidateToken = candidateTokens[c];

            // '>' absorbs every remaining token, so the pattern covers the rest
            // whatever it is.
            if (patternToken == MatchAllRemaining)
                return true;

            // The candidate reaches further than the pattern allows: '>' here
            // spans arbitrarily many tokens that a '*' or a literal cannot.
            if (candidateToken == MatchAllRemaining)
                return false;

            // '*' covers any single token, including another '*'.
            if (patternToken != MatchOneToken && patternToken != candidateToken)
                return false;

            p++;
            c++;
        }

        // A trailing '>' still covers when the candidate ended exactly at it
        // only if it has at least one token to absorb, which the loop above
        // already established. Otherwise both must have been consumed together.
        if (p < patternTokens.Length)
            return false;

        return c == candidateTokens.Length;
    }

    /// <summary>
    /// True when either subject covers the other — the condition JetStream
    /// rejects within a single stream's subject list.
    /// </summary>
    public static bool Overlaps(string a, string b) => Covers(a, b) || Covers(b, a);
}
