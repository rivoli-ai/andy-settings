using Andy.Settings.Application.DTOs.Common;
using FluentAssertions;

namespace Andy.Settings.Tests.Unit.DTOs;

// rivoli-ai/andy-settings#134. Unvalidated page/pageSize produced a negative
// SQL OFFSET, which SQLite clamps (silently serving page 1) and PostgreSQL
// rejects (500) — the same request behaving differently in the embedded and
// shared deployments.
public class PagingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-500)]
    public void Normalize_PageBelowOne_ClampsToFirstPage(int page)
    {
        var (normalizedPage, _) = Paging.Normalize(page, 25);

        normalizedPage.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Normalize_PageSizeBelowOne_FallsBackToDefault(int pageSize)
    {
        var (_, normalizedPageSize) = Paging.Normalize(1, pageSize);

        normalizedPageSize.Should().Be(Paging.DefaultPageSize);
    }

    [Fact]
    public void Normalize_PageSizeAboveMax_ClampsToMax()
    {
        var (_, pageSize) = Paging.Normalize(1, 1_000_000);

        pageSize.Should().Be(Paging.MaxPageSize);
    }

    [Fact]
    public void Normalize_ValidInput_PassesThroughUnchanged()
    {
        var (page, pageSize) = Paging.Normalize(3, 50);

        page.Should().Be(3);
        pageSize.Should().Be(50);
    }

    // The offset is what actually reached the database. Guarding it here is
    // the point of the helper.
    [Theory]
    [InlineData(0, 25)]
    [InlineData(-5, 25)]
    [InlineData(1, -1)]
    public void Normalize_NeverProducesNegativeOffset(int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize);

        ((normalizedPage - 1) * normalizedPageSize).Should().BeGreaterThanOrEqualTo(0);
    }
}
