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

final class PetController: NSObject {
    let window: NSWindow, view: PetView
    var velocity = CGVector(dx: 180, dy: 100), timer: Timer?, hidden = false, paused = false, sugar: CGPoint?
    var onStatusChanged: (() -> Void)?
    private var lastTime = ProcessInfo.processInfo.systemUptime, wanderAngle = CGFloat.random(in: 0...(2 * .pi)), wanderRemaining: TimeInterval = 1
    override init() {
        view = PetView(frame: NSRect(x: 0, y: 0, width: 120, height: 120))
        view.skin = PetSkin(rawValue: UserDefaults.standard.string(forKey: "skin") ?? "fly") ?? .fly
        view.isRare = Double.random(in: 0...1) < 0.1
        window = NSWindow(contentRect: view.frame, styleMask: [.borderless], backing: .buffered, defer: false)
        super.init(); window.contentView = view; window.isOpaque = false; window.backgroundColor = .clear; window.hasShadow = false; window.level = .floating
        window.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary]; window.isMovableByWindowBackground = false
        view.onHit = { [weak self] in self?.hit() }; recenter()
    }
    func start() { window.orderFrontRegardless(); timer = Timer.scheduledTimer(withTimeInterval: 1.0/60.0, repeats: true) { [weak self] _ in self?.tick() }; RunLoop.main.add(timer!, forMode: .common) }
    func toggleVisible() { hidden.toggle(); hidden ? window.orderOut(nil) : window.orderFrontRegardless(); onStatusChanged?() }
    func togglePause() { paused.toggle(); onStatusChanged?() }
    func switchSkin() { view.skin = view.skin == .fly ? .cockroach : .fly; view.isRare = Double.random(in: 0...1) < 0.1; UserDefaults.standard.set(view.skin.rawValue, forKey: "skin"); onStatusChanged?(); view.needsDisplay = true }
    func dropSugar() { sugar = NSEvent.mouseLocation; onStatusChanged?() }
    func recenter() { guard let frame = NSScreen.main?.visibleFrame else { return }; window.setFrameOrigin(CGPoint(x: frame.midX-60, y: frame.midY-60)) }
    func revive() { view.health = 100; view.fullness = 65; view.isRare = Double.random(in: 0...1) < 0.1; paused = false; recenter(); window.orderFrontRegardless(); onStatusChanged?() }
    private func hit() { view.health = max(0, view.health-40); velocity.dx *= -1.5; velocity.dy *= -1.5; if view.health <= 0 { paused = true }; onStatusChanged?() }
    private func tick() {
        let now = ProcessInfo.processInfo.systemUptime, dt = CGFloat(min(0.05, now-lastTime)); lastTime = now
        guard !paused, !hidden, view.health > 0, let screen = window.screen ?? NSScreen.main else { return }
        let frame = screen.visibleFrame.insetBy(dx: 42, dy: 42); var center = CGPoint(x: window.frame.midX, y: window.frame.midY); let mouse = NSEvent.mouseLocation
        let mx = center.x-mouse.x, my = center.y-mouse.y, distance = hypot(mx,my)
        wanderRemaining -= Double(dt); if wanderRemaining <= 0 { wanderRemaining = Double.random(in: 1.4...3.8); wanderAngle += CGFloat.random(in: -1.8...1.8) }
        var desired = CGVector(dx: cos(wanderAngle), dy: sin(wanderAngle)); var targetSpeed: CGFloat = view.skin == .cockroach ? 190 : 235
        if let food = sugar { let fx=food.x-center.x, fy=food.y-center.y, fd=max(1,hypot(fx,fy)); if fd < 45 { sugar=nil; view.fullness=min(100,view.fullness+32); view.health=min(100,view.health+10) } else if view.fullness < 92 { desired=CGVector(dx: fx/fd,dy: fy/fd) } }
        if distance < 260 { let d=max(1,distance); desired=CGVector(dx: mx/d,dy: my/d); targetSpeed *= 2.7 }
        let blend=1-exp(-5*dt); velocity.dx += (desired.dx*targetSpeed-velocity.dx)*blend; velocity.dy += (desired.dy*targetSpeed-velocity.dy)*blend
        center.x += velocity.dx*dt; center.y += velocity.dy*dt
        if center.x < frame.minX || center.x > frame.maxX { velocity.dx *= -0.72; center.x=min(frame.maxX,max(frame.minX,center.x)); wanderAngle=atan2(velocity.dy,velocity.dx) }
        if center.y < frame.minY || center.y > frame.maxY { velocity.dy *= -0.72; center.y=min(frame.maxY,max(frame.minY,center.y)); wanderAngle=atan2(velocity.dy,velocity.dx) }
        let size: CGFloat = view.skin == .cockroach ? (view.isRare ? 300 : 132) : 112
        window.setFrame(NSRect(x: center.x-size/2,y: center.y-size/2,width: size,height: size), display: false)
        view.heading=atan2(velocity.dx,velocity.dy); view.speed=hypot(velocity.dx,velocity.dy); view.fullness=max(0,view.fullness-dt*0.01); view.advance(dt)
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    private var statusItem: NSStatusItem!, pet: PetController!, visibleItem: NSMenuItem!, pauseItem: NSMenuItem!, skinItem: NSMenuItem!
    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory); statusItem=NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        if let b=statusItem.button { b.image=Self.menuBarIcon(); b.image?.isTemplate=true; b.toolTip="FlyPet · MaleCNS 桌宠" }
        let menu=NSMenu(); let title=NSMenuItem(title:"FlyPet · MaleCNS",action:nil,keyEquivalent:""); title.isEnabled=false; menu.addItem(title); menu.addItem(.separator())
        visibleItem=menu.addItem(withTitle:"隐藏桌宠",action:#selector(toggleVisible),keyEquivalent:"h"); pauseItem=menu.addItem(withTitle:"暂停",action:#selector(togglePause),keyEquivalent:"p")
        skinItem=menu.addItem(withTitle:"切换为广东双马尾",action:#selector(switchSkin),keyEquivalent:"k"); menu.addItem(withTitle:"在鼠标处投糖",action:#selector(dropSugar),keyEquivalent:"s")
        menu.addItem(withTitle:"召回桌宠",action:#selector(recenter),keyEquivalent:"r"); menu.addItem(withTitle:"立即复活",action:#selector(revive),keyEquivalent:""); menu.addItem(.separator()); menu.addItem(withTitle:"退出 FlyPet",action:#selector(quit),keyEquivalent:"q")
        for item in menu.items { item.target=self }; statusItem.menu=menu
        pet=PetController(); pet.onStatusChanged={ [weak self] in self?.refreshMenu() }; pet.start(); refreshMenu()
    }
    private func refreshMenu() { visibleItem.title=pet.hidden ? "显示桌宠":"隐藏桌宠"; pauseItem.title=pet.paused ? "继续":"暂停"; skinItem.title=pet.view.skin == .fly ? "切换为广东双马尾":"切换为果蝇"; statusItem.button?.toolTip="FlyPet · 生命 \(Int(pet.view.health))% · 饱腹 \(Int(pet.view.fullness))%" }
    @objc private func toggleVisible(){pet.toggleVisible()}; @objc private func togglePause(){pet.togglePause()}; @objc private func switchSkin(){pet.switchSkin()}; @objc private func dropSugar(){pet.dropSugar()}; @objc private func recenter(){pet.recenter()}; @objc private func revive(){pet.revive()}; @objc private func quit(){NSApp.terminate(nil)}
    static func menuBarIcon() -> NSImage {
        let image=NSImage(size:NSSize(width:18,height:18),flipped:false){ _ in NSColor.black.setFill(); NSBezierPath(ovalIn:NSRect(x:6.5,y:3,width:5,height:11)).fill(); NSBezierPath(ovalIn:NSRect(x:1.5,y:7,width:6,height:5)).fill(); NSBezierPath(ovalIn:NSRect(x:10.5,y:7,width:6,height:5)).fill(); NSBezierPath(ovalIn:NSRect(x:6,y:12,width:6,height:5)).fill(); return true }; image.isTemplate=true; return image
    }
}

let app=NSApplication.shared, delegate=AppDelegate(); app.delegate=delegate; app.run()
