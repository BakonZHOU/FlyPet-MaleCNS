import AppKit
import Foundation
guard CommandLine.arguments.count == 2 else { fatalError("usage: make_icon.swift <iconset>") }
let output=URL(fileURLWithPath:CommandLine.arguments[1],isDirectory:true); try FileManager.default.createDirectory(at:output,withIntermediateDirectories:true)
let entries=[(16,"icon_16x16.png"),(32,"icon_16x16@2x.png"),(32,"icon_32x32.png"),(64,"icon_32x32@2x.png"),(128,"icon_128x128.png"),(256,"icon_128x128@2x.png"),(256,"icon_256x256.png"),(512,"icon_256x256@2x.png"),(512,"icon_512x512.png"),(1024,"icon_512x512@2x.png")]
for (pixels,name) in entries {
    let image=NSImage(size:NSSize(width:pixels,height:pixels)); image.lockFocus(); let s=CGFloat(pixels)
    NSColor(calibratedRed: 0.96, green: 0.72, blue: 0.20, alpha: 1).setFill(); NSBezierPath(roundedRect: NSRect(x: 0, y: 0, width: s, height: s), xRadius: s * 0.22, yRadius: s * 0.22).fill()
    NSColor(calibratedRed: 0.16, green: 0.18, blue: 0.09, alpha: 1).setFill(); NSBezierPath(ovalIn: NSRect(x: s * 0.40, y: s * 0.19, width: s * 0.20, height: s * 0.53)).fill()
    NSColor(calibratedWhite: 1, alpha: 0.72).setFill(); NSBezierPath(ovalIn: NSRect(x: s * 0.13, y: s * 0.38, width: s * 0.34, height: s * 0.25)).fill(); NSBezierPath(ovalIn: NSRect(x: s * 0.53, y: s * 0.38, width: s * 0.34, height: s * 0.25)).fill()
    NSColor.systemRed.setFill(); NSBezierPath(ovalIn: NSRect(x: s * 0.42, y: s * 0.64, width: s * 0.09, height: s * 0.09)).fill(); NSBezierPath(ovalIn: NSRect(x: s * 0.51, y: s * 0.64, width: s * 0.09, height: s * 0.09)).fill(); image.unlockFocus()
    guard let tiff=image.tiffRepresentation,let rep=NSBitmapImageRep(data:tiff),let png=rep.representation(using:.png,properties:[:]) else { fatalError("icon render failed") }; try png.write(to:output.appendingPathComponent(name))
}
