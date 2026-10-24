// The web platform the engine expects, implemented only as far as it uses it.
// I/O ends in the host: fetch and the Cache API read resources by URL.
;(function (g) {
  'use strict'
  const host = g.__host

  g.self = g
  g.location = {
    href: 'https://webqwen.invalid/',
    origin: 'https://webqwen.invalid',
    protocol: 'https:',
    host: 'webqwen.invalid',
    hostname: 'webqwen.invalid',
    pathname: '/',
    search: '',
    hash: '',
  }

  // ---- console ------------------------------------------------------------

  const format = (args) =>
    args
      .map((a) => {
        if (typeof a === 'string') return a
        if (a instanceof Error) return a.stack || String(a)
        try {
          return JSON.stringify(a)
        } catch (e) {
          return String(a)
        }
      })
      .join(' ')
  g.console = {
    debug: (...a) => host.log(0, format(a)),
    trace: (...a) => host.log(0, format(a)),
    log: (...a) => host.log(1, format(a)),
    info: (...a) => host.log(1, format(a)),
    warn: (...a) => host.log(2, format(a)),
    error: (...a) => host.log(3, format(a)),
  }

  // ---- time ---------------------------------------------------------------

  const timeOrigin = Date.now() - host.now()
  g.performance = { now: () => host.now(), timeOrigin, mark() {}, measure() {} }

  const timers = new Map()
  let nextTimer = 1
  const startTimer = (fn, ms, args, repeat) => {
    const id = nextTimer++
    ms = Math.max(0, Number(ms) || 0)
    timers.set(id, { fn, ms, args, repeat })
    host.setTimer(id, ms)
    return id
  }
  const stopTimer = (id) => {
    if (timers.delete(id)) host.clearTimer(id)
  }
  g.__wqFireTimer = (id) => {
    const t = timers.get(id)
    if (t === undefined) return
    if (t.repeat) host.setTimer(id, t.ms)
    else timers.delete(id)
    try {
      if (typeof t.fn === 'function') t.fn(...t.args)
    } catch (e) {
      console.error('uncaught exception in timer:', e)
    }
  }
  g.setTimeout = (fn, ms, ...args) => startTimer(fn, ms, args, false)
  g.setInterval = (fn, ms, ...args) => startTimer(fn, ms, args, true)
  g.clearTimeout = stopTimer
  g.clearInterval = stopTimer
  g.queueMicrotask = (fn) => {
    Promise.resolve().then(fn)
  }

  // ---- text and bytes -----------------------------------------------------

  const decoders = {
    'utf-8': (b) => host.utf8Decode(b),
    utf8: (b) => host.utf8Decode(b),
    'unicode-1-1-utf-8': (b) => host.utf8Decode(b),
    'utf-16le': (b) => host.utf16Decode(b),
    'utf-16': (b) => host.utf16Decode(b),
  }
  class TextEncoder {
    get encoding() {
      return 'utf-8'
    }
    encode(input = '') {
      return host.utf8Encode(String(input))
    }
  }
  class TextDecoder {
    constructor(label = 'utf-8', options = {}) {
      const l = String(label).trim().toLowerCase()
      this._decode = decoders[l]
      if (this._decode === undefined) throw new RangeError('TextDecoder: unsupported encoding ' + label)
      if (options && options.fatal) throw new TypeError('TextDecoder: fatal mode is not supported')
      this.encoding = l.startsWith('utf-16') ? 'utf-16le' : 'utf-8'
    }
    decode(input, options) {
      if (options && options.stream) throw new TypeError('TextDecoder: streaming is not supported')
      if (input === undefined) return ''
      return this._decode(input)
    }
  }
  g.TextEncoder = TextEncoder
  g.TextDecoder = TextDecoder
  g.atob = (s) => host.atob(s)
  g.btoa = (s) => host.btoa(s)

  // ---- crypto -------------------------------------------------------------

  const hex = (b) => b.toString(16).padStart(2, '0')
  g.crypto = {
    getRandomValues(view) {
      return host.randomFill(view)
    },
    randomUUID() {
      const b = host.randomFill(new Uint8Array(16))
      b[6] = (b[6] & 0x0f) | 0x40
      b[8] = (b[8] & 0x3f) | 0x80
      const h = Array.from(b, hex).join('')
      return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`
    },
    subtle: {
      async digest(algorithm, data) {
        const name = String(typeof algorithm === 'string' ? algorithm : algorithm.name).toUpperCase()
        if (name !== 'SHA-256') throw new DOMException('digest: unsupported algorithm ' + name, 'NotSupportedError')
        return host.sha256(data)
      },
    },
  }

  // ---- errors and abort ---------------------------------------------------

  class DOMException extends Error {
    constructor(message = '', name = 'Error') {
      super(message)
      this.name = name
    }
  }
  g.DOMException = DOMException

  class AbortSignal {
    constructor() {
      this.aborted = false
      this.reason = undefined
      this.onabort = null
      this._listeners = []
    }
    addEventListener(type, fn) {
      if (type === 'abort') this._listeners.push(fn)
    }
    removeEventListener(type, fn) {
      const i = this._listeners.indexOf(fn)
      if (i >= 0) this._listeners.splice(i, 1)
    }
    throwIfAborted() {
      if (this.aborted) throw this.reason
    }
  }
  class AbortController {
    constructor() {
      this.signal = new AbortSignal()
    }
    abort(reason = new DOMException('The operation was aborted.', 'AbortError')) {
      const s = this.signal
      if (s.aborted) return
      s.aborted = true
      s.reason = reason
      const event = { type: 'abort', target: s }
      if (typeof s.onabort === 'function') s.onabort(event)
      for (const fn of s._listeners.slice()) fn(event)
    }
  }
  g.AbortSignal = AbortSignal
  g.AbortController = AbortController

  // ---- URL ----------------------------------------------------------------

  const URL_RE = /^([a-zA-Z][a-zA-Z0-9+.-]*):\/\/([^/?#]*)([^?#]*)(\?[^#]*)?(#.*)?$/
  const removeDots = (path) => {
    const out = []
    const parts = path.split('/')
    for (let i = 0; i < parts.length; i++) {
      const p = parts[i]
      if (p === '..') {
        if (out.length > 1) out.pop()
        if (i === parts.length - 1) out.push('')
      } else if (p === '.') {
        if (i === parts.length - 1) out.push('')
      } else {
        out.push(p)
      }
    }
    return out.join('/')
  }
  class URL {
    constructor(url, base) {
      url = String(url)
      let m = URL_RE.exec(url)
      if (!m) {
        if (base === undefined) throw new TypeError('Invalid URL: ' + url)
        const b = base instanceof URL ? base : new URL(base)
        if (url.startsWith('//')) {
          m = URL_RE.exec(b.protocol + url)
        } else if (url.startsWith('/')) {
          m = URL_RE.exec(b.origin + url)
        } else if (url.startsWith('?')) {
          m = URL_RE.exec(b.origin + b.pathname + url)
        } else if (url.startsWith('#')) {
          m = URL_RE.exec(b.origin + b.pathname + b.search + url)
        } else {
          const dir = b.pathname.slice(0, b.pathname.lastIndexOf('/') + 1)
          m = URL_RE.exec(b.origin + dir + url)
        }
        if (!m) throw new TypeError('Invalid URL: ' + url)
      }
      this.protocol = m[1].toLowerCase() + ':'
      this.host = m[2].toLowerCase()
      const colon = this.host.lastIndexOf(':')
      this.hostname = colon >= 0 ? this.host.slice(0, colon) : this.host
      this.port = colon >= 0 ? this.host.slice(colon + 1) : ''
      this.pathname = removeDots(m[3] || '/') || '/'
      this.search = m[4] && m[4] !== '?' ? m[4] : ''
      this.hash = m[5] && m[5] !== '#' ? m[5] : ''
    }
    get origin() {
      return this.protocol + '//' + this.host
    }
    get href() {
      return this.origin + this.pathname + this.search + this.hash
    }
    toString() {
      return this.href
    }
    toJSON() {
      return this.href
    }
  }
  g.URL = URL

  // ---- fetch and the Cache API --------------------------------------------

  class Headers {
    constructor(init) {
      this._map = new Map()
      if (init instanceof Headers) init._map.forEach((v, k) => this._map.set(k, v))
      else if (Array.isArray(init)) for (const [k, v] of init) this.set(k, v)
      else if (init) for (const k of Object.keys(init)) this.set(k, init[k])
    }
    get(k) {
      const v = this._map.get(String(k).toLowerCase())
      return v === undefined ? null : v
    }
    set(k, v) {
      this._map.set(String(k).toLowerCase(), String(v))
    }
    has(k) {
      return this._map.has(String(k).toLowerCase())
    }
    forEach(fn) {
      this._map.forEach((v, k) => fn(v, k, this))
    }
  }
  g.Headers = Headers

  class Request {
    constructor(input, init = {}) {
      this.url = input instanceof Request ? input.url : new URL(String(input), location.href).href
      this.method = init.method || (input instanceof Request ? input.method : 'GET')
      this.signal = init.signal || (input instanceof Request ? input.signal : undefined)
      this.headers = new Headers(init.headers)
    }
  }
  g.Request = Request

  const bodyBytes = (body) => {
    if (body === null || body === undefined) return new Uint8Array(0)
    if (typeof body === 'string') return host.utf8Encode(body)
    if (body instanceof ArrayBuffer) return new Uint8Array(body)
    if (ArrayBuffer.isView(body)) return new Uint8Array(body.buffer, body.byteOffset, body.byteLength)
    throw new TypeError('Response: unsupported body')
  }

  // A response backed by a host resource reads it on demand.
  class Response {
    constructor(body = null, init = {}) {
      this.status = init.status === undefined ? 200 : init.status
      this.statusText = init.statusText || ''
      this.ok = this.status >= 200 && this.status < 300
      this.headers = new Headers(init.headers)
      this.url = init.url || ''
      this.body = null
      this.bodyUsed = false
      this._resource = init._resource
      this._bytes = this._resource ? null : bodyBytes(body)
    }
    _take() {
      if (this.bodyUsed) return Promise.reject(new TypeError('Response body is already used'))
      this.bodyUsed = true
      try {
        if (this._resource) return Promise.resolve(host.resourceRead(this._resource.url, 0, this._resource.size))
        return Promise.resolve(this._bytes.slice().buffer)
      } catch (e) {
        return Promise.reject(e)
      }
    }
    arrayBuffer() {
      return this._take()
    }
    text() {
      return this._take().then((b) => host.utf8Decode(b))
    }
    json() {
      return this.text().then((t) => JSON.parse(t))
    }
    clone() {
      if (this.bodyUsed) throw new TypeError('Response body is already used')
      const r = new Response(null, { status: this.status, statusText: this.statusText, headers: this.headers, url: this.url, _resource: this._resource })
      if (!this._resource) r._bytes = this._bytes
      return r
    }
  }
  g.Response = Response

  const resourceResponse = (url) => {
    const size = host.resourceSize(url)
    if (size < 0) return undefined
    return new Response(null, { status: 200, url, _resource: { url, size } })
  }

  g.fetch = (input, init = {}) => {
    try {
      const request = input instanceof Request ? input : new Request(input, init)
      const signal = init.signal || request.signal
      if (signal && signal.aborted) return Promise.reject(signal.reason)
      return Promise.resolve(resourceResponse(request.url) || new Response(null, { status: 404, url: request.url }))
    } catch (e) {
      return Promise.reject(e)
    }
  }

  const keyUrl = (request) => (request instanceof Request ? request.url : new URL(String(request), location.href).href)
  // Every cache scope is a view of the host's resources: whatever the host
  // has is cached, and adding something it lacks fails.
  const cache = {
    match: (request) => Promise.resolve(resourceResponse(keyUrl(request))),
    add(request) {
      const url = keyUrl(request)
      if (host.resourceSize(url) < 0) return Promise.reject(new TypeError('WebQwen: resource not available: ' + url))
      return Promise.resolve()
    },
    addAll(requests) {
      return Promise.all(requests.map((r) => cache.add(r))).then(() => undefined)
    },
    put: () => Promise.resolve(),
    delete: () => Promise.resolve(false),
    keys: () => Promise.resolve([]),
  }
  g.caches = {
    open: () => Promise.resolve(cache),
    has: () => Promise.resolve(true),
    delete: () => Promise.resolve(false),
    keys: () => Promise.resolve([]),
    match: (request) => cache.match(request),
  }
})(globalThis)
