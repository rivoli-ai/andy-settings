// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Andy.Settings.Domain.Entities;
using Andy.Settings.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Andy.Settings.Infrastructure.Messaging;

// Persistent consumer-side dedup store per ADR 0001 AK3. Backed by the
// SeenMessages table on whichever DB provider the host configured
// (SQLite in the Conductor-embedded mode, Postgres in Docker /
// standalone). Inserting on a duplicate MsgId raises a unique-violation
// which the caller treats as "already seen" — a single round-trip
// instead of an explicit existence check + insert race.
public sealed class SqlSeenMessageStore
{
    private readonly SettingsDbContext _db;

    public SqlSeenMessageStore(SettingsDbContext db)
    {
        _db = db;
    }

    // Returns true if this is the first time we've seen MsgId
    // (insertion succeeded), false if the row already existed.
    // Consumers gate their side-effect logic on this return.
    public async Task<bool> TryMarkSeenAsync(
        Guid msgId,
        string subject,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        // Fast path: existence check before insert. The unique
        // constraint on MsgId catches the race; the existence check
        // saves a DB round-trip in the common "first delivery"
        // scenario.
        var exists = await _db.Set<SeenMessage>().AnyAsync(x => x.MsgId == msgId, ct);
        if (exists)
        {
            return false;
        }

        var row = new SeenMessage
        {
            MsgId = msgId,
            Subject = subject,
            SeenAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(ttl),
        };

        _db.Set<SeenMessage>().Add(row);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Race lost: another worker inserted the same MsgId between
            // our AnyAsync and SaveChangesAsync. The competing worker
            // is processing the message; we are not.
            return false;
        }
    }

    // Purge expired rows. Called from the cleanup hosted service. The
    // batch cap keeps a large purge from monopolizing the DB.
    public async Task<int> PurgeExpiredAsync(int batchSize, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expired = await _db.Set<SeenMessage>()
            .Where(x => x.ExpiresAt < now)
            .OrderBy(x => x.ExpiresAt)
            .Take(batchSize)
            .ToListAsync(ct);

        if (expired.Count == 0)
        {
            return 0;
        }

        _db.Set<SeenMessage>().RemoveRange(expired);
        await _db.SaveChangesAsync(ct);
        return expired.Count;
    }
}
