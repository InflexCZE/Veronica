#include "Engine.h"

#include <windows.h>
#include <bcrypt.h>

#include <cmath>
#include <cstring>

namespace wq {

namespace {

v8::Local<v8::ArrayBuffer> NewBuffer(const Info& info, size_t length, bool* ok) {
    auto store = v8::ArrayBuffer::NewBackingStore(info.GetIsolate(), length, v8::BackingStoreInitializationMode::kUninitialized,
                                                  v8::BackingStoreOnFailureMode::kReturnNull);
    *ok = store != nullptr;
    if (!*ok) {
        Throw(info, "out of memory allocating " + std::to_string(length) + " bytes");
        return {};
    }
    return v8::ArrayBuffer::New(info.GetIsolate(), std::move(store));
}

Op* LiveOp(const Info& info, int index) {
    Engine* engine = FromInfo(info);
    if (!info[index]->IsExternal()) return nullptr;
    auto* op = (Op*)info[index].As<v8::External>()->Value(v8::kExternalPointerTypeTagDefault);
    return engine->ops.count(op) ? op : nullptr;
}

double Arg(const Info& info, int index) {
    return info[index]->NumberValue(info.GetIsolate()->GetCurrentContext()).FromMaybe(NAN);
}

void Log(const Info& info) {
    Engine* engine = FromInfo(info);
    if (!engine->host.log) return;
    std::string text = Utf8(info.GetIsolate(), info[1]);
    engine->host.log(engine->host.ctx, (int32_t)Arg(info, 0), text.data(), text.size());
}

void Now(const Info& info) {
    Engine* engine = FromInfo(info);
    info.GetReturnValue().Set(std::chrono::duration<double, std::milli>(Clock::now() - engine->epoch).count());
}

void SetTimer(const Info& info) {
    Engine* engine = FromInfo(info);
    auto id = (int64_t)Arg(info, 0);
    double ms = Arg(info, 1);
    if (!(ms > 0)) ms = 0;
    auto due = Clock::now() + std::chrono::duration_cast<Clock::duration>(std::chrono::duration<double, std::milli>(ms));
    engine->timers[id] = due;
    engine->timerQueue.emplace(due, id);
}

void ClearTimer(const Info& info) { FromInfo(info)->timers.erase((int64_t)Arg(info, 0)); }

void Utf8Encode(const Info& info) {
    v8::String::Utf8Value text(info.GetIsolate(), info[0]);
    bool ok;
    auto buffer = NewBuffer(info, text.length(), &ok);
    if (!ok) return;
    if (text.length()) memcpy(buffer->Data(), *text, text.length());
    info.GetReturnValue().Set(v8::Uint8Array::New(buffer, 0, text.length()));
}

void Utf8Decode(const Info& info) {
    uint8_t* data;
    size_t length;
    if (!Bytes(info[0], &data, &length)) return Throw(info, "utf8Decode: not a buffer");
    // A leading BOM is not part of the text (TextDecoder's default).
    if (length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) data += 3, length -= 3;
    if (length > (size_t)v8::String::kMaxLength) return Throw(info, "utf8Decode: text too long");
    v8::Local<v8::String> s;
    if (!v8::String::NewFromUtf8(info.GetIsolate(), (const char*)data, v8::NewStringType::kNormal, (int)length).ToLocal(&s))
        return Throw(info, "utf8Decode: text too long");
    info.GetReturnValue().Set(s);
}

void Utf16Decode(const Info& info) {
    uint8_t* data;
    size_t length;
    if (!Bytes(info[0], &data, &length)) return Throw(info, "utf16Decode: not a buffer");
    if (length >= 2 && data[0] == 0xFF && data[1] == 0xFE) data += 2, length -= 2;
    if (length / 2 >= (size_t)v8::String::kMaxLength) return Throw(info, "utf16Decode: text too long");
    std::u16string text(length / 2, u'\0');
    if (!text.empty()) memcpy(text.data(), data, text.size() * 2);
    // A trailing odd byte is an incomplete code unit (TextDecoder's U+FFFD).
    if (length % 2) text.push_back(u'\xFFFD');
    v8::Local<v8::String> s;
    if (!v8::String::NewFromTwoByte(info.GetIsolate(), (const uint16_t*)text.data(), v8::NewStringType::kNormal, (int)text.size())
             .ToLocal(&s))
        return Throw(info, "utf16Decode: text too long");
    info.GetReturnValue().Set(s);
}

int Base64Value(uint16_t c) {
    if (c >= 'A' && c <= 'Z') return c - 'A';
    if (c >= 'a' && c <= 'z') return c - 'a' + 26;
    if (c >= '0' && c <= '9') return c - '0' + 52;
    if (c == '+') return 62;
    if (c == '/') return 63;
    return -1;
}

// Copies the characters of argument 0 converted to a string. ValueView
// forbids allocating on the V8 heap while it is alive, so nothing else may
// happen inside its scope.
bool Chars(const Info& info, std::u16string& out) {
    auto* isolate = info.GetIsolate();
    v8::Local<v8::String> input;
    if (!info[0]->ToString(isolate->GetCurrentContext()).ToLocal(&input)) return false;
    v8::String::ValueView view(isolate, input);
    out.resize(view.length());
    for (int i = 0; i < view.length(); i++) out[i] = view.is_one_byte() ? view.data8()[i] : view.data16()[i];
    return true;
}

void Atob(const Info& info) {
    auto* isolate = info.GetIsolate();
    std::u16string input;
    if (!Chars(info, input)) return;
    std::string out;
    out.reserve(input.size() / 4 * 3);
    uint32_t bits = 0;
    int count = 0, padding = 0;
    for (char16_t c : input) {
        if (c == ' ' || c == '\t' || c == '\n' || c == '\f' || c == '\r') continue;
        if (c == '=') {
            padding++;
            continue;
        }
        int v = Base64Value(c);
        if (v < 0 || padding) return Throw(info, "atob: invalid character");
        bits = (bits << 6) | (uint32_t)v;
        if (++count == 4) {
            out.push_back((char)(bits >> 16));
            out.push_back((char)(bits >> 8));
            out.push_back((char)bits);
            bits = 0;
            count = 0;
        }
    }
    if (count == 1 || padding > 2) return Throw(info, "atob: invalid length");
    if (count == 2) out.push_back((char)(bits >> 4));
    if (count == 3) {
        out.push_back((char)(bits >> 10));
        out.push_back((char)(bits >> 2));
    }
    v8::Local<v8::String> s;
    if (!v8::String::NewFromOneByte(isolate, (const uint8_t*)out.data(), v8::NewStringType::kNormal, (int)out.size()).ToLocal(&s))
        return Throw(info, "atob: output too long");
    info.GetReturnValue().Set(s);
}

void Btoa(const Info& info) {
    static const char kAlphabet[] = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    auto* isolate = info.GetIsolate();
    std::u16string input;
    if (!Chars(info, input)) return;
    std::string bytes(input.size(), '\0');
    for (size_t i = 0; i < input.size(); i++) {
        if (input[i] > 0xFF) return Throw(info, "btoa: character outside Latin1");
        bytes[i] = (char)input[i];
    }
    std::string out;
    out.reserve((bytes.size() + 2) / 3 * 4);
    for (size_t i = 0; i < bytes.size(); i += 3) {
        uint32_t n = (uint8_t)bytes[i] << 16;
        if (i + 1 < bytes.size()) n |= (uint8_t)bytes[i + 1] << 8;
        if (i + 2 < bytes.size()) n |= (uint8_t)bytes[i + 2];
        out.push_back(kAlphabet[(n >> 18) & 63]);
        out.push_back(kAlphabet[(n >> 12) & 63]);
        out.push_back(i + 1 < bytes.size() ? kAlphabet[(n >> 6) & 63] : '=');
        out.push_back(i + 2 < bytes.size() ? kAlphabet[n & 63] : '=');
    }
    info.GetReturnValue().Set(Str(isolate, out));
}

void Sha256(const Info& info) {
    uint8_t* data;
    size_t length;
    if (!Bytes(info[0], &data, &length)) return Throw(info, "sha256: not a buffer");
    bool ok;
    auto buffer = NewBuffer(info, 32, &ok);
    if (!ok) return;
    BCRYPT_HASH_HANDLE hash = nullptr;
    NTSTATUS status = BCryptCreateHash(BCRYPT_SHA256_ALG_HANDLE, &hash, nullptr, 0, nullptr, 0, 0);
    // BCryptHashData takes a ULONG length, so large inputs go in pieces.
    for (size_t done = 0; BCRYPT_SUCCESS(status) && done < length;) {
        ULONG piece = (ULONG)std::min<size_t>(length - done, 1u << 30);
        status = BCryptHashData(hash, data + done, piece, 0);
        done += piece;
    }
    if (BCRYPT_SUCCESS(status)) status = BCryptFinishHash(hash, (PUCHAR)buffer->Data(), 32, 0);
    if (hash) BCryptDestroyHash(hash);
    if (!BCRYPT_SUCCESS(status)) return Throw(info, "sha256 failed");
    info.GetReturnValue().Set(buffer);
}

void RandomFill(const Info& info) {
    uint8_t* data;
    size_t length;
    if (!Bytes(info[0], &data, &length)) return Throw(info, "randomFill: not a buffer");
    if (length > 65536) return Throw(info, "randomFill: more than 65536 bytes");
    if (!BCRYPT_SUCCESS(BCryptGenRandom(nullptr, data, (ULONG)length, BCRYPT_USE_SYSTEM_PREFERRED_RNG)))
        return Throw(info, "randomFill failed");
    info.GetReturnValue().Set(info[0]);
}

void ResourceSize(const Info& info) {
    Engine* engine = FromInfo(info);
    std::string url = Utf8(info.GetIsolate(), info[0]);
    info.GetReturnValue().Set((double)engine->host.resource_size(engine->host.ctx, url.c_str()));
}

void ResourceRead(const Info& info) {
    Engine* engine = FromInfo(info);
    std::string url = Utf8(info.GetIsolate(), info[0]);
    double offset = Arg(info, 1), length = Arg(info, 2);
    if (!(offset >= 0) || !(length >= 0)) return Throw(info, "resourceRead: bad range");
    bool ok;
    auto buffer = NewBuffer(info, (size_t)length, &ok);
    if (!ok) return;
    int64_t read = engine->host.resource_read(engine->host.ctx, url.c_str(), (int64_t)offset, (int64_t)length, buffer->Data());
    if (read != (int64_t)length) return Throw(info, "resourceRead: short read of " + url);
    info.GetReturnValue().Set(buffer);
}

void Progress(const Info& info) {
    Op* op = LiveOp(info, 0);
    if (!op || !op->progress) return;
    std::string text = Utf8(info.GetIsolate(), info[2]);
    op->progress(op->user, Arg(info, 1), text.c_str());
}

void Delta(const Info& info) {
    Op* op = LiveOp(info, 0);
    if (!op || !op->delta) return;
    std::string text = Utf8(info.GetIsolate(), info[2]);
    op->delta(op->user, (int32_t)Arg(info, 1), text.data(), text.size());
}

struct Method {
    const char* name;
    v8::FunctionCallback fn;
};

const Method kHost[] = {
    {"log", Log},
    {"now", Now},
    {"setTimer", SetTimer},
    {"clearTimer", ClearTimer},
    {"utf8Encode", Utf8Encode},
    {"utf8Decode", Utf8Decode},
    {"utf16Decode", Utf16Decode},
    {"atob", Atob},
    {"btoa", Btoa},
    {"sha256", Sha256},
    {"randomFill", RandomFill},
    {"resourceSize", ResourceSize},
    {"resourceRead", ResourceRead},
    {"progress", Progress},
    {"delta", Delta},
};

}  // namespace

v8::Local<v8::ObjectTemplate> HostTemplate(v8::Isolate* isolate, v8::Local<v8::Value> data) {
    auto t = v8::ObjectTemplate::New(isolate);
    for (auto& m : kHost) t->Set(isolate, m.name, v8::FunctionTemplate::New(isolate, m.fn, data));
    return t;
}

std::string_view EmbeddedScript(int id) {
    HMODULE module = nullptr;
    GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                       (LPCWSTR)&EmbeddedScript, &module);
    HRSRC resource = FindResourceW(module, MAKEINTRESOURCEW(id), RT_RCDATA);
    if (!resource) return {};
    HGLOBAL loaded = LoadResource(module, resource);
    return {(const char*)LockResource(loaded), SizeofResource(module, resource)};
}

}  // namespace wq
