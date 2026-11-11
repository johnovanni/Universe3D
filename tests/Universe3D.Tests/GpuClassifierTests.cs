using Universe3D.Core.Display;

namespace Universe3D.Tests;

public class GpuClassifierTests
{
    [Theory]
    [InlineData("Intel(R) Iris(R) Xe Graphics", 20, GpuClass.Recommended)]
    [InlineData("Intel(R) UHD Graphics 620", 4, GpuClass.Minimum)]
    [InlineData("NVIDIA GeForce RTX 3050 Laptop GPU/PCIe/SSE2", 30, GpuClass.Recommended)]
    [InlineData("Unknown GPU", 5, GpuClass.Recommended)]
    [InlineData("Unknown GPU", 12, GpuClass.Minimum)]
    public void Classify(string renderer, double medianFrameMs, GpuClass expected) =>
        Assert.Equal(expected, GpuClassifier.Classify(renderer, medianFrameMs));

    [Theory]
    [InlineData("DisplayLink USB Device", true)]
    [InlineData("Intel(R) Iris(R) Xe Graphics", false)]
    public void UsbAdapter(string adapter, bool expected) =>
        Assert.Equal(expected, DisplayAdapterInfo.IsUsbDisplayAdapter(adapter));
}
