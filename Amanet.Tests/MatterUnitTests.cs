using System.ComponentModel.DataAnnotations;
using Amanet.Application.DTOs.Matters;
using Amanet.Application.Exceptions;
using Amanet.Application.Services;
using Amanet.Core.Entities;

namespace Amanet.Tests;

public sealed class MatterUnitTests
{
    [Fact]
    public void Active_matter_can_be_changed()
    {
        var matter = new Matter();

        var exception = Record.Exception(() => MatterRules.EnsureWritable(matter));

        Assert.Null(exception);
    }

    [Fact]
    public void Archived_matter_cannot_be_changed()
    {
        var matter = new Matter { ArchivedAtUtc = DateTimeOffset.UnixEpoch };

        Assert.Throws<ConflictException>(() => MatterRules.EnsureWritable(matter));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    public void Target_close_date_may_be_omitted_or_on_or_after_the_opened_date(int? daysAfterOpening)
    {
        var request = CreateMatterRequest(daysAfterOpening);

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void Target_close_date_cannot_precede_the_opened_date()
    {
        var request = CreateMatterRequest(daysAfterOpening: -1);

        var error = Assert.Single(Validate(request));

        Assert.Equal(nameof(MatterRequest.TargetCloseDate), Assert.Single(error.MemberNames));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 100)]
    public void Pagination_accepts_the_minimum_and_maximum_page_sizes(int page, int pageSize)
    {
        var query = new MatterListQuery { Page = page, PageSize = pageSize };

        Assert.Empty(Validate(query));
    }

    [Theory]
    [InlineData(0, 20, nameof(MatterListQuery.Page))]
    [InlineData(1, 101, nameof(MatterListQuery.PageSize))]
    public void Pagination_rejects_values_outside_the_allowed_range(int page, int pageSize, string invalidField)
    {
        var query = new MatterListQuery { Page = page, PageSize = pageSize };

        var error = Assert.Single(Validate(query));

        Assert.Equal(invalidField, Assert.Single(error.MemberNames));
    }

    private static MatterRequest CreateMatterRequest(int? daysAfterOpening)
    {
        var openedDate = new DateOnly(2026, 1, 15);
        return new MatterRequest
        {
            ReferenceNumber = "MAT-001",
            Title = "Contract review",
            Description = "Review the draft agreement.",
            PrimaryClientId = 1,
            OpenedDate = openedDate,
            TargetCloseDate = daysAfterOpening is int days ? openedDate.AddDays(days) : null
        };
    }

    private static List<ValidationResult> Validate(object model)
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), errors, validateAllProperties: true);
        return errors;
    }
}
