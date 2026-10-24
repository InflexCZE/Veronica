// navigator.gpu over the host's __gpu functions. Objects hold the host's
// integer handle in _h; each method maps onto one native call.
;(function (g) {
  'use strict'
  const gpu = g.__gpu

  // Handles of objects the script drops are released when they are collected.
  // Objects released explicitly (submitted command buffers) unregister first,
  // because the host reuses handle numbers.
  const registry = new FinalizationRegistry((h) => gpu.release(h))
  const track = (obj) => {
    if (obj._h) registry.register(obj, obj._h, obj)
    return obj
  }

  g.GPUBufferUsage = { MAP_READ: 1, MAP_WRITE: 2, COPY_SRC: 4, COPY_DST: 8, INDEX: 16, VERTEX: 32, UNIFORM: 64, STORAGE: 128, INDIRECT: 256, QUERY_RESOLVE: 512 }
  g.GPUMapMode = { READ: 1, WRITE: 2 }
  g.GPUShaderStage = { VERTEX: 1, FRAGMENT: 2, COMPUTE: 4 }

  class GPUError {
    constructor(message) {
      this.message = message
    }
  }
  class GPUValidationError extends GPUError {}
  class GPUOutOfMemoryError extends GPUError {}
  class GPUInternalError extends GPUError {}
  g.GPUError = GPUError
  g.GPUValidationError = GPUValidationError
  g.GPUOutOfMemoryError = GPUOutOfMemoryError
  g.GPUInternalError = GPUInternalError
  // WGPUErrorType values
  const makeError = (type, message) =>
    type === 2 ? new GPUValidationError(message) : type === 3 ? new GPUOutOfMemoryError(message) : type === 4 ? new GPUInternalError(message) : new GPUError(message)

  // A mapped buffer stays reachable until unmapped: its ranges point into
  // memory the buffer owns.
  const mapped = new Set()

  class GPUBuffer {
    constructor(device, h, size, usage, mappedAtCreation, label) {
      this._device = device
      this._h = h
      this.size = size
      this.usage = usage
      this.label = label || ''
      this._ranges = []
      this._writable = mappedAtCreation
      this.mapState = mappedAtCreation ? 'mapped' : 'unmapped'
      if (mappedAtCreation && h) mapped.add(this)
    }
    mapAsync(mode, offset = 0, size) {
      if (size === undefined) size = Math.max(0, this.size - offset)
      this.mapState = 'pending'
      return gpu.mapAsync(this._h, mode, offset, size).then(
        () => {
          this.mapState = 'mapped'
          this._writable = (mode & g.GPUMapMode.WRITE) !== 0
          mapped.add(this)
        },
        (e) => {
          this.mapState = 'unmapped'
          throw new DOMException(e.message, 'OperationError')
        },
      )
    }
    getMappedRange(offset = 0, size) {
      if (size === undefined) size = Math.max(0, this.size - offset)
      const range = gpu.getMappedRange(this._h, offset, size, this._writable)
      this._ranges.push(range)
      return range
    }
    unmap() {
      gpu.unmap(this._h, this._ranges)
      this._ranges = []
      this.mapState = 'unmapped'
      mapped.delete(this)
    }
    destroy() {
      if (this.mapState === 'mapped') this.unmap()
      gpu.destroyBuffer(this._h)
    }
  }

  class GPUComputePassEncoder {
    constructor(h) {
      this._h = h
    }
    setPipeline(pipeline) {
      gpu.setPipeline(this._h, pipeline._h)
    }
    setBindGroup(index, group, offsets, start, length) {
      let dynamic
      if (offsets && offsets.length) {
        dynamic = offsets instanceof Uint32Array ? offsets : new Uint32Array(offsets)
        if (start !== undefined) dynamic = dynamic.subarray(start, start + length)
      }
      gpu.setBindGroup(this._h, index, group ? group._h : 0, dynamic)
    }
    dispatchWorkgroups(x, y = 1, z = 1) {
      gpu.dispatchWorkgroups(this._h, x, y, z)
    }
    dispatchWorkgroupsIndirect(buffer, offset) {
      gpu.dispatchWorkgroupsIndirect(this._h, buffer._h, offset)
    }
    end() {
      gpu.endPass(this._h)
      this._h = 0
    }
    pushDebugGroup() {}
    popDebugGroup() {}
    insertDebugMarker() {}
  }

  class GPUCommandBuffer {
    constructor(h) {
      this._h = h
    }
  }

  class GPUCommandEncoder {
    constructor(h) {
      this._h = h
    }
    beginComputePass(desc) {
      const tw = desc && desc.timestampWrites
      const begin = tw && tw.beginningOfPassWriteIndex !== undefined ? tw.beginningOfPassWriteIndex : -1
      const end = tw && tw.endOfPassWriteIndex !== undefined ? tw.endOfPassWriteIndex : -1
      return new GPUComputePassEncoder(gpu.beginComputePass(this._h, tw ? tw.querySet._h : 0, begin, end))
    }
    copyBufferToBuffer(src, a, b, c, d) {
      if (typeof a === 'number') gpu.copyBufferToBuffer(this._h, src._h, a, b._h, c, d === undefined ? src.size - a : d)
      else gpu.copyBufferToBuffer(this._h, src._h, 0, a._h, 0, b === undefined ? src.size : b)
    }
    clearBuffer(buffer, offset = 0, size) {
      gpu.clearBuffer(this._h, buffer._h, offset, size === undefined ? -1 : size)
    }
    resolveQuerySet(querySet, first, count, destination, offset) {
      gpu.resolveQuerySet(this._h, querySet._h, first, count, destination._h, offset)
    }
    finish() {
      const h = gpu.finish(this._h)
      this._h = 0
      return track(new GPUCommandBuffer(h))
    }
    pushDebugGroup() {}
    popDebugGroup() {}
    insertDebugMarker() {}
  }

  class GPUQueue {
    submit(buffers) {
      const handles = []
      for (const b of buffers) {
        registry.unregister(b)
        handles.push(b._h)
        b._h = 0
      }
      try {
        gpu.submit(handles)
      } catch (e) {
        for (const h of handles) if (h) gpu.release(h)
        throw e
      }
    }
    writeBuffer(buffer, offset, data, dataOffset = 0, size) {
      // Offsets and sizes count elements for typed arrays, bytes otherwise.
      const unit = data instanceof ArrayBuffer || data instanceof DataView ? 1 : data.BYTES_PER_ELEMENT || 1
      const byteOffset = dataOffset * unit
      const byteSize = size === undefined ? data.byteLength - byteOffset : size * unit
      gpu.writeBuffer(buffer._h, offset, data, byteOffset, byteSize)
    }
    onSubmittedWorkDone() {
      return gpu.onSubmittedWorkDone()
    }
  }

  class GPUShaderModule {
    constructor(h) {
      this._h = h
    }
    getCompilationInfo() {
      return Promise.resolve({ messages: [] })
    }
  }
  class GPUBindGroupLayout {
    constructor(h) {
      this._h = h
    }
  }
  class GPUPipelineLayout {
    constructor(h) {
      this._h = h
    }
  }
  class GPUBindGroup {
    constructor(h) {
      this._h = h
    }
  }
  class GPUComputePipeline {
    constructor(h) {
      this._h = h
    }
    getBindGroupLayout(index) {
      return track(new GPUBindGroupLayout(gpu.getBindGroupLayout(this._h, index)))
    }
  }
  class GPUQuerySet {
    constructor(h, type, count) {
      this._h = h
      this.type = type
      this.count = count
    }
    destroy() {
      gpu.destroyQuerySet(this._h)
    }
  }

  // WGPUBufferBindingType values
  const bindingType = { uniform: 2, storage: 3, 'read-only-storage': 4 }

  const pipelineArgs = (d) => {
    const constants = d.compute.constants ? Object.entries(d.compute.constants).flat() : undefined
    return [d.layout === 'auto' || !d.layout ? 0 : d.layout._h, d.compute.module._h, d.compute.entryPoint, constants, d.label]
  }

  const devices = []

  class GPUDevice {
    constructor(h, features, limits, adapterInfo) {
      this._h = h
      this.features = new Set(features)
      this.limits = Object.freeze(limits)
      this.adapterInfo = adapterInfo
      this.queue = new GPUQueue()
      this.label = ''
      this.onuncapturederror = null
      this._listeners = []
      this.lost = new Promise((resolve) => {
        this._lose = resolve
      })
    }
    addEventListener(type, fn) {
      if (type === 'uncapturederror') this._listeners.push(fn)
    }
    removeEventListener(type, fn) {
      const i = this._listeners.indexOf(fn)
      if (i >= 0) this._listeners.splice(i, 1)
    }
    createBuffer(d) {
      const mappedAtCreation = !!d.mappedAtCreation
      return track(new GPUBuffer(this, gpu.createBuffer(d.size, d.usage, mappedAtCreation, d.label), d.size, d.usage, mappedAtCreation, d.label))
    }
    createShaderModule(d) {
      return track(new GPUShaderModule(gpu.createShaderModule(d.code, d.label)))
    }
    createBindGroupLayout(d) {
      const rows = new Float64Array(d.entries.length * 5)
      d.entries.forEach((e, i) => {
        if (!e.buffer) throw new TypeError('webgpu: only buffer bindings are supported')
        rows.set([e.binding, e.visibility, bindingType[e.buffer.type || 'uniform'], e.buffer.hasDynamicOffset ? 1 : 0, e.buffer.minBindingSize || 0], i * 5)
      })
      return track(new GPUBindGroupLayout(gpu.createBindGroupLayout(rows)))
    }
    createPipelineLayout(d) {
      return track(new GPUPipelineLayout(gpu.createPipelineLayout(d.bindGroupLayouts.map((l) => l._h))))
    }
    createComputePipeline(d) {
      return track(new GPUComputePipeline(gpu.createComputePipeline(...pipelineArgs(d))))
    }
    createComputePipelineAsync(d) {
      try {
        return gpu.createComputePipelineAsync(...pipelineArgs(d)).then(
          (h) => track(new GPUComputePipeline(h)),
          (e) => {
            throw new DOMException(e.message, 'OperationError')
          },
        )
      } catch (e) {
        return Promise.reject(e)
      }
    }
    createBindGroup(d) {
      const rows = new Float64Array(d.entries.length * 4)
      d.entries.forEach((e, i) => {
        const r = e.resource instanceof GPUBuffer ? { buffer: e.resource } : e.resource
        if (!(r.buffer instanceof GPUBuffer)) throw new TypeError('webgpu: only buffer bindings are supported')
        rows.set([e.binding, r.buffer._h, r.offset || 0, r.size === undefined ? -1 : r.size], i * 4)
      })
      return track(new GPUBindGroup(gpu.createBindGroup(d.layout._h, rows, d.label)))
    }
    createQuerySet(d) {
      return track(new GPUQuerySet(gpu.createQuerySet(d.type, d.count), d.type, d.count))
    }
    createCommandEncoder() {
      return new GPUCommandEncoder(gpu.createCommandEncoder())
    }
    pushErrorScope(filter) {
      gpu.pushErrorScope(filter)
    }
    popErrorScope() {
      try {
        return gpu.popErrorScope().then((e) => (e === null ? null : makeError(e.type, e.message)))
      } catch (e) {
        return Promise.reject(e)
      }
    }
    // Destroying or losing a device unmaps its buffers: their ranges must be
    // detached before Dawn frees the mappings.
    _unmapAll() {
      for (const b of [...mapped]) if (b._device === this) b.unmap()
    }
    destroy() {
      this._unmapAll()
      gpu.destroyDevice(this._h)
    }
  }

  g.__gpuError = (h, type, message) => {
    const error = makeError(type, message)
    for (const d of devices.filter((d) => d._h === h)) {
      const event = { type: 'uncapturederror', error }
      if (typeof d.onuncapturederror === 'function') d.onuncapturederror(event)
      for (const fn of d._listeners.slice()) fn(event)
      if (!d.onuncapturederror && d._listeners.length === 0) console.warn('WebGPU error:', message)
    }
  }
  g.__gpuLost = (h, reason, message) => {
    const i = devices.findIndex((d) => d._h === h)
    if (i < 0) return
    const d = devices[i]
    devices.splice(i, 1)
    d._unmapAll()
    d._lose({ reason, message })
  }

  class GPUAdapter {
    constructor(h) {
      this._h = h
      const a = gpu.adapterInfo(h)
      this.features = new Set(a.features)
      this.limits = Object.freeze(a.limits)
      this.info = a.info
      this.isFallbackAdapter = a.info.isFallbackAdapter
    }
    requestDevice(d = {}) {
      try {
        return gpu.requestDevice(this._h, d.requiredFeatures || [], d.requiredLimits || {}).then((r) => {
          const device = new GPUDevice(r.handle, r.features, r.limits, this.info)
          devices.push(device)
          return device
        })
      } catch (e) {
        return Promise.reject(e)
      }
    }
    requestAdapterInfo() {
      return Promise.resolve(this.info)
    }
  }

  g.navigator = {
    userAgent: 'WebQwen',
    gpu: {
      requestAdapter(options = {}) {
        return gpu.requestAdapter(options.powerPreference || '').then((h) => (h === null ? null : new GPUAdapter(h)))
      },
      getPreferredCanvasFormat() {
        return 'bgra8unorm'
      },
      wgslLanguageFeatures: new Set(),
    },
  }
})(globalThis)
