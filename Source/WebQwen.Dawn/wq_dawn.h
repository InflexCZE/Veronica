// Residency of a device's large buffers, the only D3D12-aware part of
// webgpu_dawn.dll's surface. Buffers made through wqDawnCreateBuffer can be
// evicted from and restored to video memory as one set per device.
#pragma once
#include <dawn/webgpu.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

// Like wgpuDeviceCreateBuffer. Large storage buffers are backed by memory this
// library tracks; the rest are plain Dawn buffers.
WGPUBuffer wqDawnCreateBuffer(WGPUDevice device, const WGPUBufferDescriptor* descriptor);

// The GPU must have finished all work that uses the device's buffers.
// Returns the number of buffers evicted, or -1 on failure.
int32_t wqDawnEvict(WGPUDevice device);

// Blocks until the device's buffers are resident again. Returns the number of
// buffers made resident, or -1 on failure.
int32_t wqDawnMakeResident(WGPUDevice device);

// Evicts and stops tracking the device's buffers; call once the device is
// destroyed or before releasing it, with the GPU idle.
void wqDawnReleaseDevice(WGPUDevice device);

#ifdef __cplusplus
}
#endif
