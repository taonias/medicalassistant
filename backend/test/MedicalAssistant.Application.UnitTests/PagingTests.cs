using MedicalAssistant.Application.Models;
using Shouldly;

namespace MedicalAssistant.Application.UnitTests.Features;

public class PagingTests
{
    [Fact]
    public void Normalize_uses_defaults_and_clamps()
    {
        Paging.Normalize(null, null).ShouldBe((1, Paging.DefaultPageSize));
        Paging.Normalize(0, 0).ShouldBe((1, 1));
        Paging.Normalize(-3, 999).ShouldBe((1, Paging.MaxPageSize));
        Paging.Normalize(4, 10).ShouldBe((4, 10));
    }

    [Fact]
    public void ClampPage_stays_within_available_pages()
    {
        Paging.ClampPage(5, 20, 0).ShouldBe(1);
        Paging.ClampPage(1, 20, 15).ShouldBe(1);
        Paging.ClampPage(9, 10, 25).ShouldBe(3);
    }
}
