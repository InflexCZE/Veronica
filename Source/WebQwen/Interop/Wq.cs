// 1:1 mirror of wq.h.
using System.Runtime.InteropServices;

namespace WebQwen.Interop;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct wq_host
{
    public void* ctx;
    public delegate* unmanaged<void*, int, byte*, nuint, void> log;
    public delegate* unmanaged<void*, byte*, long> resource_size;
    public delegate* unmanaged<void*, byte*, long, long, void*, long> resource_read;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct wq_model
{
    public byte* model_id;
    public byte* model_url;
    public byte* lib_url;
    public byte* lib_sha256;
    public byte** required_features;
    public nuint required_feature_count;
    public int context_window_size;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct wq_message
{
    public byte* role;
    public byte* content;
    public byte* reasoning;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct wq_chat_request
{
    public wq_message* messages;
    public nuint message_count;
    public int max_tokens;
    public double temperature;
    public double top_p;
    public long seed;
    public int enable_thinking;
}

[StructLayout(LayoutKind.Sequential)]
internal struct wq_usage
{
    public int prompt_tokens;
    public int completion_tokens;
    public double prefill_tokens_per_s;
    public double decode_tokens_per_s;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct wq_scores
{
    public int argmax;
    public int vocab_size;
    public nuint top_count;
    public int* top_ids;
    public double* top_logprobs;
}

internal static unsafe partial class Wq
{
    public const int WQ_LOG_DEBUG = 0;
    public const int WQ_LOG_INFO = 1;
    public const int WQ_LOG_WARN = 2;
    public const int WQ_LOG_ERROR = 3;

    public const int WQ_CHANNEL_CONTENT = 0;
    public const int WQ_CHANNEL_REASONING = 1;

    private const string Lib = "wq";

    [LibraryImport(Lib)]
    public static partial nint wq_create(wq_host* host, byte** error);

    [LibraryImport(Lib)]
    public static partial void wq_destroy(nint engine);

    [LibraryImport(Lib)]
    public static partial void wq_free(void* p);

    [LibraryImport(Lib)]
    public static partial void wq_load_model(nint engine, wq_model* model,
        delegate* unmanaged<void*, double, byte*, void> progress,
        delegate* unmanaged<void*, int, byte*, void> done, void* user);

    [LibraryImport(Lib)]
    public static partial void wq_chat(nint engine, wq_chat_request* request,
        delegate* unmanaged<void*, int, byte*, nuint, void> delta,
        delegate* unmanaged<void*, int, byte*, wq_usage*, byte*, void> done, void* user);

    [LibraryImport(Lib)]
    public static partial void wq_interrupt(nint engine);

    [LibraryImport(Lib)]
    public static partial void wq_reset(nint engine, delegate* unmanaged<void*, int, byte*, void> done, void* user);

    [LibraryImport(Lib)]
    public static partial void wq_score(nint engine, int* tokens, nuint count, int top_k,
        delegate* unmanaged<void*, int, byte*, wq_scores*, void> done, void* user);
}
