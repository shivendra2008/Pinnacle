// Word timing for a TTS line, measured on the actual dialogue audio.
//
// Usage: align_words <voice-name> <say-rate> <final.wav> <text> <out.json>
//
// The game's VO is macOS `say` run through ffmpeg (Tools/acts_dialogue.py). `say` cannot report timing, but the same
// voice through AVSpeechSynthesizer reports where every word starts (sample offsets). So:
//   1. render the line with `say` exactly as the VO pipeline did (the source of the shipped file);
//   2. render it with AVSpeechSynthesizer, same voice, speaking rate searched until the length matches, and collect
//      its word markers;
//   3. align the AVSpeech audio to the `say` audio with dynamic time warping on log-mel features and carry the word
//      starts across;
//   4. find the offset between the `say` audio and the final .wav (cross-correlation of the energy envelopes; the
//      ffmpeg chain keeps the timing, its echo only adds a tail) and express everything in the final file's time;
//   5. word ends: the next word's start, or the start of the pause that follows (energy).
// Writes JSON: words (text, start, end, seconds in the final .wav), the final file's 10 ms energy envelope (dB) and
// its high-band (fricative) energy, and quality figures (DTW cost, path slope, envelope correlation).
import AVFoundation
import Accelerate
import Foundation

setvbuf(stdout, nil, _IONBF, 0)
let args = CommandLine.arguments
guard args.count >= 6 else { print("usage: align_words voice rate final.wav text out.json"); exit(2) }
let voiceName = args[1], sayRate = args[2], finalPath = args[3], text = args[4], outPath = args[5]

// ------------------------------------------------------------------ audio helpers
func loadMono(_ path: String) -> (samples: [Float], rate: Double) {
    let url = URL(fileURLWithPath: path)
    guard let f = try? AVAudioFile(forReading: url) else { fatalError("cannot read \(path)") }
    let fmt = AVAudioFormat(commonFormat: .pcmFormatFloat32, sampleRate: f.fileFormat.sampleRate, channels: f.fileFormat.channelCount, interleaved: false)!
    let buf = AVAudioPCMBuffer(pcmFormat: fmt, frameCapacity: AVAudioFrameCount(f.length))!
    try! f.read(into: buf)
    let n = Int(buf.frameLength)
    var out = [Float](repeating: 0, count: n)
    let ch = Int(fmt.channelCount)
    for c in 0..<ch { let p = buf.floatChannelData![c]; for i in 0..<n { out[i] += p[i] / Float(ch) } }
    return (out, f.fileFormat.sampleRate)
}

func resample(_ x: [Float], from: Double, to: Double) -> [Float] {
    if abs(from - to) < 1 { return x }
    let n = Int(Double(x.count) * to / from)
    var y = [Float](repeating: 0, count: n)
    let r = from / to
    for i in 0..<n {
        let p = Double(i) * r; let k = Int(p); let f = Float(p - Double(k))
        let a = k < x.count ? x[k] : 0, b = k + 1 < x.count ? x[k + 1] : 0
        y[i] = a + (b - a) * f
    }
    return y
}

// log-mel features: 25 ms Hann window, 10 ms hop, 26 bands up to 8 kHz, at 16 kHz
let SR = 16000.0, hop = 160, win = 400, nfft = 512, nMel = 26
let fftSetup = vDSP_create_fftsetup(vDSP_Length(9), FFTRadix(kFFTRadix2))!
var hann = [Float](repeating: 0, count: win)
vDSP_hann_window(&hann, vDSP_Length(win), Int32(vDSP_HANN_NORM))
func mel(_ f: Double) -> Double { 2595 * log10(1 + f / 700) }
func imel(_ m: Double) -> Double { 700 * (pow(10, m / 2595) - 1) }
let melEdges: [Double] = (0..<(nMel + 2)).map { imel(mel(60) + (mel(7800) - mel(60)) * Double($0) / Double(nMel + 1)) }

func spectrum(_ x: [Float], at start: Int) -> [Float] {
    var frame = [Float](repeating: 0, count: nfft)
    for i in 0..<win { let k = start + i; frame[i] = (k >= 0 && k < x.count ? x[k] : 0) * hann[i] }
    var re = [Float](repeating: 0, count: nfft / 2), im = [Float](repeating: 0, count: nfft / 2)
    var mags = [Float](repeating: 0, count: nfft / 2)
    re.withUnsafeMutableBufferPointer { rp in
        im.withUnsafeMutableBufferPointer { ip in
            var split = DSPSplitComplex(realp: rp.baseAddress!, imagp: ip.baseAddress!)
            frame.withUnsafeBufferPointer { fp in
                fp.baseAddress!.withMemoryRebound(to: DSPComplex.self, capacity: nfft / 2) { cp in
                    vDSP_ctoz(cp, 2, &split, 1, vDSP_Length(nfft / 2))
                }
            }
            vDSP_fft_zrip(fftSetup, &split, 1, vDSP_Length(9), FFTDirection(FFT_FORWARD))
            vDSP_zvmags(&split, 1, &mags, 1, vDSP_Length(nfft / 2))
        }
    }
    return mags
}

func features(_ x: [Float]) -> [[Float]] {
    let frames = max(1, (x.count - win) / hop + 1)
    var out: [[Float]] = []
    for f in 0..<frames {
        let mags = spectrum(x, at: f * hop)
        var bands = [Float](repeating: 0, count: nMel)
        for b in 0..<nMel {
            let lo = melEdges[b], mid = melEdges[b + 1], hi = melEdges[b + 2]
            var s: Float = 0
            for k in 1..<(nfft / 2) {
                let hz = Double(k) * SR / Double(nfft)
                if hz <= lo || hz >= hi { continue }
                let w = hz < mid ? (hz - lo) / (mid - lo) : (hi - hz) / (hi - mid)
                s += Float(w) * mags[k]
            }
            bands[b] = log10(s + 1e-7)
        }
        // per-frame mean removal keeps the spectral shape and drops the loudness (the two renders differ in level)
        let m = bands.reduce(0, +) / Float(nMel)
        out.append(bands.map { $0 - m } + [m * 0.6])
    }
    return out
}

func envelopeDb(_ x: [Float], rate: Double) -> (all: [Float], high: [Float]) {
    // 10 ms frames: overall level, and the level above 3 kHz (frication: s, f, sh, th)
    let y = resample(x, from: rate, to: SR)
    let frames = max(1, (y.count - win) / hop + 1)
    var all: [Float] = [], high: [Float] = []
    for f in 0..<frames {
        let mags = spectrum(y, at: f * hop)
        var a: Float = 0, h: Float = 0
        for k in 1..<(nfft / 2) { a += mags[k]; if Double(k) * SR / Double(nfft) > 3000 { h += mags[k] } }
        all.append(10 * log10(a + 1e-9)); high.append(10 * log10(h + 1e-9))
    }
    return (all, high)
}

// ------------------------------------------------------------------ 1. `say`, exactly as the VO pipeline
let tmp = NSTemporaryDirectory() + "align_\(getpid())_say.aiff"
let p = Process()
p.executableURL = URL(fileURLWithPath: "/usr/bin/say")
p.arguments = ["-v", voiceName, "-r", sayRate, "-o", tmp, text]
try! p.run(); p.waitUntilExit()
let sayAudio = loadMono(tmp)
try? FileManager.default.removeItem(atPath: tmp)
let sayDur = Double(sayAudio.samples.count) / sayAudio.rate

// ------------------------------------------------------------------ 2. AVSpeechSynthesizer with word markers
guard let voice = AVSpeechSynthesisVoice.speechVoices().first(where: { $0.name == voiceName && $0.language.hasPrefix("en") }) else { fatalError("no AVSpeech voice \(voiceName)") }
let synth = AVSpeechSynthesizer()
func render(rate: Float) -> (samples: [Float], rate: Double, words: [(Int, Int, Int)]) {
    let u = AVSpeechUtterance(string: text)
    u.voice = voice; u.rate = rate
    var samples: [Float] = [], sr = 22050.0, words: [(Int, Int, Int)] = [], done = false
    synth.write(u, toBufferCallback: { b in
        guard let pcm = b as? AVAudioPCMBuffer else { return }
        if pcm.frameLength == 0 { done = true; return }
        sr = pcm.format.sampleRate
        if let d = pcm.floatChannelData { samples.append(contentsOf: UnsafeBufferPointer(start: d[0], count: Int(pcm.frameLength))) }
        else if let d = pcm.int16ChannelData { for i in 0..<Int(pcm.frameLength) { samples.append(Float(d[0][i]) / 32768) } }
    }, toMarkerCallback: { ms in
        for m in ms where m.mark == .word { words.append((m.byteSampleOffset, m.textRange.location, m.textRange.length)) }
    })
    let t0 = Date()
    while !done && Date().timeIntervalSince(t0) < 30 { RunLoop.main.run(until: Date().addingTimeInterval(0.01)) }
    return (samples, sr, words)
}
var lo: Float = 0.2, hi: Float = 0.6
var best = render(rate: 0.4)
var bestErr = 1e9
for _ in 0..<12 {
    let mid = (lo + hi) / 2
    let r = render(rate: mid)
    let d = Double(r.samples.count) / r.rate
    let err = abs(d - sayDur)
    if err < bestErr { bestErr = err; best = r }
    if d > sayDur { lo = mid } else { hi = mid }      // longer than `say`: speak faster
    if err < 0.004 * sayDur { break }
}
let avDur = Double(best.samples.count) / best.rate
// byteSampleOffset is in bytes of the delivered PCM; the compact voices deliver Float32 mono
let bytesPerSample = 4.0

// ------------------------------------------------------------------ 3. DTW AVSpeech -> say
let fa = features(resample(best.samples, from: best.rate, to: SR))
let fb = features(resample(sayAudio.samples, from: sayAudio.rate, to: SR))
let n = fa.count, m = fb.count
let band = max(25, Int(Double(max(n, m)) * 0.12))
var cost = [Float](repeating: .infinity, count: (n + 1) * (m + 1))
func idx(_ i: Int, _ j: Int) -> Int { i * (m + 1) + j }
cost[idx(0, 0)] = 0
for i in 1...n {
    let jc = Int(Double(i) * Double(m) / Double(n))
    let j0 = max(1, jc - band), j1 = min(m, jc + band)
    if j0 > j1 { continue }
    for j in j0...j1 {
        var d: Float = 0
        let a = fa[i - 1], b = fb[j - 1]
        for k in 0..<a.count { let e = a[k] - b[k]; d += e * e }
        d = d.squareRoot()
        let c = min(cost[idx(i - 1, j - 1)], min(cost[idx(i - 1, j)] + 0.15, cost[idx(i, j - 1)] + 0.15))
        cost[idx(i, j)] = d + c
    }
}
var path: [(Int, Int)] = []
var i = n, j = m
while i > 0 && j > 0 {
    path.append((i - 1, j - 1))
    let a = cost[idx(i - 1, j - 1)], b = cost[idx(i - 1, j)], c = cost[idx(i, j - 1)]
    if a <= b && a <= c { i -= 1; j -= 1 } else if b < c { i -= 1 } else { j -= 1 }
}
path.reverse()
var mapAB = [Int](repeating: 0, count: n)
for (a, b) in path { mapAB[a] = b }
let dtwCost = cost[idx(n, m)] / Float(path.count)

// ------------------------------------------------------------------ 4. say -> final file offset
let finalAudio = loadMono(finalPath)
let envSay = envelopeDb(sayAudio.samples, rate: sayAudio.rate).all
let envFin = envelopeDb(finalAudio.samples, rate: finalAudio.rate)
func corr(_ a: [Float], _ b: [Float], lag: Int) -> Float {
    var s: Float = 0, sa: Float = 0, sb: Float = 0, c = 0
    for k in 0..<a.count { let kb = k + lag; if kb < 0 || kb >= b.count { continue }; s += a[k] * b[kb]; sa += a[k] * a[k]; sb += b[kb] * b[kb]; c += 1 }
    return c > 10 ? s / (sa.squareRoot() * sb.squareRoot() + 1e-9) : -1
}
let ma = envSay.reduce(0, +) / Float(envSay.count), mf = envFin.all.reduce(0, +) / Float(envFin.all.count)
let za = envSay.map { $0 - ma }, zf = envFin.all.map { $0 - mf }
var bestLag = 0; var bestC: Float = -2
for lag in -40...40 { let c = corr(za, zf, lag: lag); if c > bestC { bestC = c; bestLag = lag } }
let offset = Double(bestLag) * 0.01

// ------------------------------------------------------------------ 5. word times in the final file
let ns = text as NSString
var words: [[String: Any]] = []
let finalDur = Double(finalAudio.samples.count) / finalAudio.rate
let floorDb = envFin.all.sorted()[envFin.all.count / 10]                 // the quietest tenth: the noise floor
let peakDb = envFin.all.max() ?? 0
let silentDb = floorDb + (peakDb - floorDb) * 0.18
for (k, w) in best.words.enumerated() {
    let tAv = Double(w.0) / bytesPerSample / best.rate
    let fA = min(n - 1, max(0, Int(tAv / 0.01)))
    let tSay = Double(mapAB[fA]) * 0.01
    var start = tSay + offset
    var end: Double
    if k + 1 < best.words.count {
        let tAv2 = Double(best.words[k + 1].0) / bytesPerSample / best.rate
        end = Double(mapAB[min(n - 1, max(0, Int(tAv2 / 0.01)))]) * 0.01 + offset
    } else { end = sayDur + offset }
    // a pause inside the span (comma, full stop): the word ends where the sound drops away for 70 ms or more
    let f0 = max(0, Int(start / 0.01)), f1 = min(envFin.all.count, Int(end / 0.01))
    var run = 0
    if f1 - f0 > 8 {
        for f in (f0 + 5)..<f1 {
            if envFin.all[f] < silentDb { run += 1 } else { run = 0 }
            if run >= 7 { end = Double(f - run + 1) * 0.01; break }
        }
    }
    start = max(0, start); end = min(finalDur, max(start + 0.03, end))
    words.append(["text": ns.substring(with: NSRange(location: w.1, length: w.2)), "start": start, "end": end])
}

let out: [String: Any] = [
    "text": text, "voice": voiceName, "sayRate": sayRate,
    "sayDuration": sayDur, "avDuration": avDur, "finalDuration": finalDur, "offset": offset,
    "dtwCost": dtwCost, "envCorrelation": bestC, "floorDb": floorDb, "peakDb": peakDb,
    "words": words, "envDb": envFin.all.map { Double($0) }, "highDb": envFin.high.map { Double($0) },
]
let data = try! JSONSerialization.data(withJSONObject: out, options: [.prettyPrinted])
try! data.write(to: URL(fileURLWithPath: outPath))
print(String(format: "ok  say %.2fs  av %.2fs  dtw %.3f  env-corr %.3f  offset %+.2fs  words %d", sayDur, avDur, dtwCost, bestC, offset, words.count))
