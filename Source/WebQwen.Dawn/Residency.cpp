#include "wq_dawn.h"

#include <dawn/native/D3D12Backend.h>

#include <map>
#include <mutex>
#include <vector>

using Microsoft::WRL::ComPtr;

namespace {

constexpr uint64_t kMinTrackedSize = 1 << 20;

struct Entry {
    ComPtr<ID3D12Resource> resource;
    // MakeResident and Evict are reference-counted, so each resource is only
    // ever moved from one state to the other.
    bool resident = true;
};

struct Device {
    // Held here because a destroyed WGPUDevice no longer hands it out.
    ComPtr<ID3D12Device> d3d;
    // Dawn keeps imported memory until the device is released, whether or not
    // its buffer is still alive, so the list only grows.
    std::vector<Entry> entries;
};

std::mutex g_mutex;
std::map<WGPUDevice, Device> g_devices;

bool Tracked(WGPUDevice device, const WGPUBufferDescriptor& d) {
    return (d.usage & WGPUBufferUsage_Storage) && !(d.usage & (WGPUBufferUsage_MapRead | WGPUBufferUsage_MapWrite)) &&
           !d.mappedAtCreation && d.size >= kMinTrackedSize &&
           wgpuDeviceHasFeature(device, WGPUFeatureName_SharedBufferMemoryD3D12Resource);
}

// Dawn sizes its own buffers this way; views and clears assume it.
uint64_t ResourceSize(const WGPUBufferDescriptor& d) {
    uint64_t align = (d.usage & WGPUBufferUsage_Uniform) ? 256 : 4;
    return (d.size + align - 1) / align * align;
}

int32_t Move(Device& device, bool toResident) {
    std::vector<ID3D12Pageable*> pageables;
    for (auto& e : device.entries)
        if (e.resident != toResident) pageables.push_back(e.resource.Get());
    if (pageables.empty()) return 0;
    HRESULT hr = toResident ? device.d3d->MakeResident((UINT)pageables.size(), pageables.data())
                            : device.d3d->Evict((UINT)pageables.size(), pageables.data());
    if (FAILED(hr)) return -1;
    for (auto& e : device.entries) e.resident = toResident;
    return (int32_t)pageables.size();
}

int32_t Move(WGPUDevice device, bool toResident) {
    std::lock_guard<std::mutex> lock(g_mutex);
    auto it = g_devices.find(device);
    return it == g_devices.end() ? 0 : Move(it->second, toResident);
}

}  // namespace

extern "C" WGPUBuffer wqDawnCreateBuffer(WGPUDevice device, const WGPUBufferDescriptor* descriptor) {
    if (!Tracked(device, *descriptor)) return wgpuDeviceCreateBuffer(device, descriptor);
    ComPtr<ID3D12Device> d3d = dawn::native::d3d12::GetD3D12Device(device);
    D3D12_HEAP_PROPERTIES heap{D3D12_HEAP_TYPE_DEFAULT};
    D3D12_RESOURCE_DESC rd{};
    rd.Dimension = D3D12_RESOURCE_DIMENSION_BUFFER;
    rd.Width = ResourceSize(*descriptor);
    rd.Height = rd.DepthOrArraySize = rd.MipLevels = 1;
    rd.SampleDesc.Count = 1;
    rd.Layout = D3D12_TEXTURE_LAYOUT_ROW_MAJOR;
    rd.Flags = D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS;
    ComPtr<ID3D12Resource> resource;
    if (!d3d || FAILED(d3d->CreateCommittedResource(&heap, D3D12_HEAP_FLAG_NONE, &rd, D3D12_RESOURCE_STATE_COMMON, nullptr,
                                                    IID_PPV_ARGS(&resource))))
        return wgpuDeviceCreateBuffer(device, descriptor);

    dawn::native::d3d12::SharedBufferMemoryD3D12ResourceDescriptor imported;
    imported.resource = resource;
    WGPUSharedBufferMemoryDescriptor md = WGPU_SHARED_BUFFER_MEMORY_DESCRIPTOR_INIT;
    md.nextInChain = reinterpret_cast<WGPUChainedStruct*>(&imported);
    md.label = descriptor->label;
    WGPUSharedBufferMemory memory = wgpuDeviceImportSharedBufferMemory(device, &md);
    WGPUBuffer buffer = wgpuSharedBufferMemoryCreateBuffer(memory, descriptor);
    // Access stays open for the buffer's whole life: only this device uses it.
    WGPUSharedBufferMemoryBeginAccessDescriptor access = WGPU_SHARED_BUFFER_MEMORY_BEGIN_ACCESS_DESCRIPTOR_INIT;
    WGPUStatus status = wgpuSharedBufferMemoryBeginAccess(memory, buffer, &access);
    wgpuSharedBufferMemoryRelease(memory);
    if (status != WGPUStatus_Success) {
        wgpuBufferRelease(buffer);
        return wgpuDeviceCreateBuffer(device, descriptor);
    }

    std::lock_guard<std::mutex> lock(g_mutex);
    Device& tracked = g_devices[device];
    if (!tracked.d3d) tracked.d3d = d3d;
    tracked.entries.push_back({resource});
    return buffer;
}

extern "C" int32_t wqDawnEvict(WGPUDevice device) { return Move(device, false); }

extern "C" int32_t wqDawnMakeResident(WGPUDevice device) { return Move(device, true); }

extern "C" void wqDawnReleaseDevice(WGPUDevice device) {
    std::lock_guard<std::mutex> lock(g_mutex);
    auto it = g_devices.find(device);
    if (it == g_devices.end()) return;
    // Dawn may hold the memory a while longer; evicted, it no longer takes VRAM.
    Move(it->second, false);
    g_devices.erase(it);
}
