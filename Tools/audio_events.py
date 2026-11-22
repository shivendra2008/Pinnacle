#!/usr/bin/env python3
"""Sound events in a PlayRoute capture's offline mix (route.wav): onsets, level and left/right balance.

    python3 Tools/audio_events.py <capture dir> [from_s to_s]

Splits the mix into 10 ms frames, finds onsets (a rise of more than 9 dB over the previous 60 ms, above -50 dBFS) and
prints, per onset, the time, the peak level in the next 120 ms, the left/right balance (dB, + = right), and how long the
sound takes to fall 20 dB (a rough tail length: long tails mean reverb or a sound that does not stop).
It measures; it is not listening.
"""
import array, math, os, sys, wave


def main():
    d = sys.argv[1]
    t0 = float(sys.argv[2]) if len(sys.argv) > 2 else 0.0
    t1 = float(sys.argv[3]) if len(sys.argv) > 3 else 1e9
    w = wave.open(os.path.join(d, "route.wav"))
    ch, sr = w.getnchannels(), w.getframerate()
    a = array.array("h", w.readframes(w.getnframes()))
    hop = sr // 100
    n = len(a) // ch // hop
    L, R = [], []
    for k in range(n):
        sl = sr2 = 0.0
        base = k * hop * ch
        for i in range(hop):
            l = a[base + i * ch]; r = a[base + i * ch + (1 if ch > 1 else 0)]
            sl += l * l; sr2 += r * r
        L.append(10 * math.log10(sl / hop / 32768.0 ** 2 + 1e-12)); R.append(10 * math.log10(sr2 / hop / 32768.0 ** 2 + 1e-12))
    M = [max(x, y) for x, y in zip(L, R)]
    print("%s: %.1f s, %d ch" % (d, n / 100.0, ch))
    last = -1
    for k in range(6, n):
        t = k / 100.0
        if t < t0 or t > t1: continue
        before = max(M[k - 6:k])
        if M[k] > -50 and M[k] - before > 9 and k - last > 15:
            seg = range(k, min(n, k + 12))
            pk = max(M[i] for i in seg)
            bal = sum(R[i] - L[i] for i in seg) / len(seg)
            tail = 0
            for j in range(k, min(n, k + 400)):
                if M[j] < pk - 20: tail = (j - k) * 10; break
            else: tail = 4000
            print("  %7.2f s  peak %6.1f dBFS  balance %+5.1f dB  falls 20 dB in %4d ms" % (t, pk, bal, tail))
            last = k


if __name__ == "__main__":
    main()
