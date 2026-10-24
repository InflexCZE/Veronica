// The prebuilt Dawn release is compiled with a newer MSVC STL than the local
// toolset (14.44), which lacks these vectorized helpers. Scalar equivalents
// with the STL's semantics: first minimum/maximum, end on an empty range.
#include <cstdint>
#include <cstring>
#include <vector>

template <typename T>
static const void* MinElement(const void* first, const void* last) {
    auto* b = (const T*)first;
    auto* e = (const T*)last;
    if (b == e) return last;
    auto* best = b;
    for (auto* p = b + 1; p != e; ++p)
        if (*p < *best) best = p;
    return best;
}

template <typename T>
static const void* MaxElement(const void* first, const void* last) {
    auto* b = (const T*)first;
    auto* e = (const T*)last;
    if (b == e) return last;
    auto* best = b;
    for (auto* p = b + 1; p != e; ++p)
        if (*best < *p) best = p;
    return best;
}

template <typename T>
static void* Unique(void* first, void* last) {
    auto* b = (T*)first;
    auto* e = (T*)last;
    if (b == e) return last;
    auto* out = b;
    for (auto* p = b + 1; p != e; ++p)
        if (!(*out == *p)) *++out = *p;
    return out + 1;
}

extern "C" {
const void* __std_min_element_4u(const void* f, const void* l) noexcept { return MinElement<uint32_t>(f, l); }
const void* __std_min_element_8u(const void* f, const void* l) noexcept { return MinElement<uint64_t>(f, l); }
const void* __std_max_element_4u(const void* f, const void* l) noexcept { return MaxElement<uint32_t>(f, l); }
void* __std_unique_4(void* f, void* l) noexcept { return Unique<uint32_t>(f, l); }
void* __std_unique_8(void* f, void* l) noexcept { return Unique<uint64_t>(f, l); }

void __std_rotate(void* first, void* mid, void* last) noexcept {
    auto* b = (unsigned char*)first;
    auto* m = (unsigned char*)mid;
    auto* e = (unsigned char*)last;
    size_t left = m - b, right = e - m;
    if (!left || !right) return;
    std::vector<unsigned char> tmp(b, m);
    memmove(b, m, right);
    memcpy(b + right, tmp.data(), left);
}
}
