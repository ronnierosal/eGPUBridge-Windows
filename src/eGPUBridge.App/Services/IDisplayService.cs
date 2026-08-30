using eGPUBridge.App.Models;

namespace eGPUBridge.App.Services;

public interface IDisplayService
{
    DisplayTopology GetCurrentTopology();

    DisplaySnapshot GetSnapshot();

    void ApplyTopology(DisplayTopology topology);
}
