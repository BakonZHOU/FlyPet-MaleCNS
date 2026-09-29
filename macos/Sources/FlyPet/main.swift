import AppKit
import Foundation

enum PetSkin: String { case fly, cockroach }

final class PetView: NSView {
    var skin: PetSkin = .fly
    var heading: CGFloat = 0, speed: CGFloat = 0, fullness: CGFloat = 65, health: CGFloat = 100
    var isRare = false
    private var phase: CGFloat = 0
    var onHit: (() -> Void)?
    override var isOpaque: Bool { false }
    override func mouseDown(with event: NSEvent) { onHit?() }
    func advance(_ dt: CGFloat) { phase += dt * (8 + min(speed / 30, 20)); needsDisplay = true }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)
        guard let c = NSGraphicsContext.current?.cgContext else { return }
        let side = min(bounds.width, bounds.height)
        c.saveGState(); c.translateBy(x: bounds.midX, y: bounds.midY); c.rotate(by: -heading); c.scaleBy(x: side/120, y: side/120)
        c.setShadow(offset: CGSize(width: 0, height: -4), blur: 6, color: NSColor.black.withAlphaComponent(0.35).cgColor)
        skin == .fly ? drawFly(c) : drawCockroach(c)
        c.restoreGState()
        if health < 100 || fullness < 35 {
            let w = bounds.width * 0.72
            drawMeter(NSRect(x: (bounds.width-w)/2, y: 4, width: w, height: 4), value: health/100, color: .systemRed)
            drawMeter(NSRect(x: (bounds.width-w)/2, y: 10, width: w, height: 4), value: fullness/100, color: .systemYellow)
        }
    }
    private func ellipse(_ c: CGContext, _ rect: CGRect, _ color: NSColor) { c.setFillColor(color.cgColor); c.fillEllipse(in: rect) }
    private func drawFly(_ c: CGContext) {
        let flap = sin(phase) * 8
        c.setFillColor(NSColor(calibratedWhite: 0.88, alpha: 0.72).cgColor)
        c.addEllipse(in: CGRect(x: -55, y: -8-flap, width: 48, height: 29)); c.fillPath()
        c.addEllipse(in: CGRect(x: 7, y: -8+flap, width: 48, height: 29)); c.fillPath()
        ellipse(c, CGRect(x: -17, y: -39, width: 34, height: 73), isRare ? .white : NSColor(calibratedRed: 0.18, green: 0.22, blue: 0.12, alpha: 1))
        ellipse(c, CGRect(x: -22, y: 20, width: 44, height: 37), NSColor(calibratedRed: 0.28, green: 0.24, blue: 0.14, alpha: 1))
        ellipse(c, CGRect(x: -22, y: 29, width: 18, height: 18), isRare ? .white : .systemRed)
        ellipse(c, CGRect(x: 4, y: 29, width: 18, height: 18), isRare ? .white : .systemRed)
        c.setStrokeColor(NSColor.black.withAlphaComponent(0.8).cgColor); c.setLineWidth(3)
        for sign: CGFloat in [-1, 1] {
            c.move(to: CGPoint(x: sign*10, y: -5)); c.addLine(to: CGPoint(x: sign*38, y: -28)); c.addLine(to: CGPoint(x: sign*52, y: -39))
            c.move(to: CGPoint(x: sign*12, y: 12)); c.addLine(to: CGPoint(x: sign*43, y: 5)); c.addLine(to: CGPoint(x: sign*55, y: -8))
        }
        c.strokePath()
    }
    private func drawCockroach(_ c: CGContext) {
        let flap = speed > 520 ? sin(phase) * 12 : 0
        if speed > 520 {
            c.setFillColor(NSColor(calibratedWhite: 0.82, alpha: 0.7).cgColor)
            c.addEllipse(in: CGRect(x: -58, y: -5-flap, width: 52, height: 22)); c.fillPath()
            c.addEllipse(in: CGRect(x: 6, y: -5+flap, width: 52, height: 22)); c.fillPath()
        }
        ellipse(c, CGRect(x: -27, y: -45, width: 54, height: 83), NSColor(calibratedRed: 0.32, green: 0.12, blue: 0.055, alpha: 1))
        ellipse(c, CGRect(x: -23, y: 27, width: 46, height: 31), NSColor(calibratedRed: 0.20, green: 0.07, blue: 0.035, alpha: 1))
        c.setStrokeColor(NSColor(calibratedRed: 0.08, green: 0.025, blue: 0.01, alpha: 1).cgColor); c.setLineWidth(2.5)
        c.move(to: CGPoint(x: 0, y: -43)); c.addLine(to: CGPoint(x: 0, y: 34))
        for sign: CGFloat in [-1, 1] {
            for y: CGFloat in [-25, -2, 19] { c.move(to: CGPoint(x: sign*18, y: y)); c.addLine(to: CGPoint(x: sign*43, y: y-13)); c.addLine(to: CGPoint(x: sign*55, y: y-22)) }
            c.move(to: CGPoint(x: sign*11, y: 50)); c.addCurve(to: CGPoint(x: sign*52, y: 72), control1: CGPoint(x: sign*24, y: 63), control2: CGPoint(x: sign*41, y: 70))
        }
        c.strokePath()
    }
    private func drawMeter(_ rect: NSRect, value: CGFloat, color: NSColor) {
        NSColor.black.withAlphaComponent(0.55).setFill(); NSBezierPath(roundedRect: rect, xRadius: 2, yRadius: 2).fill()
        color.setFill(); NSBezierPath(roundedRect: NSRect(x: rect.minX, y: rect.minY, width: rect.width*max(0,min(1,value)), height: rect.height), xRadius: 2, yRadius: 2).fill()
    }
}

struct EngineTick: Codable { let dt: Float; let left, top, width, height: Int; let mouseX, mouseY: Float; let threat: Bool; let action: String? }
struct EngineState: Codable { let x, y, heading, speed, health, fullness: Float; let size: Int; let skin, behavior: String; let dead: Bool; let spikes: Int64; let neurons, edges: Int; let avoidanceSkill, learnedEdgeRisk, navigationRate: Float; let successfulAvoidances, edgeCollisions: Int }

final class BrainEngine {
    private let process = Process(), input = Pipe(), output = Pipe()
    private var buffer = Data(), waiting = false
    private(set) var state: EngineState?
    init?() {
        #if arch(arm64)
        let architecture = "arm64"
        #else
        let architecture = "x86_64"
        #endif
        guard let resources = Bundle.main.resourceURL else { return nil }
        process.executableURL = resources.appendingPathComponent("engine-\(architecture)/FlyPet.Engine")
        process.standardInput = input; process.standardOutput = output; process.standardError = Pipe()
        do { try process.run() } catch { return nil }
        output.fileHandleForReading.readabilityHandler = { [weak self] handle in self?.receive(handle.availableData) }
    }
    deinit { output.fileHandleForReading.readabilityHandler = nil; process.terminate() }
    @discardableResult func tick(_ request: EngineTick) -> Bool {
        guard process.isRunning, !waiting, let data = try? JSONEncoder().encode(request) else { return false }
        waiting = true; input.fileHandleForWriting.write(data); input.fileHandleForWriting.write(Data([10]))
        return true
    }
    private func receive(_ data: Data) {
        guard !data.isEmpty else { return }; buffer.append(data)
        while let newline = buffer.firstIndex(of: 10) {
            let line = buffer.prefix(upTo: newline); buffer.removeSubrange(...newline)
            guard let reply = try? JSONDecoder().decode(EngineState.self, from: line) else { continue }
            DispatchQueue.main.async { [weak self] in self?.state = reply; self?.waiting = false }
        }
    }
}

final class PetController: NSObject {
    let window: NSWindow, view: PetView
    let engine: BrainEngine?
    var timer: Timer?, hidden = false, paused = false
    var onStatusChanged: (() -> Void)?
    private var lastTime = ProcessInfo.processInfo.systemUptime, pendingAction: String?
    override init() {
        view = PetView(frame: NSRect(x: 0, y: 0, width: 120, height: 120)); engine = BrainEngine()
        window = NSWindow(contentRect: view.frame, styleMask: [.borderless], backing: .buffered, defer: false)
        super.init(); window.contentView = view; window.isOpaque = false; window.backgroundColor = .clear; window.hasShadow = false; window.level = .floating
        window.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary]; window.isMovableByWindowBackground = false
        view.onHit = { [weak self] in self?.hit() }; recenter()
    }
    func start() { window.orderFrontRegardless(); timer = Timer.scheduledTimer(withTimeInterval: 1.0/60.0, repeats: true) { [weak self] _ in self?.tick() }; RunLoop.main.add(timer!, forMode: .common) }
    func toggleVisible() { hidden.toggle(); hidden ? window.orderOut(nil) : window.orderFrontRegardless(); onStatusChanged?() }
    func togglePause() { paused.toggle(); onStatusChanged?() }
    func switchSkin() { pendingAction = "skin:\(view.skin == .fly ? "cockroach" : "fly")"; onStatusChanged?() }
    func dropSugar() { let p=NSEvent.mouseLocation; pendingAction="drop:\(p.x):\(p.y)"; onStatusChanged?() }
    func recenter() { pendingAction="recenter"; guard let frame=NSScreen.main?.visibleFrame else{return}; window.setFrameOrigin(CGPoint(x:frame.midX-60,y:frame.midY-60)) }
    func revive() { paused=false; pendingAction="revive"; window.orderFrontRegardless(); onStatusChanged?() }
    private func hit() { pendingAction="hit:\(window.frame.midX):\(window.frame.midY)" }
    private func tick() {
        let now=ProcessInfo.processInfo.systemUptime, dt=Float(min(0.05,now-lastTime)); lastTime=now
        guard !paused, !hidden, let screen=window.screen ?? NSScreen.main else { return }
        let frame=screen.visibleFrame, mouse=NSEvent.mouseLocation
        let sent = engine?.tick(EngineTick(dt:dt,left:Int(frame.minX),top:Int(frame.minY),width:Int(frame.width),height:Int(frame.height),mouseX:Float(mouse.x),mouseY:Float(mouse.y),threat:true,action:pendingAction)) ?? false
        if sent { pendingAction=nil }
        guard let state=engine?.state else { return }
        view.skin=PetSkin(rawValue:state.skin) ?? .fly; view.heading=CGFloat(state.heading); view.speed=CGFloat(state.speed); view.health=CGFloat(state.health); view.fullness=CGFloat(state.fullness)
        let size=CGFloat(state.size); window.setFrame(NSRect(x:CGFloat(state.x)-size/2,y:CGFloat(state.y)-size/2,width:size,height:size),display:false); view.advance(CGFloat(dt)); onStatusChanged?()
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    private var statusItem: NSStatusItem!, pet: PetController!, visibleItem: NSMenuItem!, pauseItem: NSMenuItem!, skinItem: NSMenuItem!, learningItem: NSMenuItem!
    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory); statusItem=NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        if let b=statusItem.button { b.image=Self.menuBarIcon(); b.image?.isTemplate=true; b.toolTip="FlyPet · MaleCNS 桌宠" }
        let menu=NSMenu(); let title=NSMenuItem(title:"FlyPet · MaleCNS",action:nil,keyEquivalent:""); title.isEnabled=false; menu.addItem(title)
        learningItem=NSMenuItem(title:"脑引擎正在启动…",action:nil,keyEquivalent:""); learningItem.isEnabled=false; menu.addItem(learningItem); menu.addItem(.separator())
        visibleItem=menu.addItem(withTitle:"隐藏桌宠",action:#selector(toggleVisible),keyEquivalent:"h"); pauseItem=menu.addItem(withTitle:"暂停",action:#selector(togglePause),keyEquivalent:"p")
        skinItem=menu.addItem(withTitle:"切换为广东双马尾",action:#selector(switchSkin),keyEquivalent:"k"); menu.addItem(withTitle:"在鼠标处投糖",action:#selector(dropSugar),keyEquivalent:"s")
        menu.addItem(withTitle:"召回桌宠",action:#selector(recenter),keyEquivalent:"r"); menu.addItem(withTitle:"立即复活",action:#selector(revive),keyEquivalent:""); menu.addItem(.separator()); menu.addItem(withTitle:"退出 FlyPet",action:#selector(quit),keyEquivalent:"q")
        for item in menu.items { item.target=self }; statusItem.menu=menu
        pet=PetController(); pet.onStatusChanged={ [weak self] in self?.refreshMenu() }; pet.start(); refreshMenu()
    }
    private func refreshMenu() { visibleItem.title=pet.hidden ? "显示桌宠":"隐藏桌宠"; pauseItem.title=pet.paused ? "继续":"暂停"; skinItem.title=pet.view.skin == .fly ? "切换为广东双马尾":"切换为果蝇"; if let state=pet.engine?.state { learningItem.title="边缘经验 \(Int(state.avoidanceSkill*100))% · 成功 \(state.successfulAvoidances) · 撞击 \(state.edgeCollisions)"; statusItem.button?.toolTip="FlyPet · \(state.behavior) · 导航 \(String(format:"%.1f",state.navigationRate)) Hz" } else { learningItem.title="脑引擎正在启动…"; statusItem.button?.toolTip="FlyPet · MaleCNS 脑引擎正在启动" } }
    @objc private func toggleVisible(){pet.toggleVisible()}; @objc private func togglePause(){pet.togglePause()}; @objc private func switchSkin(){pet.switchSkin()}; @objc private func dropSugar(){pet.dropSugar()}; @objc private func recenter(){pet.recenter()}; @objc private func revive(){pet.revive()}; @objc private func quit(){NSApp.terminate(nil)}
    static func menuBarIcon() -> NSImage {
        let image=NSImage(size:NSSize(width:18,height:18),flipped:false){ _ in NSColor.black.setFill(); NSBezierPath(ovalIn:NSRect(x:6.5,y:3,width:5,height:11)).fill(); NSBezierPath(ovalIn:NSRect(x:1.5,y:7,width:6,height:5)).fill(); NSBezierPath(ovalIn:NSRect(x:10.5,y:7,width:6,height:5)).fill(); NSBezierPath(ovalIn:NSRect(x:6,y:12,width:6,height:5)).fill(); return true }; image.isTemplate=true; return image
    }
}

let app=NSApplication.shared, delegate=AppDelegate(); app.delegate=delegate; app.run()
