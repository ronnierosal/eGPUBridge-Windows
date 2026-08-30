using eGPUBridge.App.Models;

namespace eGPUBridge.App.Services;

public interface IDisplayService
{
    DisplaySnapshot GetSnapshot();

    void ApplyTopology(DisplayTopology topology);
}

