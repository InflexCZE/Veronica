#pragma once
#define WQ_BUILD
#include "wq.h"

#include <dawn/webgpu.h>
#include <libplatform/libplatform.h>
#include <v8.h>

#include <chrono>
#include <condition_variable>
#include <cstdint>
#include <deque>
#include <functional>
#include <map>
#include <mutex>
#include <set>
#include <string>
#include <thread>
#include <vector>

namespace wq {

using Info = v8::FunctionCallbackInfo<v8::Value>;
using Clock = std::chrono::steady_clock;

struct Op {
    void* user = nullptr;
    wq_done_fn done = nullptr;
    wq_progress_fn progress = nullptr;
    wq_delta_fn delta = nullptr;
    wq_chat_done_fn chatDone = nullptr;
    wq_score_fn scoreDone = nullptr;

    void Fail(const std::string& error) const;
};

enum class GpuKind : uint8_t {
    Free,
    Adapter,
    Device,
    Buffer,
    ShaderModule,
    BindGroupLayout,
    PipelineLayout,
    ComputePipeline,
    BindGroup,
    CommandEncoder,
    ComputePassEncoder,
    CommandBuffer,
    QuerySet,
};

struct GpuHandle {
    void* object;
    GpuKind kind;
};

struct Gpu {
    WGPUInstance instance = nullptr;
    WGPUAdapter adapter = nullptr;
    WGPUDevice device = nullptr;
    WGPUQueue queue = nullptr;
    std::vector<GpuHandle> handles{{nullptr, GpuKind::Free}};
    std::vector<uint32_t> freeHandles;
    // Callbacks Dawn has yet to deliver; while any are outstanding the loop
    // polls Dawn instead of sleeping.
    int pending = 0;
    // The device's large buffers are evicted: set between engine operations.
    bool released = false;
    // Calls that queued GPU work; a release waits again if any came meanwhile.
    uint64_t queued = 0;

    uint32_t Add(void* object, GpuKind kind);
    void* Get(uint32_t id, GpuKind kind) const;
    uint32_t Find(void* object) const;
    void Release(uint32_t id);
    void ReleaseAll();
};

// A queued API call; op is null for calls that report nothing.
struct Command {
    Op* op;
    std::function<void()> run;
};

struct Engine {
    wq_host host{};

    std::thread thread;
    std::mutex mutex;
    std::condition_variable wake;
    std::deque<Command> commands;
    // Work queued from Dawn callbacks, which may run inside a Dawn call or on
    // another thread; run by the loop with V8 entered.
    std::deque<std::function<void()>> completions;
    bool quit = false;

    v8::Isolate* isolate = nullptr;
    v8::ArrayBuffer::Allocator* allocator = nullptr;
    v8::Global<v8::Context> context;
    Clock::time_point epoch = Clock::now();

    std::map<int64_t, Clock::time_point> timers;
    std::multimap<Clock::time_point, int64_t> timerQueue;

    std::set<Op*> ops;
    Gpu gpu;

    void Run(std::string* startError, std::mutex* startMutex, std::condition_variable* started, bool* ready);
    bool Start(std::string& error);
    void Loop();
    void Turn();
    void Post(Op* op, std::function<void()> run);
    void Complete(std::function<void()> completion);
    void Shutdown();

    // Calls __wq.<method>(op, ...args); the op completes when the returned
    // promise settles.
    void Invoke(Op* op, const char* method, std::vector<v8::Local<v8::Value>> args,
                std::function<void(Op*, v8::Local<v8::Value>)> onResult);
    void FinishOp(Op* op);

    void FireTimers();
    void Microtasks();
};

Engine* FromInfo(const Info& info);
v8::Local<v8::String> Str(v8::Isolate* isolate, const std::string& s);
v8::Local<v8::String> Str(v8::Isolate* isolate, const char* s, size_t length);
std::string Utf8(v8::Isolate* isolate, v8::Local<v8::Value> value);
std::string ExceptionText(v8::Isolate* isolate, v8::Local<v8::Value> exception);
void Throw(const Info& info, const std::string& message);
// Bytes of an ArrayBuffer or ArrayBufferView, or false if it is neither.
bool Bytes(v8::Local<v8::Value> value, uint8_t** data, size_t* length);

std::string_view EmbeddedScript(int id);

v8::Local<v8::ObjectTemplate> HostTemplate(v8::Isolate* isolate, v8::Local<v8::Value> data);
v8::Local<v8::ObjectTemplate> GpuTemplate(v8::Isolate* isolate, v8::Local<v8::Value> data);

}  // namespace wq

struct wq_engine : wq::Engine {};
