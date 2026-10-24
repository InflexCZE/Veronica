#include "Engine.h"

#include "Resources.h"

#include <cmath>
#include <cstdlib>
#include <cstring>

namespace wq {

namespace {

std::unique_ptr<v8::Platform> g_platform;
std::once_flag g_platformOnce;

char* Dup(const std::string& s) {
    char* p = (char*)malloc(s.size() + 1);
    memcpy(p, s.c_str(), s.size() + 1);
    return p;
}

v8::Local<v8::External> Ext(v8::Isolate* isolate, void* p) {
    return v8::External::New(isolate, p, v8::kExternalPointerTypeTagDefault);
}

void* ExtValue(v8::Local<v8::Value> value) {
    return value.As<v8::External>()->Value(v8::kExternalPointerTypeTagDefault);
}

v8::MaybeLocal<v8::Module> NoImports(v8::Local<v8::Context> context, v8::Local<v8::String> specifier, v8::Local<v8::FixedArray>,
                                      v8::Local<v8::Module>) {
    auto* isolate = v8::Isolate::GetCurrent();
    isolate->ThrowError(Str(isolate, "import of '" + Utf8(isolate, specifier) + "' is not available"));
    return {};
}

double Number(v8::Local<v8::Context> context, v8::Local<v8::Object> o, const char* key, double fallback) {
    v8::Local<v8::Value> v;
    if (!o->Get(context, Str(v8::Isolate::GetCurrent(), key)).ToLocal(&v) || !v->IsNumber()) return fallback;
    return v.As<v8::Number>()->Value();
}

}  // namespace

void Op::Fail(const std::string& error) const {
    if (done) done(user, 0, error.c_str());
    else if (chatDone) chatDone(user, 0, error.c_str(), nullptr, nullptr);
    else if (scoreDone) scoreDone(user, 0, error.c_str(), nullptr);
}

Engine* FromInfo(const Info& info) { return (Engine*)ExtValue(info.Data()); }

v8::Local<v8::String> Str(v8::Isolate* isolate, const char* s, size_t length) {
    v8::Local<v8::String> out;
    if (length > (size_t)v8::String::kMaxLength ||
        !v8::String::NewFromUtf8(isolate, s, v8::NewStringType::kNormal, (int)length).ToLocal(&out))
        return v8::String::NewFromUtf8Literal(isolate, "<string too long>");
    return out;
}

v8::Local<v8::String> Str(v8::Isolate* isolate, const std::string& s) { return Str(isolate, s.data(), s.size()); }

std::string Utf8(v8::Isolate* isolate, v8::Local<v8::Value> value) {
    v8::String::Utf8Value u(isolate, value);
    return *u ? std::string(*u, u.length()) : std::string();
}

std::string ExceptionText(v8::Isolate* isolate, v8::Local<v8::Value> exception) {
    if (exception.IsEmpty()) return "unknown error";
    auto context = isolate->GetCurrentContext();
    if (exception->IsObject()) {
        v8::Local<v8::Value> stack;
        if (exception.As<v8::Object>()->Get(context, Str(isolate, "stack")).ToLocal(&stack) && stack->IsString())
            return Utf8(isolate, stack);
    }
    return Utf8(isolate, exception);
}

void Throw(const Info& info, const std::string& message) { info.GetIsolate()->ThrowError(Str(info.GetIsolate(), message)); }

bool Bytes(v8::Local<v8::Value> value, uint8_t** data, size_t* length) {
    if (value->IsArrayBufferView()) {
        auto view = value.As<v8::ArrayBufferView>();
        *data = (uint8_t*)view->Buffer()->Data() + view->ByteOffset();
        *length = view->ByteLength();
        return true;
    }
    if (value->IsArrayBuffer()) {
        auto buffer = value.As<v8::ArrayBuffer>();
        *data = (uint8_t*)buffer->Data();
        *length = buffer->ByteLength();
        return true;
    }
    return false;
}

bool Engine::Start(std::string& error) {
    std::call_once(g_platformOnce, [] {
        g_platform = v8::platform::NewDefaultPlatform();
        v8::V8::InitializePlatform(g_platform.get());
        v8::V8::Initialize();
    });

    v8::Isolate::CreateParams params;
    allocator = v8::ArrayBuffer::Allocator::NewDefaultAllocator();
    params.array_buffer_allocator = allocator;
    isolate = v8::Isolate::New(params);
    isolate->SetMicrotasksPolicy(v8::MicrotasksPolicy::kExplicit);

    v8::Isolate::Scope isolateScope(isolate);
    v8::HandleScope handles(isolate);
    auto data = Ext(isolate, this);
    auto global = v8::ObjectTemplate::New(isolate);
    global->Set(isolate, "__host", HostTemplate(isolate, data));
    global->Set(isolate, "__gpu", GpuTemplate(isolate, data));
    auto ctx = v8::Context::New(isolate, nullptr, global);
    context.Reset(isolate, ctx);
    v8::Context::Scope contextScope(ctx);
    v8::TryCatch tc(isolate);

    const struct {
        int id;
        const char* name;
    } shims[] = {{WQ_RES_RUNTIME_JS, "runtime.js"}, {WQ_RES_WEBGPU_JS, "webgpu.js"}, {WQ_RES_API_JS, "api.js"}};
    for (auto& shim : shims) {
        auto source = EmbeddedScript(shim.id);
        v8::ScriptOrigin origin(Str(isolate, shim.name));
        v8::Local<v8::Script> script;
        if (!v8::Script::Compile(ctx, Str(isolate, source.data(), source.size()), &origin).ToLocal(&script) ||
            script->Run(ctx).IsEmpty()) {
            error = std::string(shim.name) + ": " + ExceptionText(isolate, tc.Exception());
            return false;
        }
    }

    auto engineSource = EmbeddedScript(WQ_RES_ENGINE_JS);
    v8::ScriptOrigin moduleOrigin(Str(isolate, "web-llm.js"), 0, 0, false, -1, v8::Local<v8::Value>(), false, false, true);
    v8::ScriptCompiler::Source source(Str(isolate, engineSource.data(), engineSource.size()), moduleOrigin);
    v8::Local<v8::Module> module;
    v8::Local<v8::Value> evaluated;
    if (!v8::ScriptCompiler::CompileModule(isolate, &source).ToLocal(&module) ||
        !module->InstantiateModule(ctx, NoImports).FromMaybe(false) || !module->Evaluate(ctx).ToLocal(&evaluated)) {
        error = "web-llm.js: " + ExceptionText(isolate, tc.Exception());
        return false;
    }
    auto promise = evaluated.As<v8::Promise>();
    while (promise->State() == v8::Promise::kPending) {
        v8::HandleScope turn(isolate);
        Turn();
    }
    if (promise->State() == v8::Promise::kRejected) {
        error = "web-llm.js: " + ExceptionText(isolate, promise->Result());
        return false;
    }
    if (ctx->Global()->Set(ctx, Str(isolate, "__webllm"), module->GetModuleNamespace()).IsNothing()) {
        error = "web-llm.js: " + ExceptionText(isolate, tc.Exception());
        return false;
    }
    return true;
}

void Engine::Run(std::string* startError, std::mutex* startMutex, std::condition_variable* started, bool* ready) {
    std::string error;
    bool ok = Start(error);
    {
        // Notified under the lock: the waiter owns these and may return as
        // soon as it sees ready.
        std::lock_guard<std::mutex> lock(*startMutex);
        *startError = error;
        *ready = true;
        started->notify_one();
    }
    if (ok) Loop();
    Shutdown();
}

void Engine::Post(Op* op, std::function<void()> run) {
    {
        std::lock_guard<std::mutex> lock(mutex);
        commands.push_back({op, std::move(run)});
    }
    wake.notify_one();
}

void Engine::Complete(std::function<void()> completion) {
    {
        std::lock_guard<std::mutex> lock(mutex);
        completions.push_back(std::move(completion));
    }
    wake.notify_one();
}

void Engine::Microtasks() { isolate->PerformMicrotaskCheckpoint(); }

void Engine::FireTimers() {
    auto now = Clock::now();
    std::vector<int64_t> due;
    while (!timerQueue.empty() && timerQueue.begin()->first <= now) {
        auto [when, id] = *timerQueue.begin();
        timerQueue.erase(timerQueue.begin());
        auto it = timers.find(id);
        if (it == timers.end() || it->second != when) continue;
        timers.erase(it);
        due.push_back(id);
    }
    if (due.empty()) return;
    auto ctx = context.Get(isolate);
    v8::Local<v8::Value> fire;
    if (!ctx->Global()->Get(ctx, Str(isolate, "__wqFireTimer")).ToLocal(&fire) || !fire->IsFunction()) return;
    for (int64_t id : due) {
        v8::HandleScope scope(isolate);
        v8::TryCatch tc(isolate);
        v8::Local<v8::Value> argv[] = {v8::Number::New(isolate, (double)id)};
        (void)fire.As<v8::Function>()->Call(ctx, ctx->Global(), 1, argv);
        Microtasks();
    }
}

void Engine::Turn() {
    if (gpu.instance) wgpuInstanceProcessEvents(gpu.instance);
    std::deque<std::function<void()>> ready;
    {
        std::lock_guard<std::mutex> lock(mutex);
        ready.swap(completions);
    }
    for (auto& completion : ready) {
        v8::HandleScope scope(isolate);
        completion();
        Microtasks();
    }
    FireTimers();
    while (v8::platform::PumpMessageLoop(g_platform.get(), isolate)) Microtasks();
    Microtasks();
}

void Engine::Loop() {
    v8::Isolate::Scope isolateScope(isolate);
    v8::HandleScope handles(isolate);
    auto ctx = context.Get(isolate);
    v8::Context::Scope contextScope(ctx);
    for (;;) {
        std::deque<Command> batch;
        {
            std::unique_lock<std::mutex> lock(mutex);
            auto hasWork = [this] { return quit || !commands.empty() || !completions.empty(); };
            if (!hasWork()) {
                if (gpu.pending > 0) {
                    lock.unlock();
                    std::this_thread::yield();
                    lock.lock();
                } else {
                    auto deadline = Clock::time_point::max();
                    if (!timerQueue.empty()) deadline = timerQueue.begin()->first;
                    // V8 posts the foreground half of async work (WebAssembly
                    // compilation) without telling us, so poll while busy.
                    if (!ops.empty()) deadline = std::min(deadline, Clock::now() + std::chrono::milliseconds(1));
                    if (deadline == Clock::time_point::max()) wake.wait(lock, hasWork);
                    else wake.wait_until(lock, deadline, hasWork);
                }
            }
            if (quit) return;
            batch.swap(commands);
        }
        for (auto& command : batch) {
            v8::HandleScope scope(isolate);
            command.run();
            Microtasks();
        }
        v8::HandleScope scope(isolate);
        Turn();
    }
}

void Engine::Shutdown() {
    if (isolate) {
        v8::Isolate::Scope isolateScope(isolate);
        v8::HandleScope handles(isolate);
        for (Op* op : ops) {
            op->Fail("the engine was destroyed");
            delete op;
        }
        ops.clear();
        std::deque<Command> queued;
        {
            std::lock_guard<std::mutex> lock(mutex);
            queued.swap(commands);
        }
        for (auto& command : queued) {
            if (!command.op) continue;
            command.op->Fail("the engine was destroyed");
            delete command.op;
        }
        context.Reset();
    }
    gpu.ReleaseAll();
    if (isolate) {
        v8::platform::NotifyIsolateShutdown(g_platform.get(), isolate);
        isolate->Dispose();
    }
    delete allocator;
}

void Engine::FinishOp(Op* op) {
    ops.erase(op);
    delete op;
}

namespace {

struct Pending {
    Engine* engine;
    Op* op;
    std::function<void(Op*, v8::Local<v8::Value>)> onResult;
};

Pending* TakePending(const Info& info) {
    auto* pending = (Pending*)ExtValue(info.Data());
    return pending;
}

}  // namespace

void Engine::Invoke(Op* op, const char* method, std::vector<v8::Local<v8::Value>> args,
                    std::function<void(Op*, v8::Local<v8::Value>)> onResult) {
    auto ctx = context.Get(isolate);
    v8::TryCatch tc(isolate);
    ops.insert(op);
    v8::Local<v8::Value> api, fn, result;
    if (!ctx->Global()->Get(ctx, Str(isolate, "__wq")).ToLocal(&api) || !api->IsObject() ||
        !api.As<v8::Object>()->Get(ctx, Str(isolate, method)).ToLocal(&fn) || !fn->IsFunction()) {
        op->Fail(std::string("__wq.") + method + " is missing");
        FinishOp(op);
        return;
    }
    args.insert(args.begin(), Ext(isolate, op));
    if (!fn.As<v8::Function>()->Call(ctx, api, (int)args.size(), args.data()).ToLocal(&result)) {
        op->Fail(ExceptionText(isolate, tc.Exception()));
        FinishOp(op);
        return;
    }
    // Both handlers share one Pending; whichever runs frees it. If the engine
    // is destroyed first, Shutdown fails the op and the Pending leaks with
    // the isolate, which is going away.
    auto* pending = new Pending{this, op, std::move(onResult)};
    auto data = Ext(isolate, pending);
    auto fulfilled = v8::Function::New(ctx, [](const Info& info) {
        auto* pending = TakePending(info);
        Engine* engine = pending->engine;
        if (engine->ops.count(pending->op)) {
            pending->onResult(pending->op, info[0]);
            engine->FinishOp(pending->op);
        }
        delete pending;
    }, data).ToLocalChecked();
    auto rejected = v8::Function::New(ctx, [](const Info& info) {
        auto* pending = TakePending(info);
        Engine* engine = pending->engine;
        if (engine->ops.count(pending->op)) {
            pending->op->Fail(ExceptionText(info.GetIsolate(), info[0]));
            engine->FinishOp(pending->op);
        }
        delete pending;
    }, data).ToLocalChecked();
    v8::Local<v8::Promise> promise;
    if (result->IsPromise()) {
        promise = result.As<v8::Promise>();
    } else {
        auto resolver = v8::Promise::Resolver::New(ctx).ToLocalChecked();
        (void)resolver->Resolve(ctx, result);
        promise = resolver->GetPromise();
    }
    if (promise->Then(ctx, fulfilled, rejected).IsEmpty()) {
        delete pending;
        op->Fail(ExceptionText(isolate, tc.Exception()));
        FinishOp(op);
    }
}

}  // namespace wq

using namespace wq;

namespace {

v8::Local<v8::Value> JsString(v8::Isolate* isolate, const std::string& s) { return Str(isolate, s); }

}  // namespace

extern "C" {

WQ_API wq_engine* wq_create(const wq_host* host, char** error) {
    auto* engine = new wq_engine();
    engine->host = *host;
    std::string startError;
    std::mutex startMutex;
    std::condition_variable started;
    bool ready = false;
    engine->thread = std::thread(&Engine::Run, engine, &startError, &startMutex, &started, &ready);
    {
        std::unique_lock<std::mutex> lock(startMutex);
        started.wait(lock, [&] { return ready; });
    }
    if (!startError.empty()) {
        engine->thread.join();
        delete engine;
        if (error) *error = Dup(startError);
        return nullptr;
    }
    return engine;
}

WQ_API void wq_destroy(wq_engine* engine) {
    if (!engine) return;
    // From a callback the engine thread would join itself.
    if (std::this_thread::get_id() == engine->thread.get_id()) {
        static const char kMessage[] = "wq_destroy called from an engine callback; ignored";
        if (engine->host.log) engine->host.log(engine->host.ctx, WQ_LOG_ERROR, kMessage, sizeof(kMessage) - 1);
        return;
    }
    {
        std::lock_guard<std::mutex> lock(engine->mutex);
        engine->quit = true;
    }
    engine->wake.notify_one();
    engine->thread.join();
    delete engine;
}

WQ_API void wq_free(void* p) { free(p); }

WQ_API void wq_load_model(wq_engine* engine, const wq_model* model, wq_progress_fn progress, wq_done_fn done, void* user) {
    std::string modelId = model->model_id ? model->model_id : "";
    std::string modelUrl = model->model_url ? model->model_url : "";
    std::string libUrl = model->lib_url ? model->lib_url : "";
    std::string libSha256 = model->lib_sha256 ? model->lib_sha256 : "";
    std::vector<std::string> features;
    for (size_t i = 0; i < model->required_feature_count; i++) features.emplace_back(model->required_features[i]);
    int32_t contextWindow = model->context_window_size;
    auto* op = new Op();
    op->user = user;
    op->done = done;
    op->progress = progress;
    engine->Post(op, [=] {
        auto* isolate = engine->isolate;
        auto ctx = isolate->GetCurrentContext();
        auto o = v8::Object::New(isolate);
        (void)o->Set(ctx, Str(isolate, "modelId"), JsString(isolate, modelId));
        (void)o->Set(ctx, Str(isolate, "modelUrl"), JsString(isolate, modelUrl));
        (void)o->Set(ctx, Str(isolate, "libUrl"), JsString(isolate, libUrl));
        (void)o->Set(ctx, Str(isolate, "libSha256"), JsString(isolate, libSha256));
        auto list = v8::Array::New(isolate, (int)features.size());
        for (size_t i = 0; i < features.size(); i++) (void)list->Set(ctx, (uint32_t)i, JsString(isolate, features[i]));
        (void)o->Set(ctx, Str(isolate, "requiredFeatures"), list);
        (void)o->Set(ctx, Str(isolate, "contextWindowSize"), v8::Integer::New(isolate, contextWindow));
        engine->Invoke(op, "load", {o}, [](Op* op, v8::Local<v8::Value>) { op->done(op->user, 1, nullptr); });
    });
}

WQ_API void wq_chat(wq_engine* engine, const wq_chat_request* request, wq_delta_fn delta, wq_chat_done_fn done, void* user) {
    struct Message {
        std::string role, content, reasoning;
        bool hasReasoning;
    };
    std::vector<Message> messages;
    for (size_t i = 0; i < request->message_count; i++) {
        const wq_message& m = request->messages[i];
        messages.push_back({m.role ? m.role : "", m.content ? m.content : "", m.reasoning ? m.reasoning : "",
                            m.reasoning != nullptr});
    }
    int32_t maxTokens = request->max_tokens;
    double temperature = request->temperature, topP = request->top_p;
    int64_t seed = request->seed;
    bool enableThinking = request->enable_thinking != 0;
    auto* op = new Op();
    op->user = user;
    op->delta = delta;
    op->chatDone = done;
    engine->Post(op, [=] {
        auto* isolate = engine->isolate;
        auto ctx = isolate->GetCurrentContext();
        auto list = v8::Array::New(isolate, (int)messages.size());
        for (size_t i = 0; i < messages.size(); i++) {
            auto m = v8::Object::New(isolate);
            (void)m->Set(ctx, Str(isolate, "role"), JsString(isolate, messages[i].role));
            (void)m->Set(ctx, Str(isolate, "content"), JsString(isolate, messages[i].content));
            if (messages[i].hasReasoning)
                (void)m->Set(ctx, Str(isolate, "reasoning"), JsString(isolate, messages[i].reasoning));
            (void)list->Set(ctx, (uint32_t)i, m);
        }
        auto o = v8::Object::New(isolate);
        (void)o->Set(ctx, Str(isolate, "messages"), list);
        (void)o->Set(ctx, Str(isolate, "maxTokens"), v8::Integer::New(isolate, maxTokens));
        (void)o->Set(ctx, Str(isolate, "temperature"), v8::Number::New(isolate, temperature));
        (void)o->Set(ctx, Str(isolate, "topP"), v8::Number::New(isolate, topP));
        (void)o->Set(ctx, Str(isolate, "seed"), v8::Number::New(isolate, (double)seed));
        (void)o->Set(ctx, Str(isolate, "enableThinking"), v8::Boolean::New(isolate, enableThinking));
        engine->Invoke(op, "chat", {o}, [](Op* op, v8::Local<v8::Value> result) {
            auto* isolate = v8::Isolate::GetCurrent();
            auto ctx = isolate->GetCurrentContext();
            if (!result->IsObject()) return op->Fail("chat returned no result");
            auto r = result.As<v8::Object>();
            wq_usage usage{};
            usage.prompt_tokens = (int32_t)Number(ctx, r, "promptTokens", 0);
            usage.completion_tokens = (int32_t)Number(ctx, r, "completionTokens", 0);
            usage.prefill_tokens_per_s = Number(ctx, r, "prefillTokensPerS", NAN);
            usage.decode_tokens_per_s = Number(ctx, r, "decodeTokensPerS", NAN);
            v8::Local<v8::Value> reason;
            std::string finish = r->Get(ctx, Str(isolate, "finishReason")).ToLocal(&reason) && reason->IsString()
                                     ? Utf8(isolate, reason)
                                     : std::string();
            op->chatDone(op->user, 1, nullptr, &usage, finish.c_str());
        });
    });
}

WQ_API void wq_interrupt(wq_engine* engine) {
    engine->Post(nullptr, [engine] {
        auto* isolate = engine->isolate;
        auto ctx = isolate->GetCurrentContext();
        v8::TryCatch tc(isolate);
        v8::Local<v8::Value> api, fn;
        if (ctx->Global()->Get(ctx, Str(isolate, "__wq")).ToLocal(&api) && api->IsObject() &&
            api.As<v8::Object>()->Get(ctx, Str(isolate, "interrupt")).ToLocal(&fn) && fn->IsFunction())
            (void)fn.As<v8::Function>()->Call(ctx, api, 0, nullptr);
    });
}

WQ_API void wq_reset(wq_engine* engine, wq_done_fn done, void* user) {
    auto* op = new Op();
    op->user = user;
    op->done = done;
    engine->Post(op, [=] {
        engine->Invoke(op, "reset", {}, [](Op* op, v8::Local<v8::Value>) { op->done(op->user, 1, nullptr); });
    });
}

WQ_API void wq_score(wq_engine* engine, const int32_t* tokens, size_t count, int32_t top_k, wq_score_fn done, void* user) {
    std::vector<int32_t> ids(tokens, tokens + count);
    auto* op = new Op();
    op->user = user;
    op->scoreDone = done;
    engine->Post(op, [=] {
        auto* isolate = engine->isolate;
        auto ctx = isolate->GetCurrentContext();
        auto list = v8::Array::New(isolate, (int)ids.size());
        for (size_t i = 0; i < ids.size(); i++) (void)list->Set(ctx, (uint32_t)i, v8::Integer::New(isolate, ids[i]));
        engine->Invoke(op, "score", {list, v8::Integer::New(isolate, top_k)}, [](Op* op, v8::Local<v8::Value> result) {
            auto* isolate = v8::Isolate::GetCurrent();
            auto ctx = isolate->GetCurrentContext();
            if (!result->IsObject()) return op->Fail("score returned no result");
            auto r = result.As<v8::Object>();
            v8::Local<v8::Value> topValue;
            if (!r->Get(ctx, Str(isolate, "top")).ToLocal(&topValue) || !topValue->IsArray())
                return op->Fail("score returned no top list");
            auto top = topValue.As<v8::Array>();
            std::vector<int32_t> topIds(top->Length());
            std::vector<double> topLogprobs(top->Length());
            for (uint32_t i = 0; i < top->Length(); i++) {
                v8::Local<v8::Value> pair;
                if (!top->Get(ctx, i).ToLocal(&pair) || !pair->IsArray()) return op->Fail("score returned a malformed top list");
                auto p = pair.As<v8::Array>();
                topIds[i] = (int32_t)p->Get(ctx, 0).ToLocalChecked()->NumberValue(ctx).FromMaybe(-1);
                topLogprobs[i] = p->Get(ctx, 1).ToLocalChecked()->NumberValue(ctx).FromMaybe(NAN);
            }
            wq_scores score{};
            score.argmax = (int32_t)Number(ctx, r, "argmax", -1);
            score.vocab_size = (int32_t)Number(ctx, r, "vocabSize", 0);
            score.top_count = topIds.size();
            score.top_ids = topIds.data();
            score.top_logprobs = topLogprobs.data();
            op->scoreDone(op->user, 1, nullptr, &score);
        });
    });
}

}  // extern "C"
