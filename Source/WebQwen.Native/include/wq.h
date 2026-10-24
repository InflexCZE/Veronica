// wq.dll: the WebQwen engine (web-llm bundle in V8, WebGPU through Dawn) behind
// a typed inference API. Every call returns immediately; the work runs on the
// engine's own thread and every callback is invoked on that thread. Strings are
// UTF-8. Inputs are copied before a call returns.
#pragma once
#include <stddef.h>
#include <stdint.h>

#ifdef WQ_BUILD
#define WQ_API __declspec(dllexport)
#else
#define WQ_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct wq_engine wq_engine;

enum wq_log_level { WQ_LOG_DEBUG = 0, WQ_LOG_INFO = 1, WQ_LOG_WARN = 2, WQ_LOG_ERROR = 3 };

// The host serves every resource the engine fetches, keyed by URL.
typedef struct wq_host {
    void* ctx;
    void (*log)(void* ctx, int32_t level, const char* text, size_t length);
    // Size of the resource in bytes, or -1 if the host does not have it.
    int64_t (*resource_size)(void* ctx, const char* url);
    // Reads [offset, offset + length) into dst. Returns the bytes read, or -1.
    int64_t (*resource_read)(void* ctx, const char* url, int64_t offset, int64_t length, void* dst);
} wq_host;

typedef struct wq_model {
    const char* model_id;
    const char* model_url;
    const char* lib_url;
    const char* lib_sha256;  // hex; the library is rejected on mismatch
    const char* const* required_features;
    size_t required_feature_count;
    int32_t context_window_size;  // <= 0: the model's default
} wq_model;

typedef struct wq_usage {
    int32_t prompt_tokens;
    int32_t completion_tokens;
    double prefill_tokens_per_s;
    double decode_tokens_per_s;
} wq_usage;

// Log-probabilities of the next token after the tokens fed so far.
typedef struct wq_score {
    int32_t argmax;
    int32_t vocab_size;
    size_t top_count;
    const int32_t* top_ids;        // descending by probability
    const double* top_logprobs;
} wq_scores;

// error is null when ok != 0.
typedef void (*wq_done_fn)(void* user, int32_t ok, const char* error);
typedef void (*wq_progress_fn)(void* user, double progress, const char* text);
// WQ_CHANNEL_TOOL_CALL: one finished tool call per delta, as JSON
// {"index":0,"id":"...","name":"...","arguments":"<JSON text the model wrote>"}.
enum wq_channel { WQ_CHANNEL_CONTENT = 0, WQ_CHANNEL_REASONING = 1, WQ_CHANNEL_TOOL_CALL = 2 };
typedef void (*wq_delta_fn)(void* user, int32_t channel, const char* text, size_t length);
// usage and finish_reason are null when ok == 0.
typedef void (*wq_chat_done_fn)(void* user, int32_t ok, const char* error, const wq_usage* usage, const char* finish_reason);
// score is null when ok == 0.
typedef void (*wq_score_fn)(void* user, int32_t ok, const char* error, const wq_scores* scores);

// Starts the engine thread and evaluates the engine. Blocks until it is ready.
// On failure returns null and, if error is not null, a message to free with wq_free.
WQ_API wq_engine* wq_create(const wq_host* host, char** error);

// Stops the engine thread. Operations still pending complete with an error first.
// Must not be called from a callback (it is ignored there).
WQ_API void wq_destroy(wq_engine* engine);

WQ_API void wq_free(void* p);

// Loads a model, replacing any loaded one. progress may be null.
WQ_API void wq_load_model(wq_engine* engine, const wq_model* model, wq_progress_fn progress, wq_done_fn done, void* user);

// Runs a chat completion, streaming deltas. request_json is an OpenAI-shaped chat
// request: {"messages":[...], "tools":[...], "max_tokens":n, "temperature":x,
// "top_p":x, "seed":n, "enable_thinking":bool}, every member but messages
// optional. Assistant messages carry "reasoning_content" and "tool_calls"; tool
// messages "tool_call_id". The conversation's KV cache is reused when messages
// extend the previous request's.
WQ_API void wq_chat(wq_engine* engine, const char* request_json, wq_delta_fn delta, wq_chat_done_fn done, void* user);

// Stops the running chat completion; it completes with what it generated so far.
WQ_API void wq_interrupt(wq_engine* engine);

// Clears the conversation and its KV cache.
WQ_API void wq_reset(wq_engine* engine, wq_done_fn done, void* user);

// Feeds tokens without sampling and scores the next token: the top_k
// log-probabilities over the full vocabulary. Continues from the tokens fed
// since the last wq_reset.
WQ_API void wq_score(wq_engine* engine, const int32_t* tokens, size_t count, int32_t top_k, wq_score_fn done, void* user);

#ifdef __cplusplus
}
#endif
