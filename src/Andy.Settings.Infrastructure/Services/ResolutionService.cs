using Andy.Settings.Application.DTOs.Effective;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Andy.Settings.Infrastructure.Services;

public class ResolutionService : IResolutionService
{
    private readonly SettingsDbContext _db;

    public ResolutionService(SettingsDbContext db) => _db = db;

    public async Task<ResolvedSetting> ResolveAsync(string key, ResolutionContext context, CancellationToken ct = default)
    {
        return await ResolveInternal(key, context, includeSourceChain: false, ct);
    }

    public async Task<IReadOnlyList<ResolvedSetting>> ResolveBatchAsync(
        IEnumerable<string> keys, ResolutionContext context, CancellationToken ct = default)
    {
        var results = new List<ResolvedSetting>();
        foreach (var key in keys)
            results.Add(await ResolveInternal(key, context, includeSourceChain: false, ct));
        return results;
    }

    public async Task<ResolvedSetting> ExplainAsync(string key, ResolutionContext context, CancellationToken ct = default)
    {
        return await ResolveInternal(key, context, includeSourceChain: true, ct);
    }

    private async Task<ResolvedSetting> ResolveInternal(
        string key, ResolutionContext context, bool includeSourceChain, CancellationToken ct)
    {
        var definition = await _db.SettingDefinitions.FirstOrDefaultAsync(d => d.Key == key, ct);
        if (definition is null)
        {
            return new ResolvedSetting
            {
                Key = key,
                IsValid = false,
                ValidationMessage = $"Definition '{key}' not found."
            };
        }

        // Build scope candidates in precedence order (lowest to highest)
        var scopeCandidates = BuildScopeCandidates(context);

        // Query all matching assignments for this definition
        var assignments = await _db.SettingAssignments
            .Where(a => a.DefinitionId == definition.Id)
            .ToListAsync(ct);

        // Build source chain and find winner
        var sourceChain = new List<SourceChainEntry>();
        string? winningValue = null;
        ScopeType? winningScopeType = null;
        string? winningScopeId = null;
        bool isDefault = true;

        // Add default as base entry
        if (includeSourceChain && definition.DefaultValueJson is not null)
        {
            sourceChain.Add(new SourceChainEntry(ScopeType.Machine, null, definition.DefaultValueJson, false));
        }

        // Evaluate each scope level in precedence order
        foreach (var (scopeType, scopeId) in scopeCandidates)
        {
            var match = assignments.FirstOrDefault(a =>
                a.ScopeType == scopeType &&
                (scopeId is null ? a.ScopeId == null : a.ScopeId == scopeId));

            if (match is not null)
            {
                winningValue = match.ValueJson;
                winningScopeType = scopeType;
                winningScopeId = match.ScopeId;
                isDefault = false;
            }

            if (includeSourceChain && match is not null)
            {
                sourceChain.Add(new SourceChainEntry(scopeType, match.ScopeId, match.ValueJson, false));
            }
        }

        // Fall back to default if no assignment found
        if (isDefault)
        {
            winningValue = definition.DefaultValueJson;
        }

        // Mark the winner in the source chain
        if (includeSourceChain)
        {
            sourceChain = sourceChain.Select(e =>
                e.ScopeType == winningScopeType && e.ScopeId == winningScopeId && !isDefault
                    ? e with { IsWinner = true }
                    : e
            ).ToList();

            // If default wins, mark the default entry
            if (isDefault && sourceChain.Count > 0)
            {
                sourceChain[0] = sourceChain[0] with { IsWinner = true };
            }
        }

        return new ResolvedSetting
        {
            Key = key,
            EffectiveValue = definition.IsSecret ? null : winningValue,
            WinningScopeType = winningScopeType,
            WinningScopeId = winningScopeId,
            DataType = definition.DataType,
            IsSecret = definition.IsSecret,
            IsDefault = isDefault,
            SourceChain = includeSourceChain ? sourceChain : [],
            IsValid = true
        };
    }

    /// <summary>
    /// Builds scope candidates in ascending precedence order.
    /// </summary>
    private static List<(ScopeType ScopeType, string? ScopeId)> BuildScopeCandidates(ResolutionContext context)
    {
        var candidates = new List<(ScopeType, string?)>
        {
            (ScopeType.Machine, null)
        };

        if (!string.IsNullOrEmpty(context.ApplicationCode))
            candidates.Add((ScopeType.Application, context.ApplicationCode));

        if (!string.IsNullOrEmpty(context.ServiceCode))
            candidates.Add((ScopeType.Service, context.ServiceCode));

        if (!string.IsNullOrEmpty(context.UserId))
            candidates.Add((ScopeType.User, context.UserId));

        if (!string.IsNullOrEmpty(context.TeamId))
            candidates.Add((ScopeType.Team, context.TeamId));

        if (!string.IsNullOrEmpty(context.WorkspaceId))
            candidates.Add((ScopeType.Workspace, context.WorkspaceId));

        // RuntimeOverride is always checked (scopeId from any of the context fields)
        candidates.Add((ScopeType.RuntimeOverride, context.UserId));

        return candidates;
    }
}
