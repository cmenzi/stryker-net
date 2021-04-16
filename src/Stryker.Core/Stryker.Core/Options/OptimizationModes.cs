using System;

namespace Stryker.Core.Options
{
    // Optimization options 
    [Flags]
    public enum OptimizationModes
    {
        NoOptimization = 0,
        SkipUncoveredMutants = 1,
        CoverageBasedTest = 2,
        DisableAbortTestOnKill = 4,
        CaptureCoveragePerTest = 8,
        DisableTestMix = 16
    }
}