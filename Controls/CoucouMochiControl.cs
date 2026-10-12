using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DynamicIslandWin.Services;

namespace DynamicIslandWin.Controls;

public enum MochiState
{
    Idle,
    Working,
    Thinking,
    Searching,
    Approval,
    Question,
    Error,
    Finished,
    RateLimit,
    Sleeping,
    Dizzy
}

public enum MochiEyeShape
{
    Pill,
    Wide,
    Dot,
    Line,
    Flat,
    Happy,
    Closed,
    Spiral,
    Heart,
    Star,
    Tired,
    Wink,
    Cup
}

public enum MochiEmote
{
    Love,
    Surprised,
    Proud,
    Wink,
    Yawn,
    Happy,
    Annoyed
}

public class MochiParticle
{
    public string Type = "heart"; // heart, star, spark, sweat, z
    public double X, Y;
    public double Vx, Vy;
    public double Age;
    public double Life;
    public double Rot;
    public double Size;
}

/// <summary>
/// Authentic Coucou Mochi Canvas Element for WPF.
/// Features super-ellipse (squircle) body, 3D perspective eye tracking,
/// 12 expressive eye shapes, breathing spring physics, mouth file swallow,
/// floating particle bursts, and tactile poke/pet responses.
/// </summary>
public class CoucouMochiControl : FrameworkElement
{
    private static readonly Color BaseTopColor = Color.FromRgb(255, 250, 245);  // #FFFAF5
    private static readonly Color BaseBottomColor = Color.FromRgb(221, 204, 191); // #DDCCBF
    private static readonly Color InkColor = Color.FromRgb(26, 20, 18);        // #1A1412
    private static readonly Color BlushPink = Color.FromArgb(140, 255, 120, 150);

    // Spring & physics state
    private double _yaw = 0, _pitch = 0, _roll = 0, _tilt = 0, _open = 1.0;
    private double _sx = 1.0, _sy = 1.0, _oy = 0, _ox = 0;
    private double _tint = 0, _morph = 0, _hands = 0, _blush = 0, _es = 1.0;

    // Target values for spring interpolation
    private double _tgYaw = 0, _tgPitch = 0, _tgTilt = 0, _tgSy = 1.0, _tgSx = 1.0, _tgEs = 1.0, _tgOy = 0;

    // Mouth swallow physics
    private double _slotH = 0;
    private double _slotHTarget = 0;
    private double _slotHVel = 0;
    private bool _isChewing = false;

    // Color tints
    private Color _currentColor = Color.FromRgb(230, 233, 238);
    private Color _targetColor = Color.FromRgb(230, 233, 238);

    private MochiState _state = MochiState.Idle;
    private MochiEyeShape? _eyeOverride = null;
    private double _eyeOverrideUntil = 0;

    private readonly List<MochiParticle> _particles = new();

    // Timing & animation
    private double _lastRenderTime = 0;
    private double _nextBlink = 0;
    private double _waveUntil = 0;
    private double _lastAmbientTime = 0;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    // Normalized look target (-1.0 to +1.0)
    public double LookTargetX { get; set; } = 0;
    public double LookTargetY { get; set; } = 0;

    // Rapid clicking / poke detection
    private readonly List<double> _pokeTimestamps = new();

    public CoucouMochiControl()
    {
        ClipToBounds = false;
        Cursor = Cursors.Hand;
        Focusable = false;

        _lastRenderTime = _stopwatch.Elapsed.TotalSeconds;
        _nextBlink = _lastRenderTime + 2.0;

        CompositionTarget.Rendering += OnCompositionRendering;

        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        // Curious tilt & slight squash
        _tgTilt = 0.08;
        _tgSy = 0.95;
        _tgSx = 1.05;
        Squash(0.92);
        InvalidateVisual();
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        _tgTilt = 0;
        _tgSy = 1.0;
        _tgSx = 1.0;
        LookTargetX = 0;
        LookTargetY = 0;
        InvalidateVisual();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        double now = _stopwatch.Elapsed.TotalSeconds;
        _pokeTimestamps.RemoveAll(t => now - t > 1.6);
        _pokeTimestamps.Add(now);

        if (_pokeTimestamps.Count >= 3)
        {
            // Rapid poke -> Dizzy spin!
            _pokeTimestamps.Clear();
            TriggerDizzy();
        }
        else
        {
            // Gentle Pet / Tickle -> Hearts & purr
            Pet();
        }

        e.Handled = true;
    }

    public void SetLookAt(double screenX, double screenY)
    {
        try
        {
            if (!IsLoaded || ActualWidth <= 0 || ActualHeight <= 0) return;
            Point center = PointToScreen(new Point(ActualWidth / 2.0, ActualHeight / 2.0));
            double dx = (screenX - center.X) / 380.0;
            double dy = (screenY - center.Y) / 300.0;
            LookTargetX = Math.Clamp(dx, -1.2, 1.2);
            LookTargetY = Math.Clamp(dy, -1.0, 1.0);
        }
        catch { }
    }

    public void SetState(MochiState newState)
    {
        _state = newState;
        switch (newState)
        {
            case MochiState.Idle:
                _targetColor = Color.FromRgb(230, 233, 238);
                _tint = 0;
                _eyeOverride = null;
                break;
            case MochiState.Working:
                _targetColor = Color.FromRgb(59, 158, 255);
                _tint = 0.72;
                _eyeOverride = MochiEyeShape.Pill;
                SoundEffectsService.PlayCoucou("work");
                break;
            case MochiState.Thinking:
                _targetColor = Color.FromRgb(139, 92, 246);
                _tint = 0.72;
                _eyeOverride = MochiEyeShape.Pill;
                SoundEffectsService.PlayCoucou("think");
                break;
            case MochiState.Searching:
                _targetColor = Color.FromRgb(99, 102, 241);
                _tint = 0.72;
                SoundEffectsService.PlayCoucou("search");
                break;
            case MochiState.Approval:
                _targetColor = Color.FromRgb(245, 165, 36);
                _tint = 0.78;
                _eyeOverride = MochiEyeShape.Wide;
                SoundEffectsService.PlayCoucou("approval");
                break;
            case MochiState.Finished:
                _targetColor = Color.FromRgb(52, 211, 153);
                _tint = 0.35;
                _eyeOverride = MochiEyeShape.Happy;
                Roll(1, 950);
                EmitParticles("spark", 5);
                SoundEffectsService.PlayCoucou("finish");
                break;
            case MochiState.Sleeping:
                _targetColor = Color.FromRgb(148, 163, 184);
                _tint = 0.32;
                _eyeOverride = MochiEyeShape.Closed;
                SoundEffectsService.PlayCoucou("sleep");
                break;
            case MochiState.Dizzy:
                _targetColor = Color.FromRgb(244, 114, 182);
                _tint = 0.70;
                _eyeOverride = MochiEyeShape.Spiral;
                Roll(2, 1300);
                SoundEffectsService.PlayCoucou("dizzy");
                break;
        }
    }

    public void PlayEmote(MochiEmote emote, int durationMs = 1800)
    {
        double now = _stopwatch.Elapsed.TotalSeconds;
        _eyeOverrideUntil = now + (durationMs / 1000.0);

        switch (emote)
        {
            case MochiEmote.Love:
                _eyeOverride = MochiEyeShape.Heart;
                _blush = 1.0;
                Squash(0.85);
                EmitParticles("heart", 4);
                SoundEffectsService.PlayCoucou("love");
                break;
            case MochiEmote.Happy:
                _eyeOverride = MochiEyeShape.Happy;
                _blush = 0.6;
                Squash(0.82);
                SoundEffectsService.PlayCoucou("pop");
                break;
            case MochiEmote.Wink:
                _eyeOverride = MochiEyeShape.Wink;
                _tgTilt = 0.12;
                SoundEffectsService.PlayCoucou("wink");
                break;
            case MochiEmote.Surprised:
                _eyeOverride = MochiEyeShape.Dot;
                _tgEs = 1.25;
                Squash(0.8);
                SoundEffectsService.PlayCoucou("peek");
                break;
            case MochiEmote.Proud:
                _eyeOverride = MochiEyeShape.Star;
                EmitParticles("star", 4);
                SoundEffectsService.PlayCoucou("proud");
                break;
            case MochiEmote.Yawn:
                _eyeOverride = MochiEyeShape.Tired;
                _tgSy = 1.15;
                _tgSx = 0.92;
                EmitParticles("z", 2);
                SoundEffectsService.PlayCoucou("yawn");
                break;
            case MochiEmote.Annoyed:
                _eyeOverride = MochiEyeShape.Line;
                _tgTilt = -0.15;
                SoundEffectsService.PlayCoucou("annoyed");
                break;
        }
    }

    public void Pet()
    {
        PlayEmote(MochiEmote.Love, 1600);
    }

    public void SetEyeShape(MochiEyeShape shape, int durationMs = 2500)
    {
        double now = _stopwatch.Elapsed.TotalSeconds;
        _eyeOverride = shape;
        _eyeOverrideUntil = now + (durationMs / 1000.0);
    }

    public void SpawnPetParticles()
    {
        EmitParticles("heart", 6);
    }

    public void SpawnStarParticles()
    {
        EmitParticles("star", 6);
    }

    public void TriggerDizzy()
    {
        SetState(MochiState.Dizzy);
    }

    public void Greet()
    {
        double now = _stopwatch.Elapsed.TotalSeconds;
        _waveUntil = now + 1.8;
        _hands = 1.0;
        PlayEmote(MochiEmote.Happy, 1800);
        SoundEffectsService.PlayCoucou("greet");
    }

    public void Blink()
    {
        _open = 0.08;
    }

    public void Squash(double amount = 0.85)
    {
        _sy = amount;
        _sx = 1.0 + (1.0 - amount) * 0.9;
    }

    public void Roll(int turns = 1, int durationMs = 900)
    {
        _roll = turns * Math.PI * 2;
    }

    public void OpenMouth(double amount = 1.0)
    {
        _slotHTarget = Math.Clamp(amount, 0, 1.35);
        _morph = 0.85;
        _eyeOverride = MochiEyeShape.Cup;
        _eyeOverrideUntil = _stopwatch.Elapsed.TotalSeconds + 4.0;
    }

    public void CloseMouth()
    {
        _slotHTarget = 0;
        _morph = 0;
        _isChewing = false;
        _eyeOverride = null;
    }

    public void SwallowFile(Action? onComplete = null)
    {
        _isChewing = true;
        _slotHTarget = 0;
        _morph = 0.9;
        _eyeOverride = MochiEyeShape.Happy;
        _eyeOverrideUntil = _stopwatch.Elapsed.TotalSeconds + 2.5;

        SoundEffectsService.PlayCoucou("gulp");
        EmitParticles("spark", 3);

        // Chew motion: quick squashes
        Squash(0.82);
    }

    public void EmitParticles(string type, int count)
    {
        Random rnd = Random.Shared;
        for (int i = 0; i < count; i++)
        {
            _particles.Add(new MochiParticle
            {
                Type = type,
                X = (rnd.NextDouble() - 0.5) * 0.8 + (type == "z" ? 0.45 : 0),
                Y = -0.7 - rnd.NextDouble() * 0.2,
                Vx = (rnd.NextDouble() - 0.5) * 0.3 + (type == "z" ? 0.16 : 0),
                Vy = -(0.4 + rnd.NextDouble() * 0.35),
                Age = -i * 0.12,
                Life = 1.2 + rnd.NextDouble() * 0.5,
                Rot = rnd.NextDouble() * Math.PI * 2,
                Size = 0.16 + rnd.NextDouble() * 0.08
            });
        }
    }

    private void OnCompositionRendering(object? sender, EventArgs e)
    {
        double now = _stopwatch.Elapsed.TotalSeconds;
        double dt = Math.Min(0.05, now - _lastRenderTime);
        _lastRenderTime = now;

        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        UpdatePhysics(now, dt);
        InvalidateVisual();
    }

    private void UpdatePhysics(double now, double dt)
    {
        double t = now;

        // 1. Mouse Look-at & Eye Target Interpolation
        double targetY = LookTargetX * 0.62;
        double targetP = LookTargetY * 0.5;

        if (_state == MochiState.Thinking)
        {
            targetY = targetY * 0.35 + 0.55 * 0.55;
            targetP = targetP * 0.3 + 0.55 * 0.5;
        }
        else if (_state == MochiState.Searching)
        {
            targetY = Math.Sin(t * 2.6) * 0.6;
            targetP = -0.06;
        }
        else if (_state == MochiState.Sleeping)
        {
            targetY = 0;
            targetP = -0.14;
        }
        else if (_state == MochiState.Dizzy)
        {
            targetY = Math.Sin(t * 9.0) * 0.25;
        }

        _tgYaw = targetY;
        _tgPitch = targetP;

        // 2. Idle Breathing (Freebuff spring breathing)
        bool breathes = _state == MochiState.Sleeping || _state == MochiState.Idle;
        if (breathes)
        {
            _tgSy = 1.0 + Math.Sin(t * 1.8) * 0.038;
            _tgSx = 1.0 - Math.Sin(t * 1.8) * 0.022;
        }
        else
        {
            _tgSy = 1.0;
            _tgSx = 1.0;
        }

        // Chewing bounce
        if (_isChewing)
        {
            _tgSy = 1.0 + Math.Sin(t * 14.0) * 0.12;
            _tgSx = 1.0 - Math.Sin(t * 14.0) * 0.08;
        }

        // 3. Smooth Damping to Targets
        double kLook = 1.0 - Math.Pow(0.0025, dt);
        double kGen = 1.0 - Math.Pow(0.0008, dt);

        _yaw += (_tgYaw - _yaw) * kLook;
        _pitch += (_tgPitch - _pitch) * kLook;
        _tilt += (_tgTilt - _tilt) * kGen;
        _sy += (_tgSy - _sy) * kGen;
        _sx += (_tgSx - _sx) * kGen;
        _es += (_tgEs - _es) * kGen;
        _oy += (_tgOy - _oy) * kGen;

        if (_roll > 0.01)
        {
            _roll = Math.Max(0, _roll - dt * 6.0);
        }

        // Open/Blink animation
        if (_open < 0.98)
        {
            _open = Math.Min(1.0, _open + dt * 7.5);
        }

        // Blush fade
        if (_blush > 0.01)
        {
            _blush = Math.Max(0, _blush - dt * 0.35);
        }

        // Mouth spring: ω₀ = 2π/0.25, ζ = 0.6
        double omega = (2.0 * Math.PI) / 0.25;
        double zeta = 0.6;
        double acc = omega * omega * (_slotHTarget - _slotH) - 2.0 * zeta * omega * _slotHVel;
        _slotHVel += acc * dt;
        _slotH = Math.Max(0, _slotH + _slotHVel * dt);

        // Blinking timer
        if (now > _nextBlink)
        {
            if (_state != MochiState.Sleeping && _state != MochiState.Dizzy)
            {
                Blink();
            }
            _nextBlink = now + 2.2 + Random.Shared.NextDouble() * 3.2;
        }

        // Ambient particles (Zs when sleeping)
        if (now - _lastAmbientTime > 1.4)
        {
            _lastAmbientTime = now;
            if (_state == MochiState.Sleeping)
            {
                EmitParticles("z", 1);
            }
        }

        // Eye override expiry
        if (_eyeOverride.HasValue && now > _eyeOverrideUntil)
        {
            _eyeOverride = null;
        }

        // Update particles
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Age += dt;
            if (p.Age >= p.Life)
            {
                _particles.RemoveAt(i);
            }
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Radius calculation
        double R = Math.Min(w, h) * 0.36;
        double rx = R * 1.14;
        double ry = R * 0.88;

        double cx = w / 2.0 + _ox * R;
        double cy = h / 2.0 + _oy * R + R * 0.06;

        dc.PushTransform(new TranslateTransform(cx, cy));
        if (_tilt != 0) dc.PushTransform(new RotateTransform(_tilt * 180.0 / Math.PI));
        dc.PushTransform(new ScaleTransform(_sx, _sy));

        // 1. Hands behind
        DrawHands(dc, R, rx, ry, front: false);

        // 2. Super-ellipse Body Geometry
        Geometry bodyGeom = CreateSuperEllipseGeometry(rx, ry, _morph, R);

        // 3. Draw Body Shading (Soft gradient + 3D depth)
        DrawBody(dc, bodyGeom, rx, ry, R);

        // 4. Soft Rosy Cheeks (Blush)
        double blushVal = Math.Max(_blush, _state == MochiState.Idle ? 0.35 : 0.15) * (1.0 - _morph);
        if (blushVal > 0.02)
        {
            double yawOffset = Math.Sin(_yaw) * rx * 0.8;
            byte blushAlpha = (byte)(Math.Clamp(blushVal * 120, 0, 200));
            var blushBrush = new SolidColorBrush(Color.FromArgb(blushAlpha, 255, 120, 150));
            blushBrush.Freeze();

            dc.DrawEllipse(blushBrush, null, new Point(-rx * 0.55 + yawOffset, ry * 0.22), R * 0.17, R * 0.10);
            dc.DrawEllipse(blushBrush, null, new Point(rx * 0.55 + yawOffset, ry * 0.22), R * 0.17, R * 0.10);
        }

        // 5. Draw 3D Eyes
        DrawEyes(dc, rx, ry, R);

        // 6. Draw Mouth Slot
        if (_morph > 0.05 || _slotH > 0.05)
        {
            DrawMouth(dc, R);
        }

        // 7. Hands in front
        DrawHands(dc, R, rx, ry, front: true);

        // Pop transforms
        dc.Pop(); // Scale
        if (_tilt != 0) dc.Pop(); // Rotate
        dc.Pop(); // Translate

        // 8. Draw Flying Particles (Hearts, Stars, Zs)
        DrawParticles(dc, cx, cy, R);
    }

    private Geometry CreateSuperEllipseGeometry(double rx, double ry, double morph, double R)
    {
        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            int n = 72;
            double expN = 2.0 / 2.7; // ~0.7407 authentic Coucou exponent
            for (int i = 0; i <= n; i++)
            {
                double a = (double)i / n * Math.PI * 2.0;
                double ca = Math.Cos(a);
                double sa = Math.Sin(a);
                double px = rx * Math.Sign(ca) * Math.Pow(Math.Abs(ca), expN);
                double py = ry * Math.Sign(sa) * Math.Pow(Math.Abs(sa), expN);

                if (morph > 0.01)
                {
                    double w = rx * 1.02;
                    double h = ry * 0.94;
                    double cr = Math.Min(rx, ry) * 0.34;
                    // Blend to rounded rect
                    px = px * (1.0 - morph) + Math.Clamp(px, -w + cr, w - cr) * morph;
                    py = py * (1.0 - morph) + Math.Clamp(py, -h + cr, h - cr) * morph;
                }

                Point pt = new Point(px, py);
                if (i == 0) ctx.BeginFigure(pt, isFilled: true, isClosed: true);
                else ctx.LineTo(pt, isStroked: false, isSmoothJoin: true);
            }
        }
        geom.Freeze();
        return geom;
    }

    private void DrawBody(DrawingContext dc, Geometry body, double rx, double ry, double R)
    {
        // 1. Base Porcelain/Cream Linear Gradient
        var baseBrush = new LinearGradientBrush(
            BaseTopColor,
            BaseBottomColor,
            new Point(0.7, 0.1),
            new Point(0.2, 0.9));
        baseBrush.Freeze();
        dc.DrawGeometry(baseBrush, null, body);

        // 2. Neon State Tint (if active)
        if (_tint > 0.02)
        {
            byte tintAlpha = (byte)(Math.Clamp(_tint * 180, 0, 220));
            Color tintCol = Color.FromArgb(tintAlpha, _targetColor.R, _targetColor.G, _targetColor.B);
            var tintBrush = new LinearGradientBrush(
                tintCol,
                Color.FromArgb(0, _targetColor.R, _targetColor.G, _targetColor.B),
                new Point(0.5, 1.0),
                new Point(0.5, 0.0));
            tintBrush.Freeze();
            dc.DrawGeometry(tintBrush, null, body);
        }

        // 3. 3D Radial Shadow for depth
        var shadowBrush = new RadialGradientBrush(
            Colors.Transparent,
            Color.FromArgb(50, 0, 0, 0))
        {
            Center = new Point(0.5, 0.4),
            RadiusX = 0.6,
            RadiusY = 0.6
        };
        shadowBrush.Freeze();
        dc.DrawGeometry(shadowBrush, null, body);

        // 4. Soft Specular Gloss Highlight at top-right
        var highlightBrush = new RadialGradientBrush(
            Color.FromArgb(140, 255, 255, 255),
            Colors.Transparent)
        {
            Center = new Point(0.65, 0.25),
            RadiusX = 0.35,
            RadiusY = 0.35
        };
        highlightBrush.Freeze();
        dc.DrawGeometry(highlightBrush, null, body);
    }

    private void DrawEyes(DrawingContext dc, double rx, double ry, double R)
    {
        MochiEyeShape shape = _eyeOverride ?? (_state == MochiState.Sleeping ? MochiEyeShape.Closed : MochiEyeShape.Pill);
        if (_morph > 0.5)
        {
            shape = _isChewing ? MochiEyeShape.Happy : MochiEyeShape.Cup;
        }

        var inkBrush = new SolidColorBrush(InkColor);
        inkBrush.Freeze();
        var inkPen = new Pen(inkBrush, R * 0.08) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        inkPen.Freeze();

        double eyeSp = 0.37;
        double eyeP = -0.12;

        for (int sd = -1; sd <= 1; sd += 2)
        {
            double yaw = sd * eyeSp + _yaw;
            double pitch = eyeP + _pitch + _roll;
            pitch = ((pitch + Math.PI) % (Math.PI * 2) + Math.PI * 2) % (Math.PI * 2) - Math.PI;

            double cp = Math.Cos(pitch);
            if (Math.Cos(yaw) * cp < 0.04) continue;

            double px = Math.Sin(yaw) * cp * rx;
            double py = -Math.Sin(pitch) * ry + (_morph > 0 ? ry * 0.14 * _morph : 0);

            double fx = Math.Clamp(Math.Cos(yaw), 0.25, 1.0);
            double fy = Math.Clamp(cp, 0.25, 1.0);

            double ew = R * 0.25 * _es;
            double eh = R * 0.27 * _es;

            dc.PushTransform(new TranslateTransform(px, py));
            dc.PushTransform(new ScaleTransform(fx, fy));

            DrawSingleEye(dc, shape, ew, eh, _open, sd, inkBrush, inkPen);

            dc.Pop(); // Scale
            dc.Pop(); // Translate
        }
    }

    private void DrawSingleEye(DrawingContext dc, MochiEyeShape shape, double w, double h, double open, int sd, Brush ink, Pen pen)
    {
        double t = _stopwatch.Elapsed.TotalSeconds;

        switch (shape)
        {
            case MochiEyeShape.Wide:
                w *= 1.16;
                h *= 1.12;
                goto case MochiEyeShape.Pill;

            case MochiEyeShape.Pill:
                {
                    double hh = Math.Max(h * open, w * 0.3);
                    double radius = Math.Min(w / 2.0, hh / 2.0);
                    dc.DrawRoundedRectangle(ink, null, new Rect(-w / 2.0, -hh / 2.0, w, hh), radius, radius);
                    break;
                }

            case MochiEyeShape.Dot:
                dc.DrawEllipse(ink, null, new Point(0, 0), w * 0.45, w * 0.45);
                break;

            case MochiEyeShape.Line:
                dc.DrawRoundedRectangle(ink, null, new Rect(-w * 0.78, -w * 0.2, w * 1.56, w * 0.4), w * 0.2, w * 0.2);
                break;

            case MochiEyeShape.Happy:
                {
                    // Uplifting arc (^ ^)
                    var geo = new StreamGeometry();
                    using (var ctx = geo.Open())
                    {
                        ctx.BeginFigure(new Point(-w * 0.75, h * 0.1), isFilled: false, isClosed: false);
                        ctx.ArcTo(new Point(w * 0.75, h * 0.1), new Size(w * 0.8, h * 0.85), 0, false, SweepDirection.Clockwise, true, true);
                    }
                    geo.Freeze();
                    dc.DrawGeometry(null, pen, geo);
                    break;
                }

            case MochiEyeShape.Closed:
                {
                    // Sleeping downward arc (- -)
                    var geo = new StreamGeometry();
                    using (var ctx = geo.Open())
                    {
                        ctx.BeginFigure(new Point(-w * 0.75, -h * 0.1), isFilled: false, isClosed: false);
                        ctx.ArcTo(new Point(w * 0.75, -h * 0.1), new Size(w * 0.8, h * 0.65), 0, false, SweepDirection.Counterclockwise, true, true);
                    }
                    geo.Freeze();
                    dc.DrawGeometry(null, pen, geo);
                    break;
                }

            case MochiEyeShape.Spiral:
                {
                    // Dizzy swirling spiral
                    var geo = new StreamGeometry();
                    using (var ctx = geo.Open())
                    {
                        for (double a = 0; a < 4.2 * Math.PI; a += 0.2)
                        {
                            double r = w * 0.06 + a * w * 0.055;
                            double aa = a + t * 9.0 * sd;
                            Point p = new Point(Math.Cos(aa) * r, Math.Sin(aa) * r);
                            if (a == 0) ctx.BeginFigure(p, isFilled: false, isClosed: false);
                            else ctx.LineTo(p, isStroked: true, isSmoothJoin: true);
                        }
                    }
                    geo.Freeze();
                    var spiralPen = new Pen(ink, w * 0.18) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                    spiralPen.Freeze();
                    dc.DrawGeometry(null, spiralPen, geo);
                    break;
                }

            case MochiEyeShape.Heart:
                {
                    var heartBrush = new SolidColorBrush(Color.FromRgb(255, 77, 109));
                    heartBrush.Freeze();
                    Geometry heartGeom = CreateHeartGeometry(w * 1.15);
                    dc.DrawGeometry(heartBrush, null, heartGeom);
                    break;
                }

            case MochiEyeShape.Star:
                {
                    var starBrush = new SolidColorBrush(Color.FromRgb(247, 179, 43));
                    starBrush.Freeze();
                    dc.PushTransform(new RotateTransform(t * 120.0 * sd));
                    Geometry starGeom = CreateStarGeometry(w * 1.05, w * 0.46);
                    dc.DrawGeometry(starBrush, null, starGeom);
                    dc.Pop();
                    break;
                }

            case MochiEyeShape.Wink:
                if (sd < 0)
                {
                    double hh = Math.Max(h * open, w * 0.3);
                    dc.DrawRoundedRectangle(ink, null, new Rect(-w / 2.0, -hh / 2.0, w, hh), w / 2.0, w / 2.0);
                }
                else
                {
                    // Winking arc
                    var geo = new StreamGeometry();
                    using (var ctx = geo.Open())
                    {
                        ctx.BeginFigure(new Point(-w * 0.75, h * 0.1), isFilled: false, isClosed: false);
                        ctx.ArcTo(new Point(w * 0.75, h * 0.1), new Size(w * 0.8, h * 0.85), 0, false, SweepDirection.Clockwise, true, true);
                    }
                    geo.Freeze();
                    dc.DrawGeometry(null, pen, geo);
                }
                break;

            case MochiEyeShape.Cup:
                {
                    // U-shaped curious/excited eye
                    double hh = Math.Max(h * open, w * 0.45);
                    var geo = new StreamGeometry();
                    using (var ctx = geo.Open())
                    {
                        ctx.BeginFigure(new Point(-w / 2.0, -hh / 2.0), isFilled: true, isClosed: true);
                        ctx.LineTo(new Point(w / 2.0, -hh / 2.0), isStroked: false, isSmoothJoin: false);
                        ctx.LineTo(new Point(w / 2.0, hh / 4.0), isStroked: false, isSmoothJoin: false);
                        ctx.ArcTo(new Point(-w / 2.0, hh / 4.0), new Size(w / 2.0, hh / 2.0), 0, false, SweepDirection.Clockwise, true, true);
                    }
                    geo.Freeze();
                    dc.DrawGeometry(ink, null, geo);
                    break;
                }

            default:
                dc.DrawRoundedRectangle(ink, null, new Rect(-w / 2.0, -h / 2.0, w, h), w / 2.0, w / 2.0);
                break;
        }
    }

    private void DrawMouth(DrawingContext dc, double R)
    {
        double m = _morph;
        double hW = R * 1.7 * m;
        double hH = Math.Max(1.0, _slotH * R * m * 1.2);
        double hX = -hW / 2.0;
        double hY = R * 0.22;

        if (hH > 1.5)
        {
            var mouthBg = new SolidColorBrush(Color.FromRgb(24, 16, 22));
            mouthBg.Freeze();
            double radius = Math.Min(hW / 2.0, hH / 2.0);
            dc.DrawRoundedRectangle(mouthBg, null, new Rect(hX, hY, hW, hH), radius, radius);

            // Tongue
            if (hH > 6.0)
            {
                var tongueBrush = new SolidColorBrush(Color.FromRgb(255, 105, 140));
                tongueBrush.Freeze();
                dc.DrawEllipse(tongueBrush, null, new Point(0, hY + hH - 3), hW * 0.28, hH * 0.32);
            }
        }
    }

    private void DrawHands(DrawingContext dc, double R, double rx, double ry, bool front)
    {
        if (_hands < 0.05 && _waveUntil <= _stopwatch.Elapsed.TotalSeconds) return;

        double hr = R * 0.22 * Math.Max(_hands, 0.85);
        double t = _stopwatch.Elapsed.TotalSeconds;

        var pawBrush = new LinearGradientBrush(
            BaseTopColor,
            BaseBottomColor,
            new Point(0.5, 0.0),
            new Point(0.5, 1.0));
        pawBrush.Freeze();
        var pawBorder = new Pen(new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)), 1.0);
        pawBorder.Freeze();

        for (int sd = -1; sd <= 1; sd += 2)
        {
            double hx = sd * rx * 1.15;
            double hy = ry * 0.38;

            if (sd == 1 && t < _waveUntil)
            {
                // Waving right hand
                hy -= R * 0.35 + Math.Sin(t * 13.0) * R * 0.15;
                hx += Math.Cos(t * 13.0) * R * 0.08;
            }

            dc.DrawEllipse(pawBrush, pawBorder, new Point(hx, hy), hr, hr * 0.85);
        }
    }

    private void DrawParticles(DrawingContext dc, double cx, double cy, double R)
    {
        foreach (var p in _particles)
        {
            if (p.Age < 0) continue;
            double k = p.Age / p.Life;
            double alpha = k < 0.2 ? k / 0.2 : 1.0 - (k - 0.2) / 0.8;
            double px = cx + (p.X + p.Vx * p.Age) * R * 1.35;
            double py = cy + (p.Y + p.Vy * p.Age) * R * 1.35;
            double sz = R * p.Size * (1.0 + k * 0.4);

            byte a = (byte)(Math.Clamp(alpha * 255, 0, 255));
            dc.PushTransform(new TranslateTransform(px, py));

            if (p.Type == "heart")
            {
                var brush = new SolidColorBrush(Color.FromArgb(a, 255, 77, 109));
                brush.Freeze();
                dc.PushTransform(new RotateTransform(Math.Sin(p.Age * 6.0) * 18.0));
                dc.DrawGeometry(brush, null, CreateHeartGeometry(sz));
                dc.Pop();
            }
            else if (p.Type == "star" || p.Type == "spark")
            {
                var brush = new SolidColorBrush(Color.FromArgb(a, 247, 179, 43));
                brush.Freeze();
                dc.PushTransform(new RotateTransform(p.Rot * 180.0 / Math.PI + p.Age * 120.0));
                dc.DrawGeometry(brush, null, CreateStarGeometry(sz, sz * 0.45));
                dc.Pop();
            }
            else if (p.Type == "z")
            {
                var brush = new SolidColorBrush(Color.FromArgb(a, 210, 220, 245));
                brush.Freeze();
                var text = new FormattedText(
                    "z",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    sz * 2.2,
                    brush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(text, new Point(-text.Width / 2.0, -text.Height / 2.0));
            }

            dc.Pop(); // Translate
        }
    }

    private static Geometry CreateHeartGeometry(double s)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(0, s * 0.38), isFilled: true, isClosed: true);
            ctx.BezierTo(new Point(-s * 1.05, -s * 0.15), new Point(-s * 0.5, -s * 0.95), new Point(0, -s * 0.38), true, true);
            ctx.BezierTo(new Point(s * 0.5, -s * 0.95), new Point(s * 1.05, -s * 0.15), new Point(0, s * 0.38), true, true);
        }
        geo.Freeze();
        return geo;
    }

    private static Geometry CreateStarGeometry(double ro, double ri)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            for (int i = 0; i < 10; i++)
            {
                double r = i % 2 != 0 ? ri : ro;
                double a = -Math.PI / 2.0 + i * Math.PI / 5.0;
                Point p = new Point(Math.Cos(a) * r, Math.Sin(a) * r);
                if (i == 0) ctx.BeginFigure(p, isFilled: true, isClosed: true);
                else ctx.LineTo(p, isStroked: false, isSmoothJoin: true);
            }
        }
        geo.Freeze();
        return geo;
    }
}
