// On-device speech-to-text with word times, for checking recorded dialogue against its subtitles.
//
//     swiftc -parse-as-library -o transcribe_words Tools/transcribe_words.swift
//     ./transcribe_words <locale, e.g. en-IN> <out.json> <audio files...>
//
// Uses macOS's SpeechTranscriber (macOS 26 and later) with the language assets already on the Mac: nothing is
// downloaded and no audio leaves the machine. A locale whose assets are not installed is refused rather than fetched.
// Writes, per file: the transcript and every word with its start and end in seconds.
// Tools/arjun_recordings.py builds and runs this.
import AVFoundation
import Foundation
import Speech

@main
struct TranscribeWords {
    static func main() async throws {
        let args = CommandLine.arguments
        guard args.count >= 4 else {
            print("usage: transcribe_words <locale> <out.json> <audio files...>")
            exit(2)
        }
        let locale = Locale(identifier: args[1])
        let installed = await SpeechTranscriber.installedLocales
        guard installed.contains(where: { $0.identifier(.bcp47) == locale.identifier(.bcp47) }) else {
            print("the speech assets for \(args[1]) are not installed on this Mac")
            exit(3)
        }
        var all: [[String: Any]] = []
        for path in args.dropFirst(3) {
            let name = (path as NSString).lastPathComponent
            do {
                let transcriber = SpeechTranscriber(locale: locale, transcriptionOptions: [], reportingOptions: [],
                                                    attributeOptions: [.audioTimeRange])
                let analyzer = SpeechAnalyzer(modules: [transcriber])
                let file = try AVAudioFile(forReading: URL(fileURLWithPath: path))
                let collector = Task { () -> [SpeechTranscriber.Result] in
                    var out: [SpeechTranscriber.Result] = []
                    for try await r in transcriber.results { out.append(r) }
                    return out
                }
                try await analyzer.start(inputAudioFile: file, finishAfterFile: true)
                var text = ""
                var words: [[String: Any]] = []
                for r in try await collector.value where r.isFinal {
                    text += String(r.text.characters)
                    for run in r.text.runs {
                        guard let range = run.audioTimeRange else { continue }
                        let w = String(r.text[run.range].characters).trimmingCharacters(in: .whitespaces)
                        if w.isEmpty { continue }
                        words.append(["w": w, "s": range.start.seconds, "e": range.end.seconds])
                    }
                }
                all.append(["file": name, "text": text.trimmingCharacters(in: .whitespaces), "words": words])
            } catch {
                all.append(["file": name, "error": "\(error)"])
            }
        }
        let data = try JSONSerialization.data(withJSONObject: all, options: [.prettyPrinted, .sortedKeys])
        try data.write(to: URL(fileURLWithPath: args[2]))
    }
}
