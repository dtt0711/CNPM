using FluentAssertions;
using NetArchTest.Rules;
using TourismHcm.Api.Controllers.v1;
using TourismHcm.Contracts.Places;
using TourismHcm.Domain.Exceptions;
using TourismHcm.Infrastructure.Persistence;
using ApplicationComposition = TourismHcm.Application.DependencyInjection;
using InfrastructureComposition = TourismHcm.Infrastructure.DependencyInjection;

namespace TourismHcm.ArchitectureTests;

/// <summary>
/// Architecture/layering guardrails for the TourismHcm solution:
///   - Domain must not depend on any other TourismHcm project, EF Core, or ASP.NET Core.
///   - Application must not depend on EF Core, ASP.NET Core, or Infrastructure.
///   - Infrastructure must not depend on Api, and Contracts must not depend on Application,
///     Infrastructure, or Api.
///   - Api controllers must never use the EF Core DbContext or the Infrastructure assembly.
/// </summary>
public sealed class LayeringTests
{
    [Fact]
    public void Domain_Should_Not_Depend_On_Other_TourismHcm_Projects()
    {
        var result = Types.InAssembly(typeof(DomainException).Assembly)
            .Should()
            .NotHaveDependencyOnAny(
                "TourismHcm.Application",
                "TourismHcm.Contracts",
                "TourismHcm.Infrastructure",
                "TourismHcm.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Application_Should_Not_Depend_On_EfCore()
    {
        var result = Types.InAssembly(typeof(ApplicationComposition).Assembly)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Application_Should_Not_Depend_On_AspNetCore()
    {
        var result = Types.InAssembly(typeof(ApplicationComposition).Assembly)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Controllers_Should_Not_Use_DbContext()
    {
        var result = Types.InAssembly(typeof(PlacesController).Assembly)
            .That()
            .ResideInNamespace("TourismHcm.Api.Controllers.v1")
            .Should()
            .NotHaveDependencyOn(typeof(TourismHcmDbContext).FullName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure()
    {
        var result = Types.InAssembly(typeof(ApplicationComposition).Assembly)
            .Should()
            .NotHaveDependencyOn("TourismHcm.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Api()
    {
        var result = Types.InAssembly(typeof(InfrastructureComposition).Assembly)
            .Should()
            .NotHaveDependencyOn("TourismHcm.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Contracts_Should_Not_Depend_On_Application_Infrastructure_Or_Api()
    {
        var result = Types.InAssembly(typeof(PlaceDto).Assembly)
            .Should()
            .NotHaveDependencyOnAny(
                "TourismHcm.Application",
                "TourismHcm.Infrastructure",
                "TourismHcm.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_EfCore_Or_AspNetCore()
    {
        var result = Types.InAssembly(typeof(DomainException).Assembly)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Controllers_Should_Not_Depend_On_Infrastructure()
    {
        var result = Types.InAssembly(typeof(PlacesController).Assembly)
            .That()
            .ResideInNamespace("TourismHcm.Api.Controllers.v1")
            .Should()
            .NotHaveDependencyOn("TourismHcm.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    private static string FormatFailures(TestResult result) =>
        result.FailingTypeNames is null
            ? "Unknown failure."
            : string.Join(", ", result.FailingTypeNames);
}
