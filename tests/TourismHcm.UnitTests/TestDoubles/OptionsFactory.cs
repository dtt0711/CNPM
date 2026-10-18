using Microsoft.Extensions.Options;

namespace TourismHcm.UnitTests.TestDoubles;

/// <summary>
/// Tiny helper to wrap a plain options POCO into <see cref="IOptions{TOptions}"/> without
/// pulling in the full configuration binding pipeline inside unit tests.
/// </summary>
public static class OptionsFactory
{
    public static IOptions<TOptions> Create<TOptions>(TOptions value) where TOptions : class =>
        Microsoft.Extensions.Options.Options.Create(value);
}
