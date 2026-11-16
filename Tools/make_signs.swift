// Renders bilingual (Hindi + English) shop-sign textures with correct Devanagari shaping (CoreText).
// Run from the project root:  swift Tools/make_signs.swift
// White text on transparent background; the game tints it per sign. Output: Assets/Resources/Signs/<slug>.png
import AppKit

struct Sign { let english: String; let hindi: String? }

let signs: [Sign] = [
    Sign(english: "SHARMA SWEETS", hindi: "शर्मा स्वीट्स"),
    Sign(english: "GUPTA GENERAL STORE", hindi: "गुप्ता जनरल स्टोर"),
    Sign(english: "RAJU TAILORS", hindi: "राजू टेलर्स"),
    Sign(english: "MAA DURGA JEWELLERS", hindi: "माँ दुर्गा ज्वेलर्स"),
    Sign(english: "NEW BOMBAY CLOTH HOUSE", hindi: "न्यू बॉम्बे क्लॉथ हाउस"),
    Sign(english: "SHREE GANESH PAN BHANDAR", hindi: "श्री गणेश पान भंडार"),
    Sign(english: "KRISHNA MEDICAL STORE", hindi: "कृष्णा मेडिकल स्टोर"),
    Sign(english: "HOTEL ANNAPURNA", hindi: "होटल अन्नपूर्णा"),
    Sign(english: "MOBILE & RECHARGE", hindi: "मोबाइल रिचार्ज"),
    Sign(english: "BOOK DEPOT", hindi: "बुक डिपो"),
    Sign(english: "SHOE PALACE", hindi: "शू पैलेस"),
    Sign(english: "KIRANA & DRY FRUITS", hindi: "किराना ड्राई फ्रूट"),
    Sign(english: "RAMLAL HARDWARE", hindi: "रामलाल हार्डवेयर"),
    Sign(english: "STD PCO", hindi: "एसटीडी पीसीओ"),
    Sign(english: "SABZI", hindi: "सब्ज़ी"),
    Sign(english: "MASALA", hindi: "मसाले"),
    Sign(english: "MITHAI", hindi: "मिठाई"),
    Sign(english: "NARIYAL PAANI", hindi: "नारियल पानी"),
    Sign(english: "PANI PURI", hindi: "पानी पूरी"),
    Sign(english: "RECHARGE", hindi: "रिचार्ज"),
    Sign(english: "NEWS", hindi: "समाचार"),
    Sign(english: "SAMOSA JALEBI", hindi: "समोसा जलेबी"),
    Sign(english: "VIKRAM CHAI", hindi: "विक्रम चाय"),
    Sign(english: "CHANDNI CHOWK BAZAAR", hindi: "चांदनी चौक बाज़ार"),
    Sign(english: "WELCOME", hindi: "स्वागतम्"),
    Sign(english: "OM", hindi: "ॐ"),
]

func slug(_ s: String) -> String {
    var out = "", lastUnderscore = false
    for ch in s.lowercased() {
        if ch.isLetter || ch.isNumber, ch.isASCII { out.append(ch); lastUnderscore = false }
        else if !lastUnderscore { out.append("_"); lastUnderscore = true }
    }
    return out.trimmingCharacters(in: CharacterSet(charactersIn: "_"))
}

func font(_ names: [String], _ size: CGFloat) -> NSFont {
    for n in names { if let f = NSFont(name: n, size: size) { return f } }
    return NSFont.boldSystemFont(ofSize: size)
}

let hindiFont = font(["KohinoorDevanagari-Bold", "KohinoorDevanagari-Semibold", "DevanagariSangamMN-Bold", "DevanagariSangamMN"], 150)
let englishFont = font(["AvenirNext-Heavy", "AvenirNext-Bold", "HelveticaNeue-Bold"], 74)

let outDir = "Assets/Resources/Signs"
try? FileManager.default.createDirectory(atPath: outDir, withIntermediateDirectories: true)

for sign in signs {
    let only = sign.english == "OM"
    let hAttr = NSAttributedString(string: sign.hindi ?? "", attributes: [.font: only ? font(["KohinoorDevanagari-Bold"], 230) : hindiFont, .foregroundColor: NSColor.white])
    let ps = NSMutableParagraphStyle(); ps.alignment = .center
    let eAttr = NSAttributedString(string: sign.english, attributes: [.font: englishFont, .foregroundColor: NSColor.white, .kern: 6, .paragraphStyle: ps])
    let hs = hAttr.size(), es = only ? NSSize.zero : eAttr.size()
    let pad: CGFloat = 40
    let w = Int(ceil(max(hs.width, es.width) + pad * 2)), h = Int(ceil(hs.height + es.height + pad * 1.5))
    guard let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: w, pixelsHigh: h, bitsPerSample: 8, samplesPerPixel: 4,
                                     hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0),
          let ctx = NSGraphicsContext(bitmapImageRep: rep) else { continue }
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = ctx
    hAttr.draw(at: NSPoint(x: (CGFloat(w) - hs.width) / 2, y: es.height + pad * 0.9))
    if !only { eAttr.draw(at: NSPoint(x: (CGFloat(w) - es.width) / 2, y: pad * 0.5)) }
    NSGraphicsContext.restoreGraphicsState()
    if let png = rep.representation(using: .png, properties: [:]) {
        try? png.write(to: URL(fileURLWithPath: "\(outDir)/\(slug(sign.english)).png"))
        print("ok", slug(sign.english), w, h)
    }
}
