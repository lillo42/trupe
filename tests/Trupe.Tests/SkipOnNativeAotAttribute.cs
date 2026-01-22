using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Trupe.Tests;

public class SkipOnNativeAotAttribute : SkipAttribute
{
    public SkipOnNativeAotAttribute()
        : base("Test skipped because NativeAOT does not support dynamic code.") { }

    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        // If Dynamic Code is NOT supported, we are in AOT mode -> Skip = true
        bool isAot = !RuntimeFeature.IsDynamicCodeSupported;
        return Task.FromResult(isAot);
    }
}
