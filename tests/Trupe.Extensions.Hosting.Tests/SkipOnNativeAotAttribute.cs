using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Trupe.Extensions.Hosting.Tests;

public class SkipOnNativeAotAttribute : SkipAttribute
{
    public SkipOnNativeAotAttribute()
        : base("Test skipped because NativeAOT does not support dynamic code.") { }

    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        bool isAot = !RuntimeFeature.IsDynamicCodeSupported;
        return Task.FromResult(isAot);
    }
}
