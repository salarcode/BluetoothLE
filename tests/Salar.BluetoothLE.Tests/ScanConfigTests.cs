using Salar.BluetoothLE.Core.Models;
using Xunit;

namespace Salar.BluetoothLE.Tests;

public class ScanConfigTests
{
    [Fact]
    public void Default_HasExpectedDuration()
    {
        var config = ScanConfig.Default;
        Assert.Equal(TimeSpan.FromSeconds(10), config.Duration);
    }

    [Fact]
    public void Default_HasBalancedScanMode()
    {
        var config = ScanConfig.Default;
        Assert.Equal(ScanMode.Balanced, config.ScanMode);
    }

    [Fact]
    public void Default_AllowDuplicatesIsFalse()
    {
        var config = ScanConfig.Default;
        Assert.False(config.AllowDuplicates);
    }

    [Fact]
    public void Default_AndroidLegacyScanIsFalse()
    {
        var config = ScanConfig.Default;
        Assert.False(config.AndroidLegacyScan);
    }

    [Fact]
    public void Default_ServiceUuidFiltersIsEmpty()
    {
        var config = ScanConfig.Default;
        Assert.NotNull(config.ServiceUuidFilters);
        Assert.Empty(config.ServiceUuidFilters);
    }

    [Fact]
    public void CanCreate_WithCustomDuration()
    {
        var config = new ScanConfig { Duration = TimeSpan.FromSeconds(30) };
        Assert.Equal(TimeSpan.FromSeconds(30), config.Duration);
    }

    [Fact]
    public void CanCreate_WithServiceUuidFilters()
    {
        var uuid = Guid.NewGuid();
        var config = new ScanConfig { ServiceUuidFilters = new List<Guid> { uuid } };
        Assert.Single(config.ServiceUuidFilters);
        Assert.Equal(uuid, config.ServiceUuidFilters[0]);
    }

    [Fact]
    public void CanCreate_WithAllowDuplicatesTrue()
    {
        var config = new ScanConfig { AllowDuplicates = true };
        Assert.True(config.AllowDuplicates);
    }

    [Fact]
    public void CanCreate_WithAndroidLegacyScanTrue()
    {
        var config = new ScanConfig { AndroidLegacyScan = true };
        Assert.True(config.AndroidLegacyScan);
    }

    [Fact]
    public void CanCreate_WithLowLatencyScanMode()
    {
        var config = new ScanConfig { ScanMode = ScanMode.LowLatency };
        Assert.Equal(ScanMode.LowLatency, config.ScanMode);
    }

    [Fact]
    public void Default_IsSameInstanceEachTime()
    {
        var a = ScanConfig.Default;
        var b = ScanConfig.Default;
        Assert.Same(a, b);
    }
}
