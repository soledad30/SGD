using GestorDocumentoApp.Models;
using GestorDocumentoApp.Utils;
using GestorDocumentoApp.ViewModels;

namespace GestorDocumentoApp.Tests;

public class CoreBehaviorTests
{
    [Fact]
    public void GetDisplayName_Should_Return_Display_Attribute_Value()
    {
        var result = EnumHelper.GetDisplayName(PriorityCR.Immediate);

        Assert.Equal("Inmediato", result);
    }

    [Fact]
    public void GetDisplayNames_Extension_Should_Return_Display_Attribute_Value()
    {
        var result = StatusCR.Baselined.GetDisplayNames();

        Assert.Equal("En Linea base", result);
    }

    [Fact]
    public void PagedList_Should_Compute_Paging_Flags_Correctly()
    {
        var page = new PagedList<int>
        {
            Items = [1, 2, 3],
            PageNumber = 2,
            PageSize = 3,
            TotalCount = 8
        };

        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasPrevious);
        Assert.True(page.HasNext);
    }

    [Fact]
    public void PhaseHelper_Should_Return_Known_Phase_Name()
    {
        var phaseName = PhaseHelper.GetPhaseName(4);

        Assert.Equal("Implementación", phaseName);
    }
}
