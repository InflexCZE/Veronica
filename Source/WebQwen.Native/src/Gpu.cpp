// WebGPU for the engine: the __gpu functions webgpu.js builds navigator.gpu
// on. Each call maps onto one Dawn function; objects cross to script as
// integer handles.
#include "Engine.h"

#include <cmath>
#include <cstring>

namespace wq {

uint32_t Gpu::Add(void* object, GpuKind kind) {
    if (!object) return 0;
    if (!freeHandles.empty()) {
        uint32_t id = freeHandles.back();
        freeHandles.pop_back();
        handles[id] = {object, kind};
        return id;
    }
    handles.push_back({object, kind});
    return (uint32_t)handles.size() - 1;
}

void* Gpu::Get(uint32_t id, GpuKind kind) const {
    if (id == 0 || id >= handles.size() || handles[id].kind != kind) return nullptr;
    return handles[id].object;
}

uint32_t Gpu::Find(void* object) const {
    for (uint32_t id = 1; id < handles.size(); id++)
        if (object && handles[id].object == object) return id;
    return 0;
}

void Gpu::Release(uint32_t id) {
    if (id == 0 || id >= handles.size() || handles[id].kind == GpuKind::Free) return;
    void* o = handles[id].object;
    switch (handles[id].kind) {
        case GpuKind::Adapter: wgpuAdapterRelease((WGPUAdapter)o); break;
        case GpuKind::Device: wgpuDeviceRelease((WGPUDevice)o); break;
        case GpuKind::Buffer: wgpuBufferRelease((WGPUBuffer)o); break;
        case GpuKind::ShaderModule: wgpuShaderModuleRelease((WGPUShaderModule)o); break;
        case GpuKind::BindGroupLayout: wgpuBindGroupLayoutRelease((WGPUBindGroupLayout)o); break;
        case GpuKind::PipelineLayout: wgpuPipelineLayoutRelease((WGPUPipelineLayout)o); break;
        case GpuKind::ComputePipeline: wgpuComputePipelineRelease((WGPUComputePipeline)o); break;
        case GpuKind::BindGroup: wgpuBindGroupRelease((WGPUBindGroup)o); break;
        case GpuKind::CommandEncoder: wgpuCommandEncoderRelease((WGPUCommandEncoder)o); break;
        case GpuKind::ComputePassEncoder: wgpuComputePassEncoderRelease((WGPUComputePassEncoder)o); break;
        case GpuKind::CommandBuffer: wgpuCommandBufferRelease((WGPUCommandBuffer)o); break;
        case GpuKind::QuerySet: wgpuQuerySetRelease((WGPUQuerySet)o); break;
        case GpuKind::Free: break;
    }
    handles[id] = {nullptr, GpuKind::Free};
    freeHandles.push_back(id);
}

void Gpu::ReleaseAll() {
    // Children before the device, the device before the instance.
    for (uint32_t id = 1; id < handles.size(); id++)
        if (handles[id].kind != GpuKind::Device && handles[id].kind != GpuKind::Adapter) Release(id);
    if (queue) wgpuQueueRelease(queue);
    queue = nullptr;
    for (uint32_t id = 1; id < handles.size(); id++) Release(id);
    if (device) wgpuDeviceRelease(device);
    device = nullptr;
    if (adapter) wgpuAdapterRelease(adapter);
    adapter = nullptr;
    if (instance) wgpuInstanceRelease(instance);
    instance = nullptr;
}

namespace {

std::string Text(WGPUStringView v) {
    if (!v.data) return {};
    return v.length == WGPU_STRLEN ? std::string(v.data) : std::string(v.data, v.length);
}

v8::Local<v8::Context> Ctx(const Info& info) { return info.GetIsolate()->GetCurrentContext(); }

double Num(const Info& info, int i) { return info[i]->NumberValue(Ctx(info)).FromMaybe(NAN); }

uint32_t U32(const Info& info, int i) { return (uint32_t)Num(info, i); }

uint64_t U64(const Info& info, int i) {
    double v = Num(info, i);
    return v >= 0 ? (uint64_t)v : WGPU_WHOLE_SIZE;
}

template <typename T>
T Obj(const Info& info, int i, GpuKind kind, const char* what) {
    T o = (T)FromInfo(info)->gpu.Get(U32(info, i), kind);
    if (!o) Throw(info, std::string("invalid ") + what);
    return o;
}

struct Named {
    const char* name;
    WGPUFeatureName value;
};

const Named kFeatures[] = {
    {"depth-clip-control", WGPUFeatureName_DepthClipControl},
    {"depth32float-stencil8", WGPUFeatureName_Depth32FloatStencil8},
    {"texture-compression-bc", WGPUFeatureName_TextureCompressionBC},
    {"texture-compression-bc-sliced-3d", WGPUFeatureName_TextureCompressionBCSliced3D},
    {"texture-compression-etc2", WGPUFeatureName_TextureCompressionETC2},
    {"texture-compression-astc", WGPUFeatureName_TextureCompressionASTC},
    {"timestamp-query", WGPUFeatureName_TimestampQuery},
    {"indirect-first-instance", WGPUFeatureName_IndirectFirstInstance},
    {"shader-f16", WGPUFeatureName_ShaderF16},
    {"rg11b10ufloat-renderable", WGPUFeatureName_RG11B10UfloatRenderable},
    {"bgra8unorm-storage", WGPUFeatureName_BGRA8UnormStorage},
    {"float32-filterable", WGPUFeatureName_Float32Filterable},
    {"float32-blendable", WGPUFeatureName_Float32Blendable},
    {"clip-distances", WGPUFeatureName_ClipDistances},
    {"dual-source-blending", WGPUFeatureName_DualSourceBlending},
    {"subgroups", WGPUFeatureName_Subgroups},
    {"subgroup-size-control", WGPUFeatureName_SubgroupSizeControl},
};

struct LimitField {
    const char* name;
    size_t offset;
    bool is64;
};

#define LIMIT32(f) {#f, offsetof(WGPULimits, f), false}
#define LIMIT64(f) {#f, offsetof(WGPULimits, f), true}
const LimitField kLimits[] = {
    LIMIT32(maxTextureDimension1D),
    LIMIT32(maxTextureDimension2D),
    LIMIT32(maxTextureDimension3D),
    LIMIT32(maxTextureArrayLayers),
    LIMIT32(maxBindGroups),
    LIMIT32(maxBindGroupsPlusVertexBuffers),
    LIMIT32(maxBindingsPerBindGroup),
    LIMIT32(maxDynamicUniformBuffersPerPipelineLayout),
    LIMIT32(maxDynamicStorageBuffersPerPipelineLayout),
    LIMIT32(maxSampledTexturesPerShaderStage),
    LIMIT32(maxSamplersPerShaderStage),
    LIMIT32(maxStorageBuffersPerShaderStage),
    LIMIT32(maxStorageTexturesPerShaderStage),
    LIMIT32(maxUniformBuffersPerShaderStage),
    LIMIT64(maxUniformBufferBindingSize),
    LIMIT64(maxStorageBufferBindingSize),
    LIMIT32(minUniformBufferOffsetAlignment),
    LIMIT32(minStorageBufferOffsetAlignment),
    LIMIT32(maxVertexBuffers),
    LIMIT64(maxBufferSize),
    LIMIT32(maxVertexAttributes),
    LIMIT32(maxVertexBufferArrayStride),
    LIMIT32(maxInterStageShaderVariables),
    LIMIT32(maxColorAttachments),
    LIMIT32(maxColorAttachmentBytesPerSample),
    LIMIT32(maxComputeWorkgroupStorageSize),
    LIMIT32(maxComputeInvocationsPerWorkgroup),
    LIMIT32(maxComputeWorkgroupSizeX),
    LIMIT32(maxComputeWorkgroupSizeY),
    LIMIT32(maxComputeWorkgroupSizeZ),
    LIMIT32(maxComputeWorkgroupsPerDimension),
};

v8::Local<v8::Array> FeatureList(v8::Isolate* isolate, const WGPUSupportedFeatures& f) {
    auto ctx = isolate->GetCurrentContext();
    auto list = v8::Array::New(isolate);
    uint32_t n = 0;
    for (size_t i = 0; i < f.featureCount; i++)
        for (auto& k : kFeatures)
            if (f.features[i] == k.value) (void)list->Set(ctx, n++, Str(isolate, k.name));
    return list;
}

v8::Local<v8::Object> LimitsObject(v8::Isolate* isolate, const WGPULimits& l) {
    auto ctx = isolate->GetCurrentContext();
    auto o = v8::Object::New(isolate);
    for (auto& f : kLimits) {
        double v = f.is64 ? (double)*(const uint64_t*)((const char*)&l + f.offset) : *(const uint32_t*)((const char*)&l + f.offset);
        (void)o->Set(ctx, Str(isolate, f.name), v8::Number::New(isolate, v));
    }
    return o;
}

// A promise settled from a Dawn callback. While it is outstanding the
// engine loop keeps polling Dawn.
struct Pending {
    Engine* engine;
    v8::Global<v8::Promise::Resolver> resolver;
};

Pending* NewPending(const Info& info) {
    Engine* engine = FromInfo(info);
    auto resolver = v8::Promise::Resolver::New(Ctx(info)).ToLocalChecked();
    info.GetReturnValue().Set(resolver->GetPromise());
    engine->gpu.pending++;
    return new Pending{engine, v8::Global<v8::Promise::Resolver>(info.GetIsolate(), resolver)};
}

// make runs on the engine thread with V8 entered; an empty result rejects
// with error.
void Settle(Pending* pending, std::string error, std::function<v8::Local<v8::Value>(v8::Isolate*)> make) {
    pending->engine->Complete([pending, error = std::move(error), make = std::move(make)] {
        Engine* engine = pending->engine;
        engine->gpu.pending--;
        auto* isolate = engine->isolate;
        auto ctx = isolate->GetCurrentContext();
        auto resolver = pending->resolver.Get(isolate);
        if (error.empty()) (void)resolver->Resolve(ctx, make ? make(isolate) : v8::Undefined(isolate).As<v8::Value>());
        else (void)resolver->Reject(ctx, v8::Exception::Error(Str(isolate, error)));
        pending->resolver.Reset();
        delete pending;
    });
}

void CallGlobal(Engine* engine, const char* name, std::vector<v8::Local<v8::Value>> args) {
    auto* isolate = engine->isolate;
    auto ctx = isolate->GetCurrentContext();
    v8::Local<v8::Value> fn;
    if (!ctx->Global()->Get(ctx, Str(isolate, name)).ToLocal(&fn) || !fn->IsFunction()) return;
    v8::TryCatch tc(isolate);
    (void)fn.As<v8::Function>()->Call(ctx, ctx->Global(), (int)args.size(), args.data());
}

// ---- adapter and device ---------------------------------------------------

void RequestAdapter(const Info& info) {
    Engine* engine = FromInfo(info);
    Gpu& gpu = engine->gpu;
    auto* pending = NewPending(info);
    if (!gpu.instance) {
        WGPUInstanceDescriptor d = WGPU_INSTANCE_DESCRIPTOR_INIT;
        gpu.instance = wgpuCreateInstance(&d);
        if (!gpu.instance) return Settle(pending, "wgpuCreateInstance failed", nullptr);
    }
    WGPURequestAdapterOptions o = WGPU_REQUEST_ADAPTER_OPTIONS_INIT;
    o.powerPreference = Utf8(info.GetIsolate(), info[0]) == "low-power" ? WGPUPowerPreference_LowPower : WGPUPowerPreference_HighPerformance;
    o.backendType = WGPUBackendType_D3D12;
    WGPURequestAdapterCallbackInfo cb = WGPU_REQUEST_ADAPTER_CALLBACK_INFO_INIT;
    cb.mode = WGPUCallbackMode_AllowProcessEvents;
    cb.callback = [](WGPURequestAdapterStatus status, WGPUAdapter adapter, WGPUStringView, void* u1, void*) {
        auto* pending = (Pending*)u1;
        Settle(pending, "", [pending, status, adapter](v8::Isolate* isolate) -> v8::Local<v8::Value> {
            if (status != WGPURequestAdapterStatus_Success || !adapter) return v8::Null(isolate);
            Gpu& gpu = pending->engine->gpu;
            if (gpu.adapter) wgpuAdapterRelease(gpu.adapter);
            gpu.adapter = adapter;
            wgpuAdapterAddRef(adapter);
            return v8::Number::New(isolate, gpu.Add(adapter, GpuKind::Adapter));
        });
    };
    cb.userdata1 = pending;
    wgpuInstanceRequestAdapter(gpu.instance, &o, cb);
}

void AdapterInfo(const Info& info) {
    auto* isolate = info.GetIsolate();
    auto adapter = Obj<WGPUAdapter>(info, 0, GpuKind::Adapter, "adapter");
    if (!adapter) return;
    auto ctx = Ctx(info);
    WGPUSupportedFeatures features = WGPU_SUPPORTED_FEATURES_INIT;
    wgpuAdapterGetFeatures(adapter, &features);
    WGPULimits limits = WGPU_LIMITS_INIT;
    wgpuAdapterGetLimits(adapter, &limits);
    WGPUAdapterInfo ai = WGPU_ADAPTER_INFO_INIT;
    wgpuAdapterGetInfo(adapter, &ai);
    auto adapterInfo = v8::Object::New(isolate);
    (void)adapterInfo->Set(ctx, Str(isolate, "vendor"), Str(isolate, Text(ai.vendor)));
    (void)adapterInfo->Set(ctx, Str(isolate, "architecture"), Str(isolate, Text(ai.architecture)));
    (void)adapterInfo->Set(ctx, Str(isolate, "device"), Str(isolate, Text(ai.device)));
    (void)adapterInfo->Set(ctx, Str(isolate, "description"), Str(isolate, Text(ai.description)));
    (void)adapterInfo->Set(ctx, Str(isolate, "subgroupMinSize"), v8::Number::New(isolate, ai.subgroupMinSize));
    (void)adapterInfo->Set(ctx, Str(isolate, "subgroupMaxSize"), v8::Number::New(isolate, ai.subgroupMaxSize));
    (void)adapterInfo->Set(ctx, Str(isolate, "isFallbackAdapter"), v8::Boolean::New(isolate, ai.adapterType == WGPUAdapterType_CPU));
    auto o = v8::Object::New(isolate);
    (void)o->Set(ctx, Str(isolate, "features"), FeatureList(isolate, features));
    (void)o->Set(ctx, Str(isolate, "limits"), LimitsObject(isolate, limits));
    (void)o->Set(ctx, Str(isolate, "info"), adapterInfo);
    wgpuSupportedFeaturesFreeMembers(features);
    wgpuAdapterInfoFreeMembers(ai);
    info.GetReturnValue().Set(o);
}

void RequestDevice(const Info& info) {
    Engine* engine = FromInfo(info);
    auto* isolate = info.GetIsolate();
    auto ctx = Ctx(info);
    auto adapter = Obj<WGPUAdapter>(info, 0, GpuKind::Adapter, "adapter");
    if (!adapter) return;
    std::vector<WGPUFeatureName> features;
    if (info[1]->IsArray()) {
        auto list = info[1].As<v8::Array>();
        for (uint32_t i = 0; i < list->Length(); i++) {
            std::string name = Utf8(isolate, list->Get(ctx, i).ToLocalChecked());
            bool found = false;
            for (auto& k : kFeatures)
                if (name == k.name) features.push_back(k.value), found = true;
            if (!found) return Throw(info, "requestDevice: unsupported feature " + name);
        }
    }
    WGPULimits limits = WGPU_LIMITS_INIT;
    if (info[2]->IsObject()) {
        auto o = info[2].As<v8::Object>();
        auto names = o->GetOwnPropertyNames(ctx).ToLocalChecked();
        for (uint32_t i = 0; i < names->Length(); i++) {
            auto key = names->Get(ctx, i).ToLocalChecked();
            v8::Local<v8::Value> value;
            if (!o->Get(ctx, key).ToLocal(&value) || value->IsUndefined()) continue;
            std::string k = Utf8(isolate, key);
            double v = value->NumberValue(ctx).FromMaybe(NAN);
            const LimitField* field = nullptr;
            for (auto& f : kLimits)
                if (k == f.name) field = &f;
            if (!field || !(v >= 0)) return Throw(info, "requestDevice: unsupported limit " + k);
            if (field->is64) *(uint64_t*)((char*)&limits + field->offset) = (uint64_t)v;
            else *(uint32_t*)((char*)&limits + field->offset) = (uint32_t)v;
        }
    }
    WGPUDeviceDescriptor d = WGPU_DEVICE_DESCRIPTOR_INIT;
    d.requiredFeatureCount = features.size();
    d.requiredFeatures = features.data();
    d.requiredLimits = &limits;
    d.deviceLostCallbackInfo.mode = WGPUCallbackMode_AllowSpontaneous;
    d.deviceLostCallbackInfo.callback = [](WGPUDevice const* device, WGPUDeviceLostReason reason, WGPUStringView message, void* u1,
                                           void*) {
        // A device that was never created was never handed to script.
        if (reason == WGPUDeviceLostReason_CallbackCancelled || reason == WGPUDeviceLostReason_FailedCreation) return;
        auto* engine = (Engine*)u1;
        std::string text = Text(message);
        bool destroyed = reason == WGPUDeviceLostReason_Destroyed;
        WGPUDevice lost = device ? *device : nullptr;
        engine->Complete([engine, lost, text, destroyed] {
            auto* isolate = engine->isolate;
            CallGlobal(engine, "__gpuLost",
                       {v8::Number::New(isolate, engine->gpu.Find(lost)), Str(isolate, destroyed ? "destroyed" : "unknown"), Str(isolate, text)});
        });
    };
    d.deviceLostCallbackInfo.userdata1 = engine;
    d.uncapturedErrorCallbackInfo.callback = [](WGPUDevice const* device, WGPUErrorType type, WGPUStringView message, void* u1, void*) {
        auto* engine = (Engine*)u1;
        std::string text = Text(message);
        WGPUDevice source = device ? *device : nullptr;
        engine->Complete([engine, source, type, text] {
            auto* isolate = engine->isolate;
            CallGlobal(engine, "__gpuError",
                       {v8::Number::New(isolate, engine->gpu.Find(source)), v8::Number::New(isolate, (double)type), Str(isolate, text)});
        });
    };
    d.uncapturedErrorCallbackInfo.userdata1 = engine;
    auto* pending = NewPending(info);
    WGPURequestDeviceCallbackInfo cb = WGPU_REQUEST_DEVICE_CALLBACK_INFO_INIT;
    cb.mode = WGPUCallbackMode_AllowProcessEvents;
    cb.callback = [](WGPURequestDeviceStatus status, WGPUDevice device, WGPUStringView message, void* u1, void*) {
        auto* pending = (Pending*)u1;
        if (status != WGPURequestDeviceStatus_Success || !device)
            return Settle(pending, "requestDevice: " + Text(message), nullptr);
        Settle(pending, "", [pending, device](v8::Isolate* isolate) -> v8::Local<v8::Value> {
            Gpu& gpu = pending->engine->gpu;
            auto ctx = isolate->GetCurrentContext();
            if (gpu.queue) wgpuQueueRelease(gpu.queue);
            if (gpu.device) wgpuDeviceRelease(gpu.device);
            gpu.device = device;
            gpu.queue = wgpuDeviceGetQueue(device);
            wgpuDeviceAddRef(device);
            WGPUSupportedFeatures features = WGPU_SUPPORTED_FEATURES_INIT;
            wgpuDeviceGetFeatures(device, &features);
            WGPULimits limits = WGPU_LIMITS_INIT;
            wgpuDeviceGetLimits(device, &limits);
            auto o = v8::Object::New(isolate);
            (void)o->Set(ctx, Str(isolate, "handle"), v8::Number::New(isolate, gpu.Add(device, GpuKind::Device)));
            (void)o->Set(ctx, Str(isolate, "features"), FeatureList(isolate, features));
            (void)o->Set(ctx, Str(isolate, "limits"), LimitsObject(isolate, limits));
            wgpuSupportedFeaturesFreeMembers(features);
            return o;
        });
    };
    cb.userdata1 = pending;
    wgpuAdapterRequestDevice(adapter, &d, cb);
}

WGPUDevice Device(const Info& info) {
    WGPUDevice device = FromInfo(info)->gpu.device;
    if (!device) Throw(info, "no device");
    return device;
}

void DestroyDevice(const Info& info) {
    if (auto device = Obj<WGPUDevice>(info, 0, GpuKind::Device, "device")) wgpuDeviceDestroy(device);
}

void Release(const Info& info) { FromInfo(info)->gpu.Release(U32(info, 0)); }

void PushErrorScope(const Info& info) {
    WGPUDevice device = Device(info);
    if (!device) return;
    std::string filter = Utf8(info.GetIsolate(), info[0]);
    WGPUErrorFilter f = filter == "out-of-memory" ? WGPUErrorFilter_OutOfMemory
                        : filter == "internal"    ? WGPUErrorFilter_Internal
                                                  : WGPUErrorFilter_Validation;
    wgpuDevicePushErrorScope(device, f);
}

void PopErrorScope(const Info& info) {
    WGPUDevice device = Device(info);
    if (!device) return;
    auto* pending = NewPending(info);
    WGPUPopErrorScopeCallbackInfo cb = WGPU_POP_ERROR_SCOPE_CALLBACK_INFO_INIT;
    cb.mode = WGPUCallbackMode_AllowProcessEvents;
    cb.callback = [](WGPUPopErrorScopeStatus status, WGPUErrorType type, WGPUStringView message, void* u1, void*) {
        auto* pending = (Pending*)u1;
        if (status != WGPUPopErrorScopeStatus_Success) return Settle(pending, "popErrorScope: " + Text(message), nullptr);
        std::string text = Text(message);
        Settle(pending, "", [type, text](v8::Isolate* isolate) -> v8::Local<v8::Value> {
            if (type == WGPUErrorType_NoError) return v8::Null(isolate);
            auto ctx = isolate->GetCurrentContext();
            auto o = v8::Object::New(isolate);
            (void)o->Set(ctx, Str(isolate, "type"), v8::Number::New(isolate, (double)type));
            (void)o->Set(ctx, Str(isolate, "message"), Str(isolate, text));
            return o;
        });
    };
    cb.userdata1 = pending;
    wgpuDevicePopErrorScope(device, cb);
}

// ---- resources ------------------------------------------------------------

void SetLabel(const Info& info, int i, std::string& label) {
    if (info.Length() > i && info[i]->IsString()) label = Utf8(info.GetIsolate(), info[i]);
}

WGPUStringView View(const std::string& s) { return s.empty() ? WGPUStringView{nullptr, WGPU_STRLEN} : WGPUStringView{s.data(), s.size()}; }

// (size, usage, mappedAtCreation, label)
void CreateBuffer(const Info& info) {
    WGPUDevice device = Device(info);
    if (!device) return;
    std::string label;
    SetLabel(info, 3, label);
    WGPUBufferDescriptor d = WGPU_BUFFER_DESCRIPTOR_INIT;
    d.size = (uint64_t)Num(info, 0);
    d.usage = (WGPUBufferUsage)(uint64_t)Num(info, 1);
    d.mappedAtCreation = info[2]->BooleanValue(info.GetIsolate());
    d.label = View(label);
    info.GetReturnValue().Set(FromInfo(info)->gpu.Add(wgpuDeviceCreateBuffer(device, &d), GpuKind::Buffer));
}

void DestroyBuffer(const Info& info) {
    if (auto b = Obj<WGPUBuffer>(info, 0, GpuKind::Buffer, "buffer")) wgpuBufferDestroy(b);
}

// (code, label)
void CreateShaderModule(const Info& info) {
    WGPUDevice device = Device(info);
    if (!device) return;
    std::string code = Utf8(info.GetIsolate(), info[0]), label;
    SetLabel(info, 1, label);
    WGPUShaderSourceWGSL w = WGPU_SHADER_SOURCE_WGSL_INIT;
    w.code = {code.data(), code.size()};
    WGPUShaderModuleDescriptor d = WGPU_SHADER_MODULE_DESCRIPTOR_INIT;
    d.nextInChain = &w.chain;
    d.label = View(label);
    info.GetReturnValue().Set(FromInfo(info)->gpu.Add(wgpuDeviceCreateShaderModule(device, &d), GpuKind::ShaderModule));
}

bool Doubles(const Info& info, int i, size_t stride, const double** data, size_t* rows) {
    uint8_t* bytes;
    size_t length;
    if (!info[i]->IsFloat64Array() || !Bytes(info[i], &bytes, &length) || (length / sizeof(double)) % stride) {
        Throw(info, "expected a Float64Array");
        return false;
    }
    *data = (const double*)bytes;
    *rows = length / sizeof(double) / stride;
    return true;
}

// Float64Array of (binding, visibility, bufferType, hasDynamicOffset, minBindingSize) rows
void CreateBindGroupLayout(const Info& info) {
    WGPUDevice device = Device(info);
    if (!device) return;
    const double* p;
    size_t n;
    if (!Doubles(info, 0, 5, &p, &n)) return;
    std::vector<WGPUBindGroupLayoutEntry> e(n);
    for (size_t i = 0; i < n; i++, p += 5) {
        e[i] = WGPU_BIND_GROUP_LAYOUT_ENTRY_INIT;
        e[i].binding = (uint32_t)p[0];
        e[i].visibility = (WGPUShaderStage)p[1];
        e[i].buffer.type = (WGPUBufferBindingType)(int)p[2];
        e[i].buffer.hasDynamicOffset = p[3] != 0;
        e[i].buffer.minBindingSize = (uint64_t)p[4];
    }
    WGPUBindGroupLayoutDescriptor d = WGPU_BIND_GROUP_LAYOUT_DESCRIPTOR_INIT;
    d.entryCount = e.size();
    d.entries = e.data();
    info.GetReturnValue().Set(FromInfo(info)->gpu.Add(wgpuDeviceCreateBindGroupLayout(device, &d), GpuKind::BindGroupLayout));
}

// array of bind group layout handles
void CreatePipelineLayout(const Info& info) {
    WGPUDevice device = Device(info);
    if (!device) return;
    if (!info[0]->IsArray()) return Throw(info, "createPipelineLayout: expected an array");
    auto list = info[0].As<v8::Array>();
    std::vector<WGPUBindGroupLayout> layouts(list->Length());
    for (uint32_t i = 0; i < list->Length(); i++) {
        uint32_t id = (uint32_t)list->Get(Ctx(info), i).ToLocalChecked()->NumberValue(Ctx(info)).FromMaybe(0);
        layouts[i] = (WGPUBindGroupLayout)FromInfo(info)->gpu.Get(id, GpuKind::BindGroupLayout);
        if (!layouts[i]) return Throw(info, "createPipelineLayout: invalid bind group layout");
    }
    WGPUPipelineLayoutDescriptor d = WGPU_PIPELINE_LAYOUT_DESCRIPTOR_INIT;
    d.bindGroupLayoutCount = layouts.size();
    d.bindGroupLayouts = layouts.data();
    info.GetReturnValue().Set(FromInfo(info)->gpu.Add(wgpuDeviceCreatePipelineLayout(device, &d), GpuKind::PipelineLayout));
}

// (layout handle or 0 for auto, module, entryPoint, constants [key, value, ...] or undefined, label)
struct PipelineArgs {
    std::string entry, label;
    std::vector<std::string> keys;
    std::vector<WGPUConstantEntry> constants;
    WGPUComputePipelineDescriptor d = WGPU_COMPUTE_PIPELINE_DESCRIPTOR_INIT;
};

bool ReadPipeline(const Info& info, PipelineArgs& a) {
    Gpu& gpu = FromInfo(info)->gpu;
    uint32_t layout = U32(info, 0);
    if (layout) {
        a.d.layout = (WGPUPipelineLayout)gpu.Get(layout, GpuKind::PipelineLayout);
        if (!a.d.layout) return Throw(info, "invalid pipeline layout"), false;
    }
    a.d.compute.module = Obj<WGPUShaderModule>(info, 1, GpuKind::ShaderModule, "shader module");
    if (!a.d.compute.module) return false;
    if (info[2]->IsString()) {
        a.entry = Utf8(info.GetIsolate(), info[2]);
        a.d.compute.entryPoint = {a.entry.data(), a.entry.size()};
    }
    if (info[3]->IsArray()) {
        auto list = info[3].As<v8::Array>();
        auto ctx = Ctx(info);
        a.keys.resize(list->Length() / 2);
        a.constants.resize(list->Length() / 2);
        for (uint32_t i = 0; i < a.constants.size(); i++) {
            a.keys[i] = Utf8(info.GetIsolate(), list->Get(ctx, 2 * i).ToLocalChecked());
            a.constants[i] = WGPU_CONSTANT_ENTRY_INIT;
            a.constants[i].key = {a.keys[i].data(), a.keys[i].size()};
            a.constants[i].value = list->Get(ctx, 2 * i + 1).ToLocalChecked()->NumberValue(ctx).FromMaybe(0);
        }
        a.d.compute.constantCount = a.constants.size();
        a.d.compute.constants = a.constants.data();
    }
    SetLabel(info, 4, a.label);
    a.d.label = View(a.label);
    return true;
}

void CreateComputePipeline(const Info& info) {
    WGPUDevice device = Device(info);
    PipelineArgs a;
    if (!device || !ReadPipeline(info, a)) return;
    info.GetReturnValue().Set(FromInfo(info)->gpu.Add(wgpuDeviceCreateComputePipeline(device, &a.d), GpuKind::ComputePipeline));
}

void CreateComputePipelineAsync(const Info& info) {
    WGPUDevice device = Device(info);
    PipelineArgs a;
    if (!device || !ReadPipeline(info, a)) return;
    auto* pending = NewPending(info);
    WGPUCreateComputePipelineAsyncCallbackInfo cb = WGPU_CREATE_COMPUTE_PIPELINE_ASYNC_CALLBACK_INFO_INIT;
    cb.mode = WGPUCallbackMode_AllowProcessEvents;
    cb.callback = [](WGPUCreatePipelineAsyncStatus status, WGPUComputePipeline pipeline, WGPUStringView message, void* u1, void*) {
        auto* pending = (Pending*)u1;
        if (status != WGPUCreatePipelineAsyncStatus_Success || !pipeline) {
            if (pipeline) wgpuComputePipelineRelease(pipeline);
            return Settle(pending, "createComputePipelineAsync: " + Text(message), nullptr);
        }
        Settle(pending, "", [pending, pipeline](v8::Isolate* isolate) -> v8::Local<v8::Value> {
            return v8::Number::New(isolate, pending->engine->gpu.Add(pipeline, GpuKind::ComputePipeline));
        });
    };
    cb.userdata1 = pending;
    wgpuDeviceCreateComputePipelineAsync(device, &a.d, cb);
}

void GetBindGroupLayout(const Info& info) {
    auto pipeline = Obj<WGPUComputePipeline>(info, 0, GpuKind::ComputePipeline, "compute pipeline");
    if (!pipeline) return;
    info.GetReturnValue().Set(
        FromInfo(info)->gpu.Add(wgpuComputePipelineGetBindGroupLayout(pipeline, U32(info, 1)), GpuKind::BindGroupLayout));
}

// (layout, Float64Array of (binding, buffer, offset, size or -1 for the rest) rows, label)
void CreateBindGroup(const Info& info) {
    WGPUDevice device = Device(info);
    if (!device) return;
    Gpu& gpu = FromInfo(info)->gpu;
    auto layout = Obj<WGPUBindGroupLayout>(info, 0, GpuKind::BindGroupLayout, "bind group layout");
    if (!layout) return;
    const double* p;
    size_t n;
    if (!Doubles(info, 1, 4, &p, &n)) return;
    std::vector<WGPUBindGroupEntry> e(n);
    for (size_t i = 0; i < n; i++, p += 4) {
        e[i] = WGPU_BIND_GROUP_ENTRY_INIT;
        e[i].binding = (uint32_t)p[0];
        e[i].buffer = (WGPUBuffer)gpu.Get((uint32_t)p[1], GpuKind::Buffer);
        if (!e[i].buffer) return Throw(info, "createBindGroup: invalid buffer");
        e[i].offset = (uint64_t)p[2];
        e[i].size = p[3] < 0 ? WGPU_WHOLE_SIZE : (uint64_t)p[3];
    }
    std::string label;
    SetLabel(info, 2, label);
    WGPUBindGroupDescriptor d = WGPU_BIND_GROUP_DESCRIPTOR_INIT;
    d.layout = layout;
    d.entryCount = e.size();
    d.entries = e.data();
    d.label = View(label);
    info.GetReturnValue().Set(gpu.Add(wgpuDeviceCreateBindGroup(device, &d), GpuKind::BindGroup));
}

// (type: "timestamp" | "occlusion", count)
void CreateQuerySet(const Info& info) {
    WGPUDevice device = Device(info);
    if (!device) return;
    WGPUQuerySetDescriptor d = WGPU_QUERY_SET_DESCRIPTOR_INIT;
    d.type = Utf8(info.GetIsolate(), info[0]) == "occlusion" ? WGPUQueryType_Occlusion : WGPUQueryType_Timestamp;
    d.count = U32(info, 1);
    info.GetReturnValue().Set(FromInfo(info)->gpu.Add(wgpuDeviceCreateQuerySet(device, &d), GpuKind::QuerySet));
}

void DestroyQuerySet(const Info& info) {
    if (auto q = Obj<WGPUQuerySet>(info, 0, GpuKind::QuerySet, "query set")) wgpuQuerySetDestroy(q);
}

// ---- mapping --------------------------------------------------------------

// (buffer, mode, offset, size)
void MapAsync(const Info& info) {
    auto buffer = Obj<WGPUBuffer>(info, 0, GpuKind::Buffer, "buffer");
    if (!buffer) return;
    auto* pending = NewPending(info);
    WGPUBufferMapCallbackInfo cb = WGPU_BUFFER_MAP_CALLBACK_INFO_INIT;
    cb.mode = WGPUCallbackMode_AllowProcessEvents;
    cb.callback = [](WGPUMapAsyncStatus status, WGPUStringView message, void* u1, void*) {
        Settle((Pending*)u1, status == WGPUMapAsyncStatus_Success ? "" : "mapAsync: " + Text(message), nullptr);
    };
    cb.userdata1 = pending;
    wgpuBufferMapAsync(buffer, (WGPUMapMode)U32(info, 1), (size_t)Num(info, 2), (size_t)Num(info, 3), cb);
}

// (buffer, offset, size, writable): an ArrayBuffer over the mapped memory.
// webgpu.js detaches it on unmap and keeps the buffer alive while mapped.
void GetMappedRange(const Info& info) {
    auto buffer = Obj<WGPUBuffer>(info, 0, GpuKind::Buffer, "buffer");
    if (!buffer) return;
    size_t offset = (size_t)Num(info, 1), size = (size_t)Num(info, 2);
    if (size == 0) return info.GetReturnValue().Set(v8::ArrayBuffer::New(info.GetIsolate(), 0));
    void* p = info[3]->BooleanValue(info.GetIsolate()) ? wgpuBufferGetMappedRange(buffer, offset, size)
                                                       : (void*)wgpuBufferGetConstMappedRange(buffer, offset, size);
    if (!p) return Throw(info, "getMappedRange: range is not mapped");
    auto store = v8::ArrayBuffer::NewBackingStore(p, size, [](void*, size_t, void*) {}, nullptr);
    info.GetReturnValue().Set(v8::ArrayBuffer::New(info.GetIsolate(), std::move(store)));
}

// (buffer, array of ArrayBuffers handed out by getMappedRange)
void Unmap(const Info& info) {
    auto buffer = Obj<WGPUBuffer>(info, 0, GpuKind::Buffer, "buffer");
    if (!buffer) return;
    if (info[1]->IsArray()) {
        auto list = info[1].As<v8::Array>();
        for (uint32_t i = 0; i < list->Length(); i++) {
            v8::Local<v8::Value> v;
            if (list->Get(Ctx(info), i).ToLocal(&v) && v->IsArrayBuffer()) (void)v.As<v8::ArrayBuffer>()->Detach(v8::Local<v8::Value>());
        }
    }
    wgpuBufferUnmap(buffer);
}

// ---- encoding and submission ----------------------------------------------

void CreateCommandEncoder(const Info& info) {
    WGPUDevice device = Device(info);
    if (!device) return;
    info.GetReturnValue().Set(FromInfo(info)->gpu.Add(wgpuDeviceCreateCommandEncoder(device, nullptr), GpuKind::CommandEncoder));
}

// (encoder, querySet or 0, beginningOfPassWriteIndex or -1, endOfPassWriteIndex or -1)
void BeginComputePass(const Info& info) {
    Gpu& gpu = FromInfo(info)->gpu;
    auto encoder = Obj<WGPUCommandEncoder>(info, 0, GpuKind::CommandEncoder, "command encoder");
    if (!encoder) return;
    WGPUComputePassDescriptor d = WGPU_COMPUTE_PASS_DESCRIPTOR_INIT;
    WGPUPassTimestampWrites tw = WGPU_PASS_TIMESTAMP_WRITES_INIT;
    if (uint32_t qs = U32(info, 1)) {
        tw.querySet = (WGPUQuerySet)gpu.Get(qs, GpuKind::QuerySet);
        if (!tw.querySet) return Throw(info, "beginComputePass: invalid query set");
        double begin = Num(info, 2), end = Num(info, 3);
        tw.beginningOfPassWriteIndex = begin >= 0 ? (uint32_t)begin : WGPU_QUERY_SET_INDEX_UNDEFINED;
        tw.endOfPassWriteIndex = end >= 0 ? (uint32_t)end : WGPU_QUERY_SET_INDEX_UNDEFINED;
        d.timestampWrites = &tw;
    }
    info.GetReturnValue().Set(gpu.Add(wgpuCommandEncoderBeginComputePass(encoder, &d), GpuKind::ComputePassEncoder));
}

void SetPipeline(const Info& info) {
    auto pass = Obj<WGPUComputePassEncoder>(info, 0, GpuKind::ComputePassEncoder, "compute pass");
    auto pipeline = pass ? Obj<WGPUComputePipeline>(info, 1, GpuKind::ComputePipeline, "compute pipeline") : nullptr;
    if (pipeline) wgpuComputePassEncoderSetPipeline(pass, pipeline);
}

// (pass, index, bindGroup or 0, Uint32Array of dynamic offsets or undefined)
void SetBindGroup(const Info& info) {
    Gpu& gpu = FromInfo(info)->gpu;
    auto pass = Obj<WGPUComputePassEncoder>(info, 0, GpuKind::ComputePassEncoder, "compute pass");
    if (!pass) return;
    uint32_t id = U32(info, 2);
    auto group = (WGPUBindGroup)gpu.Get(id, GpuKind::BindGroup);
    if (id && !group) return Throw(info, "setBindGroup: invalid bind group");
    const uint32_t* offsets = nullptr;
    size_t count = 0;
    if (info[3]->IsUint32Array()) {
        uint8_t* data;
        size_t length;
        Bytes(info[3], &data, &length);
        offsets = (const uint32_t*)data;
        count = length / 4;
    }
    wgpuComputePassEncoderSetBindGroup(pass, U32(info, 1), group, count, offsets);
}

void DispatchWorkgroups(const Info& info) {
    if (auto pass = Obj<WGPUComputePassEncoder>(info, 0, GpuKind::ComputePassEncoder, "compute pass"))
        wgpuComputePassEncoderDispatchWorkgroups(pass, U32(info, 1), U32(info, 2), U32(info, 3));
}

void DispatchWorkgroupsIndirect(const Info& info) {
    auto pass = Obj<WGPUComputePassEncoder>(info, 0, GpuKind::ComputePassEncoder, "compute pass");
    auto buffer = pass ? Obj<WGPUBuffer>(info, 1, GpuKind::Buffer, "buffer") : nullptr;
    if (buffer) wgpuComputePassEncoderDispatchWorkgroupsIndirect(pass, buffer, (uint64_t)Num(info, 2));
}

// Ends the pass and releases it: a pass cannot be used after end().
void EndPass(const Info& info) {
    Gpu& gpu = FromInfo(info)->gpu;
    auto pass = Obj<WGPUComputePassEncoder>(info, 0, GpuKind::ComputePassEncoder, "compute pass");
    if (!pass) return;
    wgpuComputePassEncoderEnd(pass);
    gpu.Release(U32(info, 0));
}

// (encoder, src, srcOffset, dst, dstOffset, size)
void CopyBufferToBuffer(const Info& info) {
    auto encoder = Obj<WGPUCommandEncoder>(info, 0, GpuKind::CommandEncoder, "command encoder");
    auto src = encoder ? Obj<WGPUBuffer>(info, 1, GpuKind::Buffer, "buffer") : nullptr;
    auto dst = src ? Obj<WGPUBuffer>(info, 3, GpuKind::Buffer, "buffer") : nullptr;
    if (dst) wgpuCommandEncoderCopyBufferToBuffer(encoder, src, (uint64_t)Num(info, 2), dst, (uint64_t)Num(info, 4), U64(info, 5));
}

// (encoder, buffer, offset, size or -1 for the rest)
void ClearBuffer(const Info& info) {
    auto encoder = Obj<WGPUCommandEncoder>(info, 0, GpuKind::CommandEncoder, "command encoder");
    auto buffer = encoder ? Obj<WGPUBuffer>(info, 1, GpuKind::Buffer, "buffer") : nullptr;
    if (buffer) wgpuCommandEncoderClearBuffer(encoder, buffer, (uint64_t)Num(info, 2), U64(info, 3));
}

// (encoder, querySet, first, count, dst, dstOffset)
void ResolveQuerySet(const Info& info) {
    auto encoder = Obj<WGPUCommandEncoder>(info, 0, GpuKind::CommandEncoder, "command encoder");
    auto qs = encoder ? Obj<WGPUQuerySet>(info, 1, GpuKind::QuerySet, "query set") : nullptr;
    auto dst = qs ? Obj<WGPUBuffer>(info, 4, GpuKind::Buffer, "buffer") : nullptr;
    if (dst) wgpuCommandEncoderResolveQuerySet(encoder, qs, U32(info, 2), U32(info, 3), dst, (uint64_t)Num(info, 5));
}

// Finishes and releases the encoder; returns the command buffer.
void Finish(const Info& info) {
    Gpu& gpu = FromInfo(info)->gpu;
    auto encoder = Obj<WGPUCommandEncoder>(info, 0, GpuKind::CommandEncoder, "command encoder");
    if (!encoder) return;
    WGPUCommandBuffer cb = wgpuCommandEncoderFinish(encoder, nullptr);
    gpu.Release(U32(info, 0));
    info.GetReturnValue().Set(gpu.Add(cb, GpuKind::CommandBuffer));
}

// (array of command buffer handles); submitted buffers are released.
void Submit(const Info& info) {
    Gpu& gpu = FromInfo(info)->gpu;
    if (!gpu.queue) return Throw(info, "no device");
    if (!info[0]->IsArray()) return Throw(info, "submit: expected an array");
    auto list = info[0].As<v8::Array>();
    std::vector<uint32_t> ids(list->Length());
    std::vector<WGPUCommandBuffer> cbs(list->Length());
    for (uint32_t i = 0; i < list->Length(); i++) {
        ids[i] = (uint32_t)list->Get(Ctx(info), i).ToLocalChecked()->NumberValue(Ctx(info)).FromMaybe(0);
        cbs[i] = (WGPUCommandBuffer)gpu.Get(ids[i], GpuKind::CommandBuffer);
        if (!cbs[i]) return Throw(info, "submit: invalid command buffer");
    }
    wgpuQueueSubmit(gpu.queue, cbs.size(), cbs.data());
    for (uint32_t id : ids) gpu.Release(id);
}

// (buffer, bufferOffset, data, dataByteOffset, byteSize)
void WriteBuffer(const Info& info) {
    Gpu& gpu = FromInfo(info)->gpu;
    if (!gpu.queue) return Throw(info, "no device");
    auto buffer = Obj<WGPUBuffer>(info, 0, GpuKind::Buffer, "buffer");
    if (!buffer) return;
    uint8_t* data;
    size_t length;
    if (!Bytes(info[2], &data, &length)) return Throw(info, "writeBuffer: data is not a buffer");
    size_t offset = (size_t)Num(info, 3), size = (size_t)Num(info, 4);
    if (offset > length || size > length - offset) return Throw(info, "writeBuffer: range outside the data");
    wgpuQueueWriteBuffer(gpu.queue, buffer, (uint64_t)Num(info, 1), data + offset, size);
}

void OnSubmittedWorkDone(const Info& info) {
    Gpu& gpu = FromInfo(info)->gpu;
    if (!gpu.queue) return Throw(info, "no device");
    auto* pending = NewPending(info);
    WGPUQueueWorkDoneCallbackInfo cb = WGPU_QUEUE_WORK_DONE_CALLBACK_INFO_INIT;
    cb.mode = WGPUCallbackMode_AllowProcessEvents;
    cb.callback = [](WGPUQueueWorkDoneStatus status, WGPUStringView message, void* u1, void*) {
        Settle((Pending*)u1, status == WGPUQueueWorkDoneStatus_Success ? "" : "onSubmittedWorkDone: " + Text(message), nullptr);
    };
    cb.userdata1 = pending;
    wgpuQueueOnSubmittedWorkDone(gpu.queue, cb);
}

struct Method {
    const char* name;
    v8::FunctionCallback fn;
};

const Method kGpu[] = {
    {"requestAdapter", RequestAdapter},
    {"adapterInfo", AdapterInfo},
    {"requestDevice", RequestDevice},
    {"destroyDevice", DestroyDevice},
    {"release", Release},
    {"pushErrorScope", PushErrorScope},
    {"popErrorScope", PopErrorScope},
    {"createBuffer", CreateBuffer},
    {"destroyBuffer", DestroyBuffer},
    {"createShaderModule", CreateShaderModule},
    {"createBindGroupLayout", CreateBindGroupLayout},
    {"createPipelineLayout", CreatePipelineLayout},
    {"createComputePipeline", CreateComputePipeline},
    {"createComputePipelineAsync", CreateComputePipelineAsync},
    {"getBindGroupLayout", GetBindGroupLayout},
    {"createBindGroup", CreateBindGroup},
    {"createQuerySet", CreateQuerySet},
    {"destroyQuerySet", DestroyQuerySet},
    {"mapAsync", MapAsync},
    {"getMappedRange", GetMappedRange},
    {"unmap", Unmap},
    {"createCommandEncoder", CreateCommandEncoder},
    {"beginComputePass", BeginComputePass},
    {"setPipeline", SetPipeline},
    {"setBindGroup", SetBindGroup},
    {"dispatchWorkgroups", DispatchWorkgroups},
    {"dispatchWorkgroupsIndirect", DispatchWorkgroupsIndirect},
    {"endPass", EndPass},
    {"copyBufferToBuffer", CopyBufferToBuffer},
    {"clearBuffer", ClearBuffer},
    {"resolveQuerySet", ResolveQuerySet},
    {"finish", Finish},
    {"submit", Submit},
    {"writeBuffer", WriteBuffer},
    {"onSubmittedWorkDone", OnSubmittedWorkDone},
};

}  // namespace

v8::Local<v8::ObjectTemplate> GpuTemplate(v8::Isolate* isolate, v8::Local<v8::Value> data) {
    auto t = v8::ObjectTemplate::New(isolate);
    for (auto& m : kGpu) t->Set(isolate, m.name, v8::FunctionTemplate::New(isolate, m.fn, data));
    return t;
}

}  // namespace wq
